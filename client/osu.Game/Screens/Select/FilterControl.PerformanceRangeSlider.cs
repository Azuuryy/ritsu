// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class FilterControl
    {
        private partial class PerformanceRangeSlider : ShearedRangeSlider
        {
            private Container borderContainer = null!;
            private readonly LayoutValue drawSizeLayout = new LayoutValue(Invalidation.DrawSize);

            public PerformanceRangeSlider()
                : base("PP max")
            {
                NubWidth = 54;
                DefaultStringUpperBound = "∞";
                AddLayout(drawSizeLayout);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                SliderContainer.AddRange(new Drawable[]
                {
                    new Container
                    {
                        Depth = 1,
                        RelativeSizeAxes = Axes.Both,
                        Shear = OsuGame.SHEAR,
                        CornerRadius = 5,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex("87C7F4"), Color4Extensions.FromHex("B5ADF0")),
                            },
                        },
                    },
                    borderContainer = new Container
                    {
                        Depth = -1,
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Both,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            BorderColour = colourProvider.Highlight1,
                            BorderThickness = 2,
                            Masking = true,
                            Shear = OsuGame.SHEAR,
                            CornerRadius = 5,
                            Child = new Box { Colour = Color4.Transparent, RelativeSizeAxes = Axes.Both },
                        },
                    },
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                LowerBoundSlider.Current.ValueChanged += _ => updateBorder(false);
                UpperBoundSlider.Current.ValueChanged += _ => updateBorder(false);
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();
                if (!drawSizeLayout.IsValid)
                {
                    updateBorder(true);
                    drawSizeLayout.Validate();
                }
            }

            private void updateBorder(bool instant)
            {
                if (LowerBoundSlider.DrawWidth <= 0 || UpperBoundSlider.DrawWidth <= 0)
                    return;
                float start = LowerBoundSlider.NormalizedValue * LowerBoundSlider.UsableWidth / LowerBoundSlider.DrawWidth;
                float end = (UpperBoundSlider.NormalizedValue * UpperBoundSlider.UsableWidth + UpperBoundSlider.NubWidth) / UpperBoundSlider.DrawWidth;
                borderContainer.MoveToX(start, instant ? 0 : 250, Easing.OutQuint);
                borderContainer.ResizeWidthTo(end - start, instant ? 0 : 250, Easing.OutQuint);
            }

            protected override BoundSliderBar CreateBoundSlider(bool isUpper) => new PerformanceBoundSliderBar(this, isUpper);

            private partial class PerformanceBoundSliderBar : BoundSliderBar
            {
                private readonly bool isUpper;

                protected override bool FocusIndicator => false;

                public PerformanceBoundSliderBar(ShearedRangeSlider slider, bool isUpper)
                    : base(slider, isUpper)
                {
                    this.isUpper = isUpper;
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    if (isUpper)
                    {
                        LeftBox.Colour = Color4.White.Opacity(0.15f);
                        RightBox.Colour = Color4.Black.Opacity(0.65f);
                    }
                    else
                    {
                        LeftBox.Colour = Color4.Black.Opacity(0.65f);
                        RightBox.Colour = Color4.White.Opacity(0.15f);
                    }
                }

                protected override void UpdateDisplay(double value)
                {
                    base.UpdateDisplay(value);
                    if (!Current.IsDefault || !isUpper)
                        NubText.Text = value.ToString("N0");
                }
            }
        }
    }
}
