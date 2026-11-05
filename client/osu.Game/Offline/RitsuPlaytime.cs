// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Offline
{
    public enum RitsuPlaytimeMode
    {
        [Description("Map Length")]
        MapLength,
        [Description("Drain Length")]
        DrainLength,
    }

    public sealed class RitsuPlaytime
    {
        private readonly Func<BeatmapInfo, IBeatmap?> loadBeatmap;
        private readonly ConcurrentDictionary<string, Lengths> lengths = new ConcurrentDictionary<string, Lengths>();

        public RitsuPlaytime(BeatmapManager beatmaps)
            : this(map => beatmaps.GetWorkingBeatmap(map).Beatmap)
        {
        }

        public RitsuPlaytime(Func<BeatmapInfo, IBeatmap?> loadBeatmap) => this.loadBeatmap = loadBeatmap;

        public (double Seconds, int Missing) Calculate(IEnumerable<ScoreInfo> scores, RitsuPlaytimeMode mode)
        {
            double total = 0;
            int missing = 0;
            foreach (var score in scores)
            {
                try
                {
                    var map = score.BeatmapInfo;
                    if (map == null || string.IsNullOrEmpty(score.BeatmapHash) || map.Hash != score.BeatmapHash)
                    {
                        missing++;
                        continue;
                    }

                    if (!lengths.TryGetValue(map.Hash, out var duration))
                    {
                        var loaded = loadBeatmap(map);
                        if (loaded == null || loaded.HitObjects.Count == 0)
                        {
                            missing++;
                            continue;
                        }
                        duration = GetLengths(loaded);
                        if (duration.Map <= 0 || !double.IsFinite(duration.Map) || !double.IsFinite(duration.Drain))
                        {
                            missing++;
                            continue;
                        }
                        lengths.TryAdd(map.Hash, duration);
                    }

                    double rate = 1;
                    foreach (var mod in score.Mods.OfType<IApplicableToRate>())
                    {
                        if (mod is not ModRateAdjust)
                        {
                            rate = 0;
                            break;
                        }
                        rate = mod.ApplyToRate(0, rate);
                    }
                    if (!double.IsFinite(rate) || rate <= 0)
                    {
                        missing++;
                        continue;
                    }
                    total += (mode == RitsuPlaytimeMode.DrainLength ? duration.Drain : duration.Map) / rate / 1000;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Could not calculate local playtime for a score.");
                    missing++;
                }
            }
            return (total, missing);
        }

        public static Lengths GetLengths(IBeatmap beatmap)
        {
            var (start, end) = beatmap.CalculatePlayableBounds();
            if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start)
                return new Lengths(0, 0);

            double breakTime = 0;
            double previousEnd = start;
            foreach (var pause in beatmap.Breaks.OrderBy(b => b.StartTime))
            {
                if (!double.IsFinite(pause.StartTime) || !double.IsFinite(pause.EndTime))
                    continue;
                double from = Math.Max(previousEnd, Math.Max(start, pause.StartTime));
                double to = Math.Min(end, pause.EndTime);
                if (to > from)
                {
                    breakTime += to - from;
                    previousEnd = to;
                }
            }
            return new Lengths(end - start, Math.Max(0, end - start - breakTime));
        }

        public readonly record struct Lengths(double Map, double Drain);
    }
}
