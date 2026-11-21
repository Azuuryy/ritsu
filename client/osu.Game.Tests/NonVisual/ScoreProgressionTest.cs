// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ScoreProgressionTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void TestFullComboComparison(bool fullCombo)
        {
            var ruleset = new OsuRuleset();
            var beatmap = new Beatmap
            {
                HitObjects = Enumerable.Range(0, 24).Select(index => (HitObject)new HitCircle
                {
                    StartTime = 1000 + index * 200,
                    Position = new Vector2(100 + index % 4 * 50, 200),
                }).ToList(),
            };
            beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;

            var working = new TestWorkingBeatmap(beatmap);
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());
            using var processor = ruleset.CreateScoreProcessor();
            processor.ApplyBeatmap(playable);

            for (int i = 0; i < playable.HitObjects.Count; i++)
            {
                var hit = playable.HitObjects[i];
                processor.ApplyResult(new JudgementResult(hit, hit.Judgement)
                {
                    Type = i == 4 ? HitResult.Ok : i == 8 && !fullCombo ? HitResult.Miss : HitResult.Great,
                });
            }

            var score = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo);
            processor.PopulateScore(score);
            var progression = ScoreProgressionGraph.Compute(working, score, default);

            Assert.Multiple(() =>
            {
                Assert.That(progression.FullCombo, Is.EqualTo(fullCombo));
                Assert.That(progression.Values[0]![^1], Is.EqualTo(score.Accuracy * 100).Within(0.001));
                Assert.That(progression.Values[1]![^1], Is.GreaterThan(0));
                Assert.That(progression.Values[4]![^1], Is.EqualTo(fullCombo ? 0 : 1));
                Assert.That(progression.Values[5]![^1], Is.EqualTo(1));
                if (fullCombo)
                    Assert.That(progression.Values[2], Is.Null);
                else
                    Assert.That(progression.Values[2]![^1], Is.GreaterThan(progression.Values[1]![^1]));
            });
        }
    }
}
