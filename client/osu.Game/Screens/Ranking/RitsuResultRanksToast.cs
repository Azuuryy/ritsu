// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Audio;
using osu.Game.Skinning;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Offline;
using osu.Game.Overlays.ToriiBriefing;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    internal partial class RitsuResultRanksToast : CompositeDrawable
    {
        private readonly double previousPP;
        private readonly double currentPP;
        private readonly OsuSpriteText bestPlayText;
        private readonly OsuSpriteText ppText;
        private readonly OsuSpriteText gainText;
        private double animationStart;
        private readonly bool hasBestPlay;
        private readonly SkinnableSound bestPlaySound;

        public RitsuResultRanksToast(RitsuResultRanks ranks)
        {
            hasBestPlay = ranks.BestPlay.HasValue;
            previousPP = ranks.PreviousPP;
            currentPP = ranks.CurrentPP;
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
            Y = 36;

            InternalChild = new BriefingGlass
            {
                AutoSizeAxes = Axes.Both,
                RelativeContentSize = Axes.None,
                CornerSize = BriefingTheme.CornerMd,
                SurfaceLift = 1.45f,
                SurfaceOpacity = 1.5f,
                ShadowColor = BriefingTheme.AccentAmber,
                ShadowOpacity = 0.4f,
                ShadowRadius = 26,
                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Margin = new MarginPadding { Horizontal = BriefingTheme.SpacingLg, Vertical = BriefingTheme.SpacingMd },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(BriefingTheme.SpacingMd, 0),
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(46),
                                    Children = new Drawable[]
                                    {
                                        new Circle { RelativeSizeAxes = Axes.Both, Colour = BriefingTheme.AccentAmber.Opacity(0.16f) },
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Size = new Vector2(22),
                                            Icon = FontAwesome.Solid.Trophy,
                                            Colour = BriefingTheme.AccentAmber,
                                        },
                                    },
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 3),
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Text = "Score rankings",
                                            Font = OsuFont.Torus.With(size: BriefingTheme.TypeHeadline, weight: FontWeight.SemiBold),
                                            Colour = Color4.White,
                                        },
                                        bestPlayText = highlightedLine(ranks.BestPlay is int best ? $"#{best}  Profile best plays (PP)" : ""),
                                        line($"#{ranks.AllMapScores}  Map · all local scores"),
                                        line($"#{ranks.ProfileMapScores}  Map · this profile"),
                                    },
                                },
                            },
                        },
                        new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding { Top = 7 },
                            Masking = true,
                            CornerRadius = 7,
                            BorderThickness = 1,
                            BorderColour = BriefingTheme.AccentCyan.Opacity(0.35f),
                            Alpha = currentPP > previousPP + 0.0001 ? 1 : 0,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4Extensions.FromHex("#202B42"),
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Margin = new MarginPadding { Horizontal = 9, Vertical = 6 },
                                    Spacing = new Vector2(8, 0),
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = $"{previousPP:N2} pp",
                                            Font = OsuFont.Torus.With(size: BriefingTheme.TypeCaption, weight: FontWeight.SemiBold),
                                            Colour = Color4.White.Opacity(0.68f),
                                        },
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Size = new Vector2(8, 11),
                                            Icon = FontAwesome.Solid.ChevronRight,
                                            Colour = BriefingTheme.AccentCyan.Opacity(0.75f),
                                        },
                                        ppText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.Torus.With(size: BriefingTheme.TypeHeadline, weight: FontWeight.SemiBold),
                                            Colour = BriefingTheme.AccentCyan,
                                        },
                                        new Container
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Masking = true,
                                            CornerRadius = 6,
                                            Children = new Drawable[]
                                            {
                                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("#193E37") },
                                                gainText = new OsuSpriteText
                                                {
                                                    Text = $"+{currentPP - previousPP:N2} pp",
                                                    Margin = new MarginPadding { Horizontal = 7, Vertical = 3 },
                                                    Font = OsuFont.Torus.With(size: BriefingTheme.TypeCaption, weight: FontWeight.SemiBold),
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
            AddInternal(bestPlaySound = new SkinnableSound(new SampleInfo("Gameplay/rank-up")));
        }

        private static OsuSpriteText line(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.Torus.With(size: BriefingTheme.TypeCaption),
            Colour = Color4.White.Opacity(0.78f),
        };

        private static OsuSpriteText highlightedLine(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.Torus.With(size: BriefingTheme.TypeHeadline, weight: FontWeight.SemiBold),
        };

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            bestPlayText.Colour = colours.Green1;
            gainText.Colour = colours.Green1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (hasBestPlay)
                bestPlaySound.Play();
            ppText.Text = $"{previousPP:N2} pp";
            animationStart = Time.Current + 600;
            this.FadeInFromZero(250, Easing.OutQuad);
        }

        protected override void Update()
        {
            base.Update();
            if (currentPP <= previousPP + 0.0001)
                return;
            double progress = Math.Clamp((Time.Current - animationStart) / 1000, 0, 1);
            double eased = 1 - Math.Pow(1 - progress, 3);
            ppText.Text = $"{previousPP + (currentPP - previousPP) * eased:N2} pp";
        }
    }
}
