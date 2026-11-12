// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Judgements;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects
{
    public class HitCircle : OsuHitObject
    {
        public Vector2? FlickDirection { get; set; }

        public override Judgement CreateJudgement() => new OsuJudgement();
    }
}
