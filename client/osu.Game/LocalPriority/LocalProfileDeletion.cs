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
            var counts = manager.CountScoresForDeletion(id);
            withScores = withScores && counts.Deleted > 0;
            bool skipConfirmation = withScores ? manager.DeleteWithScoresWithoutAsking.Value : manager.DeleteKeepingScoresWithoutAsking.Value;

            if (skipConfirmation || dialogs == null)
            {
                manager.Delete(id, withScores);
                return;
            }

            dialogs.Push(new ConfirmDialog(name, withScores, counts.Deleted, counts.Preserved, () => manager.Delete(id, withScores)));
        }

        private static string plays(int count) => count == 1 ? @"1 score" : $@"{count:N0} scores";

        private static string withScoresText(int deleted, int preserved)
        {
            string text = $@"{plays(deleted)} recorded in Ritsu will be permanently removed.";
            if (preserved > 0)
                text += $@" {plays(preserved)} will stay in the database.";

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
            if (counts.Deleted == 0)
                dialogs.Push(new ConfirmDialog(name, false, 0, counts.Preserved, () => manager.Delete(id)));
            else
                dialogs.Push(new ChoiceDialog(name, counts.Deleted, counts.Preserved, () => manager.Delete(id), () => manager.Delete(id, true)));
        }

        public partial class ConfirmDialog : DangerousActionDialog
        {
            public ConfirmDialog(string name, bool withScores, int deletedScores, int preservedScores, Action delete)
            {
                HeaderText = withScores ? $@"Delete ""{name}"" and its Ritsu scores?" : $@"Delete ""{name}""?";
                BodyText = withScores
                    ? withScoresText(deletedScores, preservedScores)
                    : @"The profile and its Ritsu score associations are removed. All original scores and replays stay in the database.";
                DangerousAction = delete;
            }
        }

        private partial class ChoiceDialog : PopupDialog
        {
            public ChoiceDialog(string name, int deletedScores, int preservedScores, Action deleteKeepingScores, Action deleteWithScores)
            {
                Icon = FontAwesome.Solid.TrashAlt;
                HeaderText = $@"Delete ""{name}""?";
                BodyText = @"Keep its scores in the database, or delete the eligible scores recorded in Ritsu? "
                           + withScoresText(deletedScores, preservedScores);

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogDangerousButton
                    {
                        Text = @"Delete profile, keep its scores",
                        Action = deleteKeepingScores,
                    },
                    new PopupDialogDangerousButton
                    {
                        Text = @"Delete profile and its Ritsu scores",
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
