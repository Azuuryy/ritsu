using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Modosu.Integration;
using osu.Game.Rulesets.Modosu.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Modosu.Tests
{
    [TestFixture]
    public class ModosuPerformanceExportTest
    {
        internal static GameplayState createState(bool spacing)
        {
            var beatmap = new OsuBeatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 },
                    MD5Hash = "1234567890abcdef1234567890abcdef"
                },
                ControlPointInfo = new ControlPointInfo()
            };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 32; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = 1000 + i * 125,
                    Position = new Vector2(180 + i % 2 * 100, 170 + i % 3 * 18)
                });
            }

            Mod[] mods = Array.Empty<Mod>();
            if (spacing)
            {
                var fsa = new OsuModFunSpacingAdjust { StartSpacing = { Value = 1.5f }, EndSpacing = { Value = 2.5f } };
                fsa.ApplyToBeatmap(beatmap);
                mods = new Mod[] { fsa };
            }
            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var state = new GameplayState(beatmap, new ModosuRuleset(), mods);
            state.Score.ScoreInfo.Mods = mods;
            state.Score.ScoreInfo.Date = DateTimeOffset.UtcNow;
            state.ScoreProcessor.Mods.Value = mods;
            state.ScoreProcessor.ApplyBeatmap(beatmap);
            return state;
        }

        [Test]
        public async Task UsesTheNativeCalculatorAndPostModGeometry()
        {
            var state = createState(true);
            var frames = new List<ModosuPerformanceFrame>();
            double time = 0;
            using var export = new ModosuPerformanceExport(state, () => time, publish: frames.Add);
            var attributes = await export.Ready;
            for (int i = 0; i < 20; i++)
            {
                var hitObject = state.Beatmap.HitObjects[i];
                time = hitObject.StartTime;
                state.ScoreProcessor.ApplyResult(new JudgementResult(hitObject, hitObject.CreateJudgement()) { Type = HitResult.Great });
            }
            export.Update(force: true);

            var expectedScore = new ScoreInfo(state.Beatmap.BeatmapInfo, state.Ruleset.RulesetInfo) { Mods = state.Mods.ToArray() };
            state.ScoreProcessor.PopulateScore(expectedScore);
            var expected = state.Ruleset.CreatePerformanceCalculator()!.Calculate(expectedScore, attributes[19].Attributes);
            var frame = frames.Last();
            Assert.That(frame.Pp, Is.EqualTo(expected.Total).Within(0.0000001));
            Assert.That(frame.Pp, Is.GreaterThan(0));
            Assert.That(frame.Hits.Great, Is.EqualTo(20));
            Assert.That(frame.ModsJson, Does.Contain("FSA"));
            Assert.That(frame.MaximumCombo, Is.EqualTo(32));

            using var vanilla = new ModosuPerformanceExport(createState(false), () => 0, publish: _ => { });
            var vanillaAttributes = await vanilla.Ready;
            Assert.That(frame.TotalStars, Is.Not.EqualTo(vanillaAttributes.Last().Attributes.StarRating).Within(0.0001));

            // Verify the serialized shape consumed by tosu, including zero PP versus an unavailable value.
            var json = JsonSerializer.Serialize(frame, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.GetProperty("schema").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("pp").GetDouble(), Is.EqualTo(expected.Total).Within(0.0000001));
        }

        [Test]
        public async Task PublishesTheExactFinalValueAndClosesAbandonedSessions()
        {
            var frames = new List<ModosuPerformanceFrame>();
            var state = createState(false);
            state.Score.ScoreInfo.Date = default;
            var completedScore = new ScoreInfo { Date = DateTimeOffset.UtcNow };
            var export = new ModosuPerformanceExport(state, () => 1000, publish: frames.Add);
            await export.Ready;
            export.CaptureResult(completedScore);
            export.Dispose();
            export.PublishFinal(123.456789);
            Assert.That(frames.Last().Phase, Is.EqualTo("results"));
            Assert.That(frames.Last().Pp, Is.EqualTo(123.456789));
            Assert.That(DateTimeOffset.Parse(frames.Last().Date), Is.EqualTo(completedScore.Date));

            var abandoned = new ModosuPerformanceExport(createState(false), () => 0, publish: frames.Add);
            await abandoned.Ready;
            abandoned.Dispose();
            Assert.That(frames.Last().Phase, Is.EqualTo("closed"));
            Assert.That(frames.Last().Pp, Is.Null);
            Assert.That(ModosuPerformanceFrame.Finite(double.NaN), Is.Null);
            Assert.That(ModosuPerformanceFrame.Finite(0), Is.Zero);
        }

        [Test]
        public async Task ExportsPerfectMaximumAndNativeProjections()
        {
            var state = createState(true);
            var frames = new List<ModosuPerformanceFrame>();
            using var export = new ModosuPerformanceExport(state, () => 4000, publish: frames.Add);
            var attributes = await export.Ready;
            export.Update(force: true);
            var initial = frames.Last();
            Assert.That(initial.MaximumPp, Is.GreaterThan(0));
            Assert.That(initial.FcPp, Is.EqualTo(initial.MaximumPp));
            Assert.That(initial.MaxAchievablePp, Is.EqualTo(initial.MaximumPp));

            for (int i = 0; i < state.Beatmap.HitObjects.Count; i++)
            {
                var obj = state.Beatmap.HitObjects[i];
                var result = i == 5 ? HitResult.Miss : i == 6 ? HitResult.Ok : HitResult.Great;
                state.ScoreProcessor.ApplyResult(new JudgementResult(obj, obj.CreateJudgement()) { Type = result });
            }
            export.Update(force: true);
            var final = frames.Last();
            Assert.That(final.MaximumPp, Is.EqualTo(initial.MaximumPp));
            Assert.That(final.MaxAchievablePp, Is.EqualTo(final.Pp).Within(0.000001));
            Assert.That(final.FcPp, Is.GreaterThan(final.Pp!.Value));
            Assert.That(final.FcPp, Is.LessThan(final.MaximumPp!.Value));

            var score = new ScoreInfo(state.Beatmap.BeatmapInfo, state.Ruleset.RulesetInfo) { Mods = state.Mods.ToArray() };
            state.ScoreProcessor.PopulateScore(score);
            var projections = ModosuProjectedScores.Create(score, 32, state.ScoreProcessor)!.Value;
            Assert.That(projections.fc.Statistics[HitResult.Great], Is.EqualTo(31));
            Assert.That(projections.fc.Statistics[HitResult.Ok], Is.EqualTo(1));
            Assert.That(projections.fc.Statistics[HitResult.Miss], Is.Zero);
            Assert.That(projections.remaining.Statistics[HitResult.Miss], Is.EqualTo(1));
            Assert.That(projections.fc.MaxCombo, Is.EqualTo(32));
            var expected = state.Ruleset.CreatePerformanceCalculator()!.Calculate(projections.fc, attributes.Last().Attributes);
            Assert.That(final.FcPp, Is.EqualTo(expected.Total).Within(0.000001));
        }

        [Test]
        public void RemainingProjectionRetainsErrorsAndUsesCurrentCombo()
        {
            var state = createState(false);
            for (int i = 0; i < 7; i++)
            {
                var obj = state.Beatmap.HitObjects[i];
                state.ScoreProcessor.ApplyResult(new JudgementResult(obj, obj.CreateJudgement())
                {
                    Type = i == 5 ? HitResult.Miss : i == 6 ? HitResult.Ok : HitResult.Great
                });
            }
            var score = new ScoreInfo();
            state.ScoreProcessor.PopulateScore(score);
            score.BeatmapInfo = state.Beatmap.BeatmapInfo;
            score.Ruleset = state.Ruleset.RulesetInfo;
            var projection = ModosuProjectedScores.Create(score, 32, state.ScoreProcessor)!.Value;
            Assert.That(projection.remaining.Statistics[HitResult.Great], Is.EqualTo(30));
            Assert.That(projection.remaining.MaxCombo, Is.EqualTo(26));
            Assert.That(projection.remaining.Accuracy, Is.EqualTo(9100.0 / 9600).Within(0.000001));

            score.HitEvents = new List<HitEvent>();
            Assert.That(ModosuProjectedScores.Create(score, 32, state.ScoreProcessor), Is.Null);
        }

        [Test]
        public void SliderProjectionKeepsLostTailsAndTicksUntilFcCorrection()
        {
            var state = createState(false);
            var slider = new Slider();
            var tail = new SliderTailCircle(slider);
            var tick = new SliderTick();
            var score = new ScoreInfo(state.Beatmap.BeatmapInfo, state.Ruleset.RulesetInfo)
            {
                MaxCombo = 1,
                Combo = 0,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = 1, [HitResult.IgnoreMiss] = 1, [HitResult.LargeTickMiss] = 1
                },
                MaximumStatistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = 2, [HitResult.SliderTailHit] = 2, [HitResult.LargeTickHit] = 2
                },
                HitEvents = new List<HitEvent>
                {
                    new HitEvent(0, null, HitResult.Great, new HitCircle(), null!, null),
                    new HitEvent(0, null, HitResult.IgnoreMiss, tail, null!, null),
                    new HitEvent(0, null, HitResult.LargeTickMiss, tick, null!, null)
                }
            };
            var projection = ModosuProjectedScores.Create(score, 6, state.ScoreProcessor)!.Value;
            Assert.That(projection.remaining.Statistics[HitResult.SliderTailHit], Is.EqualTo(1));
            Assert.That(projection.remaining.Statistics[HitResult.LargeTickHit], Is.EqualTo(1));
            Assert.That(projection.remaining.Statistics[HitResult.LargeTickMiss], Is.EqualTo(1));
            Assert.That(projection.remaining.MaxCombo, Is.EqualTo(3));
            Assert.That(projection.remaining.Accuracy, Is.LessThan(1));
            Assert.That(projection.fc.Statistics[HitResult.SliderTailHit], Is.EqualTo(2));
            Assert.That(projection.fc.Statistics[HitResult.LargeTickHit], Is.EqualTo(2));
            Assert.That(projection.fc.Accuracy, Is.EqualTo(1));
        }
    }
}
