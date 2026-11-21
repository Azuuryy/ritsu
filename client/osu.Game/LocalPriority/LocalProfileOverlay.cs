// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens;
using osu.Game.Screens.Select;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// The local profile page, laid out like osu!lazer's player profile but with local-only information.
    /// Profiles are switched and created right here (profile chips under the name, "+" chip); picture, name and flag are edited in place.
    /// Opened by left-clicking the account button while a local profile is active.
    /// </summary>
    public partial class LocalProfileOverlay : FullscreenOverlay<LocalProfileOverlay.LocalProfileHeader>
    {
        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> gameRuleset { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> mutableRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> gameBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogs { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private IBindable<ScoringMode> scoringMode = null!;

        /// <summary>
        /// How far each list ("Best Performance", "Recent Plays", "Most Played Beatmaps") was expanded with "show more".
        /// Kept while the page is closed and reopened (e.g. after viewing a score from it), so it opens as it was left,
        /// until a beatmap is entered (played or watched as a replay) or another profile / mode is shown.
        /// </summary>
        private readonly Dictionary<string, int> expandedLists = new Dictionary<string, int>();

        private string? expandedListsKey;

        // Cleared right away rather than scheduled: a hidden overlay doesn't run its scheduler, so a scheduled clear only ran
        // after the page had already been rebuilt with the old state on reopening.
        private void onBeatmapEntered() => expandedLists.Clear();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (manager.IsNotNull())
                manager.BeatmapEntered -= onBeatmapEntered;
        }

        private readonly IBindable<int> revision = new Bindable<int>();
        private readonly IBindable<LocalProfile?> activeProfile = new Bindable<LocalProfile?>();

        /// <summary>
        /// The ruleset shown on the page (the ruleset tabs in the header), independent of the game's ruleset.
        /// </summary>
        private readonly Bindable<RulesetInfo> profileRuleset = new Bindable<RulesetInfo>();

        private Container headerArea = null!;
        private FillFlowContainer sections = null!;
        private LoadingLayer loading = null!;
        private CancellationTokenSource? refreshCancellation;

        public LocalProfileOverlay()
            : base(OverlayColourScheme.Pink)
        {
        }

        protected override LocalProfileHeader CreateHeader() => new LocalProfileHeader();

        protected override Color4 BackgroundColour => ColourProvider.Background6;

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Ruleset.BindTarget = profileRuleset;

            Children = new Drawable[]
            {
                // Popovers (picture / flag pickers) and right-click menus on the page.
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = scroll = new OverlayScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ScrollbarVisible = false,
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                Header,
                                headerArea = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                },
                                // Section tabs (Historical, Scores) like the website profile's.
                                tabsArea = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                },
                                sections = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding { Horizontal = 10, Bottom = 10 },
                                    Spacing = new Vector2(0, 10),
                                },
                            },
                        },
                    },
                    },
                },
                loading = new LoadingLayer(true),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            revision.BindTo(manager.Revision);
            activeProfile.BindTo(manager.ActiveProfile);

            revision.BindValueChanged(_ => refreshIfVisible());
            manager.BeatmapEntered += onBeatmapEntered;
            activeProfile.BindValueChanged(p =>
            {
                if (p.NewValue == null && displayedProfileId == null)
                    Hide();
                else
                    refreshIfVisible();
            });
            profileRuleset.BindValueChanged(_ => refreshIfVisible());

            // Score display mode (Settings → Gameplay) changes how every score on the page is shown.
            scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);
            scoringMode.BindValueChanged(_ => refreshIfVisible());

            State.BindValueChanged(s =>
            {
                if (s.NewValue != Visibility.Visible)
                {
                    // Next time, open on the active profile again.
                    displayedProfileId = null;
                    return;
                }

                if (gameRuleset.Value?.IsLegacyRuleset() == true)
                    profileRuleset.Value = gameRuleset.Value;

                refresh();
            });
        }

        /// <summary>
        /// A profile shown other than the active one (opened by clicking its picture on a score), or null for the active profile.
        /// </summary>
        private Guid? displayedProfileId;

        /// <summary>
        /// Opens the page of any local profile (clicking a local profile's picture on a leaderboard / results screen).
        /// </summary>
        /// <summary>
        /// A profile whose auto-switch rules editor opens once its page has finished loading (requested from the switcher).
        /// </summary>
        private Guid? pendingAutoSwitchEditor;

        /// <summary>
        /// Opens the profile's page with its auto-switch rules editor (the switcher's right-click menu).
        /// </summary>
        public void ShowAutoSwitchRules(Guid profileId)
        {
            pendingAutoSwitchEditor = profileId;
            ShowProfile(profileId);
        }

        public void ShowProfile(Guid profileId)
        {
            displayedProfileId = profileId == manager.ActiveProfile.Value?.Id ? null : profileId;

            if (State.Value == Visibility.Visible)
                refreshIfVisible();
            else
                Show();
        }

        private OverlayScrollContainer scroll = null!;
        private Container tabsArea = null!;

        /// <summary>
        /// "Historical  Scores" above the sections, like the website profile's section tabs: click one to scroll to it,
        /// drag them to put the sections in another order (saved for each profile, like supporters' section order).
        /// </summary>
        private partial class SectionTabs : CompositeDrawable
        {
            private readonly IReadOnlyList<ProfileSection> profileSections;
            private readonly Action<ProfileSection> scrollTo;
            private readonly Action<IReadOnlyList<ProfileSection>> reordered;

            private FillFlowContainer<SectionTab> flow = null!;

            public SectionTabs(IReadOnlyList<ProfileSection> profileSections, Action<ProfileSection> scrollTo, Action<IReadOnlyList<ProfileSection>> reordered)
            {
                this.profileSections = profileSections;
                this.scrollTo = scrollTo;
                this.reordered = reordered;

                RelativeSizeAxes = Axes.X;
                Height = 40;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                flow = new FillFlowContainer<SectionTab>
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(20),
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING },
                };

                for (int i = 0; i < profileSections.Count; i++)
                {
                    var section = profileSections[i];

                    var tab = new SectionTab(section)
                    {
                        Action = () => scrollTo(section),
                        Dragging = dragTo,
                        DragEnded = () => reordered(flow.FlowingChildren.Cast<SectionTab>().Select(t => t.Section).ToArray()),
                    };

                    flow.Add(tab);
                    flow.SetLayoutPosition(tab, i);
                }

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5,
                    },
                    flow,
                };
            }

            /// <summary>
            /// Moves the dragged tab past the tab under the cursor once the cursor crosses that tab's middle.
            /// </summary>
            private void dragTo(SectionTab dragged, Vector2 screenSpaceMouse)
            {
                var tabs = flow.FlowingChildren.Cast<SectionTab>().ToList();
                int from = tabs.IndexOf(dragged);

                foreach (var other in tabs)
                {
                    if (other == dragged)
                        continue;

                    int to = tabs.IndexOf(other);
                    float middle = other.ScreenSpaceDrawQuad.Centre.X;

                    if ((to > from && screenSpaceMouse.X > middle) || (to < from && screenSpaceMouse.X < middle))
                    {
                        tabs.RemoveAt(from);
                        tabs.Insert(to, dragged);

                        for (int i = 0; i < tabs.Count; i++)
                            flow.SetLayoutPosition(tabs[i], i);

                        return;
                    }
                }
            }

            private partial class SectionTab : OsuClickableContainer
            {
                public readonly ProfileSection Section;

                public Action<SectionTab, Vector2>? Dragging { get; init; }
                public Action? DragEnded { get; init; }

                private OsuSpriteText text = null!;

                [Resolved]
                private OverlayColourProvider colours { get; set; } = null!;

                public SectionTab(ProfileSection section)
                {
                    Section = section;

                    AutoSizeAxes = Axes.Both;
                    Anchor = Anchor.CentreLeft;
                    Origin = Anchor.CentreLeft;
                    TooltipText = @"Click: go to section · drag: change the order";
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    Child = text = new OsuSpriteText
                    {
                        Text = Section.Title,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    };

                    updateState();
                }

                protected override bool OnHover(HoverEvent e)
                {
                    updateState();
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    updateState();
                    base.OnHoverLost(e);
                }

                protected override bool OnDragStart(DragStartEvent e)
                {
                    if (e.Button != osuTK.Input.MouseButton.Left)
                        return false;

                    this.FadeTo(0.6f, 100);
                    return true;
                }

                protected override void OnDrag(DragEvent e) => Dragging?.Invoke(this, e.ScreenSpaceMousePosition);

                protected override void OnDragEnd(DragEndEvent e)
                {
                    this.FadeTo(1, 200);
                    DragEnded?.Invoke();
                }

                // Every tab the same colour (no "current" tab), brighter on hover.
                private void updateState() => text.FadeColour(IsHovered ? colours.Light1 : colours.Light2, 300, Easing.OutQuint);
            }
        }

        private TopSection? currentTop;
        private DetailSection? currentDetail;
        private string? currentTopSignature;

        /// <summary>
        /// Everything the top part shows, so it is only rebuilt when something in it changed.
        /// </summary>
        private static string topSignature(LocalProfile shown, IReadOnlyList<LocalProfile> all) =>
            $@"{shown.Id}#" + string.Join(@"|", all.Select(p => $@"{p.Id}:{p.Name}:{p.Country}:{p.AvatarFile}:{p.ModRules?.Describe()}:{p.AutoSwitch?.Describe()}:{p.SyncedScores.Count}:{p.LinkedOwners.Count}"));

        private (LocalProfileStatistics statistics, Dictionary<Guid, double> ppByProfile)? cachedStatistics;
        private string? cachedStatisticsKey;

        /// <summary>
        /// Bumped when scores change from this page (deleted), so remembered statistics are calculated again.
        /// Profile changes, recorded plays, transfers and syncs bump the manager's revision instead.
        /// </summary>
        private int scoresVersion;

        private void invalidateStatistics()
        {
            scoresVersion++;
            refreshIfVisible();
        }

        private Guid? takePendingAutoSwitchEditor()
        {
            var id = pendingAutoSwitchEditor;
            pendingAutoSwitchEditor = null;
            return id;
        }

        private void refreshIfVisible() => Scheduler.AddOnce(() =>
        {
            if (State.Value == Visibility.Visible)
                refresh();
        });

        private void refresh()
        {
            refreshCancellation?.Cancel();
            refreshCancellation = new CancellationTokenSource();
            var token = refreshCancellation.Token;

            var allProfilesNow = manager.GetProfiles();
            var profile = (displayedProfileId != null ? allProfilesNow.FirstOrDefault(p => p.Id == displayedProfileId) : null) ?? manager.ActiveProfile.Value;
            var mode = profileRuleset.Value ?? gameRuleset.Value;

            if (profile == null || mode == null)
            {
                headerArea.Clear();
                sections.Clear();
                return;
            }

            var allProfiles = manager.GetProfiles();

            // Same profile, mode and data as last time: show the remembered numbers at once (nothing recalculated or built twice).
            string cacheKey = $@"{profile.Id}|{mode.ShortName}|{manager.Revision.Value}|{scoresVersion}";

            if (cachedStatistics != null && cachedStatisticsKey == cacheKey)
            {
                loading.Hide();
                populate(profile, allProfiles, cachedStatistics.Value.statistics, cachedStatistics.Value.ppByProfile);
                return;
            }

            // Show the profile right away; numbers follow once calculated (the top part with the picture stays, see populate).
            populate(profile, allProfiles, null, null);
            loading.Show();

            Task.Run(async () =>
            {
                var statistics = await LocalProfileStatistics.CalculateAsync(profile, mode, realm, beatmaps, manager, token).ConfigureAwait(false);
                var ppByProfile = new Dictionary<Guid, double> { [profile.Id] = statistics.PP };

                // Other profiles only need their PP total (chips, local ranking).
                foreach (var other in allProfiles.Where(p => p.Id != profile.Id))
                    ppByProfile[other.Id] = await LocalProfileStatistics.CalculatePerformanceAsync(other, mode, realm, beatmaps, manager, token).ConfigureAwait(false);

                return (statistics, ppByProfile);
            }, token).ContinueWith(task => Schedule(() =>
            {
                if (token.IsCancellationRequested || task.IsCanceled)
                    return;

                loading.Hide();

                if (task.IsFaulted)
                {
                    Logger.Error(task.Exception, "Ritsu: could not calculate local profile statistics.");
                    return;
                }

                var result = task.GetResultSafely();

                cachedStatistics = (result.statistics, result.ppByProfile);
                cachedStatisticsKey = cacheKey;

                populate(profile, allProfiles, result.statistics, result.ppByProfile);
            }), CancellationToken.None);
        }

        private void populate(LocalProfile profile, IReadOnlyList<LocalProfile> allProfiles, LocalProfileStatistics? stats, IReadOnlyDictionary<Guid, double>? ppByProfile)
        {
            int? localRank = stats != null && ppByProfile != null && stats.BestScores.Any(s => s.CountsForPerformance)
                ? 1 + ppByProfile.Count(p => p.Key != profile.Id && p.Value > stats.PP)
                : null;

            string signature = topSignature(profile, allProfiles);

            // Numbers arriving for the page already shown: keep the top part (picture, name, chips) and only update the PP on the chips,
            // so nothing flashes; the statistics part below is replaced.
            if (currentTop != null && currentTop.Parent == headerArea && currentTop.IsLoaded
                && currentTopSignature == signature && pendingAutoSwitchEditor == null)
            {
                currentTop.SetPerformance(ppByProfile);
                currentTop.SetLastPlayed(stats?.Plays.LastPlayed);
                currentTop.DetailSlot.Child = currentDetail = new DetailSection(profile, manager, stats, localRank, profileRuleset.Value);
            }
            else
            {
                currentTopSignature = signature;

                headerArea.Child = currentTop = new TopSection(profile, allProfiles, ppByProfile)
                    {
                        LastPlayed = stats?.Plays.LastPlayed,
                        // The page is built twice (right away, then with the statistics): open the editor on the final one.
                        OpenAutoSwitchEditorFor = stats != null ? takePendingAutoSwitchEditor() : null,
                        SwitchTo = id =>
                        {
                            displayedProfileId = null;
                            manager.Switch(id);
                        },
                        HideOverlay = Hide,
                        CreateProfile = () =>
                        {
                            var created = manager.CreateDefaultProfile();
                            manager.Switch(created.Id);
                            LocalProfileSyncDialog.Offer(manager, dialogs, created.Id);
                        },
                    };

                currentTop.DetailSlot.Child = currentDetail = new DetailSection(profile, manager, stats, localRank, profileRuleset.Value);
            }

            sections.Clear();
            tabsArea.Clear();

            if (stats == null)
                return;

            string listsKey = $@"{profile.Id}|{(profileRuleset.Value ?? gameRuleset.Value)?.ShortName}";

            if (expandedListsKey != listsKey)
            {
                expandedLists.Clear();
                expandedListsKey = listsKey;
            }

            // Default order: Scores first, then Historical; each profile can drag its own order, like supporters can.
            var profileSections = new ProfileSection[]
            {
                new ScoresPanel(stats, score => presentScore(score, ScorePresentType.Results), score => scoreMenu(profile.Id, score), expandedLists),
                new HistoricalPanel(stats, presentBeatmap, expandedLists),
            };

            int orderOf(ProfileSection section)
            {
                int index = profile.SectionOrder.IndexOf(section.Identifier);
                return index < 0 ? 1000 + Array.IndexOf(profileSections, section) : index;
            }

            profileSections = profileSections.OrderBy(orderOf).ToArray();

            for (int i = 0; i < profileSections.Length; i++)
            {
                sections.Add(profileSections[i]);
                sections.SetLayoutPosition(profileSections[i], i);
            }

            Guid profileId = profile.Id;

            tabsArea.Child = new SectionTabs(profileSections, section => scroll.ScrollIntoView(section), ordered =>
            {
                for (int i = 0; i < ordered.Count; i++)
                    sections.SetLayoutPosition(ordered[i], i);

                manager.SetSectionOrder(profileId, ordered.Select(o => o.Identifier));
            });
        }

        /// <summary>
        /// Right-click menu of a score row.
        /// </summary>
        private MenuItem[] scoreMenu(Guid profileId, ScoreInfo score)
        {
            var items = new List<MenuItem>
            {
                new OsuMenuItem(@"View results", MenuItemType.Highlighted, () => presentScore(score, ScorePresentType.Results)),
            };

            if (score.Files.Count > 0)
                items.Add(new OsuMenuItem(@"Watch replay", MenuItemType.Standard, () => presentScore(score, ScorePresentType.Gameplay)));

            if (score.BeatmapInfo != null)
                items.Add(new OsuMenuItem(@"Show beatmap in song select", MenuItemType.Standard, () => presentScore(score, null)));

            items.Add(new OsuMenuItemSpacer());

            if (score.Files.Count > 0)
                items.Add(new OsuMenuItem(@"Export replay", MenuItemType.Standard, () => scoreManager.Export(score)));

            items.Add(LocalProfileScoreTransfer.CreateMenuItem(manager, score));
            items.Add(LocalPerformanceExclusions.CreateScoreMenuItem(manager, score));

            bool deleteScore = manager.CanDeleteScore(profileId, score.ID);
            items.Add(new OsuMenuItem(deleteScore ? @"Delete score" : @"Remove score from profile", MenuItemType.Destructive,
                () => dialogs?.Push(new DeleteScoreDialog(score, deleteScore, () =>
                {
                    manager.RemoveScore(profileId, score);
                    invalidateStatistics();
                }))));

            return items.ToArray();
        }

        /// <summary>
        /// Shows a score (results or replay) from song select, at the score's own beatmap, difficulty and ruleset,
        /// so leaving the results / replay lands in the song list on that difficulty instead of the main menu.
        /// With no <paramref name="type"/>, only song select is opened on that difficulty.
        /// </summary>
        private void presentScore(ScoreInfo score, ScorePresentType? type)
        {
            if (game == null)
                return;

            var beatmap = score.BeatmapInfo;

            if (beatmap == null || beatmap.BeatmapSet?.DeletePending == true)
            {
                if (type != null)
                    game.PresentScore(score, type.Value);
                return;
            }

            game.PerformFromScreen(screen =>
            {
                var ruleset = rulesets.GetRuleset(score.Ruleset.ShortName) ?? score.Ruleset;
                var working = beatmaps.GetWorkingBeatmap(beatmap);

                if (screen is SoloSongSelect songSelect)
                {
                    // Same path as "show in song select" elsewhere in the game (leaves a scoped beatmap set, cancels pending selection).
                    ((IHandlePresentBeatmap)songSelect).PresentBeatmap(working, ruleset);
                }
                else
                {
                    // Main menu: open song select already on this difficulty.
                    mutableRuleset.Value = ruleset;
                    gameBeatmap.Value = working;
                    screen.Push(new SoloSongSelect());
                }

                // The score's ruleset wins (converted plays).
                if (!mutableRuleset.Value.Equals(ruleset))
                    mutableRuleset.Value = ruleset;

                if (!gameBeatmap.Value.BeatmapInfo.Equals(beatmap))
                    gameBeatmap.Value = working;

                // Beatmap and ruleset now match, so the game shows the score on top of song select (once it has loaded).
                if (type != null)
                    game.PresentScore(score, type.Value);
            }, new[] { typeof(SoloSongSelect) });
        }

        /// <summary>
        /// "Most Played Beatmaps": opens song select on that difficulty, in the mode shown on the profile page.
        /// </summary>
        private void presentBeatmap(BeatmapInfo beatmap)
        {
            if (game == null)
                return;

            game.PerformFromScreen(screen =>
            {
                var ruleset = rulesets.GetRuleset((profileRuleset.Value ?? beatmap.Ruleset).ShortName) ?? beatmap.Ruleset;
                var working = beatmaps.GetWorkingBeatmap(beatmap);

                if (screen is SoloSongSelect songSelect)
                {
                    mutableRuleset.Value = ruleset;
                    ((IHandlePresentBeatmap)songSelect).PresentBeatmap(working, ruleset);
                }
                else
                {
                    mutableRuleset.Value = ruleset;
                    gameBeatmap.Value = working;
                    screen.Push(new SoloSongSelect());
                }
            }, new[] { typeof(SoloSongSelect) });
        }

        private partial class DeleteScoreDialog : DeletionDialog
        {
            private readonly ScoreInfo score;
            private readonly Action deleted;
            private readonly bool deleteScore;

            public DeleteScoreDialog(ScoreInfo score, bool deleteScore, Action deleted)
            {
                this.score = score;
                this.deleted = deleted;
                this.deleteScore = deleteScore;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                string beatmap = score.BeatmapInfo != null ? $@"{score.BeatmapInfo.Metadata.GetDisplayTitle()} [{score.BeatmapInfo.DifficultyName}]" : @"(beatmap not found)";

                BodyText = $@"{beatmap}{Environment.NewLine}{score.DisplayAccuracy}, {score.Rank.GetDescription()}, {score.TotalScore:N0}";
                if (!deleteScore)
                {
                    HeaderText = @"Remove score from this profile?";
                    BodyText += Environment.NewLine + @"The original score and replay stay in the database.";
                }

                DangerousAction = deleted;
            }
        }

        #region Header

        public partial class LocalProfileHeader : TabControlOverlayHeader<LocalisableString>
        {
            public readonly Bindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

            public LocalProfileHeader()
            {
                ContentSidePadding = WaveOverlayContainer.HORIZONTAL_PADDING;
                TabControl.AddItem(@"info");
            }

            protected override OverlayTitle CreateTitle() => new LocalProfileTitle();

            protected override Drawable CreateTabControlContent() => new OverlayRulesetSelector
            {
                Current = Ruleset,
            };

            private partial class LocalProfileTitle : OverlayTitle
            {
                public LocalProfileTitle()
                {
                    Title = @"local profile";
                    Description = @"offline statistics for this profile";
                    Icon = OsuIcon.Player;
                }
            }
        }

        /// <summary>
        /// Cover, avatar, name and flag, plus the profile chips used to switch / create profiles.
        /// Picture, name and flag are edited in place.
        /// </summary>
        private partial class TopSection : CompositeDrawable
        {
            public Action<Guid>? SwitchTo;
            public Action? CreateProfile;

            /// <summary>
            /// Hides the profile page (the mod rules picker takes the whole screen).
            /// </summary>
            public Action? HideOverlay;

            private readonly Dictionary<Guid, (ProfileChip Chip, string Name)> chipsById = new Dictionary<Guid, (ProfileChip, string)>();

            private static string chipText(string name, Guid id, IReadOnlyDictionary<Guid, double>? ppByProfile) =>
                ppByProfile != null && ppByProfile.TryGetValue(id, out double value) ? $@" · {value:N0}pp" : string.Empty;

            /// <summary>
            /// Shows the PP totals on the chips once they are calculated (without rebuilding the top part).
            /// </summary>
            public void SetPerformance(IReadOnlyDictionary<Guid, double>? ppByProfile)
            {
                foreach (var (id, (chip, name)) in chipsById)
                    chip.SetText($@"{name}{chipText(name, id, ppByProfile)}");
            }

            /// <summary>
            /// Opens this profile's auto-switch rules editor as soon as its chip is shown.
            /// </summary>
            public Guid? OpenAutoSwitchEditorFor { get; init; }

            private readonly LocalProfile profile;
            private readonly IReadOnlyList<LocalProfile> allProfiles;
            private readonly IReadOnlyDictionary<Guid, double>? ppByProfile;

            public TopSection(LocalProfile profile, IReadOnlyList<LocalProfile> allProfiles, IReadOnlyDictionary<Guid, double>? ppByProfile)
            {
                this.profile = profile;
                this.allProfiles = allProfiles;
                this.ppByProfile = ppByProfile;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [Resolved]
            private IDialogOverlay? dialogs { get; set; }

            [Resolved]
            private IOverlayManager? overlayManager { get; set; }

            [Resolved]
            private LocalProfileManager manager { get; set; } = null!;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                // One row of profiles; with many profiles it scrolls sideways (wheel or drag), fading out towards the edges.
                var chips = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10),
                };

                int position = 0;

                foreach (var p in allProfiles)
                {
                    Guid id = p.Id;
                    var snapshot = p;
                    string pp = chipText(p.Name, id, ppByProfile);

                    ProfileChip? chipRef = null;

                    var menu = LocalProfileMenu.Create(manager, dialogs, overlayManager, snapshot,
                        () => SwitchTo?.Invoke(id), () => chipRef?.ShowPopover(), HideOverlay);

                    var chip = chipRef = new ProfileChip($@"{p.Name}{pp}", p.Id == profile.Id, () => SwitchTo?.Invoke(id))
                    {
                        ProfileId = id,
                        ContextMenuItems = menu,
                        TooltipText = LocalProfileMenu.Tooltip(snapshot, @"drag to reorder"),
                        Dragging = (dragged, mouse) => LocalProfileDragReorder.DragTo(chips, dragged, mouse),
                        DragEnded = () => manager.Reorder(LocalProfileDragReorder.CurrentOrder(chips)),
                        PopoverFactory = () => new LocalAutoSwitchPopover(snapshot, manager),
                    };

                    chips.Add(chip);
                    chips.SetLayoutPosition(chip, position++);
                    chipsById[id] = (chip, p.Name);

                    if (OpenAutoSwitchEditorFor == id)
                        chip.OnLoadComplete += _ => Schedule(() => chip.ShowPopover());
                }

                // "+" (new profile) and, when enabled in settings, "+ mods" (new profile with mod rules) stay fixed right of the row.
                var createButtons = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10),
                    Margin = new MarginPadding { Left = 10 },
                    Children = new Drawable[]
                    {
                        new ProfileChip(null, false, () => CreateProfile?.Invoke(), FontAwesome.Solid.Plus) { TooltipText = @"New local profile" },
                    },
                };

                if (manager.ProfilesWithModRules.Value)
                {
                    createButtons.Add(new LocalProfileModRulesButton
                    {
                        Size = new Vector2(58, 36),
                        CornerRadius = 18,
                        Action = () => LocalProfileModRulesOverlay.CreateProfileWithRules(overlayManager, manager, dialogs, HideOverlay),
                    });
                }

                var chipRow = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 36,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FadingHorizontalScroll(chips, colours.Background3) { RelativeSizeAxes = Axes.Both },
                            createButtons,
                        },
                    },
                };

                // Laid out like the website profile: header (cover, picture, name, flag), statistics, a buttons bar (here: the profile chips)
                // and an info line.
                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colours.Background3,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        // Local profiles have no online cover; a soft gradient in the overlay's colours instead (shown / hidden like the profile cover).
                                        cover = new Box
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 0,
                                            Alpha = 0,
                                            Colour = ColourInfo.GradientVertical(colours.Dark1, colours.Background3),
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Children = new Drawable[]
                                            {
                                                headerFlow = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = header_content_height + 2 * header_vertical_padding,
                                                    Direction = FillDirection.Horizontal,
                                                    Padding = new MarginPadding { Left = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = header_vertical_padding },
                                                    Spacing = new Vector2(10),
                                                    Children = new Drawable[]
                                                    {
                                                        avatar = new AvatarButton(profile, manager)
                                                        {
                                                            Anchor = Anchor.BottomLeft,
                                                            Origin = Anchor.BottomLeft,
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            AutoSizeAxes = Axes.Both,
                                                            Direction = FillDirection.Vertical,
                                                            Anchor = Anchor.CentreLeft,
                                                            Origin = Anchor.CentreLeft,
                                                            Children = new Drawable[]
                                                            {
                                                                new EditableName(profile, manager),
                                                                new FillFlowContainer
                                                                {
                                                                    AutoSizeAxes = Axes.Both,
                                                                    Direction = FillDirection.Horizontal,
                                                                    Spacing = new Vector2(4, 0),
                                                                    Children = new Drawable[]
                                                                    {
                                                                        new FlagButton(profile, manager),
                                                                        new OsuSpriteText
                                                                        {
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                            Text = profile.Country.GetDescription(),
                                                                            Font = OsuFont.GetFont(size: 14f, weight: FontWeight.Regular),
                                                                        },
                                                                    },
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                                coverToggle = new ToggleCoverButton
                                                {
                                                    Anchor = Anchor.CentreRight,
                                                    Origin = Anchor.CentreRight,
                                                    Margin = new MarginPadding { Right = 10 },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                        DetailSlot,
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colours.Background3,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                                    Child = chipRow,
                                },
                            },
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colours.Background4,
                                },
                                infoText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                                },
                            },
                        },
                    },
                };

                SetLastPlayed(LastPlayed);
            }

            /// <summary>
            /// For the info line when the top part is first shown.
            /// </summary>
            public DateTimeOffset? LastPlayed { get; init; }

            private const float header_content_height = 65;
            private const float header_vertical_padding = 10;

            private Box cover = null!;
            private FillFlowContainer headerFlow = null!;
            private AvatarButton avatar = null!;
            private ToggleCoverButton coverToggle = null!;
            private OsuTextFlowContainer infoText = null!;

            /// <summary>
            /// Where the statistics go (between the header and the profile chips, like on the website profile).
            /// </summary>
            public Container DetailSlot { get; } = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // Same setting as the regular profile's cover toggle.
                // Held in a field: the toggle only keeps a weak reference to it, so once garbage collected, hiding the cover
                // stopped being saved, and the next page built (e.g. for a new profile) opened with the cover expanded.
                coverExpanded = config.GetBindable<bool>(OsuSetting.ProfileCoverExpanded);
                coverToggle.CoverExpanded.BindTo(coverExpanded);
                coverToggle.CoverExpanded.BindValueChanged(_ => updateCoverState(), true);
                FinishTransforms(true);
            }

            [Resolved]
            private OsuConfigManager config { get; set; } = null!;

            private Bindable<bool> coverExpanded = null!;

            private void updateCoverState()
            {
                const float transition_duration = 500;

                bool expanded = coverToggle.CoverExpanded.Value;

                cover.ResizeHeightTo(expanded ? 250 : 0, transition_duration, Easing.OutQuint);

                if (expanded)
                    cover.FadeIn(transition_duration, Easing.OutQuint);
                else
                    cover.FadeOut(transition_duration, Easing.InQuint);

                avatar.SetExpanded(expanded, transition_duration);
                headerFlow.TransformTo(nameof(headerFlow.Spacing), new Vector2(expanded ? 20f : 10f), transition_duration, Easing.OutQuint);
            }

            /// <summary>
            /// The info line under the profile chips, like "Joined …" on the website profile.
            /// </summary>
            public void SetLastPlayed(DateTimeOffset? lastPlayed)
            {
                infoText.Clear();

                infoText.AddText(@"Created ");
                infoText.AddText(profile.Created.Humanize(), t => t.Font = t.Font.With(weight: FontWeight.Bold));

                infoText.AddText(@"     Last played ");
                infoText.AddText(lastPlayed != null ? lastPlayed.Value.Humanize() : @"never", t => t.Font = t.Font.With(weight: FontWeight.Bold));

                int synced = manager.Profiles.CountSyncedScores(profile.Id);
                if (synced > 0)
                {
                    infoText.AddText(@"     Synced ");
                    infoText.AddText($@"{synced:N0} plays", t => t.Font = t.Font.With(weight: FontWeight.Bold));
                }

                infoText.AddText(@"     Local profile, ");
                infoText.AddText(@"fully offline",
                    t => t.Font = t.Font.With(weight: FontWeight.Bold));
            }
        }

        /// <summary>
        /// The profile picture. Click: choose a new picture. Right-click: change / remove.
        /// </summary>
        private partial class AvatarButton : OsuClickableContainer, IHasPopover, IHasContextMenu
        {
            private static readonly string[] image_extensions = { @".png", @".jpg", @".jpeg" };

            private readonly LocalProfile profile;
            private readonly LocalProfileManager manager;
            private readonly Bindable<FileInfo?> picture = new Bindable<FileInfo?>();

            private Container hoverLayer = null!;

            private Container pictureContainer = null!;

            /// <summary>
            /// Small with the cover hidden, big with it shown (like the regular profile's avatar).
            /// </summary>
            public void SetExpanded(bool expanded, double duration)
            {
                this.ResizeTo(new Vector2(expanded ? 120 : 65), duration, Easing.OutQuint);
                pictureContainer.TransformTo(nameof(pictureContainer.CornerRadius), expanded ? 40f : 20f, duration, Easing.OutQuint);
            }

            public AvatarButton(LocalProfile profile, LocalProfileManager manager)
            {
                this.profile = profile;
                this.manager = manager;

                Size = new Vector2(65);
                TooltipText = @"Click to change picture · right-click for options";
                Action = this.ShowPopover;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = pictureContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 20,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Offset = new Vector2(0, 1),
                        Radius = 3,
                        Colour = Colour4.Black.Opacity(0.25f),
                    },
                    Children = new Drawable[]
                    {
                        new LocalProfileAvatar(profile.AvatarFile) { RelativeSizeAxes = Axes.Both },
                        hoverLayer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.4f),
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(26),
                                    Icon = FontAwesome.Solid.Camera,
                                },
                            },
                        },
                    },
                };

                picture.BindValueChanged(p =>
                {
                    if (p.NewValue == null)
                        return;

                    this.HidePopover();

                    try
                    {
                        manager.SetAvatar(profile.Id, p.NewValue);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, @"Ritsu: could not use that image as a profile picture.");
                    }
                });
            }

            public Popover GetPopover() => new FormFileSelector.FileChooserPopover(image_extensions, picture, null, false);

            public MenuItem[] ContextMenuItems
            {
                get
                {
                    var items = new List<MenuItem>
                    {
                        new OsuMenuItem(@"Change picture", MenuItemType.Standard, this.ShowPopover),
                    };

                    if (profile.AvatarFile != null)
                        items.Add(new OsuMenuItem(@"Remove picture", MenuItemType.Destructive, () => manager.SetAvatar(profile.Id, null)));

                    return items.ToArray();
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverLayer.FadeIn(150, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverLayer.FadeOut(300, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// The profile name. Glows gold on hover; the pen unlocks it for editing in place (Enter saves, Escape cancels).
        /// </summary>
        private partial class EditableName : CompositeDrawable
        {
            private const float font_size = 24;

            private readonly LocalProfile profile;
            private readonly LocalProfileManager manager;

            private Container nameContainer = null!;
            private OsuSpriteText nameText = null!;
            private NameTextBox textBox = null!;
            private IconButton pen = null!;

            [Resolved]
            private OsuColour osuColour { get; set; } = null!;

            [Resolved]
            private IDialogOverlay? dialogs { get; set; }

            private bool editing;

            public EditableName(LocalProfile profile, LocalProfileManager manager)
            {
                this.profile = profile;
                this.manager = manager;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        nameContainer = new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Children = new Drawable[]
                            {
                                nameText = new OsuSpriteText
                                {
                                    Text = profile.Name,
                                    Font = OsuFont.GetFont(size: font_size, weight: FontWeight.Regular),
                                    Margin = new MarginPadding { Horizontal = 4 },
                                },
                                textBox = new NameTextBox
                                {
                                    Width = 320,
                                    Height = 34,
                                    Alpha = 0,
                                    LengthLimit = LocalProfileStore.MAX_NAME_LENGTH,
                                    CommitOnFocusLost = true,
                                    OriginalText = () => profile.Name,
                                },
                            },
                        },
                        pen = new IconButton
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.PencilAlt,
                            TooltipText = @"Edit name",
                            Action = startEditing,
                        },
                    },
                };

                textBox.OnCommit += (_, _) => finishEditing(save: true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                // Like usernames in chat: the text itself turns golden.
                if (!editing)
                    nameText.FadeColour(osuColour.Yellow, 100, Easing.OutQuint);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                nameText.FadeColour(Color4.White, 300, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            private void startEditing()
            {
                if (editing)
                {
                    finishEditing(save: true);
                    return;
                }

                editing = true;
                textBox.Text = profile.Name;
                nameText.Hide();
                textBox.Show();
                pen.Icon = FontAwesome.Solid.Check;
                pen.TooltipText = @"Save name";

                Schedule(() =>
                {
                    GetContainingFocusManager()?.ChangeFocus(textBox);
                    textBox.SelectAll();
                });
            }

            private void finishEditing(bool save)
            {
                if (!editing)
                    return;

                editing = false;

                string newName = textBox.Text.Trim();

                textBox.Hide();
                nameText.Show();
                nameText.FadeColour(IsHovered ? osuColour.Yellow : Color4.White, 200, Easing.OutQuint);
                pen.Icon = FontAwesome.Solid.PencilAlt;
                pen.TooltipText = @"Edit name";

                if (!save || newName == profile.Name)
                    return;

                try
                {
                    // The page rebuilds itself after a rename (profile change notification).
                    manager.Rename(profile.Id, newName);
                    nameText.Text = newName;

                    // Plays already saved under the new name (online account, guest, an old profile)? Offer to sync them.
                    LocalProfileSyncDialog.Offer(manager, dialogs, profile.Id);
                }
                catch (ArgumentException e)
                {
                    nameText.Text = profile.Name;
                    nameText.FlashColour(osuColour.Red, 1000, Easing.OutQuint);
                    pen.TooltipText = e.Message;
                }
            }

            private partial class NameTextBox : OsuTextBox
            {
                protected override FontUsage Font => OsuFont.GetFont(size: font_size, weight: FontWeight.Regular);

                public Func<string>? OriginalText { get; init; }

                protected override bool OnKeyDown(KeyDownEvent e)
                {
                    if (e.Key == osuTK.Input.Key.Escape)
                    {
                        // Escape cancels: restore the old text before losing focus (which commits).
                        Text = OriginalText?.Invoke() ?? Text;
                        KillFocus();
                        return true;
                    }

                    return base.OnKeyDown(e);
                }
            }
        }

        /// <summary>
        /// The profile flag. Click: choose a flag. Right-click: change / reset.
        /// </summary>
        private partial class FlagButton : OsuClickableContainer, IHasPopover, IHasContextMenu
        {
            private readonly LocalProfile profile;
            private readonly LocalProfileManager manager;

            public FlagButton(LocalProfile profile, LocalProfileManager manager)
            {
                this.profile = profile;
                this.manager = manager;

                Size = new Vector2(28, 20);
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                Action = this.ShowPopover;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new DrawableFlag(profile.Country) { RelativeSizeAxes = Axes.Both };
            }

            public Popover GetPopover() => new CountryPickerPopover(country =>
            {
                manager.SetCountry(profile.Id, country);
                this.HidePopover();
            });

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem(@"Change flag", MenuItemType.Standard, this.ShowPopover),
                new OsuMenuItem(@"Reset flag", MenuItemType.Destructive, () => manager.SetCountry(profile.Id, CountryCode.Unknown)),
            };
        }

        /// <summary>
        /// Searchable list of all flags.
        /// </summary>
        private partial class CountryPickerPopover : OsuPopover
        {
            private readonly Action<CountryCode> onPick;

            private SearchTextBox search = null!;
            private SearchContainer list = null!;

            public CountryPickerPopover(Action<CountryCode> onPick)
            {
                this.onPick = onPick;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                list = new SearchContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                };

                foreach (var country in Enum.GetValues<CountryCode>())
                    list.Add(new CountryRow(country, () => onPick(country)));

                Child = new FillFlowContainer
                {
                    Width = 300,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        search = new SearchTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            PlaceholderText = @"Search countries",
                        },
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 320,
                            Child = list,
                        },
                    },
                };

                search.Current.BindValueChanged(s => list.SearchTerm = s.NewValue);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(search));
            }

            private partial class CountryRow : OsuClickableContainer, IFilterable
            {
                private readonly CountryCode country;

                private Box hover = null!;

                public CountryRow(CountryCode country, Action action)
                {
                    this.country = country;
                    Action = action;

                    RelativeSizeAxes = Axes.X;
                    Height = 28;
                    Masking = true;
                    CornerRadius = 4;
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    Children = new Drawable[]
                    {
                        hover = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White.Opacity(0.1f),
                            Alpha = 0,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Padding = new MarginPadding { Horizontal = 6 },
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    Size = new Vector2(24, 17),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Child = new DrawableFlag(country) { RelativeSizeAxes = Axes.Both },
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = country == CountryCode.Unknown ? @"No flag" : country.GetDescription(),
                                    Font = OsuFont.GetFont(size: 14),
                                },
                            },
                        },
                    };
                }

                public IEnumerable<LocalisableString> FilterTerms => new LocalisableString[] { country.GetDescription(), country.ToString() };

                public bool MatchingFilter
                {
                    set => this.FadeTo(value ? 1 : 0);
                }

                public bool FilteringActive { set { } }

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
        }

        private partial class ProfileChip : OsuClickableContainer, IHasContextMenu, IReorderableProfileItem, IHasPopover
        {
            private OsuSpriteText? textSprite;

            public void SetText(string newText)
            {
                if (textSprite != null)
                    textSprite.Text = newText;
            }

            /// <summary>
            /// Profile chips: the auto-switch rules editor (opened from the right-click menu).
            /// </summary>
            public Func<Popover>? PopoverFactory { get; init; }

            public Popover GetPopover() => PopoverFactory?.Invoke() ?? new OsuPopover();

            private readonly string? text;
            private readonly bool isActive;
            private readonly IconUsage? icon;

            private Box hover = null!;

            public MenuItem[]? ContextMenuItems { get; init; }

            public ProfileChip(string? text, bool isActive, Action action, IconUsage? icon = null)
            {
                this.text = text;
                this.isActive = isActive;
                this.icon = icon;
                Action = action;

                Height = 36;
                Masking = true;
                CornerRadius = 18;

                if (icon != null && text == null)
                    Width = 36;
                else
                    AutoSizeAxes = Axes.X;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                bool iconOnly = icon != null && text == null;

                var label = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(7, 0),
                };

                if (icon != null)
                    label.Add(new SpriteIcon { Icon = icon.Value, Size = new Vector2(12), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft });

                if (text != null)
                {
                    label.Add(textSprite = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = text,
                        Font = OsuFont.GetFont(size: 14, weight: isActive ? FontWeight.Bold : FontWeight.SemiBold),
                    });
                }

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = isActive ? colours.Highlight1 : colours.Background2,
                    },
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.1f),
                        Alpha = 0,
                    },
                    iconOnly
                        ? label
                        : new Container
                        {
                            AutoSizeAxes = Axes.X,
                            RelativeSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Horizontal = 18 },
                            Child = label,
                        },
                };
            }

            /// <summary>
            /// Set for profile chips: they can be dragged between each other to reorder the profiles.
            /// </summary>
            public Guid? ProfileId { get; init; }

            public Action<Drawable, Vector2>? Dragging { get; init; }
            public Action? DragEnded { get; init; }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (ProfileId == null || e.Button != osuTK.Input.MouseButton.Left)
                    return false;

                this.ScaleTo(1.06f, 150, Easing.OutQuint).FadeTo(0.8f, 150);
                return true;
            }

            protected override void OnDrag(DragEvent e) => Dragging?.Invoke(this, e.ScreenSpaceMousePosition);

            protected override void OnDragEnd(DragEndEvent e)
            {
                this.ScaleTo(1, 200, Easing.OutQuint).FadeTo(1, 200);
                DragEnded?.Invoke();
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

        #endregion

        #region Details

        /// <summary>
        /// PP / local ranking with the PP graph, then one compact row with grades, play time and judgements;
        /// the statistics box on the right (like the player profile).
        /// </summary>
        private partial class DetailSection : CompositeDrawable
        {
            private readonly LocalProfile profile;
            private readonly LocalProfileManager manager;
            private readonly LocalProfileStatistics? stats;
            private readonly int? localRank;
            private readonly RulesetInfo? ruleset;

            public DetailSection(LocalProfile profile, LocalProfileManager manager, LocalProfileStatistics? stats, int? localRank, RulesetInfo? ruleset)
            {
                this.profile = profile;
                this.manager = manager;
                this.stats = stats;
                this.localRank = localRank;
                this.ruleset = ruleset;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours, OsuColour osuColour)
            {
                var plays = stats?.Plays ?? new LocalPlayStatistics();
                var time = TimeSpan.FromSeconds(plays.PlayTimeSeconds);
                bool mania = ruleset?.ShortName == @"mania";

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 15 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 12),
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(40, 0),
                                            Children = new Drawable[]
                                            {
                                                // Like the website's "Global Ranking" / "Country Ranking".
                                                bigValue(@"Estimated Global Ranking",
                                                    stats?.EstimatedGlobalRank != null ? $@"#{stats.EstimatedGlobalRank:N0}" : @"-",
                                                    ruleset?.ShortName == @"osu"
                                                        ? @"Offline estimate from a sampled PP-to-rank curve (osu! standard). Not an official rank."
                                                        : @"Only estimated for osu! standard."),
                                                bigValue(@"Local Ranking", localRank != null ? $@"#{localRank}" : @"-"),
                                            },
                                        },
                                        new LocalPerformanceGraph(stats?.History)
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 80,
                                        },
                                        // Like the website's profile: info (play time, judgements) on the left, achieved grades on the right.
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension(),
                                                new Dimension(GridSizeMode.AutoSize),
                                            },
                                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Anchor = Anchor.BottomLeft,
                                                        Origin = Anchor.BottomLeft,
                                                        Direction = FillDirection.Full,
                                                        Spacing = new Vector2(30, 8),
                                                        Children = new Drawable[]
                                                        {
                                                            // Like the website's "Medals / pp / Total Play Time" row (right-click pp for the ranking options).
                                                            new PerformanceValue(profile, manager,
                                                                value(@"pp", stats == null ? @"…" : $@"{stats.PP:N0}", performanceTooltip())),
                                                            value(@"Total Play Time", $@"{(int)time.TotalHours:N0}h {time.Minutes}m"),
                                                            // Judgement titles in their judgement's colour, like the numbers on the score rows.
                                                            judgementValue(@"300", mania ? plays.Count(HitResult.Perfect) + plays.Count(HitResult.Great) : plays.Count(HitResult.Great), osuColour.ForHitResult(HitResult.Great)),
                                                            judgementValue(@"100", plays.Count(HitResult.Ok) + plays.Count(HitResult.Good), osuColour.ForHitResult(HitResult.Ok)),
                                                            judgementValue(@"50", plays.Count(HitResult.Meh), osuColour.ForHitResult(HitResult.Meh)),
                                                            judgementValue(@"Miss", plays.Count(HitResult.Miss), osuColour.ForHitResult(HitResult.Miss)),
                                                        },
                                                    },
                                                    new FillFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                        Anchor = Anchor.BottomRight,
                                                        Origin = Anchor.BottomRight,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(6, 0),
                                                        Margin = new MarginPadding { Left = 20 },
                                                        Children = new Drawable[]
                                                        {
                                                            grade(ScoreRank.XH, stats?.CountSSH),
                                                            grade(ScoreRank.X, stats?.CountSS),
                                                            grade(ScoreRank.SH, stats?.CountSH),
                                                            grade(ScoreRank.S, stats?.CountS),
                                                            grade(ScoreRank.A, stats?.CountA),
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    Width = 2,
                                    Colour = colours.Background6,
                                    Margin = new MarginPadding { Horizontal = 15 },
                                },
                                new StatisticsBox(stats, manager.ShowTotalAccuracy.Value)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                            },
                        },
                    },
                };
            }

            private string performanceTooltip()
            {
                string maps = profile.CountAllAsRanked || manager.CountEverythingAsRanked.Value ? @"every beatmap" : @"ranked / approved beatmaps";
                string relax = profile.IncludeRelaxAutopilot ? @", including Relax / Autopilot" : string.Empty;
                return $@"Best score per beatmap on {maps}{relax}, weighted like the website. Right-click for options.";
            }

            private static Drawable bigValue(string title, string content, string? tooltip = null)
            {
                var display = new ProfileValueDisplay(true)
                {
                    Title = title,
                };
                display.Content.Text = content;
                display.Content.TooltipText = tooltip ?? string.Empty;
                return display;
            }

            /// <summary>
            /// Play time / judgement value: a little bigger than the profile's small values, to sit next to the grade counts.
            /// </summary>
            private static Drawable value(string title, string content, string? tooltip = null, Color4? titleColour = null) => new CompactValue(title, content, tooltip, titleColour);

            /// <summary>
            /// Judgement count, shortened from 10,000 on (417,473 shows as 417.4k, the exact count in the tooltip) so the row stays on one line.
            /// </summary>
            private static Drawable judgementValue(string title, long count, Color4 titleColour)
            {
                if (count < 10_000)
                    return value(title, count.ToString(@"N0"), null, titleColour);

                // Truncated rather than rounded, so the shown value never exceeds the real count.
                string shortened = count < 10_000_000
                    ? $@"{Math.Floor(count / 100d) / 10:N1}k"
                    : $@"{Math.Floor(count / 100_000d) / 10:N1}M";

                return value(title, shortened, count.ToString(@"N0"), titleColour);
            }

            private static Drawable grade(ScoreRank rank, int? count) => new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    // DrawableRank fills its parent, so it needs a fixed-size container inside auto-sized flows.
                    new Container
                    {
                        Size = new Vector2(50, 25),
                        Child = new DrawableRank(rank),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = count?.ToString(@"N0") ?? @"-",
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                    },
                },
            };
        }

        /// <summary>
        /// Wraps the Performance value; right-click toggles the profile's ranking options.
        /// </summary>
        private partial class PerformanceValue : Container, IHasContextMenu
        {
            private readonly LocalProfile profile;
            private readonly LocalProfileManager manager;

            public PerformanceValue(LocalProfile profile, LocalProfileManager manager, Drawable content)
            {
                this.profile = profile;
                this.manager = manager;

                AutoSizeAxes = Axes.Both;
                Child = content;
            }

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new ToggleMenuItem(@"Include Relax / Autopilot PP", MenuItemType.Standard, v => manager.SetRankingOptions(profile.Id, includeRelaxAutopilot: v))
                {
                    State = { Value = profile.IncludeRelaxAutopilot },
                },
                new ToggleMenuItem(manager.CountEverythingAsRanked.Value
                        ? @"Count every play as ranked (on for every profile in Settings)"
                        : @"Count every play as ranked (this profile only)", MenuItemType.Standard, v => manager.SetRankingOptions(profile.Id, countAllAsRanked: v))
                {
                    State = { Value = profile.CountAllAsRanked },
                },
            };
        }

        private partial class CompactValue : CompositeDrawable, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            private readonly string title;
            private readonly string content;
            private readonly Color4? titleColour;

            public CompactValue(string title, string content, string? tooltip, Color4? titleColour = null)
            {
                this.title = title;
                this.content = content;
                this.titleColour = titleColour;
                TooltipText = tooltip ?? string.Empty;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = title,
                            Font = OsuFont.GetFont(size: 16, weight: titleColour != null ? FontWeight.Bold : FontWeight.Regular),
                            Colour = titleColour ?? colours.Content1,
                        },
                        new OsuSpriteText
                        {
                            Text = content,
                            Font = OsuFont.GetFont(size: 26, weight: FontWeight.Light),
                            Colour = colours.Content2,
                        },
                    },
                };
            }
        }

        /// <summary>
        /// Total PP over time (one point per score that raised it), like the PP history in Ritsu.
        /// </summary>
        private partial class LocalPerformanceGraph : UserGraph<DateTimeOffset, double>
        {
            private readonly OsuSpriteText placeholder;

            public LocalPerformanceGraph(IReadOnlyList<(DateTimeOffset Date, double PP)>? history)
            {
                Add(placeholder = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = @"PP history appears after a couple of PP scores",
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
                });

                Data = history?.Select(h => new KeyValuePair<DateTimeOffset, double>(h.Date, h.PP)).ToArray();
            }

            protected override float GetDataPointHeight(double value) => (float)value;

            protected override void ShowGraph()
            {
                base.ShowGraph();
                placeholder.FadeOut(FADE_DURATION, Easing.Out);
            }

            protected override void HideGraph()
            {
                base.HideGraph();
                placeholder.FadeIn(FADE_DURATION, Easing.Out);
            }

            protected override UserGraphTooltipContent GetTooltipContent(DateTimeOffset date, double pp) => new UserGraphTooltipContent
            {
                Name = @"Performance",
                Count = $@"{pp:N0}pp",
                Time = date.ToLocalTime().ToString(@"yyyy-MM-dd HH:mm"),
            };
        }

        private partial class StatisticsBox : CompositeDrawable
        {
            private readonly LocalProfileStatistics? stats;
            private readonly bool showTotalAccuracy;

            public StatisticsBox(LocalProfileStatistics? stats, bool showTotalAccuracy)
            {
                this.stats = stats;
                this.showTotalAccuracy = showTotalAccuracy;
                AutoSizeAxes = Axes.Both;
                CornerRadius = 6;
                Masking = true;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours, OsuConfigManager config)
            {
                var plays = stats?.Plays ?? new LocalPlayStatistics();
                var scoringMode = config.Get<ScoringMode>(OsuSetting.ScoreDisplayMode);

                var entryList = new List<(string Label, string Value)>
                {
                    (@"Ranked Score", stats?.GetRankedScore(scoringMode).ToString(@"N0") ?? @"-"),
                    (@"Hit Accuracy", stats?.Accuracy.FormatAccuracy().ToString() ?? @"-"),
                    (@"Play Count", plays.PlayCount.ToString(@"N0")),
                    (@"Total Score", stats?.GetTotalScore(scoringMode).ToString(@"N0") ?? @"-"),
                    (@"Total Hits", plays.TotalHits.ToString(@"N0")),
                    (@"Hits Per Play", (plays.PlayCount > 0 ? plays.TotalHits / plays.PlayCount : 0).ToString(@"N0")),
                    (@"Maximum Combo", stats?.MaxCombo.ToString(@"N0") ?? @"-"),
                    (@"Submitted Plays", stats?.SavedScoreCount.ToString(@"N0") ?? @"-"),
                };

                // Settings → Ritsu "Calculate and show total hit accuracy".
                if (showTotalAccuracy)
                    entryList.Insert(2, (@"Total Accuracy", stats?.TotalAccuracy?.FormatAccuracy().ToString() ?? @"-"));

                var entries = entryList.ToArray();

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background4,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(20, 0),
                        Padding = new MarginPadding { Vertical = 18, Horizontal = 12 },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Children = entries.Select(e => (Drawable)new ExtendedDetails.ExtendedDetailsEntryLabel(colours) { Text = e.Label }).ToArray(),
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Children = entries.Select(e => (Drawable)new ExtendedDetails.ExtendedDetailsEntryValue(colours) { Text = e.Value }).ToArray(),
                            },
                        },
                    },
                };
            }
        }

        #endregion

        #region Scores

        /// <summary>
        /// The "ranks" section: best performance and recent plays, styled like the player profile's sections.
        /// Each list starts compact (5 scores) and grows to 50 / 100 / 150 / 200 with "show more", like the website.
        /// </summary>
        private partial class ScoresPanel : ProfileSection
        {
            public override LocalisableString Title => @"Scores";

            public override string Identifier => @"scores";

            public const int COMPACT_COUNT = 5;

            private static readonly int[] expansion_steps = { COMPACT_COUNT, 50, 100, 150, 200 };

            private readonly LocalProfileStatistics stats;
            private readonly Action<ScoreInfo> present;
            private readonly Func<ScoreInfo, MenuItem[]> menu;
            private readonly IDictionary<string, int> expandedLists;

            private FillFlowContainer bestFlow = null!;
            private FillFlowContainer recentFlow = null!;

            public ScoresPanel(LocalProfileStatistics stats, Action<ScoreInfo> present, Func<ScoreInfo, MenuItem[]> menu, IDictionary<string, int> expandedLists)
            {
                this.stats = stats;
                this.present = present;
                this.menu = menu;
                this.expandedLists = expandedLists;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // In the section's title row, on the right: scores without PP.
                if (stats.UncalculatedScores > 0)
                {
                    AddInternal(new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding { Top = 22, Right = WaveOverlayContainer.HORIZONTAL_PADDING - 10 },
                        Text = $@"{stats.UncalculatedScores:N0} score(s) have no PP because their beatmap is missing or can't be calculated.",
                        Font = OsuFont.GetFont(size: 12),
                        Alpha = 0.7f,
                    });
                }

                // The game's own profile section (title with underline, rounded background), like the website profile.
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new SubsectionHeader(@"Best Performance", stats.BestScores.Count),
                            bestFlow = createList(),
                            new SubsectionHeader(@"Recent Plays", stats.SavedScoreCount)
                            {
                                Margin = new MarginPadding { Top = 15 },
                            },
                            recentFlow = createList(),
                        },
                    },
                };

                fill(bestFlow, stats.BestScores.ToArray(), @"No PP scores yet.", @"best");
                fill(recentFlow, stats.RecentScores.ToArray(), @"No submitted plays yet on this profile.", @"recent");
            }

            private static FillFlowContainer createList() => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            [Resolved]
            private RealmAccess realm { get; set; } = null!;

            /// <summary>
            /// Shows the list as far as it was expanded last time (see <see cref="LocalProfileOverlay.expandedLists"/>).
            /// </summary>
            private void fill(FillFlowContainer flow, LocalScoreEntry[] scores, string emptyText, string listName) =>
                fill(flow, scores, emptyText, listName, Math.Clamp(expandedLists.TryGetValue(listName, out int step) ? step : 0, 0, expansion_steps.Length - 1));

            private void fill(FillFlowContainer flow, LocalScoreEntry[] scores, string emptyText, string listName, int step)
            {
                flow.Clear();

                if (scores.Length == 0)
                {
                    flow.Add(empty(emptyText));
                    return;
                }

                int shown = Math.Min(scores.Length, expansion_steps[step]);

                // The first rows were loaded with the statistics; "show more" loads the next ones here.
                LocalScoreEntry.Load(scores.Take(shown), realm);

                foreach (var s in scores.Take(shown))
                {
                    var score = s.Score;

                    if (score == null)
                        continue;

                    flow.Add(new ScoreRow(score, s.PP, s.Weight, () => present(score))
                    {
                        ContextMenuItems = menu(score),
                        NoPerformanceReason = s.NoPerformanceReason,
                    });
                }

                if (shown < scores.Length && step + 1 < expansion_steps.Length)
                {
                    flow.Add(new ShowMoreButton
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Margin = new MarginPadding { Top = 6 },
                        Action = () =>
                        {
                            expandedLists[listName] = step + 1;
                            fill(flow, scores, emptyText, listName, step + 1);
                        },
                    });
                }
            }

            private static Drawable empty(string text) => new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.GetFont(size: 14),
                Alpha = 0.7f,
                Margin = new MarginPadding { Left = 10, Vertical = 6 },
            };
        }

        /// <summary>
        /// "Best Performance" / "Recent Plays": like the player profile's subsection header (accent bar, title, counter), with a larger title.
        /// </summary>
        private partial class SubsectionHeader : CompositeDrawable
        {
            private readonly string text;
            private readonly int count;

            public SubsectionHeader(string text, int count)
            {
                this.text = text;
                this.count = count;

                AutoSizeAxes = Axes.Both;
                Padding = new MarginPadding { Vertical = 10 };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                InternalChildren = new Drawable[]
                {
                    new CircularContainer
                    {
                        RelativeSizeAxes = Axes.Y,
                        Height = 0.75f,
                        Width = 3,
                        Masking = true,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 10 },
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Highlight1,
                        },
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = text,
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            },
                            new CounterPill
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Current = { Value = count },
                            },
                        },
                    },
                };
            }
        }

        /// <summary>
        /// A score row laid out like the website profile's: rank, "title by artist", difficulty and time ago,
        /// mods, accuracy (with weighted PP for best scores), and the PP in a box on the right.
        /// Click: results over song select. Right-click: options. Hover: combo and hit counts.
        /// </summary>
        private partial class ScoreRow : OsuClickableContainer, IHasContextMenu, IHasCustomTooltip<ScoreRowTooltipContent>
        {
            private const float row_height = 40;
            private const float performance_width = 100;
            private const float performance_background_shear = 0.45f;

            private readonly ScoreInfo score;
            private readonly double? pp;
            private readonly double? weight;

            public MenuItem[]? ContextMenuItems { get; init; }

            /// <summary>
            /// Set for scores that give no PP on the profile: the row is faded (its PP like an unranked play's PP on the results screen).
            /// </summary>
            public string? NoPerformanceReason { get; init; }

            /// <summary>
            /// The faded look of scores that don't count for PP.
            /// </summary>
            private const float non_counting_content_alpha = 0.7f;

            private const float non_counting_performance_alpha = 0.5f;

            private ScoreRowTooltipContent? tooltipContent;

            // Everything shown goes through the content: one tooltip is reused for every row.
            public ScoreRowTooltipContent TooltipContent => tooltipContent ??= new ScoreRowTooltipContent(score, NoPerformanceReason);

            public ITooltip<ScoreRowTooltipContent> GetCustomTooltip() => new ScoreRowTooltip();

            public ScoreRow(ScoreInfo score, double? pp, double? weight, Action onClick)
            {
                this.score = score;
                this.pp = pp;
                this.weight = weight;
                Action = onClick;

                RelativeSizeAxes = Axes.X;
                Height = row_height;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours, OsuColour osuColour)
            {
                var beatmap = score.BeatmapInfo;

                bool counts = NoPerformanceReason == null;

                Child = new RowBackground
                {
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = counts ? 1 : non_counting_content_alpha,
                            Padding = new MarginPadding { Left = 20, Right = performance_width },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10, 0),
                                    Children = new Drawable[]
                                    {
                                        new Container
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Size = new Vector2(50, 20),
                                            Child = new DrawableRank(score.Rank),
                                        },
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2),
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Children = beatmap != null
                                                        ? new Drawable[]
                                                        {
                                                            new OsuSpriteText
                                                            {
                                                                Anchor = Anchor.BottomLeft,
                                                                Origin = Anchor.BottomLeft,
                                                                Text = new RomanisableString(beatmap.Metadata.TitleUnicode, beatmap.Metadata.Title),
                                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold, italics: true),
                                                            },
                                                            new OsuSpriteText
                                                            {
                                                                Anchor = Anchor.BottomLeft,
                                                                Origin = Anchor.BottomLeft,
                                                                Text = @" by ",
                                                                Font = OsuFont.GetFont(size: 12, italics: true),
                                                            },
                                                            new OsuSpriteText
                                                            {
                                                                Anchor = Anchor.BottomLeft,
                                                                Origin = Anchor.BottomLeft,
                                                                Text = new RomanisableString(beatmap.Metadata.ArtistUnicode, beatmap.Metadata.Artist),
                                                                Font = OsuFont.GetFont(size: 12, italics: true),
                                                            },
                                                        }
                                                        : new Drawable[]
                                                        {
                                                            new OsuSpriteText
                                                            {
                                                                Text = @"(beatmap not found)",
                                                                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold, italics: true),
                                                            },
                                                        },
                                                },
                                                new FillFlowContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(15, 0),
                                                    Children = new Drawable[]
                                                    {
                                                        new OsuSpriteText
                                                        {
                                                            Text = beatmap?.DifficultyName ?? string.Empty,
                                                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
                                                            Colour = osuColour.Yellow,
                                                        },
                                                        new DrawableDate(score.Date, 12)
                                                        {
                                                            Colour = colours.Foreground1,
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(15),
                                    Children = new Drawable[]
                                    {
                                        new Container
                                        {
                                            AutoSizeAxes = Axes.X,
                                            RelativeSizeAxes = Axes.Y,
                                            Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Child = createRightContent(osuColour),
                                        },
                                        new ModsDisplay(score.Mods)
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                        },
                                    },
                                },
                            },
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = performance_width,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    RelativeSizeAxes = Axes.Both,
                                    Height = 0.5f,
                                    Colour = colours.Background3,
                                    Shear = new Vector2(-performance_background_shear, 0),
                                    EdgeSmoothness = new Vector2(2, 0),
                                },
                                new Box
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    RelativeSizeAxes = Axes.Both,
                                    RelativePositionAxes = Axes.Y,
                                    Height = -0.5f,
                                    Position = new Vector2(0, 1),
                                    Colour = colours.Background3,
                                    Shear = new Vector2(performance_background_shear, 0),
                                    EdgeSmoothness = new Vector2(2, 0),
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Vertical = 5, Left = 30, Right = 20 },
                                    Child = createPerformance(colours).With(d =>
                                    {
                                        d.Anchor = Anchor.Centre;
                                        d.Origin = Anchor.Centre;
                                        d.Alpha = counts ? 1 : non_counting_performance_alpha;
                                    }),
                                },
                            },
                        },
                    },
                };
            }

            /// <summary>
            /// The website row background (hover colour), but it doesn't keep the hover to itself:
            /// the row behind it must count as hovered for its right-click menu to be found.
            /// </summary>
            private partial class RowBackground : ProfileItemContainer
            {
                protected override bool OnHover(HoverEvent e)
                {
                    base.OnHover(e);
                    return false;
                }
            }

            /// <summary>
            /// Accuracy (99% and above brighter), plus the weighted PP and weight for best scores.
            /// </summary>
            private Drawable createRightContent(OsuColour osuColour)
            {
                var accuracy = new Container
                {
                    Width = 65,
                    Height = 16,
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = score.Accuracy.FormatAccuracy(),
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold, italics: true),
                        Colour = score.Accuracy >= 0.99 ? osuColour.YellowLight : osuColour.Yellow.Darken(0.04f),
                    },
                };

                if (weight == null || pp == null)
                {
                    accuracy.Anchor = Anchor.CentreLeft;
                    accuracy.Origin = Anchor.CentreLeft;
                    return new Container { AutoSizeAxes = Axes.X, RelativeSizeAxes = Axes.Y, Child = accuracy };
                }

                return new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(10, 0),
                            Children = new Drawable[]
                            {
                                accuracy,
                                new Container
                                {
                                    Size = new Vector2(50, 14),
                                    Child = new OsuSpriteText
                                    {
                                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold, italics: true),
                                        Text = $@"{pp.Value * weight.Value:N0}pp",
                                    },
                                },
                            },
                        },
                        new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 12),
                            Text = $@"weighted {weight.Value:0%}",
                        },
                    },
                };
            }

            /// <summary>
            /// "356pp" (with a small "pp") in the box on the right, or "-" without PP.
            /// </summary>
            private Drawable createPerformance(OverlayColourProvider colours)
            {
                var font = OsuFont.GetFont(weight: FontWeight.Bold);

                if (pp == null)
                {
                    return new OsuSpriteText
                    {
                        Text = @"-",
                        Font = font,
                        Colour = colours.Highlight1,
                    };
                }

                return new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Font = font,
                            Text = $@"{pp.Value:N0}",
                            Colour = colours.Highlight1,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Font = font.With(size: 12),
                            Text = @"pp",
                            Colour = colours.Light3,
                        },
                    },
                };
            }
        }

        /// <summary>
        /// Hover text of a score row, styled like the Play History graph's tooltip, one thing per line:
        /// combo ("Full Combo" highlighted), judgements in their colours, and why the score gives no PP (if it doesn't).
        /// </summary>
        private record ScoreRowTooltipContent(ScoreInfo Score, string? NoPerformanceReason);

        private partial class ScoreRowTooltip : VisibilityContainer, ITooltip<ScoreRowTooltipContent>
        {
            private readonly FillFlowContainer lines;
            private readonly Box background;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public ScoreRowTooltip()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 10;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    lines = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Padding = new MarginPadding(10),
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Same background as the graph tooltips.
                background.Colour = colours.Gray1;
            }

            private ScoreRowTooltipContent? shown;

            public void SetContent(ScoreRowTooltipContent content)
            {
                if (ReferenceEquals(content, shown))
                    return;

                shown = content;
                lines.Clear();

                var score = content.Score;
                int maxCombo = score.GetMaximumAchievableCombo();

                // Gold: the whole combo, slider ends included. Green: nothing broke the combo, only slider ends were missed.
                bool perfectCombo = maxCombo > 0 && score.MaxCombo >= maxCombo;
                bool noComboBreaks = maxCombo > 0
                                     && score.Statistics.GetValueOrDefault(HitResult.Miss) == 0
                                     && score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss) == 0
                                     && score.Statistics.GetValueOrDefault(HitResult.ComboBreak) == 0;

                Color4? comboColour = perfectCombo ? colours.Yellow : noComboBreaks ? colours.Lime1 : null;

                var combo = line();
                combo.Add(text(@"Combo", bold: true));
                combo.Add(text(maxCombo > 0 ? $@"{score.MaxCombo:N0}x / {maxCombo:N0}x" : $@"{score.MaxCombo:N0}x", bold: comboColour != null, colour: comboColour));
                lines.Add(combo);

                var judgements = line();

                foreach (var statistic in score.GetStatisticsForDisplay().Where(st => st.Result.IsBasic()))
                {
                    judgements.Add(text(statistic.DisplayName.ToString(), bold: true, colour: colours.ForHitResult(statistic.Result)));
                    judgements.Add(text($@"{statistic.Count:N0}"));
                }

                lines.Add(judgements);

                if (content.NoPerformanceReason != null)
                    lines.Add(text(content.NoPerformanceReason, colour: colours.Red1));
            }

            private static FillFlowContainer line() => new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
            };

            private static OsuSpriteText text(string content, bool bold = false, Color4? colour = null) => new OsuSpriteText
            {
                Text = content,
                Font = OsuFont.GetFont(size: 12, weight: bold ? FontWeight.Bold : FontWeight.Regular),
                Colour = colour ?? Color4.White,
            };

            private bool instantMove = true;

            public void Move(Vector2 pos)
            {
                if (instantMove)
                {
                    Position = pos;
                    instantMove = false;
                }
                else
                    this.MoveTo(pos, 200, Easing.OutQuint);
            }

            protected override void PopIn()
            {
                instantMove |= !IsPresent;
                this.FadeIn(200, Easing.OutQuint);
            }

            protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);
        }

        /// <summary>
        /// The score's mods as icons. Each icon sits in its own hover cell that also covers half of the gap on either side,
        /// so the tooltip always shows only the mod closest to the cursor (never the whole combination).
        /// </summary>
        private partial class ModsDisplay : FillFlowContainer
        {
            private const float icon_scale = 0.35f;
            private const float gap = 2;

            public ModsDisplay(IReadOnlyList<Mod> mods)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;

                foreach (var mod in mods)
                    Add(new ModCell(mod));
            }

            private partial class ModCell : Container, IHasCustomTooltip<Mod>
            {
                [Resolved]
                private OverlayColourProvider? colourProvider { get; set; }

                public Mod? TooltipContent { get; }

                public ModCell(Mod mod)
                {
                    TooltipContent = mod;
                    AutoSizeAxes = Axes.Both;
                    Padding = new MarginPadding { Horizontal = gap / 2 };

                    // The icon's own tooltip is off, so only this cell (and therefore only this mod) answers the hover.
                    Child = new ModIcon(mod, showTooltip: false, showExtendedInformation: false) { Scale = new Vector2(icon_scale) };
                }

                public ITooltip<Mod> GetCustomTooltip() => new ModTooltip(colourProvider);

                // Claim the hover, so nothing behind the icons shows its own tooltip.
                protected override bool OnHover(HoverEvent e) => true;
            }
        }

        #endregion
    }
}
