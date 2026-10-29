// Adapted from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
// See LICENCE-Modosu.txt in the distribution.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    /// <summary>The recent and best score section used by Modosu's local profiles.</summary>
    public partial class RitsuLocalScoresSection : ProfileSection
    {
        public override LocalisableString Title => UsersStrings.ShowExtraTopRanksTitle;
        public override string Identifier => "top_ranks";

        private readonly RitsuScoreSubsection recent;
        private readonly RitsuScoreSubsection best;
        private readonly OsuSpriteText status;

        public RitsuLocalScoresSection()
        {
            Children = new Drawable[]
            {
                status = new OsuSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 14), Alpha = 0 },
                recent = new RitsuScoreSubsection(User, UsersStrings.ShowExtraRecentActivityTitle),
                best = new RitsuScoreSubsection(User, UsersStrings.ShowExtraTopRanksBestTitle),
            };
        }

        public void SetError(string? message)
        {
            status.Text = message ?? string.Empty;
            status.Alpha = message == null ? 0 : 1;
        }

        public void SetScores(IReadOnlyList<ScoreInfo> recentScores, IReadOnlyList<ScoreInfo> bestScores)
        {
            recent.SetScores(recentScores);
            best.SetScores(bestScores);
        }
    }

    /// <summary>Initially shows five scores and reveals more on demand, like Modosu.</summary>
    public partial class RitsuScoreSubsection : ProfileSubsection
    {
        private IReadOnlyList<ScoreInfo> scores = Array.Empty<ScoreInfo>();
        private int shown = 5;
        private bool ready;
        private FillFlowContainer items = null!;
        private ShowMoreButton more = null!;
        private OsuSpriteText missing = null!;

        public RitsuScoreSubsection(Bindable<UserProfileData?> user, LocalisableString header)
            : base(user, header, CounterVisibilityState.AlwaysVisible)
        {
        }

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                items = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 2) },
                more = new ShowMoreButton { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Margin = new MarginPadding { Top = 10 }, Action = () => { shown += 50; refresh(); } },
                missing = new OsuSpriteText { Text = "No local scores yet.", Font = OsuFont.GetFont(size: 14), Margin = new MarginPadding { Vertical = 4 } },
            },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ready = true;
            refresh();
        }

        public void SetScores(IReadOnlyList<ScoreInfo> value)
        {
            scores = value;
            shown = 5;
            if (ready) refresh();
        }

        private void refresh()
        {
            items.Clear();
            foreach (var score in scores.Take(shown))
                items.Add(new RitsuProfileScore(score));
            SetCount(scores.Count);
            missing.Alpha = scores.Count == 0 ? 1 : 0;
            more.Alpha = shown < scores.Count ? 1 : 0;
        }
    }

    /// <summary>A local score row using Modosu's rank, beatmap, accuracy and PP layout.</summary>
    public partial class RitsuProfileScore : OsuClickableContainer
    {
        private const float pp_width = 100;
        private readonly ScoreInfo score;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private RitsuLocalProfileOverlay? overlay { get; set; }

        public RitsuProfileScore(ScoreInfo score)
        {
            this.score = score;
            RelativeSizeAxes = Axes.X;
            Height = 48;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuColour osuColours)
        {
            string title = score.BeatmapInfo?.Metadata?.Title ?? score.BeatmapHash;
            string artist = score.BeatmapInfo?.Metadata?.Artist ?? string.Empty;
            string difficulty = score.BeatmapInfo?.DifficultyName ?? "Unknown";
            Action = () => { overlay?.Hide(); game?.PresentScore(score); };

            Child = new ProfileItemContainer
            {
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 20, Right = pp_width + 10 },
                        Children = new Drawable[]
                        {
                            new UpdateableRank(score.Rank) { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Size = new Vector2(50, 20) },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Left = 60, Right = 200 },
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = $"{title} by {artist}", Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold, italics: true) },
                                    new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = $"{difficulty}  ·  {score.Date.LocalDateTime:g}", Font = OsuFont.GetFont(size: 12), Colour = osuColours.Yellow },
                                },
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Text = $"{score.Accuracy * 100:0.00}%", Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold), Colour = osuColours.Yellow },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(2, 0),
                                        Children = score.Mods.Select(mod => new ModIcon(mod) { Scale = new Vector2(0.35f) }).ToArray(),
                                    },
                                },
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = pp_width,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background3 },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Text = score.PP.HasValue ? $"{score.PP.Value:N0}pp" : "– pp", Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold), Colour = colours.Highlight1 },
                                    new OsuSpriteText { Text = $"{score.TotalScore:N0}", Font = OsuFont.GetFont(size: 11), Colour = colours.Light3 },
                                },
                            },
                        },
                    },
                },
            };
        }
    }
}
