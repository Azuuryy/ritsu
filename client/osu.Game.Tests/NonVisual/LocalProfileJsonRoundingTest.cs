// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.LocalPriority;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LocalProfileJsonRoundingTest
    {
        [Test]
        public void TestUnfinishedPlayAccuracyKeepsFiveDecimals()
        {
            var play = new LocalUnfinishedPlay { Accuracy = 0.959512345678 };
            Assert.That(play.Accuracy, Is.EqualTo(0.95951));
            Assert.That(JsonConvert.SerializeObject(play), Does.Contain("\"Accuracy\":0.95951,"));

            // Old files with more decimals are rounded when read.
            var read = JsonConvert.DeserializeObject<LocalUnfinishedPlay>("{\"Accuracy\":0.9876543219}")!;
            Assert.That(read.Accuracy, Is.EqualTo(0.98765));
        }

        [Test]
        public void TestPlayTimeSavedWithOneDecimal()
        {
            var stats = new LocalPlayStatistics { PlayTimeSeconds = 50927.22596362611 };
            string json = JsonConvert.SerializeObject(stats);

            Assert.That(json, Does.Contain("\"PlayTimeSeconds\":50927.2,"));
            Assert.That(json, Does.Not.Contain("savedPlayTimeSeconds"));
            // The running total in memory stays exact.
            Assert.That(stats.PlayTimeSeconds, Is.EqualTo(50927.22596362611));
            Assert.That(JsonConvert.DeserializeObject<LocalPlayStatistics>(json)!.PlayTimeSeconds, Is.EqualTo(50927.2));
        }
    }
}
