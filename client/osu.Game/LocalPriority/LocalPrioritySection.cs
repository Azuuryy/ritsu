// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Settings → "Ritsu" (its own tab, right under Online). Local profiles themselves are managed on their profile page
    /// and in the switcher (right-click your name in the top bar), not here.
    /// </summary>
    public partial class LocalPrioritySection : SettingsSection
    {
        public override LocalisableString Header => LocalPriorityPolicy.GAME_NAME;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { @"local", @"offline", @"profile" });

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = FontAwesome.Solid.UserShield,
        };

        public LocalPrioritySection()
        {
            // Every patch with settings here has its own "Patch N. …" subsection (a PatchSettingsSubsection in one of its own files),
            // so no patch has to edit this list. They are shown in patch order.
            Children = typeof(LocalPrioritySection).Assembly.GetTypes()
                                                   .Where(t => t.IsSubclassOf(typeof(PatchSettingsSubsection)) && !t.IsAbstract)
                                                   .Select(t => (PatchSettingsSubsection)Activator.CreateInstance(t)!)
                                                   .OrderBy(s => s.PatchNumber)
                                                   .ToArray();
        }
    }

    /// <summary>
    /// The subsection of Settings → Ritsu that belongs to one part of the local profiles code, titled by its summary
    /// (Patchsu titles these "Patch N. Summary"; Ritsu has no numbered patches, so only the summary shows).
    /// Settings → Ritsu finds every subclass by itself, so a subclass only needs a public parameterless constructor.
    /// </summary>
    public abstract partial class PatchSettingsSubsection : SettingsSubsection
    {
        /// <summary>
        /// The number of the patch file (0002 → 2).
        /// </summary>
        public abstract int PatchNumber { get; }

        /// <summary>
        /// What the patch does, in three words at most.
        /// </summary>
        protected abstract string Summary { get; }

        protected sealed override LocalisableString Header => Summary;
    }

    /// <summary>
    /// A group of settings inside a subsection, headed by a plaque like the suggested offset note in Audio settings.
    /// A search that matches none of its settings hides the whole group, plaque included.
    /// </summary>
    public abstract partial class LocalSettingsGroup : FillFlowContainer, IFilterable
    {
        private readonly string title;

        private readonly FillFlowContainer content;

        protected override Container<Drawable> Content => content;

        /// <summary>
        /// Search terms of the group besides its title.
        /// </summary>
        protected virtual IEnumerable<LocalisableString> Keywords => Array.Empty<LocalisableString>();

        public IEnumerable<LocalisableString> FilterTerms => Keywords.Prepend(title);

        public bool MatchingFilter
        {
            set => this.FadeTo(value ? 1 : 0);
        }

        public bool FilteringActive { get; set; }

        protected LocalSettingsGroup(string title)
        {
            this.title = title;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);

            AddRangeInternal(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = { Value = new SettingsNote.Data(title, SettingsNote.Type.Informational) },
                    },
                },
                content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                },
            });
        }
    }

    /// <summary>
    /// Patch 2: how local profiles are created and deleted, with the profile colour and online lookup groups under it.
    /// </summary>
    public partial class LocalProfileBehaviourSettings : PatchSettingsSubsection
    {
        public override int PatchNumber => 2;

        protected override string Summary => @"Local profiles";

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[]
        {
            @"delete", @"confirm", @"sync", @"mods", @"account", @"create", @"switch", @"accuracy", @"ranked", @"unranked", @"pp",
        });

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        private readonly IBindable<LocalProfile?> activeProfile = new Bindable<LocalProfile?>();

        private SettingsButtonV2 importReplaysButton = null!;
        private SettingsButtonV2 exportTopReplaysButton = null!;
        private SettingsButtonV2 exportAllReplaysButton = null!;
        private SettingsButtonV2 rebuildStatisticsButton = null!;
        private SettingsButtonV2 resetDeathCountsButton = null!;
        private readonly IBindable<int> revision = new Bindable<int>();

        /// <summary>
        /// Ritsu: shows how far Sudden Death / Perfect Death / Speedrun profile numbering got (refreshed whenever the profiles change).
        /// </summary>
        private void updateDeathCountsButton()
        {
            var (suddenDeath, perfectDeath) = manager.GetDeathProfileCounts();
            resetDeathCountsButton.Text = $@"Reset SD/PF/SP profile count";
            resetDeathCountsButton.Enabled.Value = suddenDeath > 0 || perfectDeath > 0 || manager.GetSpeedrunProfileCount() > 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            activeProfile.BindTo(manager.ActiveProfile);
            activeProfile.BindValueChanged(p =>
            {
                importReplaysButton.Enabled.Value = p.NewValue != null;
                exportTopReplaysButton.Enabled.Value = p.NewValue != null;
                exportAllReplaysButton.Enabled.Value = p.NewValue != null;
                exportTopReplaysButton.Text = $@"Export top {LocalProfileManager.TOP_REPLAYS_EXPORT_COUNT} replays of {(p.NewValue != null ? $@"""{p.NewValue.Name}""" : @"the active profile")} to a folder";
                exportAllReplaysButton.Text = $@"Export all replays of {(p.NewValue != null ? $@"""{p.NewValue.Name}""" : @"the active profile")} to a folder";
                importReplaysButton.Text = p.NewValue != null
                    ? $@"Import replays from a folder as ""{p.NewValue.Name}""…"
                    : @"Import replays from a folder. Needs local profile.";
            }, true);

            revision.BindTo(manager.Revision);
            revision.BindValueChanged(_ => Scheduler.AddOnce(updateDeathCountsButton), true);
        }

        [Resolved]
        private IDialogOverlay? dialogs { get; set; }

        private void requestRemoveOldPlays()
        {
            var (scores, plays) = manager.CountPlaysBefore2000();

            if (scores == 0 && plays == 0)
            {
                dialogs?.Push(new LocalPriorityInfoDialog(@"Nothing to remove", @"No local profile has plays dated before the year 2000."));
                return;
            }

            dialogs?.Push(new RemoveOldPlaysDialog(scores, plays, manager.RemovePlaysBefore2000));
        }

        private void rebuildStatistics()
        {
            rebuildStatisticsButton.Enabled.Value = false;

            manager.RebuildStatisticsFromScoresAsync().ContinueWith(t => Schedule(() =>
            {
                rebuildStatisticsButton.Enabled.Value = true;

                if (t.IsFaulted)
                {
                    Logger.Error(t.Exception?.InnerException ?? t.Exception, @"Could not rebuild play statistics from saved scores");
                    return;
                }

                var (profiles, scores) = t.GetResultSafely();

                dialogs?.Push(new LocalPriorityInfoDialog(@"Play statistics rebuilt",
                    $@"Counted {scores:N0} saved score(s) of local profiles again; {profiles:N0} profile(s) got higher statistics. "
                    + @"Values only go up, so attempts recorded without a score (failed, quit or restarted) are kept."));
            }));
        }

        private partial class RemoveOldPlaysDialog : DangerousActionDialog
        {
            public RemoveOldPlaysDialog(int scores, long plays, Action remove)
            {
                HeaderText = @"Remove every play dated before 2000?";
                BodyText = $@"{scores:N0} score(s) of local profiles will be deleted and {plays:N0} recorded play(s) on such days forgotten. "
                           + @"Their dates are broken (usually 1 January 0001), which stretches the play history back to year 1.";
                DangerousAction = remove;
            }
        }

        private partial class LocalPriorityInfoDialog : PopupDialog
        {
            public LocalPriorityInfoDialog(string header, string body)
            {
                HeaderText = header;
                BodyText = body;
                Icon = FontAwesome.Solid.InfoCircle;
                Buttons = new PopupDialogButton[] { new PopupDialogOkButton { Text = @"OK" } };
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                importReplaysButton = new SettingsButtonV2
                {
                    Keywords = new[] { @"osr", @"replay", @"import", @"folder" },
                    Action = () =>
                    {
                        if (activeProfile.Value != null)
                            manager.BeginReplayImport(activeProfile.Value.Id);
                    },
                },
                exportTopReplaysButton = new SettingsButtonV2
                {
                    Keywords = new[] { @"osr", @"replay", @"export", @"folder", @"top", @"best" },
                    Action = () =>
                    {
                        if (activeProfile.Value != null)
                            manager.BeginReplayExport(activeProfile.Value.Id, true);
                    },
                },
                exportAllReplaysButton = new SettingsButtonV2
                {
                    Keywords = new[] { @"osr", @"replay", @"export", @"folder", @"all" },
                    Action = () =>
                    {
                        if (activeProfile.Value != null)
                            manager.BeginReplayExport(activeProfile.Value.Id, false);
                    },
                },
                rebuildStatisticsButton = new SettingsButtonV2
                {
                    Text = @"Rebuild play statistics from saved scores (every profile)",
                    TooltipText = @"Counts every saved score (replay) of every local profile again: play count, play time, hits, most played beatmaps "
                                  + @"and play history. Values only go up, so attempts recorded without a score are kept.",
                    Keywords = new[] { @"statistics", @"stats", @"rebuild", @"recalculate", @"repair", @"replay", @"play count", @"history" },
                    Action = rebuildStatistics,
                },
                resetDeathCountsButton = new SettingsButtonV2
                {
                    TooltipText = @"New Sudden Death / Perfect Death / Speedrun profiles are numbered by how many were ever created. This starts them back at ""SD Profile 1"", ""PF Profile 1"" and ""SP Profile 1"".",
                    Keywords = new[] { @"sudden", @"death", @"perfect", @"speedrun", @"sd", @"pf", @"sp", @"count", @"number", @"reset" },
                    Action = () =>
                    {
                        manager.ResetDeathProfileCounts();
                        updateDeathCountsButton();
                    },
                },
                new DangerousSettingsButtonV2
                {
                    Text = @"Remove scores for beatmaps that can't be calculated",
                    Keywords = new[] { @"difficulty", @"broken", @"error", @"calculate", @"pp" },
                    Action = manager.RequestRemoveUncalculableScores,
                },
                new DangerousSettingsButtonV2
                {
                    Text = @"Remove every play dated before the year 2000",
                    Keywords = new[] { @"date", @"0001", @"history", @"broken", @"emergency" },
                    Action = requestRemoveOldPlays,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Delete profiles without asking. Keeps their scores",
                    Current = manager.DeleteKeepingScoresWithoutAsking,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Delete profiles without asking WITH their scores",
                    Current = manager.DeleteWithScoresWithoutAsking,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Extend SD/PF death timer to 1 minute",
                    HintText = @"How long a failed Sudden Death / Perfect Death profile's page stays open before the profile is deleted. 30 seconds by default.",
                    Current = manager.LongerLastLook,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Suggest syncing plays saved under the same name",
                    HintText = @"After creating or renaming a profile to a name that already has scores in local database, offer to count them towards this profile",
                    Current = manager.SuggestSyncingSameNamePlays,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Create profiles with mod rules",
                    HintText = @"Adds a second ""New Profile"" button to the profile page. It opens mod select where you can configure everything",
                    Current = manager.ProfilesWithModRules,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Notify when switching profiles automatically",
                    Current = manager.AutoSwitchNotifications,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Switch profiles on beatmap start if rules are true",
                    Current = manager.SwitchOnBeatmapStart,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Show total hit accuracy",
                    HintText = @"Calculated from every judgment recorded that can be seen in local profile page. Purely statistical.",
                    Current = manager.ShowTotalAccuracy,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Count every map and play as ranked",
                    Current = manager.CountEverythingAsRanked,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Count LOCAL maps towards PP",
                    HintText = @"When off, plays on beatmaps marked LOCAL (changed in the editor) are not shown on profiles at all.",
                    Current = manager.LocalMapsGivePerformance,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Gray out scores that do not go towards PP rather instead of hiding",
                    Current = manager.ShowNonCountingScores,
                }),
                new LocalProfileColourSettings(),
                // Ritsu never signs in to an online account, so the online lookup settings are left out.
            };
        }
    }

    /// <summary>
    /// A colour for the local profile pages, like the profile colour osu!supporters pick on the website.
    /// </summary>
    public partial class LocalProfileColourSettings : LocalSettingsGroup
    {
        public LocalProfileColourSettings()
            : base(@"Profile Colors")
        {
        }

        protected override IEnumerable<LocalisableString> Keywords => new LocalisableString[]
        {
            @"colour", @"color", @"hue", @"tint", @"theme", @"supporter", @"rgb",
        };

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        private Container pickerArea = null!;
        private FillFlowContainer preview = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            LocalCompactColourPicker picker;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Color profile pages",
                    HintText = @"Tints every local profile page with the color below, like an osu!supporter's profile color. "
                               + @"Sudden Death and Perfect Death profiles keep their red and gold.",
                    Current = manager.ProfileColouring,
                }),
                pickerArea = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = picker = new LocalCompactColourPicker
                    {
                        Current = manager.ProfileColour,
                        // The page shades this colour gives (its brightness doesn't matter, the page keeps its own).
                        Footer = preview = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 16,
                            Direction = FillDirection.Horizontal,
                            Masking = true,
                            CornerRadius = 5,
                        },
                    },
                },
            };

            // The website's profile colour presets are osu!'s overlay colour schemes.
            picker.Suggestions.AddRange(new[]
            {
                OverlayColourScheme.Red, OverlayColourScheme.Orange, OverlayColourScheme.Lime, OverlayColourScheme.Green,
                OverlayColourScheme.Aquamarine, OverlayColourScheme.Blue, OverlayColourScheme.Purple, OverlayColourScheme.Plum, OverlayColourScheme.Pink,
            }.Select(scheme => Colour4.FromHSV(scheme.GetHue() / 360f, 1, 1)));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            manager.ProfileColouring.BindValueChanged(e =>
            {
                if (e.NewValue)
                    pickerArea.FadeIn(200, Easing.OutQuint);
                else
                    pickerArea.Hide();
            }, true);
            manager.ProfileColour.BindValueChanged(_ => updatePreview(), true);
        }

        private void updatePreview()
        {
            var hsv = manager.ProfileColour.Value.ToHSV();
            var colours = new OverlayColourProvider((int)MathF.Round(hsv.X * 360) % 360);
            colours.ChangeColourScheme(colours.Hue, hsv.Y);

            var shades = new[]
            {
                colours.Background5, colours.Background3, colours.Dark3, colours.Colour4, colours.Colour3, colours.Colour1, colours.Light1, colours.Content2,
            };

            preview.ChildrenEnumerable = shades.Select(c => new Box
            {
                RelativeSizeAxes = Axes.Both,
                Width = 1f / shades.Length,
                Colour = c,
            });
        }
    }

    /// <summary>
    /// A colour picker that fits the settings panel in about half the height of <see cref="OsuColourPicker"/>:
    /// the hue / saturation square on the left, presets, hex code and an optional footer on the right.
    /// </summary>
    public partial class LocalCompactColourPicker : CompositeDrawable, IHasCurrentValue<Colour4>
    {
        private const float hsv_width = 150;
        private const float spacing = 10;

        private readonly BindableWithCurrent<Colour4> current = new BindableWithCurrent<Colour4>();

        public Bindable<Colour4> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        /// <summary>
        /// Preset colours, shown as swatches.
        /// </summary>
        public BindableList<Colour4> Suggestions { get; } = new BindableList<Colour4>();

        /// <summary>
        /// Shown under the hex code (full width of the right column).
        /// </summary>
        public Drawable? Footer { get; init; }

        private OsuHSVColourPicker hsv = null!;
        private CompactSwatches swatches = null!;
        private OsuHexColourPicker hex = null!;

        public LocalCompactColourPicker()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider? colourProvider, OsuColour osuColour)
        {
            var column = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    swatches = new CompactSwatches
                    {
                        Colours = { BindTarget = Suggestions },
                    },
                    hex = new OsuHexColourPicker
                    {
                        RelativeSizeAxes = Axes.X,
                        Width = 1,
                        Padding = new MarginPadding { Horizontal = spacing, Bottom = spacing },
                        Spacing = spacing,
                    },
                },
            };

            if (Footer != null)
            {
                column.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = spacing, Bottom = spacing },
                    Child = Footer,
                });
            }

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider?.Dark6 ?? osuColour.GreySeaFoamDarker,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, hsv_width), new Dimension() },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            hsv = new OsuHSVColourPicker { Width = hsv_width },
                            column,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hsv.Current = Current;
            swatches.Current = Current;
            hex.Current = Current;
        }

        private partial class CompactSwatches : OsuSwatchColourPicker
        {
            public CompactSwatches()
            {
                Content.Padding = new MarginPadding(spacing);
                Content.Anchor = Anchor.TopLeft;
                Content.Origin = Anchor.TopLeft;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // the whole picker has one background behind the right column.
                Background.Alpha = 0;
            }
        }
    }

    /// <summary>
    /// Whether local profiles may use the signed-in online account for read-only lookups (beatmaps, tags, leaderboards).
    /// </summary>
    public partial class LocalOnlineLookupSettings : LocalSettingsGroup
    {
        public LocalOnlineLookupSettings()
            : base(@"Online lookups on local profiles")
        {
        }

        protected override IEnumerable<LocalisableString> Keywords => new LocalisableString[]
        {
            @"leaderboard", @"beatmap", @"tags", @"download", @"online", @"account", @"ban",
        };

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();
        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private readonly IBindable<LocalProfile?> activeProfile = new Bindable<LocalProfile?>();

        private SettingsItemV2 toggleItem = null!;
        private FormTextBox accountTextBox = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                toggleItem = new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Use your online account for beatmap and leaderboard requests while using local profile",
                    Current = manager.LocalProfilesUseOnlineAccount,
                }),
                new SettingsItemV2(accountTextBox = new FormTextBox
                {
                    Caption = @"Account used for connectivity",
                    ReadOnly = true,
                })
                {
                    ShowRevertToDefaultButton = false,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            apiState.BindTo(api.State);
            localUser.BindTo(api.LocalUser);
            activeProfile.BindTo(manager.ActiveProfile);

            apiState.BindValueChanged(_ => Scheduler.AddOnce(updateStatus));
            localUser.BindValueChanged(_ => Scheduler.AddOnce(updateStatus));
            activeProfile.BindValueChanged(_ => Scheduler.AddOnce(updateStatus));
            manager.LocalProfilesUseOnlineAccount.BindValueChanged(_ => Scheduler.AddOnce(updateStatus), true);
        }

        private void updateStatus()
        {
            bool enabled = manager.LocalProfilesUseOnlineAccount.Value;

            toggleItem.Note.Value = new SettingsNote.Data(
                @"Warning: Ritsu is a modified, unofficial client. This will send requests to osu! servers with your account from it "
                + @"might get your account restricted or banned. Use responsibly.",
                enabled ? SettingsNote.Type.Critical : SettingsNote.Type.Warning);

            // While a local profile is shown as the player, the signed-in online account is kept separately.
            var account = manager.IsLocalProfileActive ? LocalPriorityPolicy.OnlineAccountUser : localUser.Value;
            bool signedIn = account != null && account.Id > 1 && !LocalPriorityPolicy.IsOfflineProfileUser(account);

            switch (apiState.Value)
            {
                case APIState.Online when signedIn:
                    accountTextBox.Current.Value = $@"Signed in as {account!.Username}";
                    break;

                case APIState.Connecting:
                case APIState.Failing:
                    accountTextBox.Current.Value = @"Connecting…";
                    break;

                case APIState.RequiresSecondFactorAuth:
                    accountTextBox.Current.Value = @"Verification required: switch to the online account to finish signing in";
                    break;

                default:
                    accountTextBox.Current.Value = manager.IsLocalProfileActive && !enabled
                        ? @"None. Client does not send any data."
                        : @"Not signed in. Sign in first to use it";
                    break;
            }
        }
    }
}
