// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Offline
{
    internal sealed class RitsuPlayHistory
    {
        private readonly Dictionary<string, (int Misses, DateTimeOffset LastPlayed)> byBeatmap;
        private readonly DateTimeOffset weekStart;

        public RitsuPlayHistory(IEnumerable<ScoreInfo> scores, DateTimeOffset now, TimeZoneInfo zone)
        {
            weekStart = StartOfWeek(now, zone);
            byBeatmap = scores.Where(s => !string.IsNullOrEmpty(s.BeatmapHash)).GroupBy(s => s.BeatmapHash).ToDictionary(
                group => group.Key,
                group => (group.OrderByTotalScore().First().Statistics.GetValueOrDefault(HitResult.Miss), group.Max(s => s.Date)));
        }

        public int? BestMisses(string hash) => byBeatmap.TryGetValue(hash, out var history) ? history.Misses : null;
        public bool PlayedThisWeek(string hash) => byBeatmap.TryGetValue(hash, out var history) && history.LastPlayed >= weekStart;

        public static DateTimeOffset StartOfWeek(DateTimeOffset now, TimeZoneInfo zone)
        {
            DateTime local = TimeZoneInfo.ConvertTime(now, zone).Date;
            DateTime monday = local.AddDays(-((int)local.DayOfWeek + 6) % 7);
            return new DateTimeOffset(monday, zone.GetUtcOffset(monday));
        }
    }
}
