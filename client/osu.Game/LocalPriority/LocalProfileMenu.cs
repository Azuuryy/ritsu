// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// The right-click menu of a local profile: the same on the profile page's chips and in the switcher's rows.
    /// </summary>
    public static class LocalProfileMenu
    {
        /// <param name="manager">The profile manager.</param>
        /// <param name="dialogs">For confirmations.</param>
        /// <param name="overlays">For the mod rules picker.</param>
        /// <param name="profile">The profile the menu is for.</param>
        /// <param name="showAutoSwitchRules">Opens the auto-switch rules editor next to the item that was right-clicked.</param>
        /// <param name="hideOthers">Hides the screen the menu was opened from (the mod rules picker takes the whole screen).</param>
        internal static MenuItem[] Create(LocalProfileManager manager, IDialogOverlay? dialogs, IOverlayManager? overlays, LocalProfile profile,
                                          Action showAutoSwitchRules, Action? hideOthers)
        {
            Guid id = profile.Id;

            var menu = new List<MenuItem>
            {
                new OsuMenuItem(@"Sync plays already saved under this name", MenuItemType.Standard,
                    () => LocalProfileSyncDialog.Offer(manager, dialogs, id, tellWhenNoneFound: true)),
            };

            if (profile.SyncedScores.Count > 0 || profile.LinkedOwners.Count > 0)
            {
                menu.Add(new OsuMenuItem($@"Unsync {profile.SyncedScores.Count:N0} synced play(s) (give them back to their original player)", MenuItemType.Standard,
                    () => manager.UnsyncScores(id)));
            }

            if (profile.ModRules != null || manager.ProfilesWithModRules.Value)
            {
                menu.Add(new OsuMenuItem(profile.ModRules != null ? @"Edit mod rules" : @"Add mod rules", MenuItemType.Standard,
                    () => LocalProfileModRulesOverlay.EditRules(overlays, manager, profile, hideOthers)));
            }

            if (profile.ModRules != null)
                menu.Add(new OsuMenuItem(@"Remove mod rules", MenuItemType.Standard, () => manager.SetModRules(id, null)));

            menu.Add(new OsuMenuItem(profile.AutoSwitch != null ? @"Edit auto-switching instructions" : @"Add auto-switching instructions", MenuItemType.Standard, showAutoSwitchRules));

            menu.Add(new OsuMenuItem(@"Delete profile (keep its scores)", MenuItemType.Destructive,
                () => LocalProfileDeletion.Request(manager, dialogs, id, profile.Name, false)));
            menu.Add(new OsuMenuItem(@"Delete profile and all its scores", MenuItemType.Destructive,
                () => LocalProfileDeletion.Request(manager, dialogs, id, profile.Name, true)));

            return menu.ToArray();
        }

        /// <summary>
        /// Hover text: the profile's mod rules and auto-switch rules, then <paramref name="hint"/>.
        /// </summary>
        public static string Tooltip(LocalProfile profile, string hint)
        {
            var parts = new List<string>();
            if (profile.ModRules?.HasAnyRule == true) parts.Add($@"Mod rules: {profile.ModRules.Describe()}");
            if (profile.AutoSwitch?.Enabled == true) parts.Add($@"Auto-switch on: {profile.AutoSwitch.Describe()}");
            parts.Add(hint);
            return string.Join(@" · ", parts);
        }
    }
}
