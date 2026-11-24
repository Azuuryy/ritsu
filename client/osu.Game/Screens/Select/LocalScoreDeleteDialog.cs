// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.LocalPriority;
using osu.Game.Overlays.Dialog;
using osu.Game.Scoring;

namespace osu.Game.Screens.Select
{
    public partial class LocalScoreDeleteDialog : DeletionDialog
    {
        private readonly ScoreInfo score;

        public LocalScoreDeleteDialog(ScoreInfo score)
        {
            this.score = score;
        }

        [BackgroundDependencyLoader]
        private void load(ScoreManager scoreManager, LocalProfileManager? localProfiles)
        {
            BodyText = LocalisableString.Interpolate($"{score.User} ({score.DisplayAccuracy}, {score.Rank.GetLocalisableDescription()})");

            // Ritsu: deleting a local profile's score shows the PP it loses on the toolbar.
            if (localProfiles != null)
                DangerousAction = () => localProfiles.DeleteScoreShowingChange(score, () => scoreManager.Delete(score));
            else
                DangerousAction = () => scoreManager.Delete(score);
        }
    }
}
