// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// A local profile's picture: the chosen image, or the default guest picture.
    /// </summary>
    public partial class LocalProfileAvatar : ModelBackedDrawable<string?>
    {
        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        [Resolved]
        private LargeTextureStore textures { get; set; } = null!;

        /// <summary>
        /// The picture's file name inside "osu-local/avatars", or null for the default picture.
        /// </summary>
        public string? AvatarFile
        {
            get => Model;
            set => Model = value;
        }

        public LocalProfileAvatar(string? avatarFile = null)
        {
            Model = avatarFile;
        }

        protected override double TransformDuration => 200;

        protected override Drawable CreateDrawable(string? file)
        {
            Texture? texture = null;

            if (!string.IsNullOrEmpty(file))
                texture = manager.GetAvatarTexture(file);

            return new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Texture = texture ?? textures.Get(@"Online/avatar-guest"),
            };
        }

        /// <summary>
        /// Rounded, masked avatar of a given corner radius, convenient for lists.
        /// </summary>
        public static Container Rounded(string? avatarFile, float cornerRadius = 4) => new Container
        {
            Masking = true,
            CornerRadius = cornerRadius,
            Child = new LocalProfileAvatar(avatarFile) { RelativeSizeAxes = Axes.Both },
        };
    }
}
