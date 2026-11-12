// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
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
