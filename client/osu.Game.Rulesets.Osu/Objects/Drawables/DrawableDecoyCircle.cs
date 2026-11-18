// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class DrawableDecoyCircle : DrawableHitCircle
    {
        private readonly Bindable<DecoyCircleStyle> style = new Bindable<DecoyCircleStyle>();
        public readonly Bindable<string> NumberText = new Bindable<string>(string.Empty);

        public override bool DisplayResult => Result?.Type == HitResult.Miss;

        protected override OsuSkinComponents CirclePieceComponent => OsuSkinComponents.DecoyCircle;

        protected override Drawable CreateDefaultCirclePiece() => new DecoyMarker(style, NumberText);

        protected override void OnApply()
        {
            base.OnApply();
            var decoy = (DecoyCircle)HitObject;
            style.Value = decoy.Style;
            NumberText.Value = decoy.Number < 0 ? string.Empty : decoy.Number.ToString();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            if (Result?.Type != HitResult.IgnoreMiss)
            {
                base.UpdateHitStateTransforms(state);
                return;
            }

            this.FadeOut(60);
            Expire();
        }

        private partial class DecoyMarker : BufferedContainer
        {
            private readonly Bindable<DecoyCircleStyle> style;
            private readonly Bindable<string> numberText;
            private readonly Container cross;

            public DecoyMarker(Bindable<DecoyCircleStyle> style, Bindable<string> numberText)
            {
                this.style = style.GetBoundCopy();
                this.numberText = numberText.GetBoundCopy();
                Size = OsuHitObject.OBJECT_DIMENSIONS * 2;
                Children = new Drawable[]
                {
                    new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.HitCircle), _ => new MainCirclePiece()),
                    cross = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(42, 7), Rotation = 45 },
                            new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(42, 7), Rotation = -45 },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                style.BindValueChanged(value => GrayscaleStrength = value.NewValue == DecoyCircleStyle.Grayscale ? 1 : 0, true);
                numberText.BindValueChanged(value => cross.Alpha = string.IsNullOrEmpty(value.NewValue) ? 1 : 0, true);
            }
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows!.CanBeHit(timeOffset))
                    ApplyMinResult();
                return;
            }

            var result = ResultFor(timeOffset);
            if (result != HitResult.None)
                ApplyResult(static (judgement, _) => judgement.Type = HitResult.Miss, 0);
        }
    }
}
