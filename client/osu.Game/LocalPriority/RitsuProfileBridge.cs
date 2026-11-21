// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Offline;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// osu!local profiles decide who is playing. Ritsu's own features (commands, library memos, collections, play history filters,
    /// playtime) still read Ritsu's profile data, so the Ritsu profile with the same name is kept active (created when missing).
    /// </summary>
    public static class RitsuProfileBridge
    {
        /// <summary>
        /// Whether a score belongs to whoever is playing: while an osu!local profile is active, that profile's own scores
        /// (so transferred scores move with their player); otherwise Ritsu's profile association.
        /// Used by Ritsu's "Local" leaderboard scope, its personal best and the local rank shown on beatmaps.
        /// </summary>
        public static bool BelongsToActiveProfile(LocalProfileManager? localProfiles, RitsuProfileManager? ritsuProfiles, Scoring.ScoreInfo score)
        {
            if (localProfiles?.ActiveProfile.Value is LocalProfile profile)
                return profile.OwnsScore(score);

            return ritsuProfiles == null || ritsuProfiles.BelongsToActiveProfile(score.ID);
        }

        public static void Follow(RitsuProfileManager ritsuProfiles, LocalProfile? profile)
        {
            // No osu!local profile (guest): keep whatever Ritsu profile was active.
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                return;

            try
            {
                if (string.Equals(ritsuProfiles.ActiveProfileName, profile.Name, StringComparison.OrdinalIgnoreCase))
                    return;

                var existing = ritsuProfiles.GetProfiles().FirstOrDefault(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                    ritsuProfiles.SelectProfile(existing.Id);
                else
                    ritsuProfiles.AddProfile(profile.Name);
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Could not select the Ritsu profile for ""{profile.Name}"".");
            }
        }
    }
}
