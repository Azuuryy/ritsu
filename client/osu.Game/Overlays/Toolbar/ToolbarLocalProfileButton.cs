// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Offline;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarLocalProfileButton : ToolbarButton
    {
        [Resolved]
        private RitsuProfileManager profiles { get; set; } = null!;

        private OsuSpriteText nameText = null!;

        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarLocalProfileButton()
        {
            ButtonContent.AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OsuGame game)
        {
            SetIcon(FontAwesome.Solid.User);
            Flow.Padding = new MarginPadding { Horizontal = 10 };
            Flow.Spacing = new Vector2(7, 0);
            Flow.Add(nameText = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
            });
            TooltipMain = "Local profiles";
            TooltipSub = "Manage profiles and view statistics (Ctrl+P)";
            Action = game.ToggleLocalProfile;
            profiles.Changed += onProfilesChanged;
            nameText.Text = profiles.ActiveProfileName;
        }

        private void onProfilesChanged() => Schedule(() => nameText.Text = profiles.ActiveProfileName);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && profiles != null)
                profiles.Changed -= onProfilesChanged;
            base.Dispose(isDisposing);
        }
    }
}
