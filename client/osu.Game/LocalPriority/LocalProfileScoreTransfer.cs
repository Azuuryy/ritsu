// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Linq;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Scoring;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// "Transfer score" (right-click a local score on the song select leaderboard or on the profile page): a submenu with the local profiles.
    /// </summary>
    public static class LocalProfileScoreTransfer
    {
        public static MenuItem CreateMenuItem(LocalProfileManager manager, ScoreInfo score, Action? transferred = null)
        {
            Guid? sourceId = manager.Profiles.GetScoreProfileId(score.ID);
            var targets = manager.GetProfiles().Where(p => p.Id != sourceId).ToArray();

            MenuItem[] items = targets.Length == 0
                ? new MenuItem[] { new OsuMenuItem(@"No other local profiles", MenuItemType.Standard) { Action = { Disabled = true } } }
                : targets.Select(p =>
                {
                    Guid id = p.Id;

                    return (MenuItem)new OsuMenuItem(p.Name, MenuItemType.Standard, () =>
                    {
                        manager.TransferScore(score, id);
                        transferred?.Invoke();
                    });
                }).ToArray();

            return new OsuMenuItem(@"Transfer score", MenuItemType.Standard)
            {
                Items = items,
            };
        }
    }
}
