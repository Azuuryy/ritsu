// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        public override int Version => 20261109;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        public static double CalculateRateAdjustedApproachRate(double approachRate, double clockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(approachRate, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / clockRate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);
        }

        public static double CalculateRateAdjustedOverallDifficulty(double overallDifficulty, double clockRate)
        {
            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(overallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / clockRate;

            return (79.5 - hitWindowGreat) / 6;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            var flashlight = skills.OfType<Flashlight>().SingleOrDefault();
            var reading = skills.OfType<Reading>().Single();
            double clockRate = ModUtils.CalculateRateWithMods(mods);
            var motion = getMotionDifficulty(skills, clockRate);
            double readingDifficultyValue = reading.DifficultyValue();
            double readingDifficultNoteCount = reading.CountTopWeightedObjectDifficulties(readingDifficultyValue);
            double overallDifficulty = CalculateRateAdjustedOverallDifficulty(beatmap.Difficulty.OverallDifficulty, clockRate);

            int hitCircleCount = beatmap.HitObjects.Count(h => h is HitCircle and not DecoyCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            int decoyCount = 0;
            int weightedDecoyCount = 0;
            foreach (var decoy in beatmap.HitObjects.OfType<DecoyCircle>())
            {
                decoyCount++;
                weightedDecoyCount += decoy.Style == DecoyCircleStyle.Grayscale ? 1 : 2;
            }

            int totalHits = beatmap.HitObjects.Count - decoyCount;
            double decoyPerformanceBonus = 0.1 * weightedDecoyCount / (20 + decoyCount);

            double sliderFactor = motion.Aim > 0
                ? OsuRatingCalculator.CalculateDifficultyRating(motion.AimWithoutSliders) / OsuRatingCalculator.CalculateDifficultyRating(motion.Aim)
                : 1;

            var osuRatingCalculator = new OsuRatingCalculator(totalHits, overallDifficulty);

            double aimRating = osuRatingCalculator.ComputeAimRating(motion.Aim);
            double speedRating = osuRatingCalculator.ComputeSpeedRating(motion.Speed);
            double readingRating = osuRatingCalculator.ComputeReadingRating(readingDifficultyValue);

            double flashlightRating = 0.0;

            if (flashlight is not null)
                flashlightRating = osuRatingCalculator.ComputeFlashlightRating(flashlight.DifficultyValue());

            double sliderNestedScorePerObject = LegacyScoreUtils.CalculateNestedScorePerObject(beatmap, totalHits);
            double legacyScoreBaseMultiplier = LegacyScoreUtils.CalculateDifficultyPeppyStars(WorkingBeatmap.Beatmap);

            var simulator = new OsuLegacyScoreSimulator();
            var scoreAttributes = simulator.Simulate(WorkingBeatmap, beatmap);

            double baseAimPerformance = OsuPerformanceCalculator.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = HarmonicSkill.DifficultyToPerformance(speedRating);
            double baseReadingPerformance = HarmonicSkill.DifficultyToPerformance(readingRating);
            double baseFlashlightPerformance = Flashlight.DifficultyToPerformance(flashlightRating);
            double baseCognitionPerformance = SumCognitionDifficulty(baseReadingPerformance, baseFlashlightPerformance);

            double basePerformance = DiffUtils.Norm(OsuPerformanceCalculator.PERFORMANCE_NORM_EXPONENT, baseAimPerformance, baseSpeedPerformance, baseCognitionPerformance);

            double starRating = calculateStarRating(basePerformance * (1 + decoyPerformanceBonus));

            OsuDifficultyAttributes attributes = new OsuDifficultyAttributes
            {
                StarRating = starRating,
                DecoyPerformanceBonus = decoyPerformanceBonus,
                Mods = mods,
                AimDifficulty = aimRating,
                AimDifficultSliderCount = motion.DifficultSliders,
                SpeedDifficulty = speedRating,
                SpeedNoteCount = motion.SpeedNotes,
                FlashlightDifficulty = flashlightRating,
                ReadingDifficulty = readingRating,
                SliderFactor = sliderFactor,
                AimDifficultStrainCount = motion.AimStrains,
                SpeedDifficultStrainCount = motion.SpeedStrains,
                ReadingDifficultNoteCount = readingDifficultNoteCount,
                AimTopWeightedSliderFactor = motion.AimSliderFactor,
                SpeedTopWeightedSliderFactor = motion.SpeedSliderFactor,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = hitCircleCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount,
                NestedScorePerObject = sliderNestedScorePerObject,
                LegacyScoreBaseMultiplier = legacyScoreBaseMultiplier,
                MaximumLegacyComboScore = scoreAttributes.ComboScore
            };

            return attributes;
        }

        public static double SumCognitionDifficulty(double reading, double flashlight)
        {
            if (reading <= 0)
                return flashlight;

            if (flashlight <= 0)
                return reading;

            // Nerf flashlight value in cognition sum when reading is greater than flashlight
            return DiffUtils.Norm(OsuPerformanceCalculator.PERFORMANCE_NORM_EXPONENT, reading, flashlight * Math.Clamp(flashlight / reading, 0.25, 1.0));
        }

        private double calculateStarRating(double basePerformance)
        {
            return Math.Cbrt(basePerformance * OsuPerformanceCalculator.PERFORMANCE_BASE_MULTIPLIER);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            osu.Game.Rulesets.Objects.HitObject? previous = null;
            foreach (var current in beatmap.HitObjects)
            {
                if (current is DecoyCircle)
                    continue;
                if (previous != null)
                    objects.Add(new OsuDifficultyHitObject(current, previous, clockRate, objects, objects.Count));
                previous = current;
            }

            return objects;
        }

        private readonly record struct MotionDifficulty(double Aim, double AimWithoutSliders, double Speed, double AimStrains,
            double SpeedStrains, double SpeedNotes, double AimSliderFactor, double SpeedSliderFactor, double DifficultSliders);

        private sealed class SharedDifficulty
        {
            public readonly ReadingEvaluator.Visibility[] Visibility;
            public MotionDifficulty? Motion;

            public SharedDifficulty(int objectCount)
            {
                Visibility = new ReadingEvaluator.Visibility[objectCount];
            }
        }

        private MotionDifficulty getMotionDifficulty(Skill[] skills, double clockRate)
        {
            SharedDifficulty? shared = SharedSkillResults != null ? (SharedDifficulty)SharedSkillResults[clockRate] : null;
            if (shared?.Motion is MotionDifficulty cached)
                return cached;

            var aim = skills.OfType<Aim>().Single(a => a.IncludeSliders);
            var aimWithoutSliders = skills.OfType<Aim>().Single(a => !a.IncludeSliders);
            var speed = skills.OfType<Speed>().Single();
            double aimDifficulty = aim.DifficultyValue();
            double aimWithoutSlidersDifficulty = aimWithoutSliders.DifficultyValue();
            double speedDifficulty = speed.DifficultyValue();
            double aimStrains = aim.CountTopWeightedStrains(aimDifficulty);
            double speedStrains = speed.CountTopWeightedObjectDifficulties(speedDifficulty);
            double speedNotes = speed.RelevantNoteCount();
            double aimSliderCount = aimWithoutSliders.CountTopWeightedSliders(aimWithoutSlidersDifficulty);
            double aimWithoutSlidersStrains = aimWithoutSliders.CountTopWeightedStrains(aimWithoutSlidersDifficulty);
            double aimSliderFactor = aimSliderCount / Math.Max(1, aimWithoutSlidersStrains - aimSliderCount);
            double speedSliderCount = speed.CountTopWeightedSliders(speedDifficulty);
            double speedSliderFactor = speedSliderCount / Math.Max(1, speedStrains - speedSliderCount);
            var motion = new MotionDifficulty(aimDifficulty, aimWithoutSlidersDifficulty, speedDifficulty, aimStrains,
                speedStrains, speedNotes, aimSliderFactor, speedSliderFactor, aim.GetDifficultSliders());
            if (shared != null)
                shared.Motion = motion;
            return motion;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            SharedDifficulty? shared = null;
            if (SharedSkillResults != null)
            {
                double clockRate = ModUtils.CalculateRateWithMods(mods);
                if (SharedSkillResults.TryGetValue(clockRate, out var cached))
                    shared = (SharedDifficulty)cached;
                else
                {
                    shared = new SharedDifficulty(beatmap.HitObjects.Count);
                    SharedSkillResults.Add(clockRate, shared);
                }
            }

            if (shared?.Motion != null)
                return new Skill[] { new Reading(mods, shared.Visibility) };

            var skills = new List<Skill>
            {
                new Aim(mods, true),
                new Aim(mods, false),
                new Speed(mods),
                new Reading(mods, shared?.Visibility)
            };

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new Flashlight(mods));

            return skills.ToArray();
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new OsuModHidden(),
        };
    }
}
