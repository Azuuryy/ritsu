using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Utils;

namespace osu.Game.Tests.NonVisual
{
    public class PerfectPerformanceWarmupTest
    {
        [TestCase(false, 24)]
        [TestCase(true, 186)]
        public void TestStandardVariants(bool full, int count)
        {
            var presets = PerfectPerformanceWarmup.CreateVariants(new OsuRuleset().RulesetInfo, full).ToArray();
            Assert.That(presets, Has.Length.EqualTo(count));
            Assert.That(presets.Select(mods => string.Join(",", mods.Select(mod => mod.Acronym)) + ":" + Math.Round(ModUtils.CalculateRateWithMods(mods), 2)).Distinct().Count(),
                Is.EqualTo(count));
            Assert.That(presets.All(mods => mods.Count(m => m is ModHardRock or ModEasy) <= 1
                && mods.Count(m => m is ModDoubleTime or ModHalfTime) <= 1), Is.True);
            Assert.That(presets.Select(mods => Math.Round(ModUtils.CalculateRateWithMods(mods), 2)).Distinct(),
                Is.EquivalentTo(full ? Enumerable.Range(10, 31).Select(x => x / 20d) : new[] { 0.75, 1, 1.25, 1.50 }));
        }

        [Test]
        public void TestAutomaticCompletionMarkerIsScopedToLibraryAndVersion()
        {
            using var storage = new TemporaryNativeStorage($"pp-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("perfect-pp.db", true);
            string marker = BeatmapDifficultyCache.CreateStablePerformanceKey(1, "osu", "library", "automatic-grid-v1");
            var cache = new PerfectPerformanceStorage(path);
            Assert.That(cache.HasMarker(marker), Is.False);
            cache.SaveMarker(marker);

            cache = new PerfectPerformanceStorage(path);
            Assert.That(cache.HasMarker(marker), Is.True);
            Assert.That(cache.HasMarker(BeatmapDifficultyCache.CreateStablePerformanceKey(2, "osu", "library", "automatic-grid-v1")), Is.False);
            Assert.That(cache.HasMarker(BeatmapDifficultyCache.CreateStablePerformanceKey(1, "osu", "other-library", "automatic-grid-v1")), Is.False);
        }

        [Test]
        public void TestCachePersistsAndGroupsOnlyMatchingPreset()
        {
            using var storage = new TemporaryNativeStorage($"pp-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("perfect-pp.db", true);
            var cache = new PerfectPerformanceStorage(path);
            string first = BeatmapDifficultyCache.CreateStablePerformanceKey(1, "osu", "first", "[]");
            string second = BeatmapDifficultyCache.CreateStablePerformanceKey(1, "osu", "second", "[]");
            string changed = BeatmapDifficultyCache.CreateStablePerformanceKey(2, "osu", "first", "[]");
            cache.Save(new Dictionary<string, double> { [first] = 120.5, [second] = 200 });
            cache = new PerfectPerformanceStorage(path);
            Assert.That(cache.Read(first), Is.EqualTo(120.5));
            Assert.That(cache.Read(changed), Is.Null);
            Assert.That(cache.ReadPreset(PerfectPerformanceStorage.Preset(new[] { "1", "1", "osu", "unused", "[]" })).Count, Is.EqualTo(2));
        }
    }
}
