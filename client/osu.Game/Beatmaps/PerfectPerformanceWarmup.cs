using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using Realms;

namespace osu.Game.Beatmaps
{
    internal partial class PerfectPerformanceWarmup : Component
    {
        private readonly Bindable<string> status = new Bindable<string>();
        internal IBindable<string> Status => status;
        internal bool IsFullRunning => completeGrid && running is { IsCompleted: false } && runCancellation?.IsCancellationRequested == false;
        private CancellationTokenSource? runCancellation;
        private Task? running;
        private bool completeGrid;
        private bool disposed;
        private volatile bool playing;

        [Resolved]
        private BeatmapDifficultyCache cache { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> selectedBeatmap { get; set; } = null!;

        public void SetPlaying(bool value) => playing = value;

        public void StartAutomatic()
        {
            if (running == null)
                start(false);
        }

        public void ToggleFull()
        {
            if (IsFullRunning)
            {
                runCancellation!.Cancel();
                status.Value = "Stopping Max PP calculation…";
                return;
            }

            start(true);
        }

        private void start(bool full)
        {
            if (disposed)
                return;

            if (running != null)
                runCancellation?.Cancel();
            var previous = running;
            var cancellation = new CancellationTokenSource();
            runCancellation = cancellation;
            completeGrid = full;
            Guid current = selectedBeatmap.Value.BeatmapInfo.ID;
            status.Value = full ? "Stop full Max PP precalculation" : "Precalculating common Max PP combinations…";

            running = Task.Run(async () =>
            {
                try
                {
                    if (previous != null)
                        await previous.ConfigureAwait(false);

                    await run(full, current, cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    publish(cancellation, full ? "Resume full Max PP precalculation" : "Precalculate full Max PP grid");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Ritsu Max PP precalculation failed");
                    publish(cancellation, "Retry full Max PP precalculation");
                }
                finally
                {
                    cache.FlushPerfectPerformanceCache();
                    Schedule(() =>
                    {
                        if (!disposed && ReferenceEquals(cancellation, runCancellation))
                        {
                            running = null;
                            status.TriggerChange();
                        }
                        cancellation.Dispose();
                    });
                }
            });
            status.TriggerChange();
        }

        private async Task run(bool full, Guid current, CancellationTokenSource cancellation)
        {
            var token = cancellation.Token;
            token.ThrowIfCancellationRequested();
            var maps = realm.Run(r => r.All<BeatmapInfo>()
                .Filter("Ruleset.ShortName == $0 AND BeatmapSet.DeletePending == false AND BeatmapSet.Protected == false", "osu")
                .AsEnumerable().Where(map => !string.IsNullOrEmpty(map.Hash)).Detach())
                .OrderByDescending(map => map.ID == current).DistinctBy(map => map.Hash).ToArray();
            if (maps.Length == 0)
            {
                publish(cancellation, "No osu!standard difficulties. Precalculate full Max PP grid");
                return;
            }

            var ruleset = maps[0].Ruleset;
            var presets = CreateVariants(ruleset, full).ToArray();
            int total = maps.Length * presets.Length;
            int completed = 0;
            int unavailable = 0;
            int workers = Math.Min(full ? 8 : 4, Math.Max(1, Environment.ProcessorCount - 2));
            var options = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = token };

            foreach (var mods in presets)
            {
                var lookup = cache.CreatePerfectPerformanceLookup(ruleset, mods);
                await Parallel.ForEachAsync(maps, options, async (map, ct) =>
                {
                    while (playing)
                        await Task.Delay(250, ct).ConfigureAwait(false);

                    if (await lookup.CalculateAsync(map, ct, background: true).ConfigureAwait(false) == null)
                        Interlocked.Increment(ref unavailable);

                    int done = Interlocked.Increment(ref completed);
                    if (done % 25 == 0 || done == total)
                        publish(cancellation, $"Max PP precalculation: {done:N0}/{total:N0}");
                }).ConfigureAwait(false);
            }

            publish(cancellation, full
                ? $"Precalculate full Max PP grid again ({total - unavailable:N0}/{total:N0} available)"
                : "Precalculate full Max PP grid");
        }

        private void publish(CancellationTokenSource source, string text)
        {
            Schedule(() =>
            {
                if (!disposed && ReferenceEquals(source, runCancellation))
                    status.Value = text;
            });
        }

        internal static IEnumerable<Mod[]> CreateVariants(RulesetInfo ruleset, bool full)
        {
            int[] rates = full ? Enumerable.Range(10, 31).Select(rate => rate * 5).ToArray() : new[] { 75, 100, 125, 150 };
            var available = ruleset.CreateInstance().AllMods.OfType<Mod>().ToArray();
            foreach (int rate in rates)
            foreach (string? difficulty in new string?[] { null, "HR", "EZ" })
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
                    if (speed is ModHalfTime ht)
                        ht.SpeedChange.Value = rate / 100d;
                    else if (speed is ModDoubleTime dt)
                        dt.SpeedChange.Value = rate / 100d;
                    mods.Add(speed);
                }
                yield return mods.ToArray();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            disposed = true;
            if (isDisposing && running != null)
                runCancellation?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
