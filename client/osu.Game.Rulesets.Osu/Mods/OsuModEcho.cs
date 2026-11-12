// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;
using System.Collections.Generic;
using System.Linq;
using osuTK;
using System;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModEcho : OsuModBeatmapVariant
    {
        public override string Name => "Echo";
        public override ModType Type => ModType.System;
        public override bool UserPlayable => false;
        public override string Acronym => "ECH";
        public override LocalisableString Description => "Add a quarter-beat echo after isolated circles.";

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
            PrepareSliderDurations(beatmap);
            var objects = beatmap.HitObjects;
            var echoes = new List<OsuHitObject>();
            for (int i = 0; i < objects.Count; i++)
            {
                if (!IsIsolatedCircle(beatmap, i) || FindAdjacentPosition(beatmap, i) is not Vector2 position)
                    continue;

                var source = objects[i];
                var timing = beatmap.ControlPointInfo.TimingPointAt(source.StartTime);
                double time = source.StartTime + timing.BeatLength / 4;
                if (beatmap.ControlPointInfo.TimingPointAt(time) != timing || (i + 1 < objects.Count && objects[i + 1].StartTime <= time))
                    continue;

                echoes.Add(new HitCircle { StartTime = time, Position = position, Samples = source.Samples.Select(sample => sample.With()).ToList() });
            }

            beatmap.HitObjects = objects.Concat(echoes).OrderBy(o => o.StartTime).ToList();
        }
    }
}
