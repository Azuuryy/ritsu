// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Deleting local profiles from the profile page and the switcher. Asks first, unless Settings → Ritsu says not to.
    /// </summary>
    public static class LocalProfileDeletion
    {
        /// <summary>
        /// Delete a profile, keeping its scores or not. Asks for confirmation unless the matching "without asking" setting is on.
        /// </summary>
        public static void Request(LocalProfileManager manager, IDialogOverlay? dialogs, Guid id, string name, bool withScores)
        {
            bool skipConfirmation = withScores ? manager.DeleteWithScoresWithoutAsking.Value : manager.DeleteKeepingScoresWithoutAsking.Value;

            if (skipConfirmation || dialogs == null)
            {
                manager.Delete(id, withScores);
                return;
            }

            var counts = withScores ? manager.CountScoresForDeletion(id) : (0, 0);
            dialogs.Push(new ConfirmDialog(name, withScores, counts.Item1, counts.Item2, () => manager.Delete(id, withScores)));
        }

        private static string plays(int count) => count == 1 ? @"1 score" : $@"{count:N0} scores";

        /// <summary>
        /// What happens to the scores when a profile is deleted with them.
        /// </summary>
        private static string withScoresText(int deleted, int givenBack)
        {
            string text = deleted == 0
                ? @"The profile has no scores of its own to delete."
                : $@"{plays(deleted)} set on this profile or transferred to it will be removed from the database. This can't be undone.";

            if (givenBack > 0)
                text += $@" {plays(givenBack)} synced from before the profile existed go back to the player they belonged to.";

            return text;
        }

        /// <summary>
        /// The switcher's trash button: choose whether the scores go too. Deletes right away (keeping scores) if that is set to not ask.
        /// </summary>
        public static void RequestWithChoice(LocalProfileManager manager, IDialogOverlay? dialogs, Guid id, string name)
        {
            if (manager.DeleteKeepingScoresWithoutAsking.Value || dialogs == null)
            {
                manager.Delete(id);
                return;
            }

            // One question only: both delete buttons are hold-to-confirm, so "with scores" doesn't ask a second time.
            var counts = manager.CountScoresForDeletion(id);
            dialogs.Push(new ChoiceDialog(name, counts.Deleted, counts.GivenBack, () => manager.Delete(id), () => manager.Delete(id, true)));
        }

        public partial class ConfirmDialog : DangerousActionDialog
        {
            public ConfirmDialog(string name, bool withScores, int deletedScores, int givenBackScores, Action delete)
            {
                HeaderText = withScores ? $@"Delete ""{name}"" and all its scores?" : $@"Delete ""{name}""?";
                BodyText = withScores
                    ? withScoresText(deletedScores, givenBackScores)
                    : @"The profile is removed; its scores stay in the database (shown under its name on local leaderboards).";
                DangerousAction = delete;
            }
        }

        private partial class ChoiceDialog : PopupDialog
        {
            public ChoiceDialog(string name, int deletedScores, int givenBackScores, Action deleteKeepingScores, Action deleteWithScores)
            {
                Icon = FontAwesome.Solid.TrashAlt;
                HeaderText = $@"Delete ""{name}""?";
                BodyText = @"Keep its scores in the database (shown under its name on local leaderboards), or delete them too? "
                           + (deletedScores > 0 ? $@"Deleting them removes {plays(deletedScores)} for good." : @"It has no scores of its own.")
                           + (givenBackScores > 0 ? $@" {plays(givenBackScores)} synced from before the profile existed go back to their original player either way." : string.Empty);

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogDangerousButton
                    {
                        Text = @"Delete profile, keep its scores",
                        Action = deleteKeepingScores,
                    },
                    new PopupDialogDangerousButton
                    {
                        Text = @"Delete profile and all its scores",
                        Action = deleteWithScores,
                    },
                    new PopupDialogCancelButton
                    {
                        Text = @"Cancel",
                    },
                };
            }
        }
    }
}
