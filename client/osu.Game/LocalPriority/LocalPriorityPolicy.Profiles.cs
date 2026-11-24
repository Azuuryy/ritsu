// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Local profiles: who is playing, and how much of the network they may use.
    /// </summary>
    /// <remarks>
    /// A local profile sends nothing to osu! servers, unless Settings → Ritsu → "Use the online account for lookups" is on:
    /// then the same read-only requests go out with the signed-in online account (the local profile stays the player; nothing is submitted).
    /// </remarks>
    public static partial class LocalPriorityPolicy
    {
        /// <summary>
        /// Local profiles use negative user IDs at or below this value, so they can never collide with real osu! accounts.
        /// </summary>
        public const int LOCAL_PROFILE_ID_CEILING = -2;

        private static volatile bool localProfileActive;

        /// <summary>
        /// Whether a local profile is currently active. While true, no network traffic to osu! is allowed at all
        /// (unless <see cref="LocalProfilesUseOnlineAccount"/> is on).
        /// </summary>
        public static bool OfflineProfileActive
        {
            get => localProfileActive;
            internal set
            {
                if (localProfileActive == value)
                    return;

                localProfileActive = value;
                Logger.Log(value ? "Local profile active." : "Online account active: read-only osu! network access.", LoggingTarget.Network);
            }
        }

        private static volatile bool localProfilesUseOnlineAccount;

        /// <summary>
        /// Settings → Ritsu: while a local profile is active, the signed-in online account may still be used for the read-only
        /// requests (beatmap lookup / download, tags, leaderboards). Off by default.
        /// </summary>
        public static bool LocalProfilesUseOnlineAccount
        {
            get => localProfilesUseOnlineAccount;
            internal set
            {
                if (localProfilesUseOnlineAccount == value)
                    return;

                localProfilesUseOnlineAccount = value;
                Logger.Log(value
                    ? "Local profiles may use the online account for read-only lookups."
                    : "Local profiles are fully offline.", LoggingTarget.Network);
            }
        }

        /// <summary>
        /// Whether the active player (online account, or a local profile with online lookups on) may reach osu! servers at all.
        /// </summary>
        public static bool NetworkAllowed => !OfflineProfileActive || LocalProfilesUseOnlineAccount;

        /// <summary>
        /// The signed-in online account, kept while a local profile is shown as the player (for supporter / team checks on leaderboards).
        /// </summary>
        public static APIUser? OnlineAccountUser { get; internal set; }

        /// <summary>
        /// Whether the given user is a local, offline-only profile.
        /// </summary>
        public static bool IsOfflineProfileUser(APIUser? user) => user != null && user.Id <= LOCAL_PROFILE_ID_CEILING;

        #region Per-profile ranking options (active local profile only; always off for the online account)

        /// <summary>
        /// The active local profile counts every play as if it was set on a ranked / approved beatmap with ranked mods,
        /// and the "unranked" notices are hidden. Beatmaps themselves (and the song list) are never changed.
        /// </summary>
        public static bool CountAllAsRanked { get; internal set; }

        /// <summary>
        /// The active local profile counts Relax / Autopilot plays towards PP.
        /// </summary>
        public static bool CountRelaxAutopilot { get; internal set; }

        /// <summary>
        /// The active local profile ignores mods' "unranked" status (Relax / Autopilot still follow <see cref="CountRelaxAutopilot"/>).
        /// </summary>
        public static bool IgnoreUnrankedMods { get; internal set; }

        public static bool IsRelaxOrAutopilot(IMod mod) => mod.Acronym is @"RX" or @"AP";

        /// <summary>
        /// A copy of what local profiles exclude from PP, kept up to date by <see cref="LocalProfileStore"/>.
        /// Ritsu's local ranked status (status pill, mod select, filters) reads it, so excluded beatmaps show as unranked.
        /// </summary>
        public static LocalPerformanceExclusions PerformanceExclusions { get; internal set; } = new LocalPerformanceExclusions();

        /// <summary>
        /// Whether the active local profile gets no PP from this difficulty (it or its beatmap set is excluded).
        /// </summary>
        public static bool IsExcludedFromPerformance(BeatmapInfo beatmap)
        {
            if (!OfflineProfileActive)
                return false;

            var exclusions = PerformanceExclusions;
            return exclusions.Beatmaps.Contains(beatmap.ID) || (beatmap.BeatmapSet != null && exclusions.BeatmapSets.Contains(beatmap.BeatmapSet.ID));
        }

        /// <summary>
        /// Whether the active local profile gets no PP from any difficulty of this beatmap set.
        /// </summary>
        public static bool IsExcludedFromPerformance(BeatmapSetInfo set)
        {
            if (!OfflineProfileActive)
                return false;

            var exclusions = PerformanceExclusions;
            return exclusions.BeatmapSets.Contains(set.ID) || (set.Beatmaps.Count > 0 && set.Beatmaps.All(b => exclusions.Beatmaps.Contains(b.ID)));
        }

        /// <summary>
        /// Settings → Ritsu "Count LOCAL maps towards PP": beatmaps changed in the editor can give PP (when counted as ranked).
        /// </summary>
        public static bool LocalMapsGivePerformance { get; internal set; } = true;

        /// <summary>
        /// Whether a beatmap status gives PP, taking <see cref="CountAllAsRanked"/> and <see cref="LocalMapsGivePerformance"/> into account.
        /// </summary>
        public static bool GrantsPerformance(BeatmapOnlineStatus status) =>
            status.GrantsPerformancePoints() || (CountAllAsRanked && (status != BeatmapOnlineStatus.LocallyModified || LocalMapsGivePerformance));

        /// <summary>
        /// Whether a mod counts as ranked, taking the active profile's options into account.
        /// </summary>
        public static bool IsModRanked(Mod mod) => isModRanked(mod, CountAllAsRanked, CountRelaxAutopilot, IgnoreUnrankedMods);

        /// <summary>
        /// Same rule as the results screen: classic mod on legacy (imported) scores doesn't make a score unranked.
        /// </summary>
        public static bool HasUnrankedMods(ScoreInfo score, bool countAllAsRanked, bool countRelaxAutopilot, bool ignoreUnrankedMods = false) =>
            // Counting everything as ranked: no need to build the score's mods at all.
            !countAllAsRanked && HasUnrankedMods(score.Mods, score.IsLegacyScore, false, countRelaxAutopilot, ignoreUnrankedMods);

        /// <inheritdoc cref="HasUnrankedMods(ScoreInfo, bool, bool, bool)"/>
        public static bool HasUnrankedMods(IEnumerable<Mod> mods, bool isLegacyScore, bool countAllAsRanked, bool countRelaxAutopilot, bool ignoreUnrankedMods = false) =>
            !countAllAsRanked && mods.Where(m => !(isLegacyScore && m is ModClassic))
                                     .Any(m => !isModRanked(m, false, countRelaxAutopilot, ignoreUnrankedMods));

        private static bool isModRanked(IMod mod, bool countAllAsRanked, bool countRelaxAutopilot, bool ignoreUnrankedMods)
        {
            if (countAllAsRanked)
                return true;

            if (IsRelaxOrAutopilot(mod))
                return countRelaxAutopilot;

            return ignoreUnrankedMods || mod.Ranked;
        }

        #endregion

        #region Estimated global rank

        // PP-to-rank curve as used by Ritsu (copied there from Modosu by Azuuryy, derived from MOsu by p-720, GPL-3.0),
        // attributed to a September 2026 sample of 10,000 osu! profiles. Standard (osu!) mode only. Offline estimate, not an official rank.
        private static readonly (double PP, int Rank)[] rank_curve =
        {
            (2.11, 2852394), (45.65, 2142296), (139.97, 1750230), (375.42, 1336787),
            (721.22, 1020639), (1148.03, 779960), (1621.4, 595532), (2125.51, 454565),
            (2708.02, 335739), (3302.75, 247883), (3978.95, 177279), (4665.01, 126882),
            (5403.43, 87234), (6164.53, 58191), (6911.02, 39304), (7677.87, 26024),
            (8519.1, 16828), (9518.04, 9896), (10741.3, 4951), (11762, 2910),
            (13010.5, 1549), (15002.6, 553), (17051.7, 211), (18305.4, 120),
        };

        /// <summary>
        /// Estimated global rank for a total PP value, by linear interpolation along a sampled PP-to-rank curve.
        /// </summary>
        public static int EstimateGlobalRank(double pp)
        {
            if (pp <= rank_curve[0].PP)
                return rank_curve[0].Rank;
            if (pp >= rank_curve[^1].PP)
                return rank_curve[^1].Rank;

            for (int i = 1; i < rank_curve.Length; i++)
            {
                if (pp > rank_curve[i].PP)
                    continue;

                var previous = rank_curve[i - 1];
                var next = rank_curve[i];
                double progress = (pp - previous.PP) / (next.PP - previous.PP);
                return (int)Math.Round(previous.Rank + (next.Rank - previous.Rank) * progress);
            }

            return rank_curve[^1].Rank;
        }

        #endregion

        private static APIUser? initialLocalUser;

        /// <summary>
        /// Set before the API is created, so that a game started in a local profile never touches the network, not even briefly.
        /// </summary>
        internal static void SetInitialLocalUser(APIUser? user)
        {
            initialLocalUser = user;
            OfflineProfileActive = user != null;
        }

        internal static APIUser? TakeInitialLocalUser()
        {
            var user = initialLocalUser;
            initialLocalUser = null;
            return user;
        }
    }
}
