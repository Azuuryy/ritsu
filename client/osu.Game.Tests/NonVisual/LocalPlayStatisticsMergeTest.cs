// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.LocalPriority;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LocalPlayStatisticsMergeTest
    {
        [Test]
        public void TestValuesOnlyGoUp()
        {
            var recorded = new LocalPlayStatistics
            {
                PlayCount = 10,
                PlayTimeSeconds = 500,
                LastPlayed = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero),
                Hits = new Dictionary<HitResult, long> { [HitResult.Great] = 1000, [HitResult.Miss] = 50 },
                BeatmapPlays = new Dictionary<string, long> { ["a"] = 5 },
                DailyPlays = new Dictionary<string, long> { ["2026-11-20"] = 10 },
            };

            var fromScores = new LocalPlayStatistics
            {
                PlayCount = 4,
                PlayTimeSeconds = 900,
                LastPlayed = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                Hits = new Dictionary<HitResult, long> { [HitResult.Great] = 3000, [HitResult.Miss] = 10, [HitResult.Ok] = 7 },
                BeatmapPlays = new Dictionary<string, long> { ["a"] = 2, ["b"] = 3 },
                DailyPlays = new Dictionary<string, long> { ["2026-11-01"] = 4 },
            };

            Assert.That(recorded.MergeMaximum(fromScores), Is.True);

            Assert.That(recorded.PlayCount, Is.EqualTo(10));
            Assert.That(recorded.PlayTimeSeconds, Is.EqualTo(900));
            Assert.That(recorded.LastPlayed, Is.EqualTo(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(recorded.Count(HitResult.Great), Is.EqualTo(3000));
            Assert.That(recorded.Count(HitResult.Miss), Is.EqualTo(50));
            Assert.That(recorded.Count(HitResult.Ok), Is.EqualTo(7));
            Assert.That(recorded.BeatmapPlays, Is.EquivalentTo(new Dictionary<string, long> { ["a"] = 5, ["b"] = 3 }));
            Assert.That(recorded.DailyPlays, Is.EquivalentTo(new Dictionary<string, long> { ["2026-11-20"] = 10, ["2026-11-01"] = 4 }));

            // Merging the same again changes nothing.
            Assert.That(recorded.MergeMaximum(fromScores), Is.False);
        }
    }
}
