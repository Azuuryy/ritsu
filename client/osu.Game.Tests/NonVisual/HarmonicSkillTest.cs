// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class HarmonicSkillTest
    {
        [TestCase(1, 0.9, 0)]
        [TestCase(1, 0.9, 600)]
        [TestCase(20, 0.9, 600)]
        [TestCase(20, 0.9, 12000)]
        [TestCase(5, 0.9, 600)]
        [TestCase(20, 0.75, 600)]
        public void TestDifficultyAndWeightSumMatchOriginalFormula(double scale, double exponent, int count)
        {
            double[] objects = Enumerable.Range(0, count).Select(i => (i * 37 % 2000 - 20) / 64d).ToArray();
            var skill = new TestSkill(scale, exponent, objects);
            double expectedDifficulty = 0;
            double expectedWeightSum = 0;
            int rank = 0;

            foreach (double note in objects.Where(note => note > 0).OrderDescending())
            {
                double weight = (1 + (scale / (1 + rank))) / (Math.Pow(rank, exponent) + 1 + (scale / (1 + rank)));
                expectedWeightSum += weight;
                expectedDifficulty += note * weight;
                rank++;
            }

            Assert.That(BitConverter.DoubleToInt64Bits(skill.DifficultyValue()), Is.EqualTo(BitConverter.DoubleToInt64Bits(expectedDifficulty)));
            Assert.That(BitConverter.DoubleToInt64Bits(skill.WeightSum), Is.EqualTo(BitConverter.DoubleToInt64Bits(expectedWeightSum)));
        }

        private sealed class TestSkill : HarmonicSkill
        {
            protected override double HarmonicScale { get; }
            protected override double DecayExponent { get; }

            public double WeightSum => NoteWeightSum;

            public TestSkill(double scale, double exponent, double[] objects)
                : base([])
            {
                HarmonicScale = scale;
                DecayExponent = exponent;
                ObjectDifficulties.AddRange(objects);
            }

            protected override double ObjectDifficultyOf(DifficultyHitObject current) => throw new NotSupportedException();
        }
    }
}
