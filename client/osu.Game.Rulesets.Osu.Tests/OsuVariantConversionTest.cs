// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Replays;
using osu.Game.Tests.Beatmaps;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public class OsuVariantConversionTest
    {
        [Test]
        public void HalfStreamKeepsEndpointsAndComboStarts()
        {
            var notes = Enumerable.Range(0, 7).Select(i => circle(i * 125, 120 + i * 10)).ToList();
            notes[3].NewCombo = true;
            notes.AddRange(new[] { circle(2000), circle(2125), circle(2250) });
            var map = createBeatmap(notes.ToArray());
            new OsuModHalfStream().ApplyToBeatmap(map);
            Assert.That(map.HitObjects.Select(o => o.StartTime), Is.EqualTo(new[] { 0, 250, 375, 500, 750, 2000, 2125, 2250 }));
        }

        [Test]
        public void EchoAddsAnOffbeatOnlyAfterAnIsolatedCircle()
        {
            var map = createBeatmap(circle(0), circle(500), circle(625), circle(750));
            new OsuModEcho().ApplyToBeatmap(map);
            Assert.That(map.HitObjects.Select(o => o.StartTime), Is.EqualTo(new[] { 0, 125, 500, 625, 750 }));
            Assert.That(Vector2.Distance(map.HitObjects[0].Position, map.HitObjects[1].Position), Is.GreaterThan(64));
        }

        [Test]
        public void SwingMovesTheOffbeatWithoutOvertakingAnotherNote()
        {
            var map = createBeatmap(circle(0), circle(250), circle(500), circle(750), circle(800));
            new OsuModSwing().ApplyToBeatmap(map);
            Assert.That(map.HitObjects.Select(o => o.StartTime), Is.EqualTo(new[] { 0, 1000d / 3, 500, 750, 800 }).Within(0.001));
        }

        [Test]
        public void ReversedSlidersSwapEndpointsAndSamplesWithoutChangingDuration()
        {
            Slider makeSlider(double time, int repeats) => new Slider
            {
                StartTime = time,
                RepeatCount = repeats,
                Position = new Vector2(100, 100),
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0), new Vector2(100, 50) }),
                NodeSamples = new List<IList<HitSampleInfo>>
                {
                    new[] { new HitSampleInfo(HitSampleInfo.HIT_WHISTLE) },
                    new[] { new HitSampleInfo(HitSampleInfo.HIT_CLAP) },
                },
            };
            var slider = makeSlider(0, 0);
            var repeat = makeSlider(2000, 1);
            var map = createBeatmap(slider, repeat);
            slider.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
            double duration = slider.Duration;
            Vector2 tail = slider.EndPosition;
            new OsuModReversedSliders().ApplyToBeatmap(map);
            slider.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
            Assert.That(slider.Position, Is.EqualTo(tail));
            Assert.That(slider.EndPosition, Is.EqualTo(new Vector2(100, 100)));
            Assert.That(slider.Duration, Is.EqualTo(duration).Within(0.001));
            Assert.That(slider.NodeSamples[0][0].Name, Is.EqualTo(HitSampleInfo.HIT_CLAP));
            Assert.That(repeat.Position, Is.EqualTo(new Vector2(100, 100)));
        }

        [TestCase(400, 412)]
        [TestCase(90, 100)]
        public void PhraseMirrorRejectsAnUnsafeEntry(float previousX, float expectedX)
        {
            var map = createBeatmap(circle(0, previousX), circle(250, previousX), circle(1250, 100), circle(1500, 120));
            var ruleset = new OsuRuleset();
            Assert.That(ruleset.GetModsFor(ModType.Conversion), Has.None.InstanceOf<OsuModPhraseMirror>());
            var mod = (OsuModPhraseMirror)new APIMod { Acronym = "PMR" }.ToMod(ruleset);
            mod.ApplyToBeatmap(map);
            Assert.That(map.HitObjects[0].Position.X, Is.EqualTo(previousX));
            Assert.That(map.HitObjects[2].Position.X, Is.EqualTo(expectedX));
        }

        [TestCase(2, 9)]
        [TestCase(8, 3)]
        [TestCase(16, 2)]
        public void DecoySettingsRoundTripAndControlPlacement(int interval, int expectedCount)
        {
            var source = createBeatmap(Enumerable.Range(0, 9).Select(i => circle(i * 1000)).ToArray());
            var selected = new OsuModDecoyCircles { IntervalBeats = { Value = interval }, Style = { Value = DecoyCircleStyle.Grayscale } };
            var restored = (OsuModDecoyCircles)new APIMod(selected).ToMod(new OsuRuleset());
            Assert.That(restored.IntervalBeats.Value, Is.EqualTo(interval));
            ((IApplicableToBeatmap)restored).ApplyToBeatmap(source);

            var decoys = source.HitObjects.OfType<DecoyCircle>().ToArray();
            Assert.That(decoys, Has.Length.EqualTo(expectedCount));
            Assert.That(decoys.Select(decoy => decoy.Style), Is.All.EqualTo(DecoyCircleStyle.Grayscale));
            Assert.That(decoys.Zip(decoys.Skip(1), (left, right) => right.StartTime - left.StartTime), Is.All.GreaterThanOrEqualTo(interval * 500));
        }

        [TestCase(1000, 2, DecoyCircleStyle.Grayscale, 20, 0.05)]
        [TestCase(1000, 2, DecoyCircleStyle.RealFake, 20, 0.10)]
        [TestCase(1000, 8, DecoyCircleStyle.RealFake, 5, 0.04)]
        [TestCase(125, 2, DecoyCircleStyle.RealFake, 0, 0)]
        public void DecoyDifficultyAndPerformanceUseGeneratedCount(int gap, int interval, DecoyCircleStyle style, int expectedCount, double expectedBonus)
        {
            var ruleset = new OsuRuleset();
            var map = createBeatmap(Enumerable.Range(0, 20).Select(i => circle(1000 + i * gap, i % 2 == 0 ? 100 : 260)).ToArray());
            map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
            var working = new TestWorkingBeatmap(map);
            var calculator = ruleset.CreateDifficultyCalculator(working);
            var original = (OsuDifficultyAttributes)calculator.Calculate();
            Mod[] mods = { new OsuModDecoyCircles { IntervalBeats = { Value = interval }, Style = { Value = style } } };
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
            var withDecoys = (OsuDifficultyAttributes)calculator.Calculate(mods);
            Assert.That(playable.HitObjects.OfType<DecoyCircle>().Count(), Is.EqualTo(expectedCount));
            Assert.That(withDecoys.HitCircleCount, Is.EqualTo(original.HitCircleCount));
            Assert.That(withDecoys.MaxCombo, Is.EqualTo(original.MaxCombo));
            Assert.That(withDecoys.DecoyPerformanceBonus, Is.EqualTo(expectedBonus).Within(0.00001));
            Assert.That(withDecoys.StarRating, Is.EqualTo(original.StarRating * System.Math.Cbrt(1 + expectedBonus)).Within(0.00001));

            double perfectPerformance(Mod[] scoreMods, OsuDifficultyAttributes difficulty)
            {
                var processor = ruleset.CreateScoreProcessor();
                processor.Mods.Value = scoreMods;
                processor.ApplyBeatmap(working.GetPlayableBeatmap(ruleset.RulesetInfo, scoreMods));
                var score = new ScoreInfo(map.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = 1,
                    Mods = scoreMods,
                    MaxCombo = processor.MaximumCombo,
                    Statistics = processor.MaximumStatistics,
                    MaximumStatistics = processor.MaximumStatistics,
                };
                return ruleset.CreatePerformanceCalculator().Calculate(score, difficulty).Total;
            }

            double basePerformance = perfectPerformance(System.Array.Empty<Mod>(), original);
            Assert.That(basePerformance, Is.GreaterThan(0));
            Assert.That(perfectPerformance(mods, withDecoys), Is.EqualTo(basePerformance * (1 + expectedBonus)).Within(0.00001));
        }

        [Test]
        public void AutopilotLinearisationIsRepeatableAndKeepsAimOnTargets()
        {
            var ruleset = new OsuRuleset();
            var map = createBeatmap(Enumerable.Range(0, 12).Select(i => circle(1000 + i * 500, i % 2 == 0 ? 80 : 400)).ToArray());
            map.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
            var working = new TestWorkingBeatmap(map);
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, System.Array.Empty<Mod>());

            Replay generate(int linearisation, double frameRate) => new OsuAutoGenerator(playable, System.Array.Empty<Mod>())
            {
                Linearisation = linearisation,
                FrameRate = frameRate,
            }.Generate();

            var first = generate(50, 500);
            var second = generate(50, 500);
            var unchanged = generate(0, 60);
            var linear = generate(100, 500);

            Assert.That(first.Frames.Select(f => (f.Time, ((OsuReplayFrame)f).Position, ((OsuReplayFrame)f).Actions)),
                Is.EqualTo(second.Frames.Select(f => (f.Time, ((OsuReplayFrame)f).Position, ((OsuReplayFrame)f).Actions))));

            var targets = map.HitObjects.OfType<HitCircle>().Select(c => c.StackedPosition).ToArray();
            foreach (var replay in new[] { first, unchanged, linear })
            {
                var clicks = replay.Frames.Cast<OsuReplayFrame>().Where(f => f.Actions.Count > 0).ToArray();
                Assert.That(clicks.Select(f => f.Position), Is.EqualTo(targets));
            }
            Assert.That(first.Frames.Count, Is.GreaterThan(unchanged.Frames.Count));
            var middleFrameTime = 1000d - 1000d / 500;
            var middleProgress = (float)((middleFrameTime - 900) / 100);
            var easedMiddle = Interpolation.ValueAt(middleFrameTime, new Vector2(256, 500), targets[0], 900, 1000, Easing.Out);
            var linearMiddle = Vector2.Lerp(new Vector2(256, 500), targets[0], middleProgress);
            var defaultMiddle = first.Frames.Cast<OsuReplayFrame>().First(f => f.Time == middleFrameTime).Position;
            Assert.That(Vector2.Distance(defaultMiddle, linearMiddle), Is.LessThan(Vector2.Distance(easedMiddle, linearMiddle)));
        }

        [Test]
        public void SwingAndReversedSlidersProduceTheSameChartInEitherOrder()
        {
            var source = createBeatmap(circle(0), circle(250), new Slider
            {
                StartTime = 1000,
                Position = new Vector2(100, 100),
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }),
            });
            var ruleset = new OsuRuleset().RulesetInfo;
            source.BeatmapInfo.Ruleset = ruleset;
            var workingBeatmap = new TestWorkingBeatmap(source);
            Mod[] mods = { new OsuModSwing(), new OsuModReversedSliders() };
            Assert.That(ModUtils.CheckCompatibleSet(mods), Is.True);
            var first = workingBeatmap.GetPlayableBeatmap(ruleset, mods).HitObjects.OfType<OsuHitObject>().ToArray();
            var second = workingBeatmap.GetPlayableBeatmap(ruleset, mods.Reverse().ToArray()).HitObjects.OfType<OsuHitObject>().ToArray();
            Assert.That(first[1].StartTime, Is.EqualTo(1000d / 3).Within(0.001));
            Assert.That(first[2].Position, Is.EqualTo(new Vector2(200, 100)));
            Assert.That(second.Select(o => (o.StartTime, o.Position, o.EndPosition)), Is.EqualTo(first.Select(o => (o.StartTime, o.Position, o.EndPosition))));
            Assert.That(ModUtils.CheckCompatibleSet(mods.Append(new OsuModDecoyCircles())), Is.True);
            Assert.That(ModUtils.CheckCompatibleSet(mods.Append(new OsuModEcho())), Is.False);
        }

        [Test]
        public void SwingChangesOnlyThePlayableCopy()
        {
            var source = createBeatmap(circle(0), circle(250));
            var ruleset = new OsuRuleset().RulesetInfo;
            source.BeatmapInfo.Ruleset = ruleset;
            var workingBeatmap = new TestWorkingBeatmap(source);
            var result = workingBeatmap.GetPlayableBeatmap(ruleset, new[] { new OsuModSwing() });
            Assert.That(source.HitObjects[1].StartTime, Is.EqualTo(250));
            Assert.That(result.HitObjects[1].StartTime, Is.EqualTo(1000d / 3).Within(0.001));
        }

        [Test]
        public void UnknownTransformationVersionCannotBePlayed()
        {
            var source = createBeatmap(circle(0), circle(250));
            var ruleset = new OsuRuleset().RulesetInfo;
            source.BeatmapInfo.Ruleset = ruleset;
            var workingBeatmap = new TestWorkingBeatmap(source);
            var mod = new OsuModRandomV2 { TransformationVersion = { Value = 100 } };
            Assert.That(() => workingBeatmap.GetPlayableBeatmap(ruleset, new[] { mod }), Throws.TypeOf<UnsupportedBeatmapVariantException>());
        }

        [Test]
        public void RitsuModsUseTheirNewCategoriesAndRetiredModsStayLoadable()
        {
            var ruleset = new OsuRuleset();
            Assert.That(ruleset.GetModsFor(ModType.DifficultyReduction).OfType<OsuModHalfStream>().Single().Name, Is.EqualTo("Streamline"));
            var increase = ruleset.GetModsFor(ModType.DifficultyIncrease).ToArray();
            Assert.That(increase.Any(mod => mod is OsuModDecoyCircles and IRitsuExclusiveMod), Is.True);
            Assert.That(increase.Any(mod => mod is OsuModOneEye and IRitsuExclusiveMod), Is.True);
            Assert.That(increase.Any(mod => mod is OsuModNoSliding and IModosuMod), Is.True);
            foreach (string acronym in new[] { "ECH", "SWG", "PMR" })
                Assert.That(new APIMod { Acronym = acronym }.ToMod(ruleset).Type, Is.EqualTo(ModType.System));

            var visibleModsWithoutIcons = ruleset.CreateAllMods().Where(mod => mod.Type != ModType.System && mod.HasImplementation && mod.Icon == null).Select(mod => mod.Name);
            Assert.That(visibleModsWithoutIcons, Is.Empty);
        }

        [Test]
        public void OneEyeHalvesOnlyApproachRateOnThePlayableChart()
        {
            var source = createBeatmap(circle(1000));
            var ruleset = new OsuRuleset().RulesetInfo;
            source.BeatmapInfo.Ruleset = ruleset;
            source.Difficulty = new BeatmapDifficulty { ApproachRate = 9, OverallDifficulty = 7 };
            var working = new TestWorkingBeatmap(source);
            var result = working.GetPlayableBeatmap(ruleset, new[] { new OsuModOneEye() });
            Assert.That(source.Difficulty.ApproachRate, Is.EqualTo(9));
            Assert.That(result.Difficulty.ApproachRate, Is.EqualTo(4.5));
            Assert.That(result.Difficulty.OverallDifficulty, Is.EqualTo(7));
        }

        [TestCase(true, 5)]
        [TestCase(false, 4)]
        public void OneEyeHalvesTheAdjustedArRegardlessOfModOrder(bool hardRock, float expectedAr)
        {
            var ruleset = new OsuRuleset();
            Mod adjustment = hardRock ? new OsuModHardRock() : new OsuModDifficultyAdjust { ApproachRate = { Value = 8 } };
            Mod[] mods = { new OsuModOneEye(), adjustment };
            Assert.That(ModUtils.CheckCompatibleSet(mods), Is.True);
            Assert.That(new APIMod { Acronym = "HFR" }.ToMod(ruleset).Name, Is.EqualTo("One Eye"));

            foreach (var selection in new[] { mods, mods.Reverse().ToArray() })
            {
                var source = createBeatmap(circle(1000));
                source.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
                source.Difficulty = new BeatmapDifficulty { ApproachRate = 9 };
                var working = new TestWorkingBeatmap(source);
                var result = working.GetPlayableBeatmap(ruleset.RulesetInfo, selection);
                Assert.That(result.Difficulty.ApproachRate, Is.EqualTo(expectedAr));
                Assert.That(ruleset.GetAdjustedDisplayDifficulty(source.BeatmapInfo, selection).ApproachRate, Is.EqualTo(expectedAr).Within(0.001));
            }
        }

        private static HitCircle circle(double time, float x = 220) => new HitCircle { StartTime = time, Position = new Vector2(x, 190) };

        private static OsuBeatmap createBeatmap(params OsuHitObject[] objects)
        {
            var map = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            map.HitObjects.AddRange(objects);
            return map;
        }
    }
}
