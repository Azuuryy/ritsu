// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Scoring;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Plays, beatmaps and beatmap sets that never give PP on local profiles (e.g. maps made for skill testing).
    /// Excluded plays still show in recent scores and count towards play count and most played beatmaps.
    /// </summary>
    public class LocalPerformanceExclusions
    {
        public List<Guid> Scores { get; set; } = new List<Guid>();
        public List<Guid> Beatmaps { get; set; } = new List<Guid>();
        public List<Guid> BeatmapSets { get; set; } = new List<Guid>();

        public LocalPerformanceExclusions Clone() => new LocalPerformanceExclusions
        {
            Scores = new List<Guid>(Scores ?? new List<Guid>()),
            Beatmaps = new List<Guid>(Beatmaps ?? new List<Guid>()),
            BeatmapSets = new List<Guid>(BeatmapSets ?? new List<Guid>()),
        };

        /// <returns>Why the score is excluded from PP, or null when it isn't.</returns>
        public string? GetReason(ScoreInfo score)
        {
            if (Scores.Contains(score.ID))
                return @"This play is excluded from PP.";

            if (score.BeatmapInfo != null && Beatmaps.Contains(score.BeatmapInfo.ID))
                return @"This beatmap is excluded from PP.";

            if (score.BeatmapInfo?.BeatmapSet != null && BeatmapSets.Contains(score.BeatmapInfo.BeatmapSet.ID))
                return @"This beatmap set is excluded from PP.";

            return null;
        }

        /// <summary>
        /// The same check for many scores in a row (statistics): hash sets instead of lists, and nothing read from the score when nothing is excluded.
        /// </summary>
        public Matcher CreateMatcher() => new Matcher(this);

        public sealed class Matcher
        {
            private readonly HashSet<Guid> scores;
            private readonly HashSet<Guid> beatmaps;
            private readonly HashSet<Guid> beatmapSets;
            private readonly bool empty;

            public Matcher(LocalPerformanceExclusions exclusions)
            {
                scores = exclusions.Scores.ToHashSet();
                beatmaps = exclusions.Beatmaps.ToHashSet();
                beatmapSets = exclusions.BeatmapSets.ToHashSet();
                empty = scores.Count == 0 && beatmaps.Count == 0 && beatmapSets.Count == 0;
            }

            /// <inheritdoc cref="LocalPerformanceExclusions.GetReason"/>
            public string? GetReason(ScoreInfo score)
            {
                if (empty)
                    return null;

                if (scores.Contains(score.ID))
                    return @"This play is excluded from PP.";

                if (score.BeatmapInfo != null && beatmaps.Contains(score.BeatmapInfo.ID))
                    return @"This beatmap is excluded from PP.";

                if (score.BeatmapInfo?.BeatmapSet != null && beatmapSets.Contains(score.BeatmapInfo.BeatmapSet.ID))
                    return @"This beatmap set is excluded from PP.";

                return null;
            }
        }

        /// <summary>
        /// Right-click a score (song select leaderboard, profile page): exclude / include the play, its difficulty or its beatmap set.
        /// </summary>
        public static MenuItem CreateScoreMenuItem(LocalProfileManager manager, ScoreInfo score)
        {
            var exclusions = manager.Store.PerformanceExclusions;
            var items = new List<MenuItem>
            {
                toggle(exclusions.Scores.Contains(score.ID), @"this play", v => manager.SetExcludedFromPerformance(LocalPerformanceExclusionKind.Score, score.ID, v)),
            };

            if (score.BeatmapInfo != null)
            {
                Guid beatmapId = score.BeatmapInfo.ID;
                items.Add(toggle(exclusions.Beatmaps.Contains(beatmapId), @"this difficulty",
                    v => manager.SetExcludedFromPerformance(LocalPerformanceExclusionKind.Beatmap, beatmapId, v)));

                if (score.BeatmapInfo.BeatmapSet != null)
                {
                    Guid setId = score.BeatmapInfo.BeatmapSet.ID;
                    items.Add(toggle(exclusions.BeatmapSets.Contains(setId), @"the whole beatmap set",
                        v => manager.SetExcludedFromPerformance(LocalPerformanceExclusionKind.BeatmapSet, setId, v)));
                }
            }

            return new OsuMenuItem(@"Exclude from PP", MenuItemType.Standard) { Items = items.ToArray() };
        }

        /// <summary>
        /// Right-click a difficulty in song select.
        /// </summary>
        public static OsuMenuItem CreateBeatmapMenuItem(LocalProfileManager manager, BeatmapInfo beatmap)
        {
            Guid id = beatmap.ID;
            bool excluded = manager.Store.PerformanceExclusions.Beatmaps.Contains(id);

            return new OsuMenuItem(excluded ? @"Include this difficulty in PP again" : @"Exclude this difficulty from PP", MenuItemType.Standard,
                () => manager.SetExcludedFromPerformance(LocalPerformanceExclusionKind.Beatmap, id, !excluded));
        }

        /// <summary>
        /// Right-click a beatmap set in song select.
        /// </summary>
        public static OsuMenuItem CreateBeatmapSetMenuItem(LocalProfileManager manager, BeatmapSetInfo beatmapSet)
        {
            Guid id = beatmapSet.ID;
            bool excluded = manager.Store.PerformanceExclusions.BeatmapSets.Contains(id);

            return new OsuMenuItem(excluded ? @"Include this beatmap set in PP again" : @"Exclude this beatmap set from PP", MenuItemType.Standard,
                () => manager.SetExcludedFromPerformance(LocalPerformanceExclusionKind.BeatmapSet, id, !excluded));
        }

        private static MenuItem toggle(bool excluded, string what, Action<bool> set) =>
            new OsuMenuItem(excluded ? $@"Include {what} again" : $@"Exclude {what}", MenuItemType.Standard, () => set(!excluded));
    }

    public enum LocalPerformanceExclusionKind
    {
        Score,
        Beatmap,
        BeatmapSet,
    }
}
