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

        public override bool DisplayResult => Result?.Type == HitResult.Miss;

        protected override OsuSkinComponents CirclePieceComponent => OsuSkinComponents.DecoyCircle;

        protected override Drawable CreateDefaultCirclePiece() => new DecoyMarker(style);

        protected override void OnApply()
        {
            base.OnApply();
            style.Value = ((DecoyCircle)HitObject).Style;
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

            public DecoyMarker(Bindable<DecoyCircleStyle> style)
            {
                this.style = style.GetBoundCopy();
                Size = OsuHitObject.OBJECT_DIMENSIONS * 2;
                Children = new Drawable[]
                {
                    new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.HitCircle), _ => new MainCirclePiece()),
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(42, 7),
                        Rotation = 45,
                    },
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(42, 7),
                        Rotation = -45,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                style.BindValueChanged(value => GrayscaleStrength = value.NewValue == DecoyCircleStyle.Grayscale ? 1 : 0, true);
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
