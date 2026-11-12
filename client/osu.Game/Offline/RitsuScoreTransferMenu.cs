// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Offline
{
    internal static class RitsuScoreTransferMenu
    {
        public static OsuMenuItem? Create(RitsuProfileManager? profiles, IDialogOverlay? dialogs, Guid scoreId)
        {
            if (profiles == null || dialogs == null || profiles.GetScoreProfileId(scoreId) is not Guid sourceId)
                return null;

            var targets = profiles.GetProfiles().Where(profile => profile.Id != sourceId).ToArray();
            if (targets.Length == 0)
                return null;

            return new OsuMenuItem("Transfer score to profile")
            {
                Items = targets.Select(profile => new OsuMenuItem(profile.Name, MenuItemType.Standard, () =>
                    dialogs.Push(new ConfirmDialog($"Transfer this score to {profile.Name}?", () =>
                        profiles.TransferScore(scoreId, sourceId, profile.Id))))).ToArray(),
            };
        }
    }
}
