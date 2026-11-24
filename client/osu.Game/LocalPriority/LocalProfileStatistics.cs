// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Utils;
using Realms;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Statistics for one local profile in one ruleset, computed from the scores stored in the local database
    /// plus the per-attempt counters recorded by Ritsu.
    /// </summary>
    public class LocalProfileStatistics
    {
        public double PP { get; init; }

        /// <summary>
        /// The best PP on each beatmap (beatmap hash, PP), highest first: every score that makes up <see cref="PP"/>
        /// (not limited like <see cref="BestScores"/>). Used by the PP projections in the command palette.
        /// </summary>
        public IReadOnlyList<(string BeatmapHash, double PP)> BestPerformance { get; init; } = Array.Empty<(string, double)>();

        /// <summary>
        /// Estimated online global rank for this PP (osu! standard only), or null.
        /// </summary>
        public int? EstimatedGlobalRank { get; init; }
        public double Accuracy { get; init; }

        /// <summary>
        /// Accuracy over every judgement recorded on this profile (all counted attempts, including failed / quit ones,
        /// plus synced plays), not only the saved scores that make up <see cref="Accuracy"/>. Null without recorded hits.
        /// </summary>
        public double? TotalAccuracy { get; init; }

        /// <summary>
        /// Every attempt (finished, failed, quit, restarted), recorded as it happened.
        /// </summary>
        public LocalPlayStatistics Plays { get; init; } = new LocalPlayStatistics();

        public int SavedScoreCount { get; init; }

        /// <summary>
        /// Saved scores plus the failed / quit plays of the last day (the number next to "Recent Plays").
        /// </summary>
        public int RecentPlayCount { get; init; }
        public long RankedScore { get; init; }
        public long TotalScore { get; init; }

        /// <summary>
        /// The same totals with every score shown in classic scoring (Settings → Gameplay → Score display mode).
        /// </summary>
        public long RankedScoreClassic { get; init; }

        public long TotalScoreClassic { get; init; }

        public long GetRankedScore(ScoringMode mode) => mode == ScoringMode.Classic ? RankedScoreClassic : RankedScore;

        public long GetTotalScore(ScoringMode mode) => mode == ScoringMode.Classic ? TotalScoreClassic : TotalScore;
        public int MaxCombo { get; init; }

        public int CountSSH { get; init; }
        public int CountSS { get; init; }
        public int CountSH { get; init; }
        public int CountS { get; init; }
        public int CountA { get; init; }

        /// <summary>
        /// Best score per beatmap that gives PP, highest first, with its weighted contribution.
        /// </summary>
        public IReadOnlyList<LocalScoreEntry> BestScores { get; init; } = Array.Empty<LocalScoreEntry>();

        public IReadOnlyList<LocalScoreEntry> RecentScores { get; init; } = Array.Empty<LocalScoreEntry>();

        /// <summary>
        /// Total PP over time: one point per score that changed the total, at the date the score was set locally.
        /// </summary>
        public IReadOnlyList<(DateTimeOffset Date, double PP)> History { get; init; } = Array.Empty<(DateTimeOffset, double)>();

        /// <summary>
        /// Scores whose PP could not be calculated (beatmap missing or unsupported ruleset).
        /// </summary>
        public int UncalculatedScores { get; init; }

        /// <summary>
        /// Every beatmap played in this ruleset with how often (most played first, at most <see cref="MOST_PLAYED_LIMIT"/> listed).
        /// </summary>
        public IReadOnlyList<LocalBeatmapEntry> MostPlayed { get; init; } = Array.Empty<LocalBeatmapEntry>();

        /// <summary>
        /// How many different beatmaps were played (the number next to "Most Played Beatmaps").
        /// </summary>
        public int MostPlayedCount { get; init; }

        /// <summary>
        /// Plays per month / per week (Monday-based), oldest first, with empty periods filled in up to now.
        /// </summary>
        public IReadOnlyList<APIUserHistoryCount> MonthlyPlays { get; init; } = Array.Empty<APIUserHistoryCount>();

        public IReadOnlyList<APIUserHistoryCount> WeeklyPlays { get; init; } = Array.Empty<APIUserHistoryCount>();

        public const int MOST_PLAYED_LIMIT = 200;

        /// <summary>
        /// Plays per beatmap: the plays counted by Ritsu, or the number of saved scores on it if that's higher
        /// (plays from before Ritsu counted them, synced or transferred scores).
        /// </summary>
        private static (IReadOnlyList<LocalBeatmapEntry>, int) calculateMostPlayed(LocalPlayStatistics plays, IReadOnlyList<ScoreSummary> scores, HashSet<string> hiddenBeatmaps)
        {
            var perBeatmap = new Dictionary<string, long>(plays.BeatmapPlays.Where(p => p.Value > 0 && !hiddenBeatmaps.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

            // Plays are counted per beatmap MD5 hash. Beatmaps that only have scores (synced / imported / from before Ritsu counted plays)
            // count one play per score.
            foreach (var group in scores.Where(s => !string.IsNullOrEmpty(s.BeatmapMD5)).GroupBy(s => s.BeatmapMD5!))
                perBeatmap[group.Key] = Math.Max(perBeatmap.GetValueOrDefault(group.Key), group.Count());

            var ordered = perBeatmap.OrderByDescending(p => p.Value).ToArray();

            // Only references here: the beatmaps are looked up when their rows are shown (most of the list is never opened).
            var list = ordered.Take(MOST_PLAYED_LIMIT).Select(p => new LocalBeatmapEntry(p.Key, p.Value)).ToArray();

            return (list, ordered.Length);
        }

        /// <summary>
        /// Plays per day: counted plays, or saved scores set that day if that's higher; then summed per month and per week.
        /// </summary>
        private static (IReadOnlyList<APIUserHistoryCount>, IReadOnlyList<APIUserHistoryCount>) calculatePlayHistory(LocalPlayStatistics plays, IReadOnlyList<ScoreSummary> scores)
        {
            var perDay = new Dictionary<DateTime, long>();

            foreach (var (day, count) in plays.DailyPlays)
            {
                if (count > 0 && DateTime.TryParseExact(day, LocalPlayStatistics.DAY_FORMAT, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var date) && date >= LocalProfileManager.OLDEST_VALID_PLAY_DATE)
                    perDay[date.Date] = count;
            }

            // Plays dated before 2000 can't be real (replays without a date show as year 1): they would stretch the chart back to year 1.
            foreach (var group in scores.Where(s => s.Date.Year >= LocalProfileManager.OLDEST_VALID_PLAY_DATE.Year).GroupBy(s => s.Date.ToLocalTime().Date))
                perDay[group.Key] = Math.Max(perDay.GetValueOrDefault(group.Key), group.Count());

            if (perDay.Count == 0)
                return (Array.Empty<APIUserHistoryCount>(), Array.Empty<APIUserHistoryCount>());

            DateTime today = DateTime.Now.Date;

            static DateTime monthOf(DateTime d) => new DateTime(d.Year, d.Month, 1);
            static DateTime weekOf(DateTime d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).Date;

            return (fill(perDay, monthOf, d => d.AddMonths(1), today), fill(perDay, weekOf, d => d.AddDays(7), today));
        }

        private static IReadOnlyList<APIUserHistoryCount> fill(Dictionary<DateTime, long> perDay, Func<DateTime, DateTime> periodOf, Func<DateTime, DateTime> next, DateTime today)
        {
            var sums = perDay.GroupBy(p => periodOf(p.Key)).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

            DateTime first = sums.Keys.Min();
            DateTime last = periodOf(today);

            if (last < sums.Keys.Max())
                last = sums.Keys.Max();

            var result = new List<APIUserHistoryCount>();

            for (DateTime period = first; period <= last; period = next(period))
                result.Add(new APIUserHistoryCount { Date = period, Count = sums.GetValueOrDefault(period) });

            // The chart needs at least two points: start one period earlier, at zero.
            if (result.Count == 1)
                result.Insert(0, new APIUserHistoryCount { Date = periodOf(first.AddDays(-1)), Count = 0 });

            return result;
        }

        /// <summary>
        /// At most this many best / recent scores are listed (like the website); every score still counts towards the statistics.
        /// </summary>
        public const int BEST_SCORE_LIMIT = 200;

        public const int RECENT_SCORE_LIMIT = 200;

        /// <summary>
        /// Weighted PP the same way the website does: best scores weighted by 0.95^n, plus the bonus for the number of scored beatmaps.
        /// </summary>
        public static double WeightedTotal(IReadOnlyList<double> orderedBest)
        {
            double total = 0;
            double factor = 1;

            foreach (double pp in orderedBest)
            {
                total += pp * factor;
                factor *= 0.95;
            }

            return total + 416.6667 * (1 - Math.Pow(0.995, Math.Min(1000, orderedBest.Count)));
        }

        /// <summary>
        /// Just what the statistics need from one score, read straight from the database (no full copy of the score and its beatmap).
        /// </summary>
        private class ScoreSummary
        {
            public Guid ID;
            public string BeatmapHash = string.Empty;
            public string? BeatmapMD5;
            public bool GrantsPerformance;

            /// <summary>
            /// Why the score gives no PP on this profile (null when it does).
            /// </summary>
            public string? NoPerformanceReason;
            public long TotalScore;
            public long ClassicScore;
            public ScoreRank Rank;
            public double Accuracy;
            public int MaxCombo;
            public DateTimeOffset Date;

            /// <summary>
            /// Synced plays only (set before Ritsu counted attempts): their hits and estimated play time count as one play.
            /// </summary>
            public Dictionary<HitResult, int>? SyncedHits;

            public double SyncedSeconds;
        }

        /// <summary>
        /// Rows shown right away; further rows are loaded when "show more" needs them.
        /// </summary>
        public const int PRELOADED_ROWS = 50;

        /// <summary>
        /// Full statistics for the profile page.
        /// </summary>
        public static Task<LocalProfileStatistics> CalculateAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm, BeatmapManager beatmaps,
                                                                  LocalProfileStore store, CancellationToken cancellationToken)
            => calculateAsync(profile, ruleset, realm, beatmaps, store, true, cancellationToken);

        /// <summary>
        /// Only the PP total (for other profiles' chips and the local ranking): nothing else is built.
        /// <c>countEverythingAsRanked</c> overrides Settings → "Count everything as ranked" (to compare PP before and after it changes).
        /// </summary>
        public static async Task<double> CalculatePerformanceAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm, BeatmapManager beatmaps,
                                                                   LocalProfileStore store, CancellationToken cancellationToken, bool? countEverythingAsRanked = null)
            => (await calculateAsync(profile, ruleset, realm, beatmaps, store, false, cancellationToken, countEverythingAsRanked).ConfigureAwait(false)).PP;

        /// <summary>
        /// The PP total with and without one score, from a single pass (the toolbar's PP change after a play, without calculating anything
        /// while the play runs).
        /// </summary>
        public static async Task<(double Without, double With)> CalculatePerformanceChangeAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm,
                                                                                              BeatmapManager beatmaps, LocalProfileStore store, Guid scoreId)
        {
            var result = await calculateAsync(profile, ruleset, realm, beatmaps, store, false, CancellationToken.None, leaveOut: scoreId).ConfigureAwait(false);
            return (result.ppWithoutLeftOut ?? result.PP, result.PP);
        }

        /// <summary>
        /// The PP total without the score passed as <c>leaveOut</c>.
        /// </summary>
        private double? ppWithoutLeftOut { get; init; }

        /// <summary>
        /// Only the PP total and the best PP per beatmap (<see cref="BestPerformance"/>), for the command palette's PP projections.
        /// </summary>
        public static Task<LocalProfileStatistics> CalculateBestPerformanceAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm, BeatmapManager beatmaps,
                                                                                 LocalProfileStore store, CancellationToken cancellationToken)
            => calculateAsync(profile, ruleset, realm, beatmaps, store, false, cancellationToken);

        /// <summary>
        /// Accuracy from the summed hit counts, weighted like the ruleset weighs judgements in a play:
        /// each accuracy-affecting result's base score against the base score of the best result it could have been.
        /// </summary>
        private static double? calculateTotalAccuracy(LocalPlayStatistics plays, RulesetInfo rulesetInfo)
        {
            if (plays.Hits.Count == 0)
                return null;

            Ruleset ruleset;

            try
            {
                ruleset = rulesetInfo.CreateInstance();
            }
            catch
            {
                return null;
            }

            var scoreProcessor = ruleset.CreateScoreProcessor();

            // The best basic judgement of this ruleset (osu! / taiko / catch: Great, mania: Perfect).
            var maxBasic = ruleset.GetHitResultsForDisplay().Select(r => r.result).Append(HitResult.Great)
                                  .Where(r => r.IsBasic() && r.IsHit())
                                  .MaxBy(scoreProcessor.GetBaseScoreForResult);

            double achieved = 0;
            double maximum = 0;

            foreach (var (result, count) in plays.Hits)
            {
                if (count <= 0 || !result.AffectsAccuracy())
                    continue;

                HitResult best;

                switch (result)
                {
                    case HitResult.LargeTickHit:
                    case HitResult.LargeTickMiss:
                        best = HitResult.LargeTickHit;
                        break;

                    case HitResult.SmallTickHit:
                    case HitResult.SmallTickMiss:
                        best = HitResult.SmallTickHit;
                        break;

                    case HitResult.SliderTailHit:
                        best = HitResult.SliderTailHit;
                        break;

                    default:
                        best = result.IsBasic() ? maxBasic : result;
                        break;
                }

                achieved += (double)scoreProcessor.GetBaseScoreForResult(result) * count;
                maximum += (double)scoreProcessor.GetBaseScoreForResult(best) * count;
            }

            return maximum > 0 ? achieved / maximum : null;
        }

        private static async Task<LocalProfileStatistics> calculateAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm, BeatmapManager beatmaps,
                                                                         LocalProfileStore store, bool full, CancellationToken cancellationToken,
                                                                         bool? countEverythingAsRanked = null, Guid? leaveOut = null)
        {
            // The profile's own scores (synced and transferred plays carry its ID too), plus old-style linked owners.
            string userField = $@"{nameof(ScoreInfo.User)}.{nameof(RealmUser.OnlineID)}";
            string nameField = $@"{nameof(ScoreInfo.User)}.{nameof(RealmUser.Username)}";

            var arguments = new List<QueryArgument> { ruleset.ShortName, profile.UserId };
            var owners = new List<string> { $@"{userField} == $1" };

            foreach (var owner in profile.LinkedOwners)
            {
                owners.Add($@"({userField} == ${arguments.Count} AND {nameField} == ${arguments.Count + 1})");
                arguments.Add(owner.UserId);
                arguments.Add(owner.Username);
            }

            var syncedIds = profile.SyncedScores.Select(s => s.ScoreId).ToHashSet();
            var options = store.Options;
            var exclusions = store.PerformanceExclusions.CreateMatcher();
            var modFacts = new ModFactsCache(ruleset);
            bool countAll = profile.CountAllAsRanked || (countEverythingAsRanked ?? options.CountEverythingAsRanked);

            // Read the few values needed from every score inside the database, instead of copying each score with its whole beatmap.
            var scores = realm.Run(r =>
            {
                var results = r.All<ScoreInfo>()
                               .Filter($@"{nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0"
                                       + $@" AND {nameof(ScoreInfo.DeletePending)} == false"
                                       + $@" AND ({string.Join(@" OR ", owners)})", arguments.ToArray());

                var list = new List<ScoreSummary>(results.Count());

                foreach (var score in results)
                {
                    // Settings → Ritsu "Count LOCAL maps towards PP" off: plays on beatmaps changed in the editor aren't part of the profile.
                    if (!options.LocalMapsGivePerformance && score.BeatmapInfo?.Status == BeatmapOnlineStatus.LocallyModified)
                        continue;

                    var facts = modFacts.Get(score, countAll, profile);
                    string? noPerformanceReason = exclusions.GetReason(score) ?? getNoPerformanceReason(score, profile, countAll, facts);

                    var summary = new ScoreSummary
                    {
                        ID = score.ID,
                        BeatmapHash = score.BeatmapHash,
                        BeatmapMD5 = score.BeatmapInfo?.MD5Hash,
                        GrantsPerformance = noPerformanceReason == null,
                        NoPerformanceReason = noPerformanceReason,
                        TotalScore = score.TotalScore,
                        Rank = score.Rank,
                        Accuracy = score.Accuracy,
                        MaxCombo = score.MaxCombo,
                        Date = score.Date,
                    };

                    if (full)
                    {
                        summary.ClassicScore = score.GetDisplayScore(ScoringMode.Classic);

                        if (score.UserID != profile.UserId || syncedIds.Contains(score.ID))
                        {
                            summary.SyncedHits = new Dictionary<HitResult, int>(score.Statistics);

                            double rate = facts.Rate;
                            if (score.BeatmapInfo != null && rate > 0)
                                summary.SyncedSeconds = score.BeatmapInfo.Length / 1000 / rate;
                        }
                    }

                    list.Add(summary);
                }

                return list;
            });

            // PP comes from the cache; only scores never calculated before are loaded in full (once) to calculate it.
            var performance = new Dictionary<Guid, double?>();
            var newlyCalculated = new Dictionary<Guid, double?>();
            var difficulties = new Dictionary<string, DifficultyAttributes?>();

            foreach (var summary in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (store.TryGetCachedPerformance(summary.ID, out double? cached))
                {
                    performance[summary.ID] = cached;
                    continue;
                }

                var score = realm.Run(r => r.Find<ScoreInfo>(summary.ID)?.Detach());
                var (pp, failed) = score == null ? (null, false) : await calculatePerformance(score, beatmaps, difficulties, cancellationToken).ConfigureAwait(false);
                performance[summary.ID] = pp;

                // Scores on missing beatmaps are retried once the beatmap is imported; beatmaps that fail to calculate are remembered
                // (as null) and never calculated again.
                if (pp != null || failed)
                    newlyCalculated[summary.ID] = pp;
            }

            store.SetCachedPerformance(newlyCalculated);

            bool countsForPerformance(ScoreSummary s) => s.GrantsPerformance && performance.GetValueOrDefault(s.ID) != null;

            // Best PP per beatmap.
            var byBeatmap = scores.Where(s => !string.IsNullOrEmpty(s.BeatmapHash)).GroupBy(s => s.BeatmapHash).ToArray();

            var best = byBeatmap
                       .Select(g => g.Where(countsForPerformance).OrderByDescending(s => performance[s.ID]).FirstOrDefault())
                       .Where(s => s != null)
                       .Select(s => (Score: s!, PP: performance[s!.ID]!.Value))
                       .OrderByDescending(s => s.PP)
                       .ToArray();

            double totalPP = WeightedTotal(best.Select(b => b.PP).ToArray());
            var bestPerformance = best.Select(b => (b.Score.BeatmapHash, b.PP)).ToArray();

            if (!full)
            {
                double? withoutLeftOut = null;

                if (leaveOut != null)
                {
                    withoutLeftOut = WeightedTotal(byBeatmap.Select(g => g.Where(s => s.ID != leaveOut && countsForPerformance(s))
                                                                          .Select(s => performance[s.ID]!.Value)
                                                                          .DefaultIfEmpty(double.NaN)
                                                                          .Max())
                                                            .Where(pp => !double.IsNaN(pp))
                                                            .OrderByDescending(pp => pp)
                                                            .ToArray());
                }

                return new LocalProfileStatistics { PP = totalPP, BestPerformance = bestPerformance, ppWithoutLeftOut = withoutLeftOut };
            }

            // Best score per beatmap (by total score) for grades / ranked score.
            var topScorePerBeatmap = byBeatmap.Select(g => g.OrderByDescending(s => s.TotalScore).First()).ToArray();

            double accuracy = 0;

            if (best.Length > 0)
            {
                double weightSum = 0;
                double factor = 1;

                foreach (var b in best)
                {
                    accuracy += b.Score.Accuracy * factor;
                    weightSum += factor;
                    factor *= 0.95;
                }

                accuracy /= weightSum;
            }

            double weight = 1;
            var weightedBest = new List<LocalScoreEntry>();

            foreach (var b in best.Take(BEST_SCORE_LIMIT))
            {
                weightedBest.Add(new LocalScoreEntry(b.Score.ID, b.PP, weight));
                weight *= 0.95;
            }

            // Beatmaps marked LOCAL, when Settings → Ritsu leaves them out of profiles (by MD5 hash, like recorded plays).
            var hiddenBeatmaps = options.LocalMapsGivePerformance
                ? new HashSet<string>()
                : realm.Run(r => r.All<BeatmapInfo>().Filter($@"{nameof(BeatmapInfo.StatusInt)} == $0", (int)BeatmapOnlineStatus.LocallyModified)
                                  .AsEnumerable().Select(b => b.MD5Hash).ToHashSet());

            // Failed and quit plays of the last day are listed with the saved scores, like the website's recent plays.
            var unfinished = store.UnfinishedPlays.Get(profile.UserId, ruleset.ShortName)
                                  .Where(p => !hiddenBeatmaps.Contains(p.BeatmapMD5))
                                  .ToArray();

            var recent = scores.Select(s => (s.Date, Entry: new LocalScoreEntry(s.ID, performance.GetValueOrDefault(s.ID), null, s.NoPerformanceReason)))
                               .Concat(unfinished.Select(p => (p.Date, Entry: new LocalScoreEntry(p.Id, null, null) { Unfinished = p })))
                               .OrderByDescending(s => s.Date)
                               .Take(RECENT_SCORE_LIMIT)
                               .Select(s => s.Entry)
                               .ToArray();

            // Settings → Ritsu "Do not hide unranked scores": per beatmap, the best score that gives no PP is listed too
            // (faded, not weighted) when it would beat the beatmap's counted score.
            IReadOnlyList<LocalScoreEntry> bestScores = weightedBest;

            if (options.ShowNonCountingScores)
            {
                var countedPP = best.ToDictionary(b => b.Score.BeatmapHash, b => b.PP);

                var nonCounting = byBeatmap
                                  .Select(g => g.Where(s => !s.GrantsPerformance && performance.GetValueOrDefault(s.ID) != null)
                                                .OrderByDescending(s => performance[s.ID])
                                                .FirstOrDefault())
                                  .Where(s => s != null && (!countedPP.TryGetValue(s.BeatmapHash, out double counted) || performance[s.ID]!.Value > counted))
                                  .Select(s => new LocalScoreEntry(s!.ID, performance[s.ID], null, s.NoPerformanceReason));

                bestScores = weightedBest.Concat(nonCounting)
                                         .OrderByDescending(e => e.PP ?? 0)
                                         .Take(BEST_SCORE_LIMIT)
                                         .ToArray();
            }

            // PP history: replay the PP-giving scores in the order they were set and recompute the weighted total after each.
            var history = new List<(DateTimeOffset, double)>();
            var bestSoFar = new Dictionary<string, double>();

            // The same values, kept sorted highest first (so each step only moves one value instead of sorting all of them).
            var bestSoFarOrdered = new List<double>();
            var descending = Comparer<double>.Create((a, b) => b.CompareTo(a));

            foreach (var score in scores.Where(s => countsForPerformance(s) && !string.IsNullOrEmpty(s.BeatmapHash)).OrderBy(s => s.Date))
            {
                double pp = performance[score.ID]!.Value;

                if (bestSoFar.TryGetValue(score.BeatmapHash, out double existing) && existing >= pp)
                    continue;

                if (bestSoFar.ContainsKey(score.BeatmapHash))
                    bestSoFarOrdered.RemoveAt(bestSoFarOrdered.BinarySearch(existing, descending));

                int index = bestSoFarOrdered.BinarySearch(pp, descending);
                bestSoFarOrdered.Insert(index < 0 ? ~index : index, pp);

                bestSoFar[score.BeatmapHash] = pp;
                history.Add((score.Date, WeightedTotal(bestSoFarOrdered)));
            }

            var plays = withSyncedScores(store.GetPlayStatistics(profile.Id, ruleset.ShortName), scores.Where(s => s.SyncedHits != null));

            var (mostPlayed, mostPlayedCount) = calculateMostPlayed(plays, scores, hiddenBeatmaps);
            var (monthly, weekly) = calculatePlayHistory(plays, scores);

            // Load the rows shown right away (the rest is loaded when "show more" needs it), still off the update thread.
            LocalScoreEntry.Load(bestScores.Take(PRELOADED_ROWS).Concat(recent.Take(PRELOADED_ROWS)), realm);
            LocalBeatmapEntry.Load(mostPlayed.Take(PRELOADED_ROWS), realm);

            return new LocalProfileStatistics
            {
                MostPlayed = mostPlayed,
                MostPlayedCount = mostPlayedCount,
                MonthlyPlays = monthly,
                WeeklyPlays = weekly,
                History = history,
                PP = totalPP,
                BestPerformance = bestPerformance,
                EstimatedGlobalRank = ruleset.ShortName == @"osu" && best.Length > 0 ? LocalPriorityPolicy.EstimateGlobalRank(totalPP) : null,
                Accuracy = accuracy,
                TotalAccuracy = options.ShowTotalAccuracy ? calculateTotalAccuracy(plays, ruleset) : null,
                Plays = plays,
                SavedScoreCount = scores.Count,
                RecentPlayCount = scores.Count + unfinished.Length,
                RankedScore = topScorePerBeatmap.Where(s => s.GrantsPerformance).Sum(s => s.TotalScore),
                TotalScore = scores.Sum(s => s.TotalScore),
                RankedScoreClassic = topScorePerBeatmap.Where(s => s.GrantsPerformance).Sum(s => s.ClassicScore),
                TotalScoreClassic = scores.Sum(s => s.ClassicScore),
                MaxCombo = scores.Select(s => s.MaxCombo).DefaultIfEmpty().Max(),
                CountSSH = topScorePerBeatmap.Count(s => s.Rank == ScoreRank.XH),
                CountSS = topScorePerBeatmap.Count(s => s.Rank == ScoreRank.X),
                CountSH = topScorePerBeatmap.Count(s => s.Rank == ScoreRank.SH),
                CountS = topScorePerBeatmap.Count(s => s.Rank == ScoreRank.S),
                CountA = topScorePerBeatmap.Count(s => s.Rank == ScoreRank.A),
                BestScores = bestScores,
                RecentScores = recent,
                UncalculatedScores = performance.Count(p => p.Value == null),
            };
        }

        /// <summary>
        /// Synced scores were set before Ritsu counted attempts, so each one counts as one play
        /// (its hits, and the map's length adjusted for rate-changing mods as play time) on top of the recorded attempts.
        /// </summary>
        private static LocalPlayStatistics withSyncedScores(LocalPlayStatistics plays, IEnumerable<ScoreSummary> synced)
        {
            foreach (var score in synced)
            {
                plays.PlayCount++;
                plays.PlayTimeSeconds += score.SyncedSeconds;

                foreach (var (result, count) in score.SyncedHits!)
                {
                    if (count <= 0)
                        continue;

                    plays.Hits.TryGetValue(result, out long existing);
                    plays.Hits[result] = existing + count;
                }
            }

            return plays;
        }

        /// <summary>
        /// Like the website, only ranked and approved beatmaps with ranked mods give PP and ranked score
        /// (Relax / Autopilot never do), unless the profile's options say otherwise.
        /// </summary>
        /// <returns>Why the score gives no PP on this profile, or null when it does.</returns>
        private static string? getNoPerformanceReason(ScoreInfo score, LocalProfile profile, bool countAll, ModFacts facts)
        {
            if (score.BeatmapInfo == null)
                return @"The beatmap of this score is missing.";

            if (!countAll && !score.BeatmapInfo.Status.GrantsPerformancePoints())
                return @"PP is not awarded for unranked beatmaps on this profile.";

            if (!profile.IncludeRelaxAutopilot && facts.RelaxOrAutopilot)
                return @"PP is not awarded for Relax / Autopilot on this profile.";

            if (facts.Unranked)
                return @"PP is not awarded for unranked mods on this profile.";

            return null;
        }

        /// <summary>
        /// What the statistics need from a score's mods.
        /// </summary>
        private readonly record struct ModFacts(bool RelaxOrAutopilot, bool Unranked, double Rate);

        /// <summary>
        /// Works out <see cref="ModFacts"/> once per different set of mods in one statistics run: scores share a handful of mod
        /// combinations, and building a score's mods (<see cref="ScoreInfo.Mods"/>) makes a new ruleset instance for every mod.
        /// </summary>
        private sealed class ModFactsCache
        {
            private static readonly ModFacts no_mods = new ModFacts(false, false, 1);

            private readonly RulesetInfo rulesetInfo;
            private readonly Dictionary<(string Mods, bool Legacy), ModFacts> facts = new Dictionary<(string, bool), ModFacts>();
            private Ruleset? ruleset;

            public ModFactsCache(RulesetInfo rulesetInfo)
            {
                this.rulesetInfo = rulesetInfo;
            }

            /// <remarks>The profile and <paramref name="countAll"/> are the same for every score of one run.</remarks>
            public ModFacts Get(ScoreInfo score, bool countAll, LocalProfile profile)
            {
                string json = score.ModsJson;

                if (string.IsNullOrEmpty(json))
                    return no_mods;

                var key = (json, score.IsLegacyScore);

                if (facts.TryGetValue(key, out var known))
                    return known;

                ruleset ??= rulesetInfo.CreateInstance();

                var mods = score.APIMods.Select(m => m.ToMod(ruleset)).ToArray();

                return facts[key] = new ModFacts(
                    mods.Any(LocalPriorityPolicy.IsRelaxOrAutopilot),
                    LocalPriorityPolicy.HasUnrankedMods(mods, score.IsLegacyScore, countAll, profile.IncludeRelaxAutopilot, profile.IgnoreUnrankedMods),
                    ModUtils.CalculateRateWithMods(mods));
            }
        }

        /// <summary>
        /// The score's PP, calculated here instead of through the game's difficulty cache: a beatmap that can't be calculated
        /// is only logged quietly (the difficulty cache shows an error notification every time).
        /// </summary>
        /// <returns>The PP (null without), and whether the beatmap's difficulty failed to calculate (as opposed to the beatmap being missing).</returns>
        private static async Task<(double? PP, bool Failed)> calculatePerformance(ScoreInfo score, BeatmapManager beatmaps, Dictionary<string, DifficultyAttributes?> difficulties,
                                                                                   CancellationToken cancellationToken)
        {
            if (score.BeatmapInfo == null || score.BeatmapInfo.Hash != score.BeatmapHash)
                return (null, false);

            try
            {
                var ruleset = score.Ruleset.CreateInstance();
                var calculator = ruleset.CreatePerformanceCalculator();

                if (calculator == null)
                    return (null, false);

                // Same beatmap, mode and mods: the same difficulty (calculated once per statistics run).
                string key = $@"{score.BeatmapInfo.ID}|{score.Ruleset.ShortName}|{string.Join(@",", score.Mods.Select(m => Newtonsoft.Json.JsonConvert.SerializeObject(new APIMod(m))).OrderBy(m => m))}";

                if (!difficulties.TryGetValue(key, out var attributes))
                {
                    try
                    {
                        var working = beatmaps.GetWorkingBeatmap(score.BeatmapInfo);
                        attributes = ruleset.CreateDifficultyCalculator(working).Calculate(score.Mods, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Logger.Log($@"Ritsu: could not calculate the difficulty of {score.BeatmapInfo} ({score.Ruleset.ShortName}): {e.Message}");
                        attributes = null;
                    }

                    difficulties[key] = attributes;
                }

                if (attributes == null)
                    return (null, true);

                var result = await calculator.CalculateAsync(score, attributes, cancellationToken).ConfigureAwait(false);
                return double.IsFinite(result.Total) ? (result.Total, false) : (null, true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Log($@"Ritsu: could not calculate PP for score {score.ID}: {e.Message}");
                return (null, true);
            }
        }
    }

    /// <summary>
    /// A score row on the profile page: its ID and PP right away, the full score only once the row is shown.
    /// </summary>
    public class LocalScoreEntry
    {
        public Guid ID { get; }
        public double? PP { get; }
        public double? Weight { get; }

        /// <summary>
        /// Why this score gives no PP on the profile (shown faded), or null when it counts.
        /// </summary>
        public string? NoPerformanceReason { get; }

        public bool CountsForPerformance => NoPerformanceReason == null;

        /// <summary>
        /// A failed or quit play (not a saved score): shown in Recent Plays only, not clickable.
        /// </summary>
        public LocalUnfinishedPlay? Unfinished { get; init; }

        /// <summary>
        /// The full score, once loaded (null if it isn't loaded yet or no longer exists).
        /// </summary>
        public ScoreInfo? Score { get; private set; }

        private bool loaded;

        public LocalScoreEntry(Guid id, double? pp, double? weight, string? noPerformanceReason = null)
        {
            ID = id;
            PP = pp;
            Weight = weight;
            NoPerformanceReason = noPerformanceReason;
        }

        /// <summary>
        /// Loads the scores of these entries that aren't loaded yet (one database read for all of them).
        /// </summary>
        public static void Load(IEnumerable<LocalScoreEntry> entries, RealmAccess realm)
        {
            var missing = entries.Where(e => !e.loaded).Distinct().ToArray();

            if (missing.Length == 0)
                return;

            realm.Run(r =>
            {
                foreach (var entry in missing)
                {
                    entry.Score = entry.Unfinished != null ? entry.Unfinished.ToScore(r) : r.Find<ScoreInfo>(entry.ID)?.Detach();
                    entry.loaded = true;
                }
            });
        }
    }

    /// <summary>
    /// A "Most Played Beatmaps" row: the beatmap's MD5 hash and play count right away, the beatmap only once the row is shown.
    /// </summary>
    public class LocalBeatmapEntry
    {
        public string MD5Hash { get; }
        public long Plays { get; }

        public BeatmapInfo? Beatmap { get; private set; }

        private bool loaded;

        public LocalBeatmapEntry(string md5Hash, long plays)
        {
            MD5Hash = md5Hash;
            Plays = plays;
        }

        public static void Load(IEnumerable<LocalBeatmapEntry> entries, RealmAccess realm)
        {
            var missing = entries.Where(e => !e.loaded).ToArray();

            if (missing.Length == 0)
                return;

            realm.Run(r =>
            {
                // One query for every row (the hash isn't indexed, so each lookup would read the whole beatmap table).
                var hashes = missing.Select(e => e.MD5Hash).Distinct().ToArray();
                string filter = string.Join(@" OR ", hashes.Select((_, i) => $@"{nameof(BeatmapInfo.MD5Hash)} == ${i}"));
                var found = new Dictionary<string, BeatmapInfo>();

                foreach (var beatmap in r.All<BeatmapInfo>().Filter(filter, hashes.Select(h => (QueryArgument)h).ToArray()))
                    found.TryAdd(beatmap.MD5Hash, beatmap);

                foreach (var entry in missing)
                {
                    entry.Beatmap = found.GetValueOrDefault(entry.MD5Hash)?.Detach();
                    entry.loaded = true;
                }
            });
        }
    }
}
