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
        public static bool LocalMapsGivePerformance { get; internal set; }

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

        // PP-to-rank curves from the osu! performance data dumps of 2026-11-05 (every player of the top 10,000 and 10,000 random
        // players per ruleset), highest PP first. Offline estimate, not an official rank.
        private static readonly (double PP, int Rank)[] osu_rank_curve =
        {
            (32269, 1), (30616.9, 2), (30149.5, 3), (27135.2, 5), (26407.6, 7),
            (24119.4, 11), (23160.3, 16), (21742, 25), (21135.8, 37), (20148.7, 55),
            (19274.5, 82), (18506.6, 122), (17458.8, 182), (16634.1, 272), (15796.3, 406),
            (14939.8, 606), (14130.9, 905), (13343.1, 1350), (12569.6, 2015), (11796.6, 3008),
            (11007.9, 4489), (10292.4, 6700), (9604.45, 10000), (8360.95, 20000), (7515.49, 30000),
            (6569.26, 50000), (6168.49, 60000), (5652.95, 80000), (5058.52, 110000), (4419.15, 150000),
            (3766.61, 210000), (3161.53, 280000), (2567.87, 380000), (1975.74, 520000), (1443.37, 700000),
            (930.88, 950000), (508.4, 1280000), (198.46, 1740000), (43.83, 2360000), (2.11, 3192119),
        };

        private static readonly (double PP, int Rank)[] taiko_rank_curve =
        {
            (28614.7, 1), (27080.2, 2), (24317.2, 3), (24013.2, 5), (23198, 7),
            (22489.5, 11), (21666.7, 16), (20922, 25), (20391.2, 37), (19757.6, 55),
            (18587.1, 82), (17735.4, 122), (16732.9, 182), (15607.2, 272), (14360.6, 406),
            (13135.7, 606), (11915.3, 905), (10721, 1350), (9360.02, 2015), (7984.31, 3008),
            (6629.55, 4489), (5255.1, 6700), (3937.78, 10000), (3270.9, 12000), (2629.12, 15000),
            (2202.77, 18000), (1857.7, 21000), (1397.44, 26000), (1121, 31000), (842.77, 37000),
            (593.62, 45000), (395.63, 55000), (263.95, 66000), (170.72, 80000), (110.03, 96000),
            (68.63, 116000), (42.65, 140000), (25.85, 169000), (14.67, 205000), (7.91, 247000),
            (3.79, 298000), (2.08, 360246),
        };

        private static readonly (double PP, int Rank)[] catch_rank_curve =
        {
            (28004.3, 1), (27197.1, 2), (27079.3, 3), (26275.5, 5), (25204.9, 7),
            (24319.1, 11), (22975.6, 16), (21836.4, 25), (21072.6, 37), (20250.4, 55),
            (19343.2, 82), (18419.1, 122), (16880.1, 182), (15059.7, 272), (13287, 406),
            (11374.4, 606), (9462.41, 905), (7835.87, 1350), (6135.53, 2015), (4673.21, 3008),
            (3534.09, 4489), (2632.3, 6700), (1964.87, 10000), (1644.88, 12000), (1405.63, 14000),
            (1149.84, 17000), (892.29, 21000), (685.23, 25000), (520.73, 29000), (355.6, 35000),
            (225.83, 42000), (141.85, 50000), (89.83, 60000), (57.86, 72000), (38.12, 87000),
            (25.85, 104000), (17.79, 124000), (12.28, 149000), (8.51, 178000), (5.71, 213000),
            (3.49, 255000), (2.08, 304984),
        };

        private static readonly (double PP, int Rank)[] mania_rank_curve =
        {
            (30419.3, 1), (29184.6, 2), (29117.4, 3), (27723.2, 5), (27400.2, 7),
            (26927.6, 11), (26384.2, 16), (25285.6, 25), (24942.8, 37), (23869.7, 55),
            (22649.3, 82), (21407.7, 122), (20071.5, 182), (18583.9, 272), (17354.1, 406),
            (16435.9, 606), (15539.5, 905), (14559.2, 1350), (13505.3, 2015), (12387.1, 3008),
            (11252, 4489), (10152.3, 6700), (9035.01, 10000), (8168.33, 13000), (7611.9, 16000),
            (6821.64, 21000), (6033.29, 27000), (5431.09, 34000), (4795.08, 44000), (4227.98, 56000),
            (3662.59, 71000), (3079.73, 91000), (2539.8, 116000), (1976.19, 149000), (1542.07, 190000),
            (1115.49, 243000), (730.4, 310000), (413.19, 396000), (170.47, 507000), (48.65, 647000),
            (9.61, 827000), (2.08, 1057404),
        };

        private static (double PP, int Rank)[]? rankCurve(string ruleset) => ruleset switch
        {
            @"osu" => osu_rank_curve,
            @"taiko" => taiko_rank_curve,
            @"fruits" => catch_rank_curve,
            @"mania" => mania_rank_curve,
            _ => null,
        };

        /// <summary>
        /// Whether global ranks are estimated for this ruleset (the four built-in ones).
        /// </summary>
        public static bool HasRankEstimate(string ruleset) => rankCurve(ruleset) != null;

        /// <summary>
        /// Estimated global rank for a total PP value, or null for a ruleset without a curve.
        /// Interpolates ln(rank) linearly between the two nearest samples; more PP than the best sample is #1.
        /// </summary>
        public static int? EstimateGlobalRank(string ruleset, double pp)
        {
            if (rankCurve(ruleset) is not { } curve)
                return null;

            if (pp >= curve[0].PP)
                return 1;

            for (int i = 1; i < curve.Length; i++)
            {
                if (pp < curve[i].PP)
                    continue;

                var better = curve[i - 1];
                var worse = curve[i];
                double progress = (pp - worse.PP) / (better.PP - worse.PP);
                return (int)Math.Round(Math.Exp(Math.Log(worse.Rank) + (Math.Log(better.Rank) - Math.Log(worse.Rank)) * progress));
            }

            return curve[^1].Rank;
        }

        /// <summary>
        /// The least whole PP whose <see cref="EstimateGlobalRank"/> is <paramref name="rank"/> or better (the inverse of the estimate),
        /// or null for a ruleset without a curve.
        /// </summary>
        public static double? EstimatePPForRank(string ruleset, int rank)
        {
            if (rankCurve(ruleset) is not { } curve)
                return null;

            rank = Math.Max(1, rank);

            // Any PP at all gets the lowest estimated rank.
            if (rank >= curve[^1].Rank)
                return 1;

            int i = 0;
            while (curve[i].Rank < rank)
                i++;

            double pp = curve[i].PP;

            if (i > 0 && curve[i].Rank > rank)
            {
                var better = curve[i - 1];
                var worse = curve[i];
                pp = worse.PP + (better.PP - worse.PP) * (Math.Log(worse.Rank) - Math.Log(rank)) / (Math.Log(worse.Rank) - Math.Log(better.Rank));
            }

            // The estimate rounds the rank, so settle on the first whole PP that really gets there.
            pp = Math.Max(Math.Ceiling(pp), 1);
            while (pp > 1 && EstimateGlobalRank(ruleset, pp - 1) <= rank)
                pp--;
            while (EstimateGlobalRank(ruleset, pp) > rank)
                pp++;

            return pp;
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
