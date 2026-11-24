// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.LocalPriority
{
    public partial class LocalProfileManager
    {
        /// <summary>
        /// Ritsu: the local profile's PP and estimated global rank before and after a score was saved (or deleted / excluded from PP),
        /// shown by the toolbar the same way the online account's statistics update is shown after a score is processed.
        /// </summary>
        public IBindable<ScoreBasedUserStatisticsUpdate?> LatestStatisticsUpdate => latestStatisticsUpdate;

        private readonly Bindable<ScoreBasedUserStatisticsUpdate?> latestStatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        /// <summary>
        /// Remembers which local profile a play is for. Nothing is calculated while it runs: once its score is saved, the statistics are
        /// calculated with and without that score in one go. Returns null when no local profile is active.
        /// </summary>
        public LocalStatisticsUpdateTracker? BeginStatisticsUpdate(RulesetInfo ruleset)
        {
            var profile = activeProfile.Value;

            if (profile == null || beatmapManager == null || !ruleset.IsLegacyRuleset())
                return null;

            return new LocalStatisticsUpdateTracker(profile.Id, profile.UserId, ruleset);
        }

        /// <summary>
        /// Called once the score of a tracked play was saved: calculates the statistics with and without it and shows the change.
        /// </summary>
        public void CompleteStatisticsUpdate(LocalStatisticsUpdateTracker tracker, ScoreInfo score)
        {
            var beatmaps = beatmapManager;

            if (score.UserID != tracker.UserId || beatmaps == null)
                return;

            Task.Run(async () =>
            {
                try
                {
                    var profile = activeProfile.Value;

                    if (profile == null || profile.Id != tracker.ProfileId)
                        return;

                    var (ppBefore, ppAfter) = await LocalProfileStatistics.CalculatePerformanceChangeAsync(profile, tracker.Ruleset, realm, beatmaps, Store, score.ID)
                                                                          .ConfigureAwait(false);

                    var before = toUserStatistics(ppBefore, tracker.Ruleset);
                    var after = toUserStatistics(ppAfter, tracker.Ruleset);

                    // Plays that give no PP (unranked mods, unranked maps, or simply not a new top play) change nothing: show nothing.
                    if (after.PP == before.PP && after.GlobalRank == before.GlobalRank)
                        return;

                    Schedule(() =>
                    {
                        if (activeProfile.Value?.Id == tracker.ProfileId)
                            latestStatisticsUpdate.Value = new ScoreBasedUserStatisticsUpdate(score, before, after);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, @"Ritsu: could not calculate the local profile's statistics update.");
                }
            });
        }

        /// <summary>
        /// Deletes a score; when it belongs to the active local profile, the toolbar shows how its PP and estimated rank changed.
        /// <paramref name="delete"/> runs on the update thread.
        /// </summary>
        public void DeleteScoreShowingChange(ScoreInfo score, Action delete)
        {
            var profile = activeProfile.Value;

            if (profile == null || score.UserID != profile.UserId)
            {
                delete();
                return;
            }

            ApplyShowingStatisticsChange(delete, score.Ruleset, score);
        }

        /// <summary>
        /// Excludes / includes a play, difficulty or beatmap set (every local profile); the toolbar shows how the active profile's
        /// PP and estimated rank changed (current mode).
        /// </summary>
        public void SetExcludedFromPerformance(LocalPerformanceExclusionKind kind, Guid id, bool excluded) =>
            ApplyShowingStatisticsChange(() => Store.SetExcludedFromPerformance(kind, id, excluded), gameRuleset.Value);

        /// <summary>
        /// Applies a change that can affect the active local profile's PP, then shows the difference on the toolbar (only if anything changed).
        /// The current statistics are calculated first, so <paramref name="change"/> runs a moment later, on the update thread.
        /// </summary>
        public void ApplyShowingStatisticsChange(Action change, RulesetInfo? ruleset, ScoreInfo? score = null)
        {
            var profile = activeProfile.Value;

            if (profile == null || beatmapManager == null || ruleset == null || !ruleset.IsLegacyRuleset())
            {
                change();
                return;
            }

            ruleset = ruleset.Detach();

            Task.Run(async () =>
            {
                var before = await calculateUserStatistics(profile, ruleset).ConfigureAwait(false);

                var applied = new TaskCompletionSource();

                Schedule(() =>
                {
                    try
                    {
                        change();
                    }
                    finally
                    {
                        applied.SetResult();
                    }
                });

                await applied.Task.ConfigureAwait(false);

                var current = activeProfile.Value;

                if (before == null || current == null || current.Id != profile.Id)
                    return;

                var after = await calculateUserStatistics(current, ruleset).ConfigureAwait(false);

                if (after == null || (after.PP == before.PP && after.GlobalRank == before.GlobalRank))
                    return;

                Schedule(() =>
                {
                    if (activeProfile.Value?.Id == profile.Id)
                        latestStatisticsUpdate.Value = new ScoreBasedUserStatisticsUpdate(score ?? new ScoreInfo(), before, after);
                });
            });
        }

        /// <summary>
        /// Shows on the toolbar how the active profile's PP and estimated rank changed after its ranking options (or Settings →
        /// "Count everything as ranked") were changed. The change must already be saved; <paramref name="before"/> is the profile
        /// as it was, and <paramref name="countEverythingBefore"/> the old global setting.
        /// </summary>
        public void ShowRankingChange(LocalProfile before, bool countEverythingBefore, RulesetInfo? ruleset)
        {
            var after = Store.ActiveProfile;
            bool countEverythingAfter = Store.Options.CountEverythingAsRanked;

            if (after == null || after.Id != before.Id || beatmapManager == null || ruleset == null || !ruleset.IsLegacyRuleset())
                return;

            ruleset = ruleset.Detach();

            Task.Run(async () =>
            {
                var oldStatistics = await calculateUserStatistics(before, ruleset, countEverythingBefore).ConfigureAwait(false);
                var newStatistics = await calculateUserStatistics(after, ruleset, countEverythingAfter).ConfigureAwait(false);

                if (oldStatistics == null || newStatistics == null)
                    return;

                if (oldStatistics.PP == newStatistics.PP && oldStatistics.GlobalRank == newStatistics.GlobalRank)
                    return;

                Schedule(() =>
                {
                    if (activeProfile.Value?.Id == after.Id)
                        latestStatisticsUpdate.Value = new ScoreBasedUserStatisticsUpdate(new ScoreInfo(), oldStatistics, newStatistics);
                });
            });
        }

        private async Task<UserStatistics?> calculateUserStatistics(LocalProfile profile, RulesetInfo ruleset, bool? countEverythingAsRanked = null)
        {
            if (beatmapManager == null)
                return null;

            try
            {
                double pp = await LocalProfileStatistics.CalculatePerformanceAsync(profile, ruleset, realm, beatmapManager, Store, CancellationToken.None,
                                                                       countEverythingAsRanked)
                                                        .ConfigureAwait(false);

                return toUserStatistics(pp, ruleset);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, @"Ritsu: could not calculate the local profile's statistics.");
                return null;
            }
        }

        private static UserStatistics toUserStatistics(double pp, RulesetInfo ruleset) => new UserStatistics
        {
            PP = (decimal)pp,
            // Same estimate as the profile page: osu! standard only, and only once there is any PP.
            GlobalRank = ruleset.ShortName == @"osu" && pp > 0 ? LocalPriorityPolicy.EstimateGlobalRank(pp) : null,
        };
    }

    /// <summary>
    /// The local profile a play is for, for <see cref="LocalProfileManager.CompleteStatisticsUpdate"/>.
    /// </summary>
    public class LocalStatisticsUpdateTracker
    {
        public Guid ProfileId { get; }
        public int UserId { get; }
        public RulesetInfo Ruleset { get; }

        public LocalStatisticsUpdateTracker(Guid profileId, int userId, RulesetInfo ruleset)
        {
            ProfileId = profileId;
            UserId = userId;
            Ruleset = ruleset;
        }
    }
}
