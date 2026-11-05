// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Offline;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    public class RitsuPlaytimeTest
    {
        private static Beatmap map(double end, double breakEnd = 0) => new Beatmap
        {
            HitObjects = { new HitCircle { StartTime = 1000 }, new HitCircle { StartTime = 1000 + end } },
            Breaks = { new BreakPeriod(1000, 1000 + breakEnd) },
        };

        private static ScoreInfo score(string hash, params Mod[] mods)
        {
            var info = new BeatmapInfo { Hash = hash };
            return new ScoreInfo(info) { Mods = mods };
        }

        [Test]
        public void TestRepeatedScoresDifferentMapsAndModes()
        {
            var maps = new Dictionary<string, IBeatmap> { ["a"] = map(120000, 30000), ["b"] = map(60000, 10000) };
            int loads = 0;
            var calculator = new RitsuPlaytime(info => { loads++; return maps[info.Hash]; });
            var scores = new[] { score("a"), score("a"), score("b") };
            Assert.That(calculator.Calculate(scores, RitsuPlaytimeMode.MapLength), Is.EqualTo((300d, 0)));
            Assert.That(calculator.Calculate(scores, RitsuPlaytimeMode.DrainLength), Is.EqualTo((230d, 0)));
            Assert.That(calculator.Calculate(scores, RitsuPlaytimeMode.MapLength), Is.EqualTo((300d, 0)));
            Assert.That(loads, Is.EqualTo(2));
        }

        [Test]
        public void TestSpeedsAndIncompleteScores()
        {
            var custom = new OsuModDoubleTime();
            custom.SpeedChange.Value = 1.2;
            var scores = new[] { score("a"), score("a", new OsuModDoubleTime()), score("a", new OsuModNightcore()), score("a", new OsuModHalfTime()), score("a", custom) };
            scores[0].Passed = false;
            var calculator = new RitsuPlaytime(_ => map(120000));
            Assert.That(calculator.Calculate(scores, RitsuPlaytimeMode.MapLength).Seconds, Is.EqualTo(540).Within(0.001));
        }

        [Test]
        public void TestMissingInvalidAndBreakBounds()
        {
            var calculator = new RitsuPlaytime(info => info.Hash == "missing" ? null : map(0));
            Assert.That(calculator.Calculate(new[] { score("missing"), score("zero"), score("") }, RitsuPlaytimeMode.MapLength), Is.EqualTo((0d, 3)));
            var beatmap = map(120000);
            beatmap.Breaks.Add(new BreakPeriod(0, 31000));
            beatmap.Breaks.Add(new BreakPeriod(21000, 41000));
            beatmap.Breaks.Add(new BreakPeriod(111000, 130000));
            Assert.That(RitsuPlaytime.GetLengths(beatmap), Is.EqualTo(new RitsuPlaytime.Lengths(120000, 70000)));
        }

        [Test]
        public void TestRepresentativeHistoryAndNewScore()
        {
            var calculator = new RitsuPlaytime(_ => map(120000, 30000));
            var scores = new List<ScoreInfo>();
            for (int i = 0; i < 1000; i++) scores.Add(score("a"));
            Assert.That(calculator.Calculate(scores, RitsuPlaytimeMode.MapLength).Seconds / 3600, Is.EqualTo(33.333333).Within(0.00001));
            scores.Add(score("a"));
            Assert.That(calculator.Calculate(scores, RitsuPlaytimeMode.DrainLength).Seconds, Is.EqualTo(90090));
            var reloaded = new RitsuPlaytime(_ => map(120000, 30000));
            Assert.That(reloaded.Calculate(scores, RitsuPlaytimeMode.DrainLength), Is.EqualTo(calculator.Calculate(scores, RitsuPlaytimeMode.DrainLength)));
        }
    }
}
