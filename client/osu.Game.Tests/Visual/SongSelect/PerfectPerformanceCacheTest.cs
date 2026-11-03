// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Mods;
using System.Linq;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public class PerfectPerformanceCacheTest
    {
        [Test]
        public void OnlyDifficultyAffectingModsAreUsed()
        {
            Mod[] mods = { new OsuModNoFail(), new OsuModDoubleTime(), new OsuModHidden() };
            var filtered = BeatmapDifficultyCache.FilterPerformanceMods(mods).ToArray();
            Assert.That(filtered, Does.Not.Contain(mods[0]));
            Assert.That(filtered, Does.Contain(mods[1]));
            Assert.That(filtered, Does.Contain(mods[2]));
        }

        [Test]
        public void CacheSurvivesReopenAndSeparatesConfigurations()
        {
            using var storage = new TemporaryNativeStorage("ritsu-perfect-pp-test");
            string key = BeatmapDifficultyCache.CreateStablePerformanceKey(123, "osu", "map-hash", "DT:1.5");
            var values = new Dictionary<string, double> { [key] = 345.67 };
            BeatmapDifficultyCache.WritePerformanceCache(storage, "ritsu/perfect-pp.json", values);
            var restored = BeatmapDifficultyCache.loadPerformanceCache(storage, "ritsu/perfect-pp.json");
            Assert.That(restored[key], Is.EqualTo(345.67));
            Assert.That(BeatmapDifficultyCache.CreateStablePerformanceKey(123, "osu", "map-hash", "DT:1.5"), Is.EqualTo(key));
            Assert.That(BeatmapDifficultyCache.CreateStablePerformanceKey(124, "osu", "map-hash", "DT:1.5"), Is.Not.EqualTo(key));
            Assert.That(BeatmapDifficultyCache.CreateStablePerformanceKey(123, "osu", "map-hash", "DT:1.2"), Is.Not.EqualTo(key));
            Assert.That(BeatmapDifficultyCache.CreateStablePerformanceKey(123, "taiko", "map-hash", "DT:1.5"), Is.Not.EqualTo(key));
            Assert.That(BeatmapDifficultyCache.CreateStablePerformanceKey(123, "osu", "other-hash", "DT:1.5"), Is.Not.EqualTo(key));
        }

        [Test]
        public void MissingOrCorruptCacheReturnsEmpty()
        {
            using var storage = new TemporaryNativeStorage("ritsu-perfect-pp-corrupt");
            Assert.That(BeatmapDifficultyCache.loadPerformanceCache(storage, "ritsu/missing.json"), Is.Empty);
            using (var stream = storage.CreateFileSafely("ritsu/broken.json"))
            using (var writer = new StreamWriter(stream)) writer.Write("{ invalid json");
            Assert.That(BeatmapDifficultyCache.loadPerformanceCache(storage, "ritsu/broken.json"), Is.Empty);
        }
    }
}
