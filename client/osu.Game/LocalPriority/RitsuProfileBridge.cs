// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using osu.Game.Offline;
using osu.Game.Scoring;

namespace osu.Game.LocalPriority
{
    public static class RitsuProfileBridge
    {
        public static void ApplyToDetachedScore(LocalProfileManager? manager, RitsuProfileManager profiles, ScoreInfo score)
        {
            profiles.ApplyProfileNameToDetachedScore(score);
            if (manager == null || profiles.GetScoreProfileId(score.ID) is not { } profileId
                || manager.Store.GetProfile(profileId) is not { } profile)
                return;

            score.User.Id = profile.UserId;
            score.User.CountryCode = profile.Country;
            score.User.LocalAvatarFileName = null;
        }
    }
}
