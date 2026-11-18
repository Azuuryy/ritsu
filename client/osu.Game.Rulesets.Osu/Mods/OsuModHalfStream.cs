// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using System.Collections.Generic;
using System;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModHalfStream : OsuModBeatmapVariant
    {
        public override string Name => "Streamline";
        public override ModType Type => ModType.DifficultyReduction;
        public override IconUsage? Icon => OsuIcon.ModStreamline;
        public override string Acronym => "HST";
        public override LocalisableString Description => "Thin out long streams.";

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;
            var kept = new List<OsuHitObject>(objects.Count);
            for (int start = 0; start < objects.Count;)
            {
                int end = start + 1;
                if (objects[start] is HitCircle)
                {
                    var timing = beatmap.ControlPointInfo.TimingPointAt(objects[start].StartTime);
                    while (end < objects.Count && objects[end] is HitCircle && beatmap.ControlPointInfo.TimingPointAt(objects[end].StartTime) == timing &&
                           Math.Abs(objects[end].StartTime - objects[end - 1].StartTime - timing.BeatLength / 4) <= 1)
                        end++;
                }

                for (int i = start; i < end; i++)
                {
                    if (end - start < 7 || i == end - 1 || objects[i].NewCombo || (i - start) % 2 == 0)
                        kept.Add(objects[i]);
                }

                start = end;
            }

            beatmap.HitObjects = kept;
        }
    }
}
