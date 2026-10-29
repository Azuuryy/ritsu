// Adapted from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
// See LICENCE-Modosu.txt in the distribution.
using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Offline;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    /// <summary>The local profile pills and add-profile popover from Modosu's profile header.</summary>
    public partial class RitsuProfileCardRow : CompositeDrawable
    {
        [Resolved]
        private RitsuProfileManager profiles { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogs { get; set; }

        private readonly FillFlowContainer cards;
        private OverlayColourProvider colours = null!;
        private IReadOnlyDictionary<Guid, double> performance = new Dictionary<Guid, double>();

        public RitsuProfileCardRow()
        {
            RelativeSizeAxes = Axes.X;
            Height = 36;
            InternalChild = new OsuScrollContainer(Direction.Horizontal)
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Masking = true,
                Child = cards = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider) => colours = colourProvider;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshCards();
        }

        public void Refresh(IReadOnlyDictionary<Guid, double>? pp = null)
        {
            performance = pp ?? new Dictionary<Guid, double>();
            if (IsLoaded)
                refreshCards();
        }

        private void refreshCards()
        {
            cards.Clear();
            var all = profiles.GetProfiles();
            Guid active = profiles.ActiveProfileId;
            foreach (var profile in all)
            {
                double? pp = performance.TryGetValue(profile.Id, out double value) ? value : null;
                cards.Add(new ProfilePill(profile, active == profile.Id, all.Count > 1, pp, profiles, colours, dialogs));
            }

            var add = new AddProfileButton();
            add.Action = () =>
            {
                add.CurrentPopover = new ProfileNamePopover("New profile", string.Empty, profiles.AddProfile);
                add.ShowPopover();
            };
            cards.Add(add);
        }

        private partial class AddProfileButton : CompositeDrawable, IHasPopover, IHasTooltip
        {
            private readonly Box background;
            private OverlayColourProvider colours = null!;
            public Action? Action { get; set; }
            public Popover CurrentPopover { get; set; } = null!;
            public LocalisableString TooltipText => "Add a local profile";

            public AddProfileButton()
            {
                Size = new Vector2(32);
                CornerRadius = 16;
                Masking = true;
                InternalChildren = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new OsuSpriteText { Text = "+", Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold) },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                colours = colourProvider;
                background.Colour = colours.Light3;
            }

            protected override bool OnClick(ClickEvent e) { Action?.Invoke(); return true; }
            protected override bool OnHover(HoverEvent e) { background.Colour = colours.Light2; return true; }
            protected override void OnHoverLost(HoverLostEvent e) => background.Colour = colours.Light3;
            public Popover GetPopover() => CurrentPopover;
        }

        private partial class ProfilePill : CompositeDrawable, IHasContextMenu, IHasPopover, IHasTooltip
        {
            private readonly RitsuProfile profile;
            private readonly bool active;
            private readonly bool canDelete;
            private readonly RitsuProfileManager profiles;
            private readonly OverlayColourProvider colours;
            private readonly IDialogOverlay? dialogs;
            private readonly Box background;
            private readonly OsuSpriteText name;
            private Popover popover = null!;
            public LocalisableString TooltipText => "Click to select. Right-click to manage this profile and its PP options.";

            public ProfilePill(RitsuProfile profile, bool active, bool canDelete, double? pp, RitsuProfileManager profiles, OverlayColourProvider colours, IDialogOverlay? dialogs)
            {
                this.profile = profile;
                this.active = active;
                this.canDelete = canDelete;
                this.profiles = profiles;
                this.colours = colours;
                this.dialogs = dialogs;
                Height = 32;
                AutoSizeAxes = Axes.X;
                CornerRadius = 16;
                Masking = true;
                InternalChildren = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Padding = new MarginPadding { Horizontal = 16 },
                        Children = new Drawable[]
                        {
                            name = new OsuSpriteText { Text = profile.Name, Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold) },
                            new OsuSpriteText { Text = pp.HasValue ? $"· {pp.Value:N0}pp" : string.Empty, Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold) },
                        },
                    },
                };
                updateVisuals();
            }

            public MenuItem[] ContextMenuItems
            {
                get
                {
                    var items = new List<MenuItem>
                    {
                        new MenuItem("Rename profile", () =>
                        {
                            popover = new ProfileNamePopover("Rename profile", profile.Name, newName => profiles.RenameProfile(profile.Id, newName));
                            this.ShowPopover();
                        }),
                    };
                    if (active)
                        items.Add(new MenuItem(profile.IncludeRelaxAutopilot ? "Exclude Relax / Autopilot from PP" : "Include Relax / Autopilot in PP", () => profiles.SetIncludeRelaxAutopilot(!profile.IncludeRelaxAutopilot)));
                    if (canDelete)
                        items.Add(new MenuItem("Delete profile", confirmDelete));
                    return items.ToArray();
                }
            }

            private void confirmDelete() => dialogs?.Push(new ConfirmDialog(
                $"Delete {profile.Name}? The profile associations will be removed, but scores and replays remain in shared storage.",
                () => profiles.DeleteProfile(profile.Id)));

            private void updateVisuals()
            {
                background.Colour = IsHovered && !active ? colours.Light2 : active ? colours.Colour1 : colours.Light3;
                name.Colour = active ? colours.Content1 : colours.Content2;
            }

            protected override bool OnClick(ClickEvent e) { if (!active) profiles.SelectProfile(profile.Id); return true; }
            protected override bool OnHover(HoverEvent e) { updateVisuals(); return true; }
            protected override void OnHoverLost(HoverLostEvent e) => updateVisuals();
            public Popover GetPopover() => popover;
        }

        private partial class ProfileNamePopover : OsuPopover
        {
            private readonly FocusedTextBox textBox;
            private readonly OsuSpriteText error;
            private readonly Action<string> save;

            public ProfileNamePopover(string title, string value, Action<string> save)
            {
                this.save = save;
                AutoSizeAxes = Axes.Both;
                Origin = Anchor.TopCentre;
                Child = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding(12),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = title, Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold) },
                        textBox = new FocusedTextBox { Width = 220, Text = value, PlaceholderText = "Profile name", LengthLimit = 32, SelectAllOnFocus = true },
                        error = new OsuSpriteText { Font = OsuFont.GetFont(size: 12) },
                        new RoundedButton { Width = 220, Height = 32, Text = "Save", Action = confirm },
                    },
                };
                textBox.OnCommit += (_, _) => confirm();
            }

            protected override void PopIn()
            {
                base.PopIn();
                textBox.TakeFocus();
            }

            private void confirm()
            {
                try
                {
                    save(textBox.Text.Trim());
                    this.HidePopover();
                }
                catch (ArgumentException ex) { error.Text = ex.Message; }
                catch (InvalidOperationException ex) { error.Text = ex.Message; }
            }
        }
    }
}
