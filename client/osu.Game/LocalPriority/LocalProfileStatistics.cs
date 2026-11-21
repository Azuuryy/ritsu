// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Utils;

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
        private static (IReadOnlyList<LocalBeatmapEntry>, int) calculateMostPlayed(LocalPlayStatistics plays, IReadOnlyList<ScoreSummary> scores)
        {
            var perBeatmap = new Dictionary<string, long>(plays.BeatmapPlays.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value));

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
            public string? ModsJson;
            public double? StoredPP;
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
                                                                  LocalProfileManager manager, CancellationToken cancellationToken, Action<LocalProfileStatistics>? preliminary = null)
            => calculateAsync(profile, ruleset, realm, beatmaps, manager, true, cancellationToken, preliminary);

        /// <summary>
        /// Only the PP total (for other profiles' chips and the local ranking): nothing else is built.
        /// </summary>
        public static async Task<double> CalculatePerformanceAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm, BeatmapManager beatmaps,
                                                                   LocalProfileManager manager, CancellationToken cancellationToken)
            => (await calculateAsync(profile, ruleset, realm, beatmaps, manager, false, cancellationToken).ConfigureAwait(false)).PP;

        /// <summary>
        /// Only the PP total and the best PP per beatmap (<see cref="BestPerformance"/>), for the command palette's PP projections.
        /// </summary>
        public static Task<LocalProfileStatistics> CalculateBestPerformanceAsync(LocalProfile profile, RulesetInfo ruleset, RealmAccess realm, BeatmapManager beatmaps,
                                                                                 LocalProfileManager manager, CancellationToken cancellationToken)
            => calculateAsync(profile, ruleset, realm, beatmaps, manager, false, cancellationToken);

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
                                                                         LocalProfileManager manager, bool full, CancellationToken cancellationToken, Action<LocalProfileStatistics>? preliminary = null)
        {
            var store = manager.Store;
            var ids = manager.Profiles.GetProfilePlayIds(profile.Id, ruleset.ShortName);
            var options = store.Options;
            var exclusions = store.PerformanceExclusions;

            BeatmapInfo? versionMap = null;
            var scores = realm.Run(r =>
            {
                var results = ids.Keys.Select(id => r.Find<ScoreInfo>(id))
                                 .OfType<ScoreInfo>()
                                 .Where(score => !score.DeletePending && score.Ruleset.ShortName == ruleset.ShortName);

                var list = new List<ScoreSummary>(ids.Count);

                foreach (var score in results)
                {
                    string? noPerformanceReason = exclusions.GetReason(score) ?? getNoPerformanceReason(score, profile, options.CountEverythingAsRanked);

                    bool mapAvailable = score.BeatmapInfo != null && score.BeatmapInfo.Hash == score.BeatmapHash;
                    if (mapAvailable && versionMap == null)
                        versionMap = score.BeatmapInfo!.Detach();

                    var summary = new ScoreSummary
                    {
                        ID = score.ID,
                        ModsJson = mapAvailable ? score.ModsJson : null,
                        StoredPP = score.PP,
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

                        if (!ids[score.ID])
                        {
                            summary.SyncedHits = new Dictionary<HitResult, int>(score.Statistics);

                            double rate = ModUtils.CalculateRateWithMods(score.Mods);
                            if (score.BeatmapInfo != null && rate > 0)
                                summary.SyncedSeconds = score.BeatmapInfo.Length / 1000 / rate;
                        }
                    }

                    list.Add(summary);
                }

                return list;
            });

            var performance = new System.Collections.Concurrent.ConcurrentDictionary<Guid, double?>();
            var newlyCalculated = new System.Collections.Concurrent.ConcurrentDictionary<Guid, LocalProfileStore.ScorePerformance>();
            var missing = new List<(Guid Id, string Map, string Mods, string Key)>();
            int version = versionMap == null ? 0 : ruleset.CreateInstance().CreateDifficultyCalculator(beatmaps.GetWorkingBeatmap(versionMap)).Version;

            foreach (var summary in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (summary.ModsJson == null)
                {
                    performance[summary.ID] = null;
                    continue;
                }

                string key = BeatmapDifficultyCache.CreateStablePerformanceKey(version, ruleset.ShortName, summary.BeatmapHash, summary.ModsJson);
                if (summary.StoredPP is double pp && double.IsFinite(pp) && pp >= 0 && manager.Profiles.GetPerformanceKey(summary.ID) == key)
                    performance[summary.ID] = pp;
                else if (store.TryGetCachedPerformance(summary.ID, key, out double? cached))
                    performance[summary.ID] = cached;
                else if (summary.GrantsPerformance || full)
                    missing.Add((summary.ID, summary.BeatmapHash, summary.ModsJson, key));
            }

            await Parallel.ForEachAsync(missing.GroupBy(score => (score.Map, score.Mods)),
                new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Math.Max(1, Environment.ProcessorCount - 2)), CancellationToken = cancellationToken },
                async (group, token) =>
                {
                    var scoreIds = group.ToDictionary(score => score.Id, score => score.Key);
                    var batch = realm.Run(r => scoreIds.Keys.Select(id => r.Find<ScoreInfo>(id))
                        .OfType<ScoreInfo>().Where(score => !score.DeletePending && score.BeatmapInfo?.Hash == score.BeatmapHash).Detach());
                    if (batch.Count == 0)
                        return;

                    var first = batch[0];
                    var difficulty = await manager.DifficultyCache.GetProfileDifficultyAsync(first.BeatmapInfo!, ruleset, first.Mods, token).ConfigureAwait(false);
                    var calculator = ruleset.CreateInstance().CreatePerformanceCalculator();

                    foreach (var score in batch)
                    {
                        token.ThrowIfCancellationRequested();
                        double? pp = null;
                        if (difficulty?.DifficultyAttributes is { } attributes && calculator != null)
                        {
                            try
                            {
                                double total = (await calculator.CalculateAsync(score, attributes, token).ConfigureAwait(false)).Total;
                                if (double.IsFinite(total) && total >= 0)
                                    pp = total;
                            }
                            catch (Exception e) when (e is not OperationCanceledException)
                            {
                                Logger.Error(e, $"Ritsu: could not calculate PP for score {score.ID}");
                            }
                        }

                        performance[score.ID] = pp;
                        newlyCalculated[score.ID] = new LocalProfileStore.ScorePerformance(scoreIds[score.ID], pp);
                    }
                }).ConfigureAwait(false);

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
                return new LocalProfileStatistics { PP = totalPP, BestPerformance = bestPerformance };

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

            var recent = scores.OrderByDescending(s => s.Date)
                               .Take(RECENT_SCORE_LIMIT)
                               .Select(s => new LocalScoreEntry(s.ID, performance.GetValueOrDefault(s.ID), null, s.NoPerformanceReason))
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

            if (preliminary != null)
            {
                LocalScoreEntry.Load(bestScores.Take(PRELOADED_ROWS), realm);
                preliminary(new LocalProfileStatistics { PP = totalPP, BestScores = bestScores, BestPerformance = bestPerformance, SavedScoreCount = scores.Count });
            }

            // PP history: replay the PP-giving scores in the order they were set and recompute the weighted total after each.
            var history = new List<(DateTimeOffset, double)>();
            var bestSoFar = new Dictionary<string, double>();

            foreach (var score in scores.Where(s => countsForPerformance(s) && !string.IsNullOrEmpty(s.BeatmapHash)).OrderBy(s => s.Date))
            {
                double pp = performance[score.ID]!.Value;

                if (bestSoFar.TryGetValue(score.BeatmapHash, out double existing) && existing >= pp)
                    continue;

                bestSoFar[score.BeatmapHash] = pp;
                history.Add((score.Date, WeightedTotal(bestSoFar.Values.OrderByDescending(v => v).ToArray())));
            }

            var plays = withSyncedScores(store.GetPlayStatistics(profile.Id, ruleset.ShortName), scores.Where(s => s.SyncedHits != null));

            var (mostPlayed, mostPlayedCount) = calculateMostPlayed(plays, scores);
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
        private static string? getNoPerformanceReason(ScoreInfo score, LocalProfile profile, bool countEverythingAsRanked)
        {
            if (score.BeatmapInfo == null)
                return @"The beatmap of this score is missing.";

            bool countAll = profile.CountAllAsRanked || countEverythingAsRanked;

            if (!countAll && !score.BeatmapInfo.Status.GrantsPerformancePoints())
                return @"PP is not awarded for unranked beatmaps on this profile.";

            if (!profile.IncludeRelaxAutopilot && score.Mods.Any(LocalPriorityPolicy.IsRelaxOrAutopilot))
                return @"PP is not awarded for Relax / Autopilot on this profile.";

            if (LocalPriorityPolicy.HasUnrankedMods(score, countAll, profile.IncludeRelaxAutopilot))
                return @"PP is not awarded for unranked mods on this profile.";

            return null;
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
                    entry.Score = r.Find<ScoreInfo>(entry.ID)?.Detach();
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
                foreach (var entry in missing)
                {
                    entry.Beatmap = r.All<BeatmapInfo>().FirstOrDefault(b => b.MD5Hash == entry.MD5Hash)?.Detach();
                    entry.loaded = true;
                }
            });
        }
    }
}
