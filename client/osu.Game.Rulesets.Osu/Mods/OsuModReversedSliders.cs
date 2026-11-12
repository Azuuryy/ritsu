// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;
using System;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModReversedSliders : OsuModBeatmapVariant
    {
        public override string Name => "Reversed Sliders";
        public override ModType Type => ModType.DifficultyIncrease;
        public override IconUsage? Icon => OsuIcon.ModReversedSliders;
        public override string Acronym => "RSL";
        public override LocalisableString Description => "Reverse non-repeating sliders.";

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
            var vertices = new List<Vector2>();
            foreach (var slider in beatmap.HitObjects.OfType<Slider>())
            {
                if (slider.RepeatCount != 0 || !slider.Path.HasValidLengthForPlacement)
                    continue;

                double length = slider.Path.Distance;
                slider.Path.GetPathToProgress(vertices, 0, 1);
                Vector2 tail = vertices[^1];
                var reversed = vertices.AsEnumerable().Reverse().Where((position, index) => index == 0 || position != vertices[vertices.Count - index]).Select(position => position - tail).ToArray();
                if (reversed.Length < 2)
                    continue;

                slider.Position += tail;
                slider.Path = new SliderPath(PathType.LINEAR, reversed, length);
                slider.NodeSamples = slider.NodeSamples.Reverse().ToList();
            }
        }
    }
}
