// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Opened by right-clicking the account button in the top bar: pick the online account or one of the local profiles.
    /// </summary>
    public partial class LocalProfileSwitcher : OsuFocusedOverlayContainer
    {
        private const float transition_time = 300;
        private const float width = 320;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private LocalProfileOverlay? profileOverlay { get; set; }

        [Resolved]
        private LoginOverlay? loginOverlay { get; set; }

        [Resolved]
        private SettingsOverlay? settings { get; set; }

        [Resolved]
        private IDialogOverlay? dialogs { get; set; }

        [Resolved]
        private IOverlayManager? overlayManager { get; set; }

        private LocalProfileModRulesButton modRulesButton = null!;

        private readonly IBindable<int> revision = new Bindable<int>();
        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        private FillFlowContainer rows = null!;
        private OsuScrollContainer rowsScroll = null!;

        private const float row_height = 48;
        private const float row_spacing = 4;
        private const int visible_rows = 10;

        protected override void Update()
        {
            base.Update();

            // Grow with the rows up to 10 of them, then scroll.
            const float max_height = visible_rows * row_height + (visible_rows - 1) * row_spacing;
            rowsScroll.Height = Math.Min(rows.DrawHeight, max_height);
        }

        protected override double PopInOutSampleBalance => OsuGameBase.SFX_STEREO_STRENGTH * 0.75f;

        public LocalProfileSwitcher()
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 6;
            EdgeEffect = new EdgeEffectParameters
            {
                Colour = Color4.Black,
                Type = EdgeEffectType.Shadow,
                Radius = 10,
                Hollow = true,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new Container
                {
                    Width = width,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(10),
                            Spacing = new Vector2(0, 6),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = @"Switch profile",
                                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                    Margin = new MarginPadding { Bottom = 2 },
                                },
                                // Up to 10 rows (online account + 9 profiles) are shown; with more, the list scrolls.
                                rowsScroll = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Child = rows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, row_spacing),
                                    },
                                },
                                // "New local profile", with the compact "+ mods" button (new profile with mod rules) next to it when enabled.
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 40,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new RoundedButton
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = @"New local profile",
                                                Action = () =>
                                                {
                                                    var profile = manager.CreateDefaultProfile();
                                                    manager.Switch(profile.Id);
                                                    LocalProfileSyncDialog.Offer(manager, dialogs, profile.Id);
                                                },
                                            },
                                            modRulesButton = new LocalProfileModRulesButton
                                            {
                                                Size = new Vector2(54, 40),
                                                CornerRadius = 5,
                                                Margin = new MarginPadding { Left = 6 },
                                                Action = () => LocalProfileModRulesOverlay.CreateProfileWithRules(overlayManager, manager, dialogs, Hide),
                                            },
                                        },
                                    },
                                },
                                new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = @"Ritsu settings",
                                    Action = () =>
                                    {
                                        Hide();
                                        settings?.ShowAtControl<LocalOnlineLookupSettings>();
                                    },
                                },
                                new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Colour = colourProvider.Content2,
                                    Text = @"Local profiles never connect to osu! servers "
                                           + @"unless online lookups are turned on in Settings → Ritsu. "
                                           + @"Scores are never submitted from Ritsu to osu!web regardless of this setting. "
                                           + @"Modifying this client past whatever provided and returning full online functionality will get you banned.",
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            revision.BindTo(manager.Revision);
            localUser.BindTo(api.LocalUser);
            apiState.BindTo(api.State);

            revision.BindValueChanged(_ => Scheduler.AddOnce(refresh));
            localUser.BindValueChanged(_ => Scheduler.AddOnce(refresh));
            apiState.BindValueChanged(_ => Scheduler.AddOnce(refresh), true);

            // Hidden (and so taking no space in the row) unless enabled in Settings → Ritsu.
            manager.ProfilesWithModRules.BindValueChanged(v =>
            {
                modRulesButton.Alpha = v.NewValue ? 1 : 0;
                modRulesButton.Width = v.NewValue ? 54 : 0;
                modRulesButton.Margin = new MarginPadding { Left = v.NewValue ? 6 : 0 };
            }, true);
        }

        private void refresh()
        {
            rows.Clear();

            var active = manager.ActiveProfile.Value;

            // Ritsu: there is no online account, so the top row plays without a local profile (as a guest).
            rows.Add(new ProfileRow(@"Guest", @"No local profile", active == null, () => manager.Switch(null))
            {
                Avatar = new UpdateableAvatar(null, isInteractive: false) { RelativeSizeAxes = Axes.Both },
                Icon = FontAwesome.Solid.UserSecret,
            });

            // The online account always stays on top; local profiles below it can be dragged into any order.
            rows.SetLayoutPosition(rows.Children[^1], -1);

            int position = 0;

            foreach (var profile in manager.GetProfiles())
            {
                Guid id = profile.Id;
                string name = profile.Name;
                bool isActive = active?.Id == id;

                var row = new ProfileRow(profile.ModRules?.HasAnyRule == true ? @"Local profile · mod rules" : @"Local profile", profile.Name, isActive, () =>
                {
                    if (isActive)
                    {
                        Hide();
                        profileOverlay?.Show();
                        return;
                    }

                    manager.Switch(id);
                })
                {
                    Avatar = new LocalProfileAvatar(profile.AvatarFile) { RelativeSizeAxes = Axes.Both },
                    Flag = new UpdateableFlag(profile.Country) { RelativeSizeAxes = Axes.Both },
                    Icon = FontAwesome.Solid.PowerOff,
                    ProfileId = id,
                    Delete = () => LocalProfileDeletion.RequestWithChoice(manager, dialogs, id, name),
                    Dragging = (dragged, mouse) => LocalProfileDragReorder.DragTo(rows, dragged, mouse),
                    DragEnded = () => manager.Reorder(LocalProfileDragReorder.CurrentOrder(rows)),
                    // Same right-click menu as on the profile page.
                    ContextMenuItems = LocalProfileMenu.Create(manager, dialogs, overlayManager, profile,
                        () =>
                        {
                            Hide();
                            profileOverlay?.ShowAutoSwitchRules(id);
                        },
                        Hide),
                    TooltipText = LocalProfileMenu.Tooltip(profile, @"right-click for options · drag to reorder"),
                };

                rows.Add(row);
                rows.SetLayoutPosition(row, position++);
            }
        }

        protected override void PopIn()
        {
            this.FadeIn(transition_time, Easing.OutQuint);
            FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, WaveContainer.APPEAR_DURATION, Easing.Out);
        }

        protected override void PopOut()
        {
            base.PopOut();
            this.FadeOut(transition_time);
            FadeEdgeEffectTo(0, WaveContainer.DISAPPEAR_DURATION, Easing.In);
        }

        private partial class ProfileRow : OsuClickableContainer, IReorderableProfileItem, IHasContextMenu
        {
            public MenuItem[]? ContextMenuItems { get; init; }

            public Drawable? Avatar { get; init; }
            public Drawable? Flag { get; init; }
            public IconUsage Icon { get; init; }

            /// <summary>
            /// Local profiles: draggable to reorder, with a trash button on the right.
            /// </summary>
            public Guid? ProfileId { get; init; }

            public Action? Delete { get; init; }
            public Action<Drawable, Vector2>? Dragging { get; init; }
            public Action? DragEnded { get; init; }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (ProfileId == null || e.Button != osuTK.Input.MouseButton.Left)
                    return false;

                this.ScaleTo(1.03f, 150, Easing.OutQuint).FadeTo(0.8f, 150);
                return true;
            }

            protected override void OnDrag(DragEvent e) => Dragging?.Invoke(this, e.ScreenSpaceMousePosition);

            protected override void OnDragEnd(DragEndEvent e)
            {
                this.ScaleTo(1, 200, Easing.OutQuint).FadeTo(1, 200);
                DragEnded?.Invoke();
            }

            private readonly string kind;
            private readonly string name;
            private readonly bool isActive;

            private Box hover = null!;

            public ProfileRow(string kind, string name, bool isActive, Action action)
            {
                this.kind = kind;
                this.name = name;
                this.isActive = isActive;
                Action = action;

                RelativeSizeAxes = Axes.X;
                Height = 48;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours, OsuColour osuColour)
            {
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = isActive ? colours.Background2 : colours.Background5,
                    },
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.08f),
                        Alpha = 0,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Padding = new MarginPadding(6),
                        Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                Size = new Vector2(36),
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Masking = true,
                                CornerRadius = 5,
                                Child = Avatar ?? Empty(),
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(5, 0),
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                Size = new Vector2(20, 14),
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Alpha = Flag == null ? 0 : 1,
                                                Child = Flag ?? Empty(),
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = name,
                                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                            },
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(4, 0),
                                        Children = new Drawable[]
                                        {
                                            new SpriteIcon
                                            {
                                                Icon = Icon,
                                                Size = new Vector2(10),
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Colour = colours.Content2,
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = isActive ? $@"{kind} · active" : kind,
                                                Font = OsuFont.GetFont(size: 12),
                                                Colour = isActive ? osuColour.Lime1 : colours.Content2,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                };

                if (Delete != null)
                {
                    Add(new TrashButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 8 },
                        Action = Delete,
                    });
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.FadeIn(100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(200);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// Dark with a white trash icon; on hover it grows a little and turns bright red.
        /// </summary>
        private partial class TrashButton : OsuClickableContainer
        {
            private Box background = null!;

            private Color4 idleColour;
            private Color4 hoverColour;

            public TrashButton()
            {
                Size = new Vector2(30);
                Masking = true;
                CornerRadius = 5;
                TooltipText = @"Delete profile";
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour osuColour)
            {
                idleColour = osuColour.Red.Darken(1.2f);
                hoverColour = osuColour.Red.Lighten(0.2f);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = idleColour,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Solid.TrashAlt,
                        Size = new Vector2(13),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.ScaleTo(1.15f, 150, Easing.OutQuint);
                background.FadeColour(hoverColour, 150, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.ScaleTo(1, 250, Easing.OutQuint);
                background.FadeColour(idleColour, 250, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}
