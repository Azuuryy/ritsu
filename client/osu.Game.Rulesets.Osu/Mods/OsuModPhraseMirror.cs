// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Osu.Utils;
using osuTK;
using System;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModPhraseMirror : OsuModBeatmapVariant
    {
        public override string Name => "Phrase Mirror";
        public override string Acronym => "PMR";
        public override ModType Type => ModType.System;
        public override bool UserPlayable => false;
        public override LocalisableString Description => "Reflect alternating phrases when the entry and exit still fit.";

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
            PrepareSliderDurations(beatmap);
            var objects = beatmap.HitObjects;
            bool mirror = false;
            foreach (var (start, end) in GetPhrases(beatmap))
            {
                if (end - start < 2)
                    continue;

                mirror = !mirror;
                if (mirror)
                    continue;

                if (start > 0 && objects[start - 1] is not Spinner && !fits(objects[start - 1].EndPosition, objects[start].Position))
                    continue;
                if (end < objects.Count && objects[end] is not Spinner && !fits(objects[end].Position, objects[end - 1].EndPosition))
                    continue;

                for (int i = start; i < end; i++)
                    OsuHitObjectGenerationUtils.ReflectHorizontallyAlongPlayfield(objects[i]);
            }
        }

        private static bool fits(Vector2 fixedPosition, Vector2 movingPosition)
        {
            var reflected = new Vector2(OsuPlayfield.BASE_SIZE.X - movingPosition.X, movingPosition.Y);
            return Vector2.Distance(fixedPosition, reflected) <= Vector2.Distance(fixedPosition, movingPosition) * 1.1f + 0.001f;
        }
    }
}
