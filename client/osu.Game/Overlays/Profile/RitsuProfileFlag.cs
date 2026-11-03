// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Offline;
using osu.Game.Overlays.Notifications;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    public partial class RitsuProfileFlag : OsuClickableContainer, IHasPopover, IHasContextMenu, IHasTooltip
    {
        [Resolved]
        private RitsuProfileManager profiles { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        private readonly Sprite flag;
        private readonly Bindable<FileInfo?> selectedFile = new Bindable<FileInfo?>();
        private LargeTextureStore textures = null!;
        private TextureStore flags = null!;
        private bool chooseImage;
        private Guid selectedProfile;

        public LocalisableString TooltipText => "Click to choose a country; right-click to import a flag.";

        public RitsuProfileFlag()
        {
            AddInternal(flag = new Sprite { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fit });
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, TextureStore flags)
        {
            this.textures = textures;
            this.flags = flags;
            profiles.Changed += refreshScheduled;
            selectedFile.ValueChanged += change =>
            {
                if (change.NewValue == null) return;
                this.HidePopover();
                Guid profile = selectedProfile;
                Task.Run(() => profiles.SetFlagImage(profile, change.NewValue)).ContinueWith(task => Schedule(() =>
                {
                    if (!IsDisposed && task.IsFaulted)
                        notifications?.Post(new SimpleErrorNotification { Text = $"Could not update the flag: {task.Exception?.GetBaseException().Message}" });
                }));
            };
            refresh();
        }

        protected override bool OnClick(ClickEvent e)
        {
            chooseImage = false;
            selectedProfile = profiles.ActiveProfileId;
            this.ShowPopover();
            return true;
        }

        public MenuItem[] ContextMenuItems => new MenuItem[]
        {
            new OsuMenuItem("Choose country", MenuItemType.Standard, () => { chooseImage = false; selectedProfile = profiles.ActiveProfileId; this.ShowPopover(); }),
            new OsuMenuItem("Import custom flag", MenuItemType.Standard, () => { chooseImage = true; selectedProfile = profiles.ActiveProfileId; selectedFile.Value = null; this.ShowPopover(); }),
            new OsuMenuItem("Remove custom flag", MenuItemType.Standard, () => profiles.SetFlagImage(profiles.ActiveProfileId, null)),
        };

        public Popover GetPopover()
        {
            if (chooseImage)
                return new FormFileSelector.FileChooserPopover(new[] { ".png", ".jpg", ".jpeg", ".webp" }, selectedFile, null, false);
            return new CountryPopover(profiles.GetActiveProfile().CountryCode, country =>
            {
                profiles.SetCountry(selectedProfile, country);
                this.HidePopover();
            });
        }

        private void refreshScheduled() => Schedule(refresh);

        private void refresh()
        {
            var profile = profiles.GetActiveProfile();
            flag.Texture = (!string.IsNullOrEmpty(profile.FlagFileName) ? textures.Get(profile.FlagFileName) : null)
                           ?? flags.Get($"Flags/{(profile.CountryCode == CountryCode.Unknown ? "__" : profile.CountryCode.ToString())}")
                           ?? flags.Get("Flags/__");
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && profiles != null) profiles.Changed -= refreshScheduled;
            base.Dispose(isDisposing);
        }

        private partial class CountryPopover : OsuPopover
        {
            private readonly CountryCode initial;
            private readonly Action<CountryCode> onSelected;
            private readonly OsuDropdown<CountryCode> dropdown;

            public CountryPopover(CountryCode initial, Action<CountryCode> onSelected)
            {
                this.initial = initial;
                this.onSelected = onSelected;
                Child = new Container
                {
                    Width = 320,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(6),
                    Child = dropdown = new CountryDropdown
                    {
                        RelativeSizeAxes = Axes.X,
                        Items = Enum.GetValues<CountryCode>(),
                        AlwaysShowSearchBar = true,
                        Current = { Value = initial },
                    },
                };
            }

            private partial class CountryDropdown : OsuDropdown<CountryCode>
            {
                protected override DropdownMenu CreateMenu() => new OsuDropdownMenu { MaxHeight = 260 };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                dropdown.Current.BindValueChanged(change =>
                {
                    if (change.NewValue != initial) onSelected(change.NewValue);
                });
            }
        }
    }
}
