// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// "There already are plays saved under this name — sync them to this profile?"
    /// Asked after a local profile is created or renamed, and from the profile chip's right-click menu.
    /// Syncing only links the plays to the profile; the scores in the (shared) database are never changed.
    /// </summary>
    public partial class LocalProfileSyncDialog : PopupDialog
    {
        /// <summary>
        /// Looks for plays saved under the profile's name and asks whether to sync them. Does nothing if there are none
        /// (unless <paramref name="tellWhenNoneFound"/>, which shows a short note instead).
        /// </summary>
        public static void Offer(LocalProfileManager manager, IDialogOverlay? dialogs, Guid profileId, bool tellWhenNoneFound = false)
        {
            if (dialogs == null)
                return;

            // Settings → Ritsu: the automatic suggestion (after creating / renaming) can be turned off; asking from the menu always works.
            if (!tellWhenNoneFound && !manager.SuggestSyncingSameNamePlays.Value)
                return;

            var profile = manager.GetProfiles().FirstOrDefault(p => p.Id == profileId);
            if (profile == null)
                return;

            (IReadOnlyList<LinkedScoreOwner> owners, int count) found;

            try
            {
                found = manager.FindSameNameScores(profileId);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Ritsu: could not look for plays with the same name.");
                return;
            }

            if (found.count == 0)
            {
                if (tellWhenNoneFound)
                    dialogs.Push(new NothingFoundDialog(profile.Name));
                return;
            }

            dialogs.Push(new LocalProfileSyncDialog(profile.Name, found.owners, found.count, () => manager.SyncScores(profileId, found.owners)));
        }

        private LocalProfileSyncDialog(string name, IReadOnlyList<LinkedScoreOwner> owners, int count, Action sync)
        {
            Icon = FontAwesome.Solid.Sync;
            HeaderText = $@"Sync {count:N0} play{(count == 1 ? string.Empty : @"s")} to ""{name}""?";

            string sources = string.Join(@", ", owners.Select(describe).Distinct());

            BodyText = $@"The database already has plays saved under this name ({sources}). "
                       + @"Syncing associates them with this Ritsu profile for PP, ranks and statistics. The original scores and replays stay intact. "
                       + @"You can remove the association from the profile menu.";

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = @"Yes, sync them",
                    Action = sync,
                },
                new PopupDialogCancelButton
                {
                    Text = @"No, keep the profile empty",
                },
            };
        }

        private static string describe(LinkedScoreOwner owner)
        {
            if (LocalPriorityPolicy.IsOfflineProfileUser(new Online.API.Requests.Responses.APIUser { Id = owner.UserId }))
                return @"an earlier local profile";

            return owner.UserId > 1 ? @"an online account" : @"guest plays";
        }

        private partial class NothingFoundDialog : PopupDialog
        {
            public NothingFoundDialog(string name)
            {
                Icon = FontAwesome.Solid.Sync;
                HeaderText = @"Nothing to sync";
                BodyText = $@"There are no other plays saved under ""{name}"" in the database.";

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton { Text = @"OK" },
                };
            }
        }
    }
}
