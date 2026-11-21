// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// A single row that scrolls sideways (mouse wheel or dragging empty space) when its content is wider than the space.
    /// Items fade out towards an edge while there is more to scroll to on that side.
    /// </summary>
    public partial class FadingHorizontalScroll : CompositeDrawable
    {
        private const float fade_width = 60;

        /// <summary>
        /// How far each fade reaches past the row's edge, with a solid strip there: the clipped items' soft (anti-aliased) edge
        /// draws a pixel or two outside the row, which would otherwise show as a thin line next to the fade.
        /// </summary>
        private const float edge_overlap = 4;

        private readonly OsuScrollContainer scroll;
        private readonly Container leftFade;
        private readonly Container rightFade;

        public FadingHorizontalScroll(Drawable content, Color4 background)
        {
            InternalChildren = new Drawable[]
            {
                scroll = new OsuScrollContainer(Direction.Horizontal)
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = content,
                },
                leftFade = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = fade_width + edge_overlap * 2,
                    X = -edge_overlap * 2,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = edge_overlap * 3,
                            Colour = background,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = fade_width,
                            X = edge_overlap * 2,
                            Colour = ColourInfo.GradientHorizontal(background, background.Opacity(0)),
                        },
                    },
                },
                rightFade = new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = fade_width + edge_overlap * 2,
                    X = edge_overlap * 2,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = edge_overlap * 3,
                            Colour = background,
                        },
                        new Box
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = fade_width,
                            X = -edge_overlap * 2,
                            Colour = ColourInfo.GradientHorizontal(background.Opacity(0), background),
                        },
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            bool moreLeft = scroll.Current > 1;
            bool moreRight = scroll.Current < scroll.ScrollableExtent - 1;

            leftFade.Alpha = (float)Interpolation.DampContinuously(leftFade.Alpha, moreLeft ? 1 : 0, 50, Time.Elapsed);
            rightFade.Alpha = (float)Interpolation.DampContinuously(rightFade.Alpha, moreRight ? 1 : 0, 50, Time.Elapsed);
        }
    }

    /// <summary>
    /// Compact "new profile with mod rules" button: a plus and the mods icon from song select's "Mods" button.
    /// </summary>
    public partial class LocalProfileModRulesButton : OsuClickableContainer
    {
        private Box hover = null!;

        public LocalProfileModRulesButton()
        {
            Masking = true;
            TooltipText = @"New local profile with mod rules (opens mod select)";
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background2,
                },
                hover = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White.Opacity(0.1f),
                    Alpha = 0,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Plus,
                            Size = new Vector2(11),
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.ExchangeAlt,
                            Size = new Vector2(14),
                        },
                    },
                },
            };
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
}
