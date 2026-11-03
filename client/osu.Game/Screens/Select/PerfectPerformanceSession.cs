// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    internal enum PerfectPerformanceState { Pending, Available, Unavailable }

    internal readonly record struct PerfectPerformanceResult(PerfectPerformanceState State, double Value = 0);

    internal sealed class PerfectPerformanceSession : IDisposable
    {
        private readonly object sync = new object();
        private readonly Dictionary<Guid, PerfectPerformanceResult> results = new Dictionary<Guid, PerfectPerformanceResult>();
        private readonly Queue<BeatmapInfo> queue = new Queue<BeatmapInfo>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Func<BeatmapInfo, (bool found, double value)> tryCached;
        private readonly Func<BeatmapInfo, CancellationToken, double?> calculate;
        private readonly Action flush;
        private readonly bool allowCalculations;
        private readonly Action<PerfectPerformanceSession> changed;
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private bool running;
        private bool progressStarted;
        private int completed;
        private int cacheHits;
        private int failures;
        private Guid? currentBeatmap;
        private long currentStart;

        internal PerfectPerformanceSession(BeatmapDifficultyCache cache, RulesetInfo? ruleset, IReadOnlyList<Mod>? mods, Action<PerfectPerformanceSession> changed, bool allowCalculations = true)
        {
            var lookup = cache.CreatePerfectPerformanceLookup(ruleset, mods);
            tryCached = beatmap => (lookup.TryGet(beatmap, out double value), value);
            calculate = lookup.Calculate;
            flush = cache.FlushPerfectPerformanceCache;
            this.changed = changed;
            this.allowCalculations = allowCalculations;
            Logger.Log($"Ritsu PP calculation session started, calculations enabled: {allowCalculations}");
        }

        internal PerfectPerformanceSession(Func<BeatmapInfo, (bool found, double value)> tryCached, Func<BeatmapInfo, CancellationToken, double?> calculate, Action flush, Action<PerfectPerformanceSession> changed, bool allowCalculations = true)
        {
            this.tryCached = tryCached;
            this.calculate = calculate;
            this.flush = flush;
            this.changed = changed;
            this.allowCalculations = allowCalculations;
        }

        internal (int completed, int total, int cached, int failures) Progress
        {
            get { lock (sync) return (completed, results.Count, cacheHits, failures); }
        }

        internal PerfectPerformanceResult Get(BeatmapInfo beatmap)
        {
            lock (sync)
            {
                if (results.TryGetValue(beatmap.ID, out var existing)) return existing;
                if (cancellation.IsCancellationRequested) return new PerfectPerformanceResult(PerfectPerformanceState.Unavailable);
                try
                {
                    var cached = tryCached(beatmap);
                    if (cached.found)
                    {
                        cacheHits++;
                        completed++;
                        return results[beatmap.ID] = new PerfectPerformanceResult(PerfectPerformanceState.Available, cached.value);
                    }
                }
                catch (Exception e)
                {
                    if (failures < 5) Logger.Error(e, $"Ritsu PP cache lookup failed for beatmap {beatmap.ID}");
                    failures++;
                    completed++;
                    return results[beatmap.ID] = new PerfectPerformanceResult(PerfectPerformanceState.Unavailable);
                }
                if (!allowCalculations)
                {
                    completed++;
                    return results[beatmap.ID] = new PerfectPerformanceResult(PerfectPerformanceState.Unavailable);
                }
                var pending = new PerfectPerformanceResult(PerfectPerformanceState.Pending);
                results[beatmap.ID] = pending;
                queue.Enqueue(beatmap);
                if (!progressStarted)
                {
                    progressStarted = true;
                    changed(this);
                }
                if (!running)
                {
                    running = true;
                    _ = Task.Run(run);
                }
                return pending;
            }
        }

        private void run()
        {
            int lastReported = 0;
            using var diagnosticTimer = new Timer(_ =>
            {
                lock (sync)
                {
                    if (currentBeatmap == null) return;
                    Logger.Log($"Ritsu PP active: {completed}/{results.Count}, cached={cacheHits}, unavailable={failures}, beatmap={currentBeatmap}, current={elapsed.ElapsedMilliseconds - currentStart}ms, elapsed={elapsed.ElapsedMilliseconds}ms");
                }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
            try
            {
                while (true)
                {
                    BeatmapInfo beatmap;
                    lock (sync)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (queue.Count == 0)
                        {
                            running = false;
                            break;
                        }
                        beatmap = queue.Dequeue();
                        currentBeatmap = beatmap.ID;
                        currentStart = elapsed.ElapsedMilliseconds;
                    }
                    double? pp;
                    try { pp = calculate(beatmap, cancellation.Token); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e)
                    {
                        if (failures < 5) Logger.Error(e, $"Ritsu PP calculation failed for beatmap {beatmap.ID}");
                        pp = null;
                    }
                    lock (sync)
                    {
                        currentBeatmap = null;
                        results[beatmap.ID] = pp.HasValue ? new PerfectPerformanceResult(PerfectPerformanceState.Available, pp.Value) : new PerfectPerformanceResult(PerfectPerformanceState.Unavailable);
                        completed++;
                        if (!pp.HasValue) failures++;
                    }
                    if (++lastReported >= 25)
                    {
                        lastReported = 0;
                        var current = Progress;
                        if ((current.completed - current.cached) % 250 == 0)
                            Logger.Log($"Ritsu PP progress: {current.completed}/{current.total}, cached={current.cached}, unavailable={current.failures}, elapsed={elapsed.ElapsedMilliseconds}ms");
                        changed(this);
                    }
                }
                var progress = Progress;
                Logger.Log($"Ritsu PP session batch finished in {elapsed.ElapsedMilliseconds}ms: {progress.completed}/{progress.total}, cached={progress.cached}, unavailable={progress.failures}");
                changed(this);
            }
            catch (OperationCanceledException)
            {
                var progress = Progress;
                Logger.Log($"Ritsu PP session cancelled after {elapsed.ElapsedMilliseconds}ms: {progress.completed}/{progress.total}, cached={progress.cached}, unavailable={progress.failures}");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Ritsu PP session failed");
                lock (sync)
                {
                    running = false;
                    foreach (var id in new List<Guid>(results.Keys))
                    {
                        if (results[id].State != PerfectPerformanceState.Pending) continue;
                        results[id] = new PerfectPerformanceResult(PerfectPerformanceState.Unavailable);
                        completed++;
                        failures++;
                    }
                    queue.Clear();
                }
                changed(this);
            }
            finally
            {
                flush();
            }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            _ = Task.Run(flush);
        }
    }
}
