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
            if (profiles == null || dialogs == null)
                return null;

            if (profiles.GetScoreProfileId(scoreId) is not Guid sourceId)
                return profiles.CanAssignStoredScore(scoreId) ? createAssignmentMenu(profiles, dialogs, scoreId) : null;

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

        private static OsuMenuItem createAssignmentMenu(RitsuProfileManager profiles, IDialogOverlay dialogs, Guid scoreId)
        {
            var targets = profiles.GetProfiles().Select(profile => new OsuMenuItem(profile.Name, MenuItemType.Standard, () =>
                dialogs.Push(new ConfirmDialog($"Assign this score to {profile.Name}?", () =>
                {
                    if (!profiles.AssignStoredScore(scoreId, profile.Id))
                        dialogs.Push(new ConfirmDialog("This score can no longer be assigned to that profile.", () => { }));
                }))));

            return new OsuMenuItem("Assign score to profile")
            {
                Items = targets.Append(new OsuMenuItem("Create a new profile…", MenuItemType.Standard, () =>
                    dialogs.Push(new RitsuInputDialog("Create a profile for this score", string.Empty, name =>
                    {
                        try
                        {
                            return profiles.CreateProfileForStoredScore(scoreId, name)
                                ? null : "This score is no longer available or already belongs to a profile.";
                        }
                        catch (ArgumentException ex)
                        {
                            return ex.Message;
                        }
                    })))).ToArray(),
            };
        }
    }
}
