// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using System;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModSwing : OsuModBeatmapVariant
    {
        public override string Name => "Swing";
        public override ModType Type => ModType.System;
        public override bool UserPlayable => false;
        public override string Acronym => "SWG";
        public override LocalisableString Description => "Shift straight eighth-note pairs to a swing rhythm.";

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
            PrepareSliderDurations(beatmap);
            var objects = beatmap.HitObjects;
            for (int i = 0; i + 1 < objects.Count; i++)
            {
                if (objects[i] is not HitCircle first || objects[i + 1] is not HitCircle second)
                    continue;

                var timing = beatmap.ControlPointInfo.TimingPointAt(first.StartTime);
                double beatLength = timing.BeatLength;
                double offset = first.StartTime - timing.Time;
                if (Math.Abs(offset - Math.Round(offset / beatLength) * beatLength) > 1 || Math.Abs(second.StartTime - first.StartTime - beatLength / 2) > 1)
                    continue;

                double target = first.StartTime + beatLength * 2 / 3;
                if (beatmap.ControlPointInfo.TimingPointAt(second.StartTime) != timing || beatmap.ControlPointInfo.TimingPointAt(target) != timing
                    || (i > 0 && objects[i - 1].GetEndTime() > first.StartTime)
                    || (i + 2 < objects.Count && objects[i + 2].StartTime <= target))
                    continue;

                second.StartTime = target;
                i++;
            }
        }
    }
}
