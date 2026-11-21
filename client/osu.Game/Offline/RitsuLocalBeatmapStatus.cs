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
            return displayStatus(beatmap.Status, excluded);
        }

        public static BeatmapOnlineStatus ForSet(BeatmapSetInfo set, RitsuProfile? profile)
        {
            bool excluded = profile != null && (set.Beatmaps.Count == 0
                ? profile.ExcludedBeatmapSetIds.Contains(set.ID)
                : set.Beatmaps.All(beatmap => profile.ExcludedBeatmapIds.Contains(beatmap.ID)
                    || (profile.ExcludedBeatmapSetIds.Contains(set.ID) && !profile.IncludedBeatmapIds.Contains(beatmap.ID))));
            return displayStatus(set.Status, excluded);
        }

        private static BeatmapOnlineStatus displayStatus(BeatmapOnlineStatus original, bool excluded)
        {
            if (excluded) return BeatmapOnlineStatus.Unranked;
            if (original is BeatmapOnlineStatus.Unranked or BeatmapOnlineStatus.None or BeatmapOnlineStatus.Blacklisted)
                return BeatmapOnlineStatus.Approved;
            return original;
        }
    }
}
