// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Modosu.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public abstract class OsuModBeatmapVariant : Mod, IBeatmapVariant, IApplicableAfterBeatmapConversion, IRitsuExclusiveMod
    {
        public override ModType Type => ModType.Conversion;
        public override bool Ranked => false;
        public override bool AlwaysValidForSubmission => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;

        [SettingSource("Transformation version", HideInSettings = true)]
        public BindableInt TransformationVersion { get; } = new BindableInt();

        private static readonly (Type First, Type Second)[] conflicting_variant_pairs =
        {
            (typeof(OsuModHalfStream), typeof(OsuModEcho)),
            (typeof(OsuModHalfStream), typeof(OsuModSwing)),
            (typeof(OsuModEcho), typeof(OsuModSwing)),
            (typeof(OsuModEcho), typeof(OsuModReversedSliders)),
            (typeof(OsuModPhraseMirror), typeof(OsuModHalfStream)),
            (typeof(OsuModPhraseMirror), typeof(OsuModEcho)),
            (typeof(OsuModPhraseMirror), typeof(OsuModSwing)),
            (typeof(OsuModPhraseMirror), typeof(OsuModReversedSliders)),
            (typeof(OsuModDecoyCircles), typeof(OsuModDirectionalFlicks)),
        };

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[]
        {
            typeof(OsuModCircleGeneration), typeof(OsuModTargetPractice), typeof(OsuModNoSliding),
            typeof(OsuModRandom), typeof(OsuModRandomV2), typeof(OsuModMirror), typeof(OsuModSpacingAdjust), typeof(OsuModSpacingOvertime),
        }).Concat(conflictingVariantTypes()).ToArray();

        private IEnumerable<Type> conflictingVariantTypes()
        {
            foreach (var (first, second) in conflicting_variant_pairs)
            {
                if (first.IsInstanceOfType(this))
                    yield return second;
                else if (second.IsInstanceOfType(this))
                    yield return first;
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (beatmap is OsuBeatmap osuBeatmap)
                ApplyVariant(osuBeatmap);
        }

        protected abstract void ApplyVariant(OsuBeatmap beatmap);

        internal static void PrepareSliderDurations(OsuBeatmap beatmap)
        {
            foreach (var slider in beatmap.HitObjects.OfType<Slider>())
                slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        internal static bool IsIsolatedCircle(OsuBeatmap beatmap, int index)
        {
            var objects = beatmap.HitObjects;
            if (objects[index] is not HitCircle circle)
                return false;

            double halfBeat = beatmap.ControlPointInfo.TimingPointAt(circle.StartTime).BeatLength / 2;
            return (index == 0 || circle.StartTime - objects[index - 1].GetEndTime() + 1 >= halfBeat)
                   && (index == objects.Count - 1 || objects[index + 1].StartTime - circle.StartTime + 1 >= halfBeat);
        }

        internal static Vector2? FindAdjacentPosition(OsuBeatmap beatmap, int index)
        {
            var objects = beatmap.HitObjects;
            Vector2 anchor = objects[index].StackedPosition;
            float radius = OsuHitObject.OBJECT_RADIUS * (float)LegacyRulesetExtensions.CalculateScaleFromCircleSize(0, true);
            float spacing = radius * 2 + 4;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * MathF.PI / 4;
                Vector2 position = anchor + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spacing;
                if (position.X < radius || position.Y < radius || position.X > OsuPlayfield.BASE_SIZE.X - radius || position.Y > OsuPlayfield.BASE_SIZE.Y - radius)
                    continue;
                if (index > 0 && Vector2.Distance(position, objects[index - 1].StackedEndPosition) < spacing)
                    continue;
                if (index + 1 < objects.Count && Vector2.Distance(position, objects[index + 1].StackedPosition) < spacing)
                    continue;

                return position;
            }

            return null;
        }

        internal static IEnumerable<(int Start, int End)> GetPhrases(OsuBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;
            for (int start = 0; start < objects.Count;)
            {
                int end = start + 1;
                if (objects[start] is not Spinner)
                {
                    for (; end < objects.Count && objects[end] is not Spinner; end++)
                    {
                        var current = objects[end];
                        double beatLength = beatmap.ControlPointInfo.TimingPointAt(current.StartTime).BeatLength;
                        double gap = current.StartTime - objects[end - 1].GetEndTime();
                        if (gap >= beatLength * 1.25 || (end - start >= 4 && gap + 1 >= beatLength / 2 &&
                            (current.NewCombo || ModosuHitObjectGenerationUtils.IsHitObjectOnBeat(beatmap, current, true))))
                            break;
                    }

                    yield return (start, end);
                }

                start = end;
            }
        }
    }
}
