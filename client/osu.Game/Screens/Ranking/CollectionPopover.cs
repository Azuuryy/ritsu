// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Offline;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking
{
    public partial class CollectionPopover : OsuPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly ScoreInfo? score;

        [Resolved(canBeNull: true)]
        private RitsuLibrary? library { get; set; }

        [Resolved(canBeNull: true)]
        private RitsuProfileManager? profiles { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        public CollectionPopover(BeatmapInfo beatmapInfo, ScoreInfo? score = null)
            : base(false)
        {
            this.beatmapInfo = beatmapInfo;
            this.score = score;

            Body.CornerRadius = 4;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new[]
            {
                new OsuMenu(Direction.Vertical, true)
                {
                    Items = RitsuCollectionMenu.Create(realm, manageCollectionsDialog, beatmapInfo, library, profiles, score, game),
                    MaxHeight = 375,
                },
            };
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);
            Hide();
        }

    }
}
