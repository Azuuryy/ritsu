// Adapted functionality from Modosu and MOsu; see LICENCE-Modosu.txt.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public class ModosuSpacingTest
    {
        [Test]
        public void SpacingReapplicationDoesNotAccumulate()
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 0, Position = new Vector2(200, 150) });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 500, Position = new Vector2(250, 150) });
            var mod = new OsuModSpacingAdjust();
            mod.ObjectSpacing.Value = 2;
            mod.ApplyToBeatmap(beatmap);
            var first = beatmap.HitObjects.Select(o => o.Position).ToArray();
            mod.ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Select(o => o.Position).ToArray(), Is.EqualTo(first));
        }

        [Test]
        public void SpacingCapLimitsJumpsAndPreservesStreams()
        {
            var beatmap = new OsuBeatmap();
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            double[] times = { 0, 500, 625, 750, 875 };
            float[] positions = { 20, 480, 270, 480, 270 };
            for (int i = 0; i < times.Length; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = times[i], Position = new Vector2(positions[i], 150) });

            var mod = new OsuModSpacingAdjust();
            mod.MaximumJumpDistance.Value = 200;
            mod.ApplyToBeatmap(beatmap);

            Assert.That(Vector2.Distance(beatmap.HitObjects[0].Position, beatmap.HitObjects[1].Position), Is.EqualTo(200).Within(0.1));
            for (int i = 2; i < beatmap.HitObjects.Count; i++)
                Assert.That(Vector2.Distance(beatmap.HitObjects[i - 1].Position, beatmap.HitObjects[i].Position), Is.EqualTo(210).Within(0.1));
        }

        [Test]
        public void FunSpacingAcceptsEmptyBeatmap()
        {
            Assert.DoesNotThrow(() => new OsuModSpacingOvertime().ApplyToBeatmap(new OsuBeatmap()));
        }

        [Test]
        public void ConversionModsAreRegistered()
        {
            var mods = new OsuRuleset().GetModsFor(ModType.Conversion).ToArray();
            Assert.That(mods.Any(m => m is OsuModCircleGeneration), Is.True);
            Assert.That(new OsuRuleset().GetModsFor(ModType.DifficultyIncrease).Any(m => m is OsuModNoSliding), Is.True);
            Assert.That(mods.Any(m => m is OsuModRandomV2), Is.True);
        }

        [Test]
        public void RandomV2IsRepeatableForSameSeed()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 8; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 + i * 250, Position = new Vector2(150 + i * 20, 150 + i % 2 * 30) });
            var mod = new OsuModRandomV2();
            mod.Seed.Value = 123;
            mod.ApplyToBeatmap(beatmap);
            var first = beatmap.HitObjects.Select(o => o.Position).ToArray();
            mod.ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Select(o => o.Position).ToArray(), Is.EqualTo(first));
        }

        [TestCase(240)]
        [TestCase(120)]
        public void RandomV2PatternsKeepRhythmAndPhraseAnchors(int interval)
        {
            var legacy = createPatternBeatmap(interval);
            createPatternMod(0).ApplyToBeatmap(legacy);
            var patterned = createPatternBeatmap(interval);
            var mod = createPatternMod((1 << 9) | (1 << 11));
            var restored = (OsuModRandomV2)new APIMod(mod).ToMod(new OsuRuleset());
            restored.ApplyToBeatmap(patterned);

            Assert.That(restored.PatternMask.Value, Is.EqualTo(mod.PatternMask.Value));
            Assert.That(restored.Seed.Value, Is.EqualTo(mod.Seed.Value));
            Assert.That(patterned.HitObjects.Select(o => o.StartTime), Is.EqualTo(legacy.HitObjects.Select(o => o.StartTime)));
            var shapes = new HashSet<string>();
            for (int first = 0; first < patterned.HitObjects.Count; first += 8)
            {
                int last = first + 7;
                Assert.That(patterned.HitObjects[first].Position, Is.EqualTo(legacy.HitObjects[first].Position));
                Assert.That(patterned.HitObjects[last].Position, Is.EqualTo(legacy.HitObjects[last].Position));
                float exitDistance = Vector2.Distance(legacy.HitObjects[last - 1].Position, legacy.HitObjects[last].Position);
                Assert.That(Vector2.Distance(patterned.HitObjects[last - 1].Position, patterned.HitObjects[last].Position),
                    Is.InRange(exitDistance * 0.65f - 0.01f, exitDistance * (interval == 120 ? 1.08f : 1.20f) + 1.01f));
                if (patterned.HitObjects[first + 1].Position == legacy.HitObjects[first + 1].Position)
                    continue;

                Vector2 firstStep = patterned.HitObjects[first + 1].Position - patterned.HitObjects[first].Position;
                Vector2 secondStep = patterned.HitObjects[first + 2].Position - patterned.HitObjects[first + 1].Position;
                float cosine = Vector2.Dot(firstStep, secondStep) / (firstStep.Length * secondStep.Length);
                shapes.Add(cosine < -0.8f ? "back and forth" : "square");
            }

            Assert.That(shapes, Is.EquivalentTo(new[] { "square", "back and forth" }));
            Assert.That(patterned.HitObjects.All(o => o.Position.X >= 0 && o.Position.X <= 512 && o.Position.Y >= 0 && o.Position.Y <= 384), Is.True);
            var positions = patterned.HitObjects.Select(o => o.Position).ToArray();
            restored.ApplyToBeatmap(patterned);
            Assert.That(patterned.HitObjects.Select(o => o.Position), Is.EqualTo(positions));
        }

        [Test]
        public void RandomV2OctagonUsesFortyFiveDegreeTurns()
        {
            var mod = createPatternMod(0);
            mod.Angle.Value = OsuModRandomV2.AngleEnum.Ninety;
            var legacy = createPatternBeatmap();
            var restored = (OsuModRandomV2)new APIMod(mod).ToMod(new OsuRuleset());
            Assert.That(restored.PatternMask.Value, Is.Zero);
            restored.ApplyToBeatmap(legacy);

            mod.PatternMask.Value = 1 << 14;
            var patterned = createPatternBeatmap();
            restored = (OsuModRandomV2)new APIMod(mod).ToMod(new OsuRuleset());
            Assert.That(restored.Angle.Value, Is.EqualTo(OsuModRandomV2.AngleEnum.Ninety));
            restored.ApplyToBeatmap(patterned);

            var changedPhrases = Enumerable.Range(0, patterned.HitObjects.Count / 8).Select(phrase => phrase * 8)
                                           .Where(first => patterned.HitObjects[first + 1].Position != legacy.HitObjects[first + 1].Position)
                                           .ToArray();
            Assert.That(changedPhrases, Is.Not.Empty);
            foreach (int first in changedPhrases)
            {
                var firstStep = patterned.HitObjects[first + 1].Position - patterned.HitObjects[first].Position;
                var secondStep = patterned.HitObjects[first + 2].Position - patterned.HitObjects[first + 1].Position;
                Assert.That(Vector2.Dot(firstStep, secondStep) / (firstStep.Length * secondStep.Length), Is.EqualTo(0.7071).Within(0.001));
            }
        }

        [Test]
        public void RandomV2SeparateStreamPatternsLeaveOtherPhrasesUntouched()
        {
            OsuBeatmap createMixedBeatmap()
            {
                var beatmap = createPatternBeatmap();
                for (int phrase = 0; phrase < 12; phrase++)
                {
                    int count = phrase % 3 == 2 ? 4 : 8;
                    int interval = phrase % 3 == 0 ? 240 : 120;
                    for (int i = 0; i < count; i++)
                        beatmap.HitObjects[phrase * 8 + i].StartTime = 960 + phrase * 2880 + i * interval;
                }

                beatmap.HitObjects = beatmap.HitObjects.Where((_, i) => i / 8 % 3 != 2 || i % 8 < 4).ToList();
                return beatmap;
            }

            var reference = createMixedBeatmap();
            createPatternMod(0).ApplyToBeatmap(reference);
            var mod = createPatternMod(0);
            mod.SeparatePatterns.Value = true;
            mod.StreamPatterns.Value = 1 << 11;
            mod.MirrorResponses.Value = true;
            mod.ProgressiveSpacing.Value = true;
            var actual = createMixedBeatmap();
            Assert.That(mod.DefaultTransformationVersion, Is.EqualTo(2));
            mod.ApplyToBeatmap(actual);
            int changed = 0;
            for (int i = 0; i < actual.HitObjects.Count; i++)
            {
                int positionInCycle = i % 20;
                if (positionInCycle < 9 || positionInCycle >= 15)
                    Assert.That(actual.HitObjects[i].Position, Is.EqualTo(reference.HitObjects[i].Position));
                else if (actual.HitObjects[i].Position != reference.HitObjects[i].Position)
                    changed++;
            }

            Assert.That(changed, Is.GreaterThan(0));
            var expected = actual.HitObjects.Select(o => o.Position).ToArray();
            mod.ApplyToBeatmap(actual);
            Assert.That(actual.HitObjects.Select(o => o.Position), Is.EqualTo(expected));
        }

        [Test]
        public void RandomV2PatternsHaveNoEffectWithCustomAngleOff()
        {
            var expected = createPatternBeatmap();
            var mod = createPatternMod(0);
            mod.CustomAngle.Value = false;
            mod.ApplyToBeatmap(expected);
            var actual = createPatternBeatmap();
            mod.PatternMask.Value = 0x7fff;
            mod.ApplyToBeatmap(actual);
            Assert.That(actual.HitObjects.Select(o => o.Position), Is.EqualTo(expected.HitObjects.Select(o => o.Position)));
        }

        private static OsuModRandomV2 createPatternMod(int mask) => new OsuModRandomV2
        {
            CustomAngle = { Value = true },
            PatternMask = { Value = mask },
            Seed = { Value = 123 },
        };

        private static OsuBeatmap createPatternBeatmap(int interval = 240)
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 480 });
            for (int phrase = 0; phrase < 12; phrase++)
            {
                for (int i = 0; i < 8; i++)
                {
                    beatmap.HitObjects.Add(new HitCircle
                    {
                        StartTime = 960 + phrase * 2880 + i * interval,
                        Position = new Vector2(100 + i * 42, 155 + (i % 2) * 18),
                        NewCombo = i == 0,
                    });
                }
            }

            return beatmap;
        }

        [Test]
        public void NoSlidingConvertsSliderIntoCircles()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            var slider = new Slider
            {
                StartTime = 1000,
                Position = new Vector2(200, 150),
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(200, 0)) })
            };
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            beatmap.HitObjects.Add(slider);
            new OsuModNoSliding().ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Count, Is.GreaterThan(0));
            Assert.That(beatmap.HitObjects.All(o => o is HitCircle), Is.True);
        }

        [TestCase(OsuModNoSliding.SliderTarget.Short, OsuModNoSliding.SliderConversion.HeadOnly, 2, 1)]
        [TestCase(OsuModNoSliding.SliderTarget.Long, OsuModNoSliding.SliderConversion.HeadAndTail, 2, 2)]
        [TestCase(OsuModNoSliding.SliderTarget.Repeats, OsuModNoSliding.SliderConversion.HeadOnly, 1, 2)]
        public void NoSlidingConvertsOnlySelectedSliders(OsuModNoSliding.SliderTarget target, OsuModNoSliding.SliderConversion conversion, int circles, int sliders)
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            Slider slider(double time, float distance, int repeats = 0) => new Slider
            {
                StartTime = time,
                Position = new Vector2(100, 100),
                RepeatCount = repeats,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(distance, 0)) })
            };

            var shortSlider = slider(1000, 20);
            var longSlider = slider(3000, 200);
            var repeatedSlider = slider(6000, 20, 1);
            beatmap.HitObjects.AddRange(new OsuHitObject[] { shortSlider, longSlider, repeatedSlider });
            foreach (var item in beatmap.HitObjects)
                item.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var mod = new OsuModNoSliding();
            mod.Target.Value = target;
            mod.Conversion.Value = conversion;
            mod.ShortSliderThreshold.Value = 400;
            mod.ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects.OfType<HitCircle>().Count(), Is.EqualTo(circles));
            Assert.That(beatmap.HitObjects.OfType<Slider>().Count(), Is.EqualTo(sliders));
            Assert.That(beatmap.HitObjects, Is.Ordered.By(nameof(OsuHitObject.StartTime)));
        }

        [Test]
        public void NoSlidingSettingsRoundTripAndLegacyDefaults()
        {
            var ruleset = new OsuRuleset();
            var original = new OsuModNoSliding();
            original.Target.Value = OsuModNoSliding.SliderTarget.Short;
            original.Conversion.Value = OsuModNoSliding.SliderConversion.HeadAndTail;
            original.ShortSliderThreshold.Value = 650;

            var restored = (OsuModNoSliding)new APIMod(original).ToMod(ruleset);
            Assert.That(restored.Target.Value, Is.EqualTo(OsuModNoSliding.SliderTarget.Short));
            Assert.That(restored.Conversion.Value, Is.EqualTo(OsuModNoSliding.SliderConversion.HeadAndTail));
            Assert.That(restored.ShortSliderThreshold.Value, Is.EqualTo(650));

            var legacy = (OsuModNoSliding)new APIMod { Acronym = "NL" }.ToMod(ruleset);
            Assert.That(legacy.Target.Value, Is.EqualTo(OsuModNoSliding.SliderTarget.All));
            Assert.That(legacy.Conversion.Value, Is.EqualTo(OsuModNoSliding.SliderConversion.Streams));
        }

        [Test]
        public void CircleGenerationCreatesPracticeObjects()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1000, Position = new Vector2(200, 150) });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 3000, Position = new Vector2(250, 150) });
            new OsuModCircleGeneration().ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Count, Is.GreaterThan(2));
            Assert.That(beatmap.HitObjects.All(o => o is HitCircle), Is.True);
        }
    }
}
