// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
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
        private LargeTextureStore textures = null!;

        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarLocalProfileButton()
        {
            ButtonContent.AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OsuGame game, LargeTextureStore textures)
        {
            this.textures = textures;
            IconContainer.Size = new Vector2(26);
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
            refreshProfile();
        }

        private void onProfilesChanged() => Schedule(refreshProfile);

        private void refreshProfile()
        {
            var profile = profiles.GetActiveProfile();
            nameText.Text = profile.Name;
            var texture = string.IsNullOrEmpty(profile.AvatarFileName) ? null : textures.Get(profile.AvatarFileName);
            if (texture == null)
            {
                SetIcon(FontAwesome.Solid.User);
                return;
            }

            SetIcon(new Container
            {
                Size = new Vector2(64),
                Masking = true,
                CornerRadius = 16,
                Child = new Sprite { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill, Texture = texture },
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && profiles != null)
                profiles.Changed -= onProfilesChanged;
            base.Dispose(isDisposing);
        }
    }
}
