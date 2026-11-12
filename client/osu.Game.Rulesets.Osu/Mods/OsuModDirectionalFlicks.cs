// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Graphics;
using osu.Framework.Graphics.Sprites;
using System;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModDirectionalFlicks : OsuModBeatmapVariant, IApplicableToBeatmap, IWorkInProgressMod
    {
        public override string Name => "Directional Flicks";
        public override ModType Type => ModType.DifficultyIncrease;
        public override IconUsage? Icon => OsuIcon.ModDirectionalFlicks;
        public override string Acronym => "DFK";
        public override LocalisableString Description => "Flick in the arrow's direction.";
        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[]
        {
            typeof(ModAutoplay), typeof(ModRelax), typeof(OsuModAutopilot),
        }).ToArray();

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
        }

        void IApplicableToBeatmap.ApplyToBeatmap(IBeatmap beatmap)
        {
            if (beatmap is not OsuBeatmap osuBeatmap)
                return;

            var objects = osuBeatmap.HitObjects;
            foreach (var (start, end) in GetPhrases(osuBeatmap))
            {
                for (int i = Math.Max(1, start); i < end; i++)
                {
                    var previous = objects[i - 1];
                    if (previous is Spinner || objects[i] is not HitCircle circle)
                        continue;

                    var delta = circle.StackedPosition - previous.StackedEndPosition;
                    double halfBeat = osuBeatmap.ControlPointInfo.TimingPointAt(circle.StartTime).BeatLength / 2;
                    if (delta.Length < circle.Radius * 2 || circle.StartTime - previous.GetEndTime() + 1 < halfBeat)
                        continue;

                    circle.FlickDirection = delta.Normalized();
                    break;
                }
            }
        }
    }
}
