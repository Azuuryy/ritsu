// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Offline;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    /// <summary>A local profile avatar that can be replaced with an image from disk.</summary>
    public partial class RitsuProfileAvatar : OsuClickableContainer, IHasTooltip, IHasPopover
    {
        [Resolved]
        private RitsuProfileManager profiles { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        private LargeTextureStore textures = null!;
        private readonly Bindable<FileInfo?> selectedFile = new Bindable<FileInfo?>();
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
                Margin = new MarginPadding(3),
            });
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textureStore)
        {
            textures = textureStore;
            selectedFile.ValueChanged += file =>
            {
                if (file.NewValue == null) return;
                this.HidePopover();
                onFileSelected(file.NewValue);
            };
            profiles.Changed += onProfilesChanged;
            refreshAvatar();
        }

        protected override bool OnClick(osu.Framework.Input.Events.ClickEvent e)
        {
            selectedProfileId = profiles.ActiveProfileId;
            selectedFile.Value = null;
            this.ShowPopover();
            return true;
        }

        public Popover GetPopover() => new FormFileSelector.FileChooserPopover(new[] { ".png", ".jpg", ".jpeg", ".webp" }, selectedFile, null, false);

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

            }

            base.Dispose(isDisposing);
        }
    }
}
