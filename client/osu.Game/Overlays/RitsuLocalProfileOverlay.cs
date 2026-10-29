// Adapted from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
// See LICENCE-Modosu.txt in the distribution.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Offline;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    /// <summary>Modosu's local profile overlay backed by Ritsu's profile IDs and shared local scores.</summary>
    public partial class RitsuLocalProfileOverlay : FullscreenOverlay<RitsuLocalProfileHeader>
    {
        [Resolved]
        private RitsuProfileManager profiles { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> currentRuleset { get; set; } = null!;

        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();
        private readonly ProfileSectionsContainer sections;
        private readonly ProfileSectionTabControl tabs;
        private readonly RitsuLocalScoresSection scores;
        private readonly LoadingLayer loading;
        private ProfileSection? lastSection;
        private int refreshVersion;

        public RitsuLocalProfileOverlay()
            : base(OverlayColourScheme.Pink)
        {
            sections = new ProfileSectionsContainer
            {
                RelativeSizeAxes = Axes.Both,
                ExpandableHeader = Header,
                FixedHeader = tabs = new ProfileSectionTabControl
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                },
                HeaderBackground = new Box { RelativeSizeAxes = Axes.Both, Colour = ColourProvider.Background5 },
            };

            base.Content.AddRange(new Drawable[]
            {
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new OsuContextMenuContainer { RelativeSizeAxes = Axes.Both, Child = sections },
                },
                loading = new LoadingLayer(true),
            });

            sections.SelectedSection.ValueChanged += e =>
            {
                if (lastSection == e.NewValue) return;
                lastSection = e.NewValue;
                if (lastSection != null) tabs.Current.Value = lastSection;
            };
            tabs.Current.ValueChanged += e =>
            {
                if (lastSection == null)
                {
                    lastSection = sections.Children.FirstOrDefault();
                    if (lastSection != null) tabs.Current.Value = lastSection;
                    return;
                }
                if (e.NewValue == null || lastSection == e.NewValue) return;
                lastSection = e.NewValue;
                sections.ScrollTo(lastSection);
            };

            sections.Add(scores = new RitsuLocalScoresSection());
            tabs.AddItem(scores);
        }

        public override bool IsPresent => base.IsPresent || Scheduler.HasPendingTasks;

        protected override RitsuLocalProfileHeader CreateHeader() => new RitsuLocalProfileHeader();
        protected override Color4 BackgroundColour => ColourProvider.Background5;

        [BackgroundDependencyLoader]
        private void load()
        {
            ((IBindable<RulesetInfo>)ruleset).BindTo(currentRuleset);
            ruleset.BindValueChanged(_ => Schedule(() => { if (State.Value == Visibility.Visible) refresh(); }));
            profiles.Changed += onProfilesChanged;
            State.BindValueChanged(e =>
            {
                if (e.NewValue == Visibility.Visible)
                    Schedule(refresh);
                else
                    ++refreshVersion;
            });
        }

        private void onProfilesChanged() => Schedule(() => { if (State.Value == Visibility.Visible) refresh(); });

        private void refresh()
        {
            int version = ++refreshVersion;
            Guid id = profiles.ActiveProfileId;
            string name = profiles.ActiveProfileName;
            RulesetInfo mode = ruleset.Value.Clone();
            var allProfiles = profiles.GetProfiles();

            Header.User.Value = new UserProfileData(new APIUser { Id = APIUser.SYSTEM_USER_ID, Username = name }, mode);
            Header.RefreshProfiles();
            Header.SetHistory(Array.Empty<RitsuPpPoint>());
            Header.SetLocalRank(null);
            scores.User.Value = Header.User.Value;
            scores.SetScores(Array.Empty<ScoreInfo>(), Array.Empty<ScoreInfo>());
            scores.SetError(null);
            loading.Show();

            Task.Run(() =>
            {
                var statistics = profiles.GetStatistics(id, mode.ShortName);
                var ppByProfile = new Dictionary<Guid, double> { [id] = statistics.PP };
                foreach (var profile in allProfiles.Where(p => p.Id != id))
                {
                    try { ppByProfile[profile.Id] = profiles.GetStatistics(profile.Id, mode.ShortName).PP; }
                    catch (Exception ex) { Logger.Error(ex, "Could not load a local profile's PP for the profile selector."); }
                }
                int? localRank = statistics.BestScores.Count > 0
                    ? 1 + ppByProfile.Count(pair => pair.Key != id && pair.Value > statistics.PP)
                    : null;
                return (Statistics: statistics, PPByProfile: ppByProfile, LocalRank: localRank, Display: createDisplayStatistics(statistics));
            }).ContinueWith(task => Schedule(() =>
            {
                // Observe failures even if a newer refresh has superseded this one.
                if (task.IsFaulted && task.Exception != null)
                    Logger.Error(task.Exception.GetBaseException(), "Could not load local profile statistics.");
                if (IsDisposed || version != refreshVersion) return;
                if (task.IsCanceled || task.IsFaulted)
                {
                    scores.SetError("Could not load local statistics. See the local runtime log for details.");
                    loading.Hide();
                    return;
                }

                var result = task.GetResultSafely();
                var user = new APIUser { Id = APIUser.SYSTEM_USER_ID, Username = name, Statistics = result.Display };
                Header.User.Value = new UserProfileData(user, mode);
                Header.RefreshProfiles(result.PPByProfile);
                Header.SetHistory(result.Statistics.History);
                Header.SetLocalRank(result.LocalRank);
                scores.User.Value = Header.User.Value;
                scores.SetScores(result.Statistics.RecentScores, result.Statistics.BestScores);
                loading.Hide();
            }));
        }

        private static UserStatistics createDisplayStatistics(RitsuProfileStatistics stats)
        {
            var scores = stats.RecentScores;
            long hits = scores.Sum(s => s.Statistics.Where(h => h.Key.IsHit()).Sum(h => (long)h.Value));
            return new UserStatistics
            {
                IsRanked = false,
                PP = (decimal)stats.PP,
                GlobalRank = stats.EstimatedGlobalRank,
                Accuracy = stats.Accuracy,
                PlayCount = stats.PlayCount,
                TotalScore = scores.Sum(s => s.TotalScore),
                TotalHits = (int)Math.Min(int.MaxValue, hits),
                MaxCombo = scores.Select(s => s.MaxCombo).DefaultIfEmpty().Max(),
                GradesCount = new UserStatistics.Grades
                {
                    SSPlus = stats.BestScores.Count(s => s.Rank == ScoreRank.XH),
                    SS = stats.BestScores.Count(s => s.Rank == ScoreRank.X),
                    SPlus = stats.BestScores.Count(s => s.Rank == ScoreRank.SH),
                    S = stats.BestScores.Count(s => s.Rank == ScoreRank.S),
                    A = stats.BestScores.Count(s => s.Rank == ScoreRank.A),
                },
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (profiles != null) profiles.Changed -= onProfilesChanged;
                ruleset.UnbindBindings();
            }
            base.Dispose(isDisposing);
        }

        private partial class ProfileSectionTabControl : OsuTabControl<ProfileSection>
        {
            public ProfileSectionTabControl()
            {
                Height = 40;
                Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING };
                TabContainer.Spacing = new Vector2(20);
            }

            protected override TabItem<ProfileSection> CreateTabItem(ProfileSection value) => new ProfileSectionTabItem(value);
            protected override bool OnClick(ClickEvent e) => true;
            protected override bool OnHover(HoverEvent e) => true;

            private partial class ProfileSectionTabItem : TabItem<ProfileSection>
            {
                [Resolved]
                private OverlayColourProvider colours { get; set; } = null!;
                private OsuSpriteText text = null!;

                public ProfileSectionTabItem(ProfileSection value) : base(value) { }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AutoSizeAxes = Axes.Both;
                    Anchor = Anchor.CentreLeft;
                    Origin = Anchor.CentreLeft;
                    InternalChild = text = new OsuSpriteText { Text = Value.Title };
                    updateState();
                }

                protected override void OnActivated() => updateState();
                protected override void OnDeactivated() => updateState();
                protected override bool OnHover(HoverEvent e) { updateState(); return true; }
                protected override void OnHoverLost(HoverLostEvent e) => updateState();

                private void updateState()
                {
                    if (text == null || colours == null) return;
                    text.Font = OsuFont.Default.With(size: 14, weight: Active.Value ? FontWeight.SemiBold : FontWeight.Regular);
                    text.FadeColour(IsHovered ? colours.Light1 : Active.Value ? colours.Content1 : colours.Light2, 300, Easing.OutQuint);
                }
            }
        }

        private partial class ProfileSectionsContainer : SectionsContainer<ProfileSection>
        {
            private OverlayScrollContainer scroll = null!;
            protected override UserTrackingScrollContainer CreateScrollContainer() => scroll = new OverlayScrollContainer();
            protected override FlowContainer<ProfileSection> CreateScrollContentContainer() => new ReverseChildIDFillFlowContainer<ProfileSection>
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding { Horizontal = 10 },
                Margin = new MarginPadding { Bottom = 10 },
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();
                AddInternal(scroll.Button.CreateProxy());
            }
        }
    }
}
