// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Performance;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Catch;
using osu.Framework.Statistics;
using osu.Framework.Platform;
using osu.Game.Commands;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Offline;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class RitsuLibraryTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestSimulationUsesModePerformanceCalculator(int mode)
        {
            using var archive = new System.IO.Compression.ZipArchive(osu.Game.Tests.Resources.TestResources.GetTestBeatmapStream(true));
            using var file = archive.Entries.First(entry => entry.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)).Open();
            using var reader = new osu.Game.IO.LineBufferedReader(file);
            var decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            var working = new osu.Game.Tests.Beatmaps.TestWorkingBeatmap(decoded);
            osu.Game.Rulesets.Ruleset ruleset = mode switch
            {
                1 => new osu.Game.Rulesets.Taiko.TaikoRuleset(),
                2 => new CatchRuleset(),
                3 => new osu.Game.Rulesets.Mania.ManiaRuleset(),
                _ => new OsuRuleset(),
            };
            var mods = Array.Empty<Mod>();
            using var simulation = new RitsuSimulation(working, ruleset.RulesetInfo, mods, default);
            var difficulty = ruleset.CreateDifficultyCalculator(working).Calculate(mods);
            var calculator = ruleset.CreatePerformanceCalculator()!;
            var perfect = simulation.Calculate(new Dictionary<HitResult, int>(), null, null);
            Assert.That(perfect.Accuracy, Is.EqualTo(1));
            Assert.That(perfect.PP, Is.EqualTo(calculator.Calculate(perfect, difficulty).Total).Within(0.000001));
            Assert.That(simulation.PerfectPP, Is.EqualTo(perfect.PP));

            var changes = new Dictionary<HitResult, int> { [HitResult.Miss] = 1 };
            if (mode == 0 && simulation.Groups.Any(group => group.Best == HitResult.SliderTailHit))
                changes.Add(HitResult.IgnoreMiss, 1);

            var imperfect = simulation.Calculate(changes, null, null);
            Assert.That(imperfect.Accuracy, Is.LessThan(1));
            Assert.That(imperfect.PP, Is.EqualTo(calculator.Calculate(imperfect, difficulty).Total).Within(0.000001));
        }

        [Test]
        public Task TestPreferencesPersistAndStayOutsideLinkedDatabase() => Task.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "ritsu-library-" + Guid.NewGuid().ToString("N"));

            try
            {
                var app = new NativeStorage(Path.Combine(directory, "app"));
                var linked = new NativeStorage(Path.Combine(directory, "linked"));
                var library = new RitsuLibrary(app);
                Guid firstProfile = Guid.NewGuid();
                Guid secondProfile = Guid.NewGuid();
                Guid score = Guid.NewGuid();
                Guid searchId = Guid.NewGuid();
                Guid collectionId = Guid.NewGuid();
                var state = new RitsuSearchState("stars>=3", collectionId.ToString(), 3, 8, 10, 200, true,
                    SortMode.PerformancePoints, GroupMode.Difficulty, true, false, 100, true);
                var preset = new RitsuSessionPreset(Guid.NewGuid(), "Warm-up", "osu", "[]", Guid.NewGuid().ToString(), 1, true, state);

                using (var realm = new RealmAccess(linked, "client"))
                {
                    realm.Write(r => r.Add(new BeatmapCollection("Practice") { ID = collectionId }));
                    RitsuCollectionMenu.Add(realm, collectionId, new[] { "map", "map", "another-map" });
                    RitsuCollectionMenu.Add(realm, collectionId, new[] { "map" });
                    Assert.That(realm.Run(r => r.Find<BeatmapCollection>(collectionId)!.BeatmapMD5Hashes.ToArray()),
                        Is.EqualTo(new[] { "map", "another-map" }));

                    library.SaveSearch(searchId, "Practice", state);
                    library.AddSmartCollection(searchId, "Current practice");
                    library.SaveSubgroup(new RitsuSubgroup(Guid.NewGuid(), collectionId, "Warm-up", new[] { "map", "another-map" }));
                    library.RemoveSubgroupMembers(collectionId, new HashSet<string> { "another-map" });
                    library.SaveMemo(firstProfile, "map", new RitsuMemo("Check the jump", score, 43000));
                    library.ToggleMarkedScore(firstProfile, score);
                    library.SavePreset(preset);
                    library.SetMenuOrder("sort", new RitsuMenuOrder { Mode = RitsuMenuOrderMode.MostUsed, Pins = new[] { "Title" } });
                }

                var reloaded = new RitsuLibrary(app);
                Assert.That(linked.Exists("ritsu/library.json"), Is.False);
                Assert.That(reloaded.Searches.Single().State, Is.EqualTo(state));
                Assert.That(reloaded.Subgroups.Single().Hashes, Is.EqualTo(new[] { "map" }));
                Assert.That(reloaded.Presets.Single(), Is.EqualTo(preset));
                Assert.That(reloaded.MenuOrder("sort").Pins, Is.EqualTo(new[] { "Title" }));
                Assert.That(reloaded.GetMemo(firstProfile, "map")?.ReplayId, Is.EqualTo(score));
                Assert.That(reloaded.GetMarkedScores(firstProfile), Is.EqualTo(new[] { score }));
                Assert.That(reloaded.GetMemo(secondProfile, "map"), Is.Null);
                Assert.That(reloaded.GetMarkedScores(secondProfile), Is.Empty);

                reloaded.SaveSearch(searchId, "Practice", state with { Query = "length<2m" });
                Assert.That(reloaded.Searches.Single(s => s.Id == reloaded.SmartCollections.Single().SearchId).State.Query, Is.EqualTo("length<2m"));
                reloaded.DeleteSearch(searchId);
                Assert.That(new RitsuLibrary(app).SmartCollections, Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        });

        [Test]
        public void TestSmartCollectionUsesBestScoreAndLocalWeek()
        {
            var timeZone = TimeZoneInfo.CreateCustomTimeZone("Ritsu test zone", TimeSpan.FromHours(1), "UTC+1", "UTC+1");
            var monday = new DateTimeOffset(2026, 11, 9, 0, 30, 0, TimeSpan.FromHours(1));
            var map = new BeatmapInfo { Hash = "practice", Length = 75000 };
            var scores = new List<ScoreInfo>
            {
                score(1000, 2, monday.AddHours(-2)),
                score(500, 9, monday.AddHours(-3)),
            };
            var criteria = new FilterCriteria();
            FilterQueryParser.ApplyQueries(criteria, "length<2m pbmisses>=1 pbmisses<=3 playedthisweek=no");
            criteria.PlayHistory = new Lazy<RitsuPlayHistory>(() => new RitsuPlayHistory(scores, monday, timeZone));
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(map, criteria), Is.True);

            scores.Add(score(300, 10, monday));
            criteria.PlayHistory = new Lazy<RitsuPlayHistory>(() => new RitsuPlayHistory(scores, monday, timeZone));
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(map, criteria), Is.False);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(new BeatmapInfo { Hash = "unplayed", Length = 90000 }, criteria), Is.False);

            ScoreInfo score(int total, int misses, DateTimeOffset date) => new ScoreInfo
            {
                BeatmapHash = map.Hash,
                TotalScore = total,
                Date = date,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Miss] = misses },
            };
        }

        [Test]
        public void TestCompletionReplacesOnlyTokenAtCursor()
        {
            var completion = RitsuSearchCompletion.Get("artist=Camellia status=ran length<2m", 26).Single();
            Assert.That(completion.Query, Is.EqualTo("artist=Camellia status=Ranked length<2m"));
            Assert.That(completion.Caret, Is.EqualTo("artist=Camellia status=Ranked".Length));
        }

        [TestCase(0.75, false)]
        [TestCase(1.0, false)]
        [TestCase(1.39, false)]
        [TestCase(0.75, true)]
        [TestCase(1.39, true)]
        public void TestApproachRateCompensation(double rate, bool useCatch)
        {
            osu.Game.Rulesets.Ruleset ruleset = useCatch ? new CatchRuleset() : new OsuRuleset();
            var da = (ModDifficultyAdjust)ruleset.CreateModFromAcronym("DA")!;
            da.OverallDifficulty.Value = 7;
            Mod[]? mods = new Mod[] { da };
            Assert.That(RitsuTargetBpmDialog.TryCompensateAr(ruleset, 8, rate, ref mods, out double? effectiveAr), Is.Null);
            Assert.That(effectiveAr, Is.EqualTo(8).Within(0.02));
            Assert.That(mods!.OfType<ModDifficultyAdjust>().Single().OverallDifficulty.Value, Is.EqualTo(7));
            Assert.That(da.RitsuApproachRate!.Value.Setting.Value, Is.Null);
        }

        [Test]
        public void TestApproachRateCompensationEnablesExtendedLimits()
        {
            var ruleset = new OsuRuleset();
            Mod[]? mods = Array.Empty<Mod>();
            Assert.That(RitsuTargetBpmDialog.TryCompensateAr(ruleset, 0, 1.5, ref mods, out double? effectiveAr), Is.Null);
            Assert.That(effectiveAr, Is.EqualTo(0).Within(0.02));
            var adjust = mods!.OfType<ModDifficultyAdjust>().Single();
            Assert.That(adjust.ExtendedLimits.Value, Is.True);
            Assert.That(adjust.RitsuApproachRate!.Value.Setting.Value, Is.LessThan(0));
        }

        [Test]
        public void TestApproachRateCompensationRejectsLimitsAndConflicts()
        {
            var ruleset = new OsuRuleset();
            Mod[]? mods = Array.Empty<Mod>();
            Assert.That(RitsuTargetBpmDialog.TryCompensateAr(ruleset, 0, 3, ref mods, out _), Is.Not.Null);
            mods = new Mod[] { new OsuModHardRock() };
            Assert.That(RitsuTargetBpmDialog.TryCompensateAr(ruleset, 8, 1.3, ref mods, out _), Is.Not.Null);
        }

        [Test]
        public void TestQueueSkipsUnavailableEntriesAndOnlyConsumesAfterSelection()
        {
            var queue = new RitsuBeatmapQueue();
            var first = map("first");
            var second = map("second");
            Assert.That(queue.Add(first), Is.True);
            Assert.That(queue.Add(first), Is.False);
            Assert.That(queue.Add(second), Is.True);
            Assert.That(queue.Move("second", -1), Is.True);
            Assert.That(queue.Snapshot().Select(i => i.Hash), Is.EqualTo(new[] { "second", "first" }));
            Assert.Throws<InvalidOperationException>(() => queue.SelectNext(hash => hash == "first" ? first : null,
                _ => throw new InvalidOperationException()));
            Assert.That(queue.Snapshot().Select(i => i.Hash), Is.EqualTo(new[] { "first" }));
            Assert.That(queue.SelectNext(_ => first, selected => Assert.That(selected, Is.SameAs(first))), Is.True);
            Assert.That(queue.Snapshot(), Is.Empty);

            static BeatmapInfo map(string hash)
            {
                var beatmap = new BeatmapInfo { MD5Hash = hash };
                var set = new BeatmapSetInfo();
                beatmap.BeatmapSet = set;
                return beatmap;
            }
        }

        [Test]
        public void TestCommandMatchingAndAvailability()
        {
            var command = new RitsuCommand("test", "/test action", "Test action", "[argument]", () => false, _ => new RitsuCommandInvocation(() => { }));
            var registry = new RitsuCommandRegistry(command);
            Assert.That(registry.Match("/TEST ACTION argument"), Is.SameAs(command));
            Assert.That(registry.Match("/test actions"), Is.Null);
            Assert.That(RitsuCommandRegistry.Bind(command, string.Empty).Error, Is.Not.Null);
        }

        [Test]
        public async Task TestFrameProfilerWritesOfflineReport()
        {
            using var storage = new TemporaryNativeStorage("ritsu-frame-report");
            using var host = new HeadlessGameHost("ritsu-frame-report");
            var profiler = new RitsuFrameProfiler(storage, host);
            var export = profiler.Start(action => action(), TimeSpan.FromMilliseconds(150));
            using (FrameProfileCapture.Measure("test-operation"))
                FrameProfileCapture.Count("test.size", 42);
            await export.ConfigureAwait(false);

            string directory = storage.GetDirectories(string.Empty).Single();
            string json = File.ReadAllText(storage.GetFullPath(Path.Combine(directory, "trace.json")));
            string html = File.ReadAllText(storage.GetFullPath(Path.Combine(directory, "report.html")));
            Assert.That(json, Does.Contain("\"SchemaVersion\":1").And.Contain("test-operation"));
            Assert.That(html, Does.Contain(json).And.Contain("Ritsu frame profile"));
            Assert.That(profiler.CanStart, Is.True);
        }

        [Test]
        public void TestFrameCaptureOverflowAndNewSession()
        {
            using var first = new FrameProfileCapture(1, 1);
            using (FrameProfileCapture.Measure("operation"))
                FrameProfileCapture.Count("count", 2);
            var snapshot = first.Snapshot();
            Assert.That(snapshot.Operations.Select(o => o.Name), Is.EqualTo(new[] { "count" }));
            Assert.That(snapshot.DroppedOperations, Is.EqualTo(1));
            Assert.That(snapshot.Operations[0].Start, Is.GreaterThanOrEqualTo(snapshot.StartTimestamp));

            using var second = new FrameProfileCapture(1, 1);
            Assert.That(second.Snapshot().DroppedOperations, Is.Zero);
        }

        [Test]
        public void TestRateAndMemoTimestamp()
        {
            Assert.That(RitsuTargetBpmDialog.TryBuildMods(new OsuRuleset(), Array.Empty<Mod>(), 180, 250, out var mods, out var achieved), Is.Null);
            Assert.That(mods!.OfType<ModRateAdjust>().Single().SpeedChange.Value, Is.EqualTo(1.39).Within(0.001));
            Assert.That(achieved, Is.EqualTo(250.2).Within(0.001));
            Assert.That(RitsuTargetBpmDialog.TryBuildMods(new OsuRuleset(), mods!, 180, 180, out var normal, out _), Is.Null);
            Assert.That(normal, Is.Empty);
            Assert.That(RitsuTargetBpmDialog.TryBuildMods(new OsuRuleset(), mods, 180, 400, out _, out _), Is.Not.Null);
            Assert.That(RitsuMemoDialog.TryParseTimestamp("1:23.5", out double timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(83500));
            Assert.That(RitsuMemoDialog.TryParseTimestamp("1:60", out _), Is.False);
        }
    }
}
