using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
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
        [TestCase(false, 8)]
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
                Is.EquivalentTo(full ? Enumerable.Range(10, 31).Select(x => x / 20d) : new[] { 1d, 1.5 }));
        }

        [Test]
        public void TestAutomaticVariantsUseOnlyDefaultModsAndSettings()
        {
            var presets = PerfectPerformanceWarmup.CreateVariants(new OsuRuleset().RulesetInfo, false).ToArray();
            Assert.That(presets.SelectMany(mods => mods).Select(mod => mod.Acronym).Distinct(), Is.EquivalentTo(new[] { "HD", "DT", "HR" }));
            Assert.That(presets.SelectMany(mods => mods).OfType<ModDoubleTime>().All(mod => mod.SpeedChange.IsDefault), Is.True);
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
        public void TestLegacyCacheAddsExactPreviewAndPreservesExistingPP()
        {
            using var storage = new TemporaryNativeStorage($"pp-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("perfect-pp.db", true);
            string key = BeatmapDifficultyCache.CreateStablePerformanceKey(1, "osu", "first", "[]");
            string preset = PerfectPerformanceStorage.Preset(new[] { "1", "1", "osu", "unused", "[]" });

            SQLitePCL.Batteries_V2.Init();
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE performance (key TEXT PRIMARY KEY, preset TEXT NOT NULL, pp REAL NOT NULL);
                    INSERT INTO performance (key, preset, pp) VALUES ($key, $preset, 120.5);
                    """;
                command.Parameters.AddWithValue("$key", key);
                command.Parameters.AddWithValue("$preset", preset);
                command.ExecuteNonQuery();
            }

            var cache = new PerfectPerformanceStorage(path);
            Assert.That(cache.ReadEntry(key), Is.EqualTo(new PerfectPerformanceStorage.Entry(120.5)));
            cache.Save(new Dictionary<string, PerfectPerformanceStorage.Entry> { [key] = new PerfectPerformanceStorage.Entry(999, 4.012345678901234, 416) });
            cache.Save(new Dictionary<string, double> { [key] = 999 });

            cache = new PerfectPerformanceStorage(path);
            var expected = new PerfectPerformanceStorage.Entry(120.5, 4.012345678901234, 416);
            Assert.That(cache.ReadEntry(key), Is.EqualTo(expected));
            Assert.That(cache.ReadPreset(preset)[key], Is.EqualTo(expected));
            Assert.That(cache.ReadEntry(key)?.Preview, Is.EqualTo(new StarDifficulty(expected.Stars!.Value, 416)));
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
