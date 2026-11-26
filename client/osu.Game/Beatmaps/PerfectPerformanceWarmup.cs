using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using Realms;

namespace osu.Game.Beatmaps
{
    internal partial class PerfectPerformanceWarmup : Component
    {
        private const int presets_per_batch = 12;
        private const double minimum_star_rating = 0.1;

        private readonly Bindable<string> status = new Bindable<string>();
        private readonly Bindable<RunState> state = new Bindable<RunState>();

        internal IBindable<string> Status => status;
        internal IBindable<RunState> State => state;

        private CancellationTokenSource? runCancellation;
        private ProgressNotification? notification;
        private INotificationOverlay? notificationOverlay;
        private RunRequest? pausedRun;
        private bool disposed;
        private volatile bool playing;

        [Resolved]
        private BeatmapDifficultyCache cache { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> selectedBeatmap { get; set; } = null!;

        internal enum RunState
        {
            Idle,
            Running,
            Stopping,
            Paused,
        }

        private readonly record struct RunRequest(bool Full, string? CompletionMarker = null, TimeSpan Elapsed = default);

        public void SetPlaying(bool value) => playing = value;

        public void StartAutomatic(int difficultyVersion, string libraryPath, INotificationOverlay? notifications = null)
        {
            notificationOverlay = notifications;
            start(new RunRequest(false, BeatmapDifficultyCache.CreateStablePerformanceKey(difficultyVersion, "osu", libraryPath, "automatic-grid-v1")));
        }

        public void StopOrStartFull()
        {
            if (state.Value == RunState.Running)
                stop();
            else if (state.Value is RunState.Idle or RunState.Paused)
                start(pausedRun ?? new RunRequest(true));
        }

        private void stop()
        {
            if (state.Value != RunState.Running)
                return;

            state.Value = RunState.Stopping;
            status.Value = string.Empty;
            if (notification != null)
                notification.Text = "Stopping Max PP precalculation…";
            runCancellation!.Cancel();
        }

        private void start(RunRequest request)
        {
            if (disposed || state.Value is RunState.Running or RunState.Stopping)
                return;

            var cancellation = new CancellationTokenSource();
            var elapsed = Stopwatch.StartNew();
            runCancellation = cancellation;
            pausedRun = null;
            Guid current = selectedBeatmap.Value.BeatmapInfo.ID;
            status.Value = request.Full ? "Precalculating full Max PP grid…" : string.Empty;
            state.Value = RunState.Running;

            _ = Task.Run(async () =>
            {
                bool failed = false;
                try
                {
                    if (request.CompletionMarker != null && cache.HasPerfectPerformanceMarker(request.CompletionMarker))
                        return;

                    await run(request, current, cancellation, elapsed).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception e)
                {
                    failed = true;
                    Logger.Error(e, "Ritsu Max PP precalculation failed");
                    publish(cancellation, "Max PP precalculation failed.");
                }
                finally
                {
                    cache.FlushPerfectPerformanceCache();
                    elapsed.Stop();
                    Schedule(() =>
                    {
                        if (!disposed && ReferenceEquals(cancellation, runCancellation))
                        {
                            runCancellation = null;
                            if (cancellation.IsCancellationRequested)
                            {
                                pausedRun = request with { Elapsed = request.Elapsed + elapsed.Elapsed };
                                status.Value = $"Max PP precalculation paused. Elapsed: {formatDuration(pausedRun.Value.Elapsed)}";
                            }

                            if (notification != null)
                            {
                                notification.CancelRequested = null;
                                notification.Text = status.Value;
                                notification.CompletionText = status.Value;
                                notification.State = pausedRun != null || failed ? ProgressNotificationState.Cancelled : ProgressNotificationState.Completed;
                                notification = null;
                            }
                            state.Value = pausedRun == null ? RunState.Idle : RunState.Paused;
                        }
                        cancellation.Dispose();
                    });
                }
            });
        }

        private async Task run(RunRequest request, Guid current, CancellationTokenSource cancellation, Stopwatch elapsed)
        {
            bool full = request.Full;
            var token = cancellation.Token;
            token.ThrowIfCancellationRequested();
            var maps = realm.Run(r => r.All<BeatmapInfo>()
                .Filter("Ruleset.ShortName == $0 AND BeatmapSet.DeletePending == false AND BeatmapSet.Protected == false AND (StarRating < 0 OR StarRating >= $1)", "osu", minimum_star_rating)
                .AsEnumerable().Where(map => !string.IsNullOrEmpty(map.Hash)).Detach())
                .OrderByDescending(map => map.ID == current).DistinctBy(map => map.Hash).ToArray();
            if (maps.Length == 0)
            {
                publish(cancellation, "No eligible osu!standard difficulties for Max PP precalculation.");
                return;
            }

            var ruleset = maps[0].Ruleset;
            var presets = CreateVariants(ruleset, full).ToArray();
            int total = maps.Length * presets.Length;
            int completed = 0;
            int unavailable = 0;
            int uncached = 0;
            Action countCacheMiss = () => Interlocked.Increment(ref uncached);
            int workers = cache.BenchmarkWorkers ?? Math.Min(2, Math.Max(1, Environment.ProcessorCount - 2));
            string title = full ? "Full Max PP precalculation" : "Base Max PP precalculation";
            var options = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = token };

            Schedule(() =>
            {
                if (!isRunning(cancellation))
                    return;

                if (notificationOverlay != null)
                {
                    notification = new ProgressNotification
                    {
                        State = ProgressNotificationState.Active,
                        CancelRequested = () =>
                        {
                            stop();
                            return false;
                        },
                    };
                    updateProgress();
                    notificationOverlay.Post(notification);
                }
                else
                    updateProgress();

                ScheduledDelegate? timer = null;
                timer = Scheduler.AddDelayed(() =>
                {
                    if (isRunning(cancellation))
                        updateProgress();
                    else
                        timer!.Cancel();
                }, 1000, true);
            });

            foreach (var batch in presets.Chunk(presets_per_batch))
            {
                var lookups = GroupVariantsForConversion(batch, full).Select(group => group.Select(mods => cache.CreatePerfectPerformanceLookup(ruleset, mods)).ToArray()).ToArray();
                await Parallel.ForEachAsync(maps, options, async (map, ct) =>
                {
                    var workingBeatmap = new Lazy<WorkingBeatmap>(() => beatmapManager.GetWorkingBeatmap(map));
                    SliderEventGenerator.EventCache? sliderEvents = null;
                    foreach (var group in lookups)
                    {
                        var playableBeatmap = group.Length > 1
                            ? new Lazy<BeatmapDifficultyCache.PlayableCachedWorkingBeatmap>(() =>
                                new BeatmapDifficultyCache.PlayableCachedWorkingBeatmap(workingBeatmap.Value, shareHidden: true, sliderEvents: sliderEvents ??= new SliderEventGenerator.EventCache()))
                            : null;
                        foreach (var lookup in group)
                        {
                            while (playing)
                                await Task.Delay(250, ct).ConfigureAwait(false);

                            if (await lookup.CalculateAsync(map, ct, background: true, workingBeatmap: workingBeatmap, onCacheMiss: countCacheMiss, sharedPlayableBeatmap: playableBeatmap).ConfigureAwait(false) == null)
                                Interlocked.Increment(ref unavailable);

                            int done = Interlocked.Increment(ref completed);
                            if (done % 25 == 0 || done == total)
                                Scheduler.AddOnce(updateProgress);
                        }
                    }
                }).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();
            if (request.CompletionMarker != null)
                cache.SavePerfectPerformanceMarker(request.CompletionMarker);

            publish(cancellation, $"{title} complete ({total - unavailable:N0}/{total:N0} available). Elapsed: {formatDuration(request.Elapsed + elapsed.Elapsed)}");
            Logger.Log($"{title} completed {total:N0} variants ({total - uncached:N0} from Max PP cache, {uncached:N0} missing from cache, {unavailable:N0} unavailable) with {workers} worker(s) in {elapsed.Elapsed.TotalSeconds:F2}s.");

            void updateProgress()
            {
                if (!isRunning(cancellation))
                    return;

                int done = Volatile.Read(ref completed);
                TimeSpan currentElapsed = elapsed.Elapsed;
                string remaining = playing ? "Paused during gameplay" : "Estimating…";
                if (!playing && done >= 25 && currentElapsed.TotalSeconds >= 1)
                    remaining = $"Remaining: {formatDuration(TimeSpan.FromSeconds(currentElapsed.TotalSeconds * (total - done) / done))}";

                status.Value = $"{title}: {done:N0}/{total:N0} · Elapsed: {formatDuration(request.Elapsed + currentElapsed)} · {remaining} · {workers} worker(s)";
                if (notification != null)
                {
                    notification.Text = status.Value;
                    notification.Progress = (float)done / total;
                }
            }
        }

        private static string formatDuration(TimeSpan time) => time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes:00}:{time.Seconds:00}";

        private bool isRunning(CancellationTokenSource source) => !disposed && ReferenceEquals(source, runCancellation) && state.Value == RunState.Running;

        private void publish(CancellationTokenSource source, string text)
        {
            Schedule(() =>
            {
                if (isRunning(source))
                    status.Value = text;
            });
        }

        internal static IEnumerable<Mod[][]> GroupVariantsForConversion(IEnumerable<Mod[]> variants, bool full) => full
            ? variants.Select(mods => new[] { mods })
            : variants.GroupBy(mods => BeatmapDifficultyCache.CreatePerfectPerformanceModKey(mods.Where(mod => mod is not ModDoubleTime and not ModHidden)))
                      .Select(group => group.OrderBy(mods => mods.Any(mod => mod is ModHidden)).ToArray());

        internal static IEnumerable<Mod[]> CreateVariants(RulesetInfo ruleset, bool full)
        {
            int[] rates = full ? Enumerable.Range(10, 31).Select(rate => rate * 5).ToArray() : new[] { 100, 150 };
            string?[] difficulties = full ? [null, "HR", "EZ"] : [null, "HR"];
            var available = ruleset.CreateInstance().AllMods.OfType<Mod>().ToArray();
            foreach (int rate in rates)
            foreach (string? difficulty in difficulties)
            foreach (bool hidden in new[] { false, true })
            {
                var mods = new List<Mod>();
                if (difficulty != null)
                    mods.Add(available.Single(mod => mod.Acronym == difficulty).DeepClone());
                if (hidden)
                    mods.Add(available.Single(mod => mod.Acronym == "HD").DeepClone());
                if (rate != 100)
                {
                    var speed = available.Single(mod => mod.Acronym == (rate < 100 ? "HT" : "DT")).DeepClone();
                    if (full && speed is ModRateAdjust rateAdjust)
                        rateAdjust.SpeedChange.Value = rate / 100d;
                    mods.Add(speed);
                }
                yield return mods.ToArray();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            disposed = true;
            if (isDisposing)
                runCancellation?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
