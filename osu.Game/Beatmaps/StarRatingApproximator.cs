// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Utils;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Provides a fast heuristic approximation of mod-adjusted star rating,
    /// for use when an exact difficulty calculation is not yet available.
    /// </summary>
    public static class StarRatingApproximator
    {
        /// <summary>
        /// Determines whether a given mod alters beatmap difficulty / star rating.
        /// </summary>
        public static bool ModAffectsDifficulty(Mod mod) =>
            mod is IApplicableToDifficulty || mod is IApplicableToRate || mod is ModFlashlight || mod is ModHidden
            || mod is ModRelax || mod is ModTouchDevice || mod is IAffectsDifficulty;

        /// <summary>
        /// Computes a fast O(1) approximation of star rating when difficulty-altering mods are applied
        /// and the exact calculation is not yet available in the cache.
        /// </summary>
        public static double Approximate(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod> mods)
        {
            double rate = ModUtils.CalculateRateWithMods(mods);
            var flattenedMods = ModUtils.FlattenMods(mods).ToArray();
            return Approximate(beatmapInfo, rulesetInfo, rate, flattenedMods);
        }

        // overload used by carousel lookups to avoid re-flattening mods per beatmap
        public static double Approximate(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, double rate, IReadOnlyList<Mod> flattenedMods)
        {
            double baseStars = beatmapInfo.StarRating;
            if (baseStars <= 0)
                return baseStars;

            string rulesetName = rulesetInfo?.ShortName ?? beatmapInfo.Ruleset?.ShortName ?? @"osu";
            double multiplier = 1.0;

            switch (rulesetName)
            {
                case "taiko":
                    if (Math.Abs(rate - 1.0) > 0.001)
                    {
                        double exp = rate > 1.0 ? 0.726 : 0.85;
                        multiplier *= Math.Pow(rate, exp);
                    }

                    foreach (var mod in flattenedMods)
                    {
                        switch (mod)
                        {
                            case ModHardRock:
                                multiplier *= 1.05;
                                break;

                            case ModEasy:
                                multiplier *= 0.90;
                                break;
                        }
                    }
                    break;

                case "fruits":
                    if (Math.Abs(rate - 1.0) > 0.001)
                    {
                        double exp = rate > 1.0 ? 0.602 : 0.75;
                        multiplier *= Math.Pow(rate, exp);
                    }

                    foreach (var mod in flattenedMods)
                    {
                        switch (mod)
                        {
                            case ModHardRock:
                                multiplier *= 1.10;
                                break;

                            case ModEasy:
                                multiplier *= 0.85;
                                break;
                        }
                    }
                    break;

                case "mania":
                    if (Math.Abs(rate - 1.0) > 0.001)
                    {
                        double exp = rate > 1.0 ? 0.430 : 0.55;
                        multiplier *= Math.Pow(rate, exp);
                    }
                    break;

                case "osu":
                default:
                    var diff = beatmapInfo.Difficulty;
                    double cs = diff?.CircleSize ?? 4f;
                    double approachRate = diff?.ApproachRate ?? IBeatmapDifficultyInfo.DEFAULT_DIFFICULTY;

                    // log(rate) on a speed-up, 0 otherwise. Lowered Difficulty Adjust approach rate, Relax and Freeze Frame don't multiply with a speed-up
                    // in the calculator, so below their factors are raised to a power fitted on DT 1.2-2.0 (a power of 1 at or below rate 1).
                    double speedUp = rate - 1.0 > 0.001 ? Math.Log(rate) : 0;

                    // Clock rate scaling
                    if (Math.Abs(rate - 1.0) > 0.001)
                    {
                        if (rate > 1.0)
                        {
                            // In Torii pp-dev, star rating is driven by the norm of Aim, Speed, and Reading skills.
                            // The effective clock rate scaling exponent falls linearly from 0.57 at 0★ to 0.45 at 1.5★,
                            // then rises with base star rating towards 0.97 (~0.71 at 4★, ~0.85 at 6.6★, ~0.89 at 8★, ~0.94 at 12★).
                            double rateExp = baseStars <= 1.5
                                ? 0.45 + 0.12 * Math.Max(0, 1.5 - baseStars) / 1.5
                                : 0.45 + 0.52 * (1.0 - Math.Exp(-0.28 * (baseStars - 1.5)));

                            multiplier *= Math.Pow(rate, rateExp + speedUpExponentCorrection(rate, approachRate, flattenedMods));
                        }
                        else
                        {
                            // HT scaling (rate < 1.0): drops faster for high star maps due to collapsing speed/reading strains
                            double rateExp = 0.58 + 0.35 * Math.Clamp((baseStars - 1.5) / 7.0, 0.0, 1.0);
                            multiplier *= Math.Pow(rate, rateExp + rateExponentCorrection(rate));
                        }
                    }

                    // Additional mod multipliers
                    foreach (var mod in flattenedMods)
                    {
                        switch (mod)
                        {
                            case ModHardRock:
                            {
                                double hrFactor = 1.05 + 0.008 * Math.Min(8.0, baseStars) + 0.01 * Math.Max(0, cs - 5.0);
                                multiplier *= hrFactor;
                                break;
                            }

                            case ModEasy:
                            {
                                if (diff != null && diff.ApproachRate >= 7.0f)
                                {
                                    // In Torii pp-dev, EZ halves AR. On higher-AR and higher-star maps, dense overlapping objects
                                    // produce heavy reading strain (raising star rating), whereas low-AR/starter maps see a difficulty reduction.
                                    double ar = diff.ApproachRate;
                                    double ezFactor = 0.85 + 0.03 * Math.Clamp(baseStars - 2.0, 0.0, 5.0) + 0.025 * (ar - 7.0);
                                    multiplier *= ezFactor;
                                }
                                else
                                {
                                    multiplier *= 0.85;
                                }
                                break;
                            }

                            case ModDifficultyAdjust da:
                            {
                                if (diff != null)
                                {
                                    if (tryGetDifficultySetting(da, "circle_size", out float csVal))
                                        multiplier *= difficultyAdjustCircleSizeFactor(diff.CircleSize, csVal);
                                    if (tryGetDifficultySetting(da, "approach_rate", out float arVal))
                                    {
                                        double arFactor = difficultyAdjustApproachRateFactor(diff.ApproachRate, arVal, baseStars);

                                        // A speed-up already shortens the preempt, so lowering approach rate adds less reading difficulty.
                                        if (arVal < diff.ApproachRate)
                                            arFactor = Math.Pow(arFactor, 1 - 1.3320 * speedUp);

                                        multiplier *= arFactor;
                                    }
                                    if (tryGetDifficultySetting(da, "overall_difficulty", out float odVal))
                                        multiplier *= difficultyAdjustOverallDifficultyFactor(diff.OverallDifficulty, odVal);
                                }
                                break;
                            }

                            case ModFlashlight:
                            {
                                multiplier *= 1.15;
                                break;
                            }

                            case ModHidden:
                                multiplier *= 1.035;
                                break;

                            case ModRelax:
                                // Under a speed-up Relax removes less on low star rating beatmaps and more on high ones.
                                multiplier *= Math.Pow(skillReductionFactor(baseStars, -0.2248, -0.0638), 1 - (0.4396 - 0.5380 * Math.Log(baseStars)) * speedUp);
                                break;

                            case ModTouchDevice:
                                multiplier *= skillReductionFactor(baseStars, -0.1183, -0.0137);
                                break;

                            case IAffectsDifficulty:
                            {
                                double factor = osuAffectsDifficultyFactor(mod, baseStars);

                                // Under a speed-up Freeze Frame adds less, and above about AR 9 at high rates it lowers star rating, as in the calculator.
                                if (mod.Acronym == @"FR")
                                    factor = Math.Pow(factor, 1 - (0.5326 + 0.5044 * Math.Max(0, approachRate - 7)) * speedUp);

                                multiplier *= factor;
                                break;
                            }
                        }
                    }
                    break;
            }

            return baseStars * multiplier;
        }

        // The clock rate corrections below were fitted to the osu! difficulty calculator, on log(exact / approximate) star rating
        // across DT 1.1-2.0 and HT 0.5-0.95 on a sample of real beatmaps, and checked on a second library.
        // Without them the exponent above under-estimated speed-ups at every approach rate, by about 5-8% at DT 1.5 and 15-20% at DT 2.0.

        /// <summary>
        /// Correction to the clock rate exponent that only depends on the rate.
        /// </summary>
        private static double rateExponentCorrection(double rate) => 0.0955 + 0.1383 * (rate - 1);

        /// <summary>
        /// Correction to the speed-up exponent. On top of <see cref="rateExponentCorrection"/>, reading difficulty grows faster under a speed-up
        /// above AR 9, using the approach rate after Hard Rock, Easy or Difficulty Adjust.
        /// Easy's own factor doesn't compose with a speed-up (too low on low-AR beatmaps, too high on high-AR ones), so it gets its own term.
        /// </summary>
        private static double speedUpExponentCorrection(double rate, double approachRate, IReadOnlyList<Mod> mods)
        {
            double adjustedApproachRate = approachRate;
            bool easy = false;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ModHardRock:
                        adjustedApproachRate = Math.Min(10, approachRate * 1.4);
                        break;

                    case ModEasy:
                        adjustedApproachRate = approachRate * 0.5;
                        easy = true;
                        break;
                }
            }

            // Difficulty Adjust overrides the approach rate.
            foreach (var mod in mods)
            {
                if (mod is ModDifficultyAdjust da && tryGetDifficultySetting(da, "approach_rate", out float daApproachRate))
                    adjustedApproachRate = daApproachRate;
            }

            double highApproachRate = Math.Max(0, adjustedApproachRate - 9);
            double correction = rateExponentCorrection(rate) + highApproachRate * highApproachRate * (-0.0455 + 0.2468 * (rate - 1));

            if (easy)
                correction += 0.2223 - 0.1823 * Math.Max(0, approachRate - 7);

            return correction;
        }

        // The Relax, Autopilot, Touch Device, Magnetised and Freeze Frame factors below were fitted to the osu! difficulty calculator,
        // on log(exact / nomod) star rating against log(nomod star rating) across a sample of real beatmaps.
        // They only depend on star rating, so they never change the nomod order.

        /// <summary>
        /// Autopilot, Magnetised and Freeze Frame are osu!-only mods, so they're matched by acronym.
        /// </summary>
        private static double osuAffectsDifficultyFactor(Mod mod, double baseStars)
        {
            switch (mod.Acronym)
            {
                case @"AP":
                    return skillReductionFactor(baseStars, -0.9283, 0.1139);

                case @"MG":
                    // The calculator scales aim, reading and flashlight by (1 - attraction strength).
                    float strength = mod.SettingsMap.TryGetValue(@"attraction_strength", out var setting) && setting is IBindable<float> bindable ? bindable.Value : 0.5f;
                    return skillReductionFactor(baseStars, -0.6129, 0.1114, strength);

                case @"FR":
                    // Freeze Frame changes when objects appear, which never lowered star rating in the measured sample.
                    return Math.Max(1, Math.Exp(0.2774 - 0.1044 * Math.Log(baseStars)));

                default:
                    return 1;
            }
        }

        /// <summary>
        /// Relax, Autopilot, Touch Device and Magnetised only scale skills down in the calculator, so they never raise star rating.
        /// </summary>
        private static double skillReductionFactor(double baseStars, double a, double b, double scale = 1)
            => Math.Min(1, Math.Exp(scale * (a + b * Math.Log(baseStars))));

        private static bool tryGetDifficultySetting(ModDifficultyAdjust mod, string key, out float value)
        {
            value = 0;

            if (!mod.SettingsMap.TryGetValue(key, out var setting) || setting is not IBindable<float?> bindable || bindable.Value is not float v)
                return false;

            value = v;
            return true;
        }

        // The Difficulty Adjust factors below were fitted to the osu! difficulty calculator, on log(exact / nomod) star rating
        // across a sample of real beatmaps with each setting swept over its extended range.
        // Raising CS and lowering AR use a piecewise-linear slope: the calculator's response starts slowly (small circle bonus,
        // note density threshold) and then steepens, so a single slope over-estimated the common range (CS 5-7, AR 5-8) to fit the extremes.

        private static readonly double[] circle_size_knots = { 0.15, 0.4, 0.8 };
        private static readonly double[] circle_size_slopes = { 0.4934, 0.6807, 0.8167, 0.674 };
        private static readonly double[] approach_rate_knots = { 0.25, 0.5, 0.9 };
        private static readonly double[] approach_rate_slopes = { 0.126, 0.2574, 0.3386, 0.223 };

        /// <summary>
        /// Circle size scales aim distances through the object radius.
        /// </summary>
        private static double difficultyAdjustCircleSizeFactor(double baseCircleSize, double circleSize)
        {
            double l = Math.Log(osuRadius(baseCircleSize) / osuRadius(circleSize));
            return Math.Exp(l > 0 ? piecewiseLinear(l, circle_size_knots, circle_size_slopes) : 0.419 * l);
        }

        /// <summary>
        /// Approach rate changes how long objects are visible for. Lowering it raises reading difficulty, most on dense high-AR maps.
        /// Raising it barely changes star rating until the preempt drops below 450ms (above AR 10).
        /// </summary>
        private static double difficultyAdjustApproachRateFactor(double baseApproachRate, double approachRate, double baseStars)
        {
            double basePreempt = osuPreempt(baseApproachRate);
            double preempt = osuPreempt(approachRate);

            double l = Math.Log(preempt / basePreempt);
            double longer = Math.Max(l, 0);
            double shorter = Math.Min(l, 0);
            double belowMinimumPreempt = (Math.Max(0, 450 - preempt) - Math.Max(0, 450 - basePreempt)) / 150;

            return Math.Exp(piecewiseLinear(longer, approach_rate_knots, approach_rate_slopes)
                            + longer * (0.0144 * Math.Log(baseStars) - 0.0336 * (baseApproachRate - 8))
                            + 0.0272 * shorter
                            + belowMinimumPreempt * (0.1412 + 0.2979 * (10 - baseApproachRate) / 10));
        }

        /// <summary>
        /// Piecewise-linear function of <paramref name="x"/> ≥ 0, with slope <c>slopes[i]</c> between <c>knots[i - 1]</c> and <c>knots[i]</c>.
        /// Monotonic while every slope is positive.
        /// </summary>
        private static double piecewiseLinear(double x, double[] knots, double[] slopes)
        {
            double sum = 0;
            double previous = 0;

            for (int i = 0; i < slopes.Length; i++)
            {
                double knot = i < knots.Length ? knots[i] : double.PositiveInfinity;
                sum += slopes[i] * (Math.Min(x, knot) - previous);

                if (x <= knot)
                    break;

                previous = knot;
            }

            return sum;
        }

        /// <summary>
        /// Overall difficulty scales the reading and aim ratings.
        /// </summary>
        private static double difficultyAdjustOverallDifficultyFactor(double baseOverallDifficulty, double overallDifficulty)
        {
            double l = Math.Log(readingOverallDifficultyMultiplier(overallDifficulty) / readingOverallDifficultyMultiplier(baseOverallDifficulty));
            return Math.Exp(l > 0 ? 0.0709 * l : 0.137 * l);
        }

        private static double osuRadius(double circleSize) => 64 * LegacyRulesetExtensions.CalculateScaleFromCircleSize((float)circleSize);

        private static double osuPreempt(double approachRate) => IBeatmapDifficultyInfo.DifficultyRange(approachRate, 1800, 1200, 450);

        private static double readingOverallDifficultyMultiplier(double overallDifficulty) => 0.75 + Math.Pow(Math.Max(0, overallDifficulty), 2.2) / 800;
    }
}
