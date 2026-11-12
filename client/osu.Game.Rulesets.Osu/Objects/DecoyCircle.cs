// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class DecoyCircle : HitCircle
    {
        public DecoyCircleStyle Style { get; init; }

        public override Judgement CreateJudgement() => new DecoyJudgement();

        private class DecoyJudgement : OsuJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;
            public override bool IsValidResult(HitResult result) => result == HitResult.Miss || base.IsValidResult(result);
        }
    }

    public enum DecoyCircleStyle
    {
        [Description("Real Fake")]
        RealFake = 0,

        Grayscale = 1,
    }
}
