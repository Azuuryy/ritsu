// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Offline;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    /// <summary>A local profile avatar that can be replaced with an image from disk.</summary>
    public partial class RitsuProfileAvatar : OsuClickableContainer, IHasTooltip
    {
        [Resolved]
        private RitsuProfileManager profiles { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        private LargeTextureStore textures = null!;
        private ISystemFileSelector? selector;
        private Guid selectedProfileId;
        private readonly Sprite portrait;

        public LocalisableString TooltipText => "Click to choose a local profile picture.";

        public RitsuProfileAvatar()
        {
            Masking = true;
            CornerRadius = 20;
            AddInternal(portrait = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
            AddInternal(new SpriteIcon
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Size = new Vector2(19),
                Icon = FontAwesome.Solid.Camera,
                Margin = new MarginPadding(3),
            });
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textureStore, GameHost host)
        {
            textures = textureStore;
            textures.AddTextureSource(host.CreateTextureLoaderStore(new StorageBackedResourceStore(profiles.AvatarStorage)));
            selector = host.CreateSystemFileSelector(new[] { ".png", ".jpg", ".jpeg", ".webp" });
            if (selector != null)
                selector.Selected += onFileSelected;

            profiles.Changed += onProfilesChanged;
            refreshAvatar();
        }

        protected override bool OnClick(osu.Framework.Input.Events.ClickEvent e)
        {
            selectedProfileId = profiles.ActiveProfileId;
            if (selector == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = "File selection is unavailable on this platform." });
                return true;
            }

            selector.Present();
            return true;
        }

        private void onFileSelected(FileInfo file)
        {
            Guid targetProfile = selectedProfileId;
            Task.Run(() => profiles.SetAvatar(targetProfile, file)).ContinueWith(task => Schedule(() =>
            {
                if (IsDisposed || task.IsCompletedSuccessfully)
                    return;

                Exception error = task.Exception?.GetBaseException() ?? new IOException("The profile picture could not be saved.");
                notifications?.Post(new SimpleErrorNotification { Text = $"Could not update the profile picture: {error.Message}" });
            }));
        }

        private void onProfilesChanged() => Schedule(refreshAvatar);

        private void refreshAvatar()
        {
            string? fileName = profiles.GetAvatarFileName(profiles.ActiveProfileId);
            portrait.Texture = string.IsNullOrEmpty(fileName) ? textures.Get(@"Online/avatar-guest") : textures.Get(fileName);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (profiles != null)
                    profiles.Changed -= onProfilesChanged;

                if (selector != null)
                {
                    selector.Selected -= onFileSelected;
                    selector.Dispose();
                }
            }

            base.Dispose(isDisposing);
        }
    }
}
