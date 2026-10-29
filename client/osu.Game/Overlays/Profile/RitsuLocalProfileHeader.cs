// Adapted from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
// See LICENCE-Modosu.txt in the distribution.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Offline;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Scoring;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    /// <summary>Local adaptation of Modosu's osu!-style profile header.</summary>
    public partial class RitsuLocalProfileHeader : TabControlOverlayHeader<LocalisableString>
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private RitsuLocalTopHeader top = null!;
        private RitsuLocalDetails details = null!;

        public RitsuLocalProfileHeader()
        {
            ContentSidePadding = WaveOverlayContainer.HORIZONTAL_PADDING;
            TabControl.AddItem("Profile");
        }

        protected override OverlayTitle CreateTitle() => new LocalProfileTitle();

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                top = new RitsuLocalTopHeader { User = { BindTarget = User } },
                details = new RitsuLocalDetails { User = { BindTarget = User } },
            },
        };

        public void RefreshProfiles(IReadOnlyDictionary<Guid, double>? pp = null) => top.RefreshProfiles(pp);

        public void SetHistories(IReadOnlyList<RitsuPpPoint> pp, IReadOnlyList<RitsuRankPoint> ranks) => details.SetHistories(pp, ranks);

        private partial class LocalProfileTitle : OverlayTitle
        {
            public LocalProfileTitle()
            {
                Title = "Local profiles";
                Icon = OsuIcon.Player;
            }
        }
    }

    /// <summary>Modosu's avatar, username and row of profile pills.</summary>
    public partial class RitsuLocalTopHeader : CompositeDrawable
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();
        private readonly RitsuProfileCardRow cards;
        private readonly UpdateableAvatar avatar;
        private readonly OsuSpriteText username;
        private readonly Box background;

        public RitsuLocalTopHeader()
        {
            RelativeSizeAxes = Axes.X;
            Height = 100;
            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                    ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 80), new Dimension() },
                    RowDimensions = new[] { new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            avatar = new UpdateableAvatar(isInteractive: false, showGuestOnNull: true)
                            {
                                Size = new Vector2(65),
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Masking = true,
                                CornerRadius = 20,
                                EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Radius = 3, Colour = Colour4.Black.Opacity(0.25f), Offset = new Vector2(0, 1) },
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Children = new Drawable[]
                                {
                                    username = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 24, weight: FontWeight.Regular) },
                                    cards = new RitsuProfileCardRow(),
                                },
                            },
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours) => background.Colour = colours.Background3;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(e =>
            {
                username.Text = e.NewValue?.User.Username ?? string.Empty;
                avatar.User = e.NewValue?.User;
            }, true);
        }

        public void RefreshProfiles(IReadOnlyDictionary<Guid, double>? pp = null) => cards.Refresh(pp);
    }

    /// <summary>Modosu's main-details and extended-details arrangement, using local-only fields.</summary>
    public partial class RitsuLocalDetails : CompositeDrawable
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();
        private RitsuLocalMainDetails main = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            main = new RitsuLocalMainDetails { RelativeSizeAxes = Axes.X, User = { BindTarget = User } },
                            new Box { RelativeSizeAxes = Axes.Y, Width = 2, Colour = colours.Background6, Margin = new MarginPadding { Horizontal = 15 } },
                            new RitsuLocalExtendedDetails { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, User = { BindTarget = User } },
                        },
                    },
                },
            };
        }

        public void SetHistories(IReadOnlyList<RitsuPpPoint> pp, IReadOnlyList<RitsuRankPoint> ranks) => Schedule(() => main.SetHistories(pp, ranks));
    }

    public partial class RitsuLocalMainDetails : CompositeDrawable
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();
        private readonly Dictionary<ScoreRank, OsuSpriteText> grades = new Dictionary<ScoreRank, OsuSpriteText>();
        private ProfileValueDisplay performance = null!;
        private ProfileValueDisplay localRank = null!;
        private RitsuPpGraph ppGraph = null!;
        private RitsuRankGraph rankGraph = null!;
        private OsuSpriteText graphPlaceholder = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 7),
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        ColumnDimensions = new[] { new Dimension(), new Dimension() },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                performance = new ProfileValueDisplay(true) { Title = "Performance points" },
                                localRank = new ProfileValueDisplay(true) { Title = "Local rank" },
                            },
                        },
                    },
                    new OsuSpriteText { Text = "PP history", Font = OsuFont.GetFont(size: 12) },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 60,
                        Children = new Drawable[]
                        {
                            ppGraph = new RitsuPpGraph { RelativeSizeAxes = Axes.Both },
                            graphPlaceholder = new OsuSpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Text = "Play more scores to display the history", Font = OsuFont.GetFont(size: 12) },
                        },
                    },
                    new OsuSpriteText { Text = "Rank history among local profiles", Font = OsuFont.GetFont(size: 12) },
                    rankGraph = new RitsuRankGraph { RelativeSizeAxes = Axes.X, Height = 48 },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            grade(ScoreRank.XH), grade(ScoreRank.X), grade(ScoreRank.SH), grade(ScoreRank.S), grade(ScoreRank.A),
                        },
                    },
                },
            };
        }

        private Drawable grade(ScoreRank rank) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Y,
            Width = 44,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new DrawableRank(rank) { RelativeSizeAxes = Axes.X, Height = 22 },
                grades[rank] = new OsuSpriteText { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold) },
            },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(e =>
            {
                var stats = e.NewValue?.User.Statistics;
                performance.Content.Text = stats?.PP != null ? $"{stats.PP.Value:N0}pp" : "0pp";
                performance.Content.TooltipText = "PP estimated from scores associated with this local profile.";
                foreach (var entry in grades)
                    entry.Value.Text = (stats?.GradesCount[entry.Key] ?? 0).ToString("N0");
            }, true);
        }

        public void SetHistories(IReadOnlyList<RitsuPpPoint> pp, IReadOnlyList<RitsuRankPoint> ranks) => Schedule(() =>
        {
            localRank.Content.Text = ranks.Count == 0 ? "–" : $"#{ranks[^1].Rank:N0}";
            localRank.Content.TooltipText = "Rank among local profiles for this mode, not an online ranking.";
            // Modosu shows a zero baseline when the profile has just one PP result.
            ppGraph.SetHistory(pp.Count == 1
                ? new[] { new RitsuPpPoint(pp[0].Date.AddDays(-1), 0), pp[0] }
                : pp.TakeLast(180));
            rankGraph.SetHistory(ranks.TakeLast(180));
            graphPlaceholder.Alpha = pp.Count == 0 ? 1 : 0;
        });

        private partial class RitsuPpGraph : UserGraph<DateTimeOffset, double>
        {
            public void SetHistory(IEnumerable<RitsuPpPoint> points) => Data = points.Select(p => new KeyValuePair<DateTimeOffset, double>(p.Date, p.PP)).ToArray();
            protected override float GetDataPointHeight(double value) => (float)value;
            protected override UserGraphTooltipContent GetTooltipContent(DateTimeOffset date, double value) => new UserGraphTooltipContent { Name = "Estimated local PP", Count = $"{value:0.##} pp", Time = date.LocalDateTime.ToString("g") };
        }

        private partial class RitsuRankGraph : UserGraph<DateTimeOffset, int>
        {
            public void SetHistory(IEnumerable<RitsuRankPoint> points) => Data = points.Select(p => new KeyValuePair<DateTimeOffset, int>(p.Date, p.Rank)).ToArray();
            protected override float GetDataPointHeight(int value) => 100 - MathF.Log(value);
            protected override UserGraphTooltipContent GetTooltipContent(DateTimeOffset date, int value) => new UserGraphTooltipContent { Name = "Local rank", Count = $"#{value:N0}", Time = date.LocalDateTime.ToString("g") };
        }
    }

    public partial class RitsuLocalExtendedDetails : CompositeDrawable
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();
        private readonly Dictionary<string, OsuSpriteText> values = new Dictionary<string, OsuSpriteText>();

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            AutoSizeAxes = Axes.Both;
            CornerRadius = 6;
            Masking = true;
            string[] labels = { "Hit accuracy", "Play count", "Total score", "Total hits", "Hits per play", "Maximum combo" };
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
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
                            Children = labels.Select(label => new ExtendedDetails.ExtendedDetailsEntryLabel(colours) { Text = label }).ToArray(),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Children = labels.Select(label => values[label] = new ExtendedDetails.ExtendedDetailsEntryValue(colours)).ToArray(),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(e => update(e.NewValue?.User.Statistics), true);
        }

        private void update(UserStatistics? stats)
        {
            values["Hit accuracy"].Text = $"{stats?.Accuracy ?? 0:0.00}%";
            values["Play count"].Text = $"{stats?.PlayCount ?? 0:N0}";
            values["Total score"].Text = $"{stats?.TotalScore ?? 0:N0}";
            values["Total hits"].Text = $"{stats?.TotalHits ?? 0:N0}";
            values["Hits per play"].Text = $"{(stats?.PlayCount > 0 ? stats.TotalHits / stats.PlayCount : 0):N0}";
            values["Maximum combo"].Text = $"{stats?.MaxCombo ?? 0:N0}";
        }
    }
}
