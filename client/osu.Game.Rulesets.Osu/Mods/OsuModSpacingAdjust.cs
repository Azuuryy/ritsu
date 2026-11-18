// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Graphics;
using osu.Framework.Graphics.Sprites;
using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Modosu.Utils;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModSpacingAdjust : Mod, IApplicableToBeatmap, IModosuMod, IBeatmapVariant
    {
        [SettingSource("Transformation version", HideInSettings = true)]
        public BindableInt TransformationVersion { get; } = new BindableInt();

        public override bool Ranked => false;
        public override bool AlwaysValidForSubmission => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;

        public override string Name => "Spacing Adjust";

        public override LocalisableString Description => "Adjust object spacing to your liking.";


        public override IconUsage? Icon => OsuIcon.ModSpacingAdjust;
        public override string Acronym => "SA";

        public override ModType Type => ModType.Conversion;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => base.IncompatibleMods
            .Concat(new[] { typeof(OsuModSpacingOvertime) })
            .ToArray();

        private readonly ModosuHitObjectGenerationUtils.OriginalHitObjectStateStore originalState = new();

        [SettingSource("Object spacing", "Modifies the spacing between objects.")]
        public BindableNumber<float> ObjectSpacing { get; } = new BindableFloat
        {
            MinValue = 0.01f,
            MaxValue = 3,
            Default = 1,
            Value = 1,
            Precision = 0.01f,
        };

        [SettingSource("Maximum jump distance", "Maximum distance between jumps; zero disables the limit.")]
        public BindableNumber<float> MaximumJumpDistance { get; } = new BindableFloat
        {
            MinValue = 0,
            MaxValue = 640,
            Default = 0,
            Value = 0,
            Precision = 1,
        };

        public override string ExtendedIconInformation =>
            ObjectSpacing.IsDefault ? string.Empty : FormattableString.Invariant($"{ObjectSpacing.Value:N2}x");

        private const double min_break_duration = 1000;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (!(beatmap is OsuBeatmap osuBeatmap))
                return;

            originalState.Restore(osuBeatmap);

            var positionInfos = ModosuHitObjectGenerationUtils.GeneratePositionInfos(osuBeatmap.HitObjects);

            for (int i = 0; i < positionInfos.Count; i++)
            {
                var positionInfo = positionInfos[i];

                // if (i == 0 || positionInfos[i - 1].HitObject is Spinner ||
                //     positionInfo.HitObject.StartTime - positionInfos[i - 1].HitObject.GetEndTime() > min_break_duration)
                // {
                //     positionInfo.StayInPlace = true;
                // }

                if (ObjectSpacing.Value >= 1)
                {
                    // When increasing jump distance, longer jumps get a smaller increase in distance
                    positionInfo.DistanceFromPrevious *= MathF.Pow(ObjectSpacing.Value, 1f - positionInfo.DistanceFromPrevious / 640f);
                }
                else
                {
                    // When reducing jump distance, shorter jumps get a smaller reduction in distance
                    positionInfo.DistanceFromPrevious *= MathF.Pow(ObjectSpacing.Value, positionInfo.DistanceFromPrevious / 640f);
                }

                if (MaximumJumpDistance.Value > 0 && i > 0 && positionInfo.HitObject is not Spinner && positionInfos[i - 1].HitObject is not Spinner
                    && !isStreamStep(i))
                    positionInfo.DistanceFromPrevious = Math.Min(positionInfo.DistanceFromPrevious, MaximumJumpDistance.Value);
            }

            bool isStreamStep(int index)
            {
                bool fastPair(int left, int right)
                {
                    if (left < 0 || right >= positionInfos.Count || positionInfos[left].HitObject is not HitCircle || positionInfos[right].HitObject is not HitCircle)
                        return false;

                    double interval = positionInfos[right].HitObject.StartTime - positionInfos[left].HitObject.StartTime;
                    return interval > 0 && interval <= beatmap.ControlPointInfo.TimingPointAt(positionInfos[left].HitObject.StartTime).BeatLength / 2;
                }

                return fastPair(index - 1, index) && (fastPair(index - 2, index - 1) || fastPair(index, index + 1));
            }

            osuBeatmap.HitObjects = ModosuHitObjectGenerationUtils.RepositionHitObjectsClampOnly(positionInfos);
        }
    }
}
