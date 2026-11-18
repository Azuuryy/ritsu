// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Offline
{
    public static class RitsuLocalBeatmapStatus
    {
        public static BeatmapOnlineStatus ForBeatmap(BeatmapInfo beatmap, RitsuProfile? profile)
        {
            bool excluded = profile != null && (profile.ExcludedBeatmapIds.Contains(beatmap.ID)
                || (beatmap.BeatmapSet != null && profile.ExcludedBeatmapSetIds.Contains(beatmap.BeatmapSet.ID)
                    && !profile.IncludedBeatmapIds.Contains(beatmap.ID)));
            bool reincluded = profile != null && (profile.ReincludedBeatmapIds.Contains(beatmap.ID)
                || (beatmap.BeatmapSet != null && profile.ReincludedBeatmapSetIds.Contains(beatmap.BeatmapSet.ID)));
            return displayStatus(beatmap.Status, excluded, reincluded);
        }

        public static BeatmapOnlineStatus ForSet(BeatmapSetInfo set, RitsuProfile? profile)
        {
            bool excluded = profile != null && (set.Beatmaps.Count == 0
                ? profile.ExcludedBeatmapSetIds.Contains(set.ID)
                : set.Beatmaps.All(beatmap => profile.ExcludedBeatmapIds.Contains(beatmap.ID)
                    || (profile.ExcludedBeatmapSetIds.Contains(set.ID) && !profile.IncludedBeatmapIds.Contains(beatmap.ID))));
            bool reincluded = profile != null && (profile.ReincludedBeatmapSetIds.Contains(set.ID)
                || (set.Beatmaps.Count > 0 && set.Beatmaps.All(beatmap => profile.ReincludedBeatmapIds.Contains(beatmap.ID))));
            return displayStatus(set.Status, excluded, reincluded);
        }

        private static BeatmapOnlineStatus displayStatus(BeatmapOnlineStatus original, bool excluded, bool reincluded)
        {
            if (excluded) return BeatmapOnlineStatus.Unranked;
            if (original is BeatmapOnlineStatus.Unranked or BeatmapOnlineStatus.None or BeatmapOnlineStatus.Blacklisted)
                return reincluded ? BeatmapOnlineStatus.Approved : BeatmapOnlineStatus.Unranked;
            return original;
        }
    }
}
