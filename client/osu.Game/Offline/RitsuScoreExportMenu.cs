// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Linq;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Scoring;

namespace osu.Game.Offline
{
    internal static class RitsuScoreExportMenu
    {
        public static OsuMenuItem? Create(RitsuProfileManager? profiles, IDialogOverlay? dialogs, ScoreManager? scores, ScoreInfo score)
        {
            if (profiles?.GetScoreProfileId(score.ID) is not Guid profileId || dialogs == null || scores == null || score.Files.Count == 0)
                return null;
            var profile = profiles.GetProfiles().FirstOrDefault(item => item.Id == profileId);
            if (profile == null)
                return null;

            return new OsuMenuItem("Export original replay", MenuItemType.Standard, () =>
                dialogs.Push(new ConfirmDialog("Export the original replay without changing its recorded name?", () => scores.Export(score, original: true))));
        }
    }
}
