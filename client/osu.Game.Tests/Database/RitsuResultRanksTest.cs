// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using NUnit.Framework;
using osu.Game.Offline;
using osu.Game.Scoring;
using osu.Game.Users;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Offline
{
    [TestFixture]
    public partial class RitsuResultRanksTest : RealmTest
    {
        [Test]
        public void TestProfileImagesAndFlagsPersistIndependently()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "profile-image-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid first = profiles.ActiveProfileId;
            string path = storage.GetFullPath("source.png", true);
            using (var image = new Image<Rgba32>(2, 2)) image.SaveAsPng(path);
            profiles.SetCountry(first, CountryCode.FR);
            profiles.SetAvatar(first, new FileInfo(path));
            profiles.SetFlagImage(first, new FileInfo(path));
            profiles.AddProfile("Second");
            Guid second = profiles.ActiveProfileId;
            profiles.SetCountry(second, CountryCode.JP);
            var reloaded = new RitsuProfileManager(storage, realm);
            var savedFirst = reloaded.GetProfiles().Single(p => p.Id == first);
            var savedSecond = reloaded.GetProfiles().Single(p => p.Id == second);
            Assert.That(savedFirst.CountryCode, Is.EqualTo(CountryCode.FR));
            Assert.That(savedFirst.AvatarFileName, Is.Not.Null.And.Not.Empty);
            Assert.That(savedFirst.FlagFileName, Is.Not.Null.And.Not.Empty);
            Assert.That(savedSecond.CountryCode, Is.EqualTo(CountryCode.JP));
            Assert.That(savedSecond.AvatarFileName, Is.Null);
            Assert.That(savedSecond.FlagFileName, Is.Null);
            var score = new ScoreInfo();
            score.RealmUser.OnlineID = 42;
            reloaded.AssociateNewScore(score.ID, first, "osu", 0, "[]");
            reloaded.ApplyProfileNameToDetachedScore(score);
            Assert.That(score.User.OnlineID, Is.EqualTo(42));
            Assert.That(score.User.CountryCode, Is.EqualTo(CountryCode.FR));
            Assert.That(score.User.LocalAvatarFileName, Is.EqualTo(savedFirst.AvatarFileName));
            Assert.That(score.User.LocalFlagFileName, Is.EqualTo(savedFirst.FlagFileName));
            reloaded.SetCountry(first, CountryCode.CA);
            Assert.That(reloaded.GetProfiles().Single(p => p.Id == first).FlagFileName, Is.Null);
        }

        [Test]
        public void TestProfileBestAndMapRankings()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "rank-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            {
                var profiles = new RitsuProfileManager(storage, realm);
                var firstProfile = profiles.ActiveProfileId;
                profiles.AddProfile("Other");
                var secondProfile = profiles.ActiveProfileId;
                var ruleset = CreateRuleset();
                var set = CreateBeatmapSet(ruleset);
                var map = set.Beatmaps[0];
                var otherMap = set.Beatmaps[1];
                map.Hash = "map";
                otherMap.Hash = "other-map";

                ScoreInfo score(long total, double? pp, bool otherBeatmap = false) => new ScoreInfo(otherBeatmap ? otherMap : map, ruleset)
                {
                    TotalScore = total,
                    PP = pp,
                };

                var current = score(100, 200);
                var earlier = score(80, 100);
                var otherBest = score(200, 300, true);
                var unassociated = score(150, 500);
                var otherProfile = score(120, 400);
                var obsolete = score(500, 500);
                obsolete.BeatmapHash = "old-version";

                var mapId = map.ID;
                var currentId = current.ID;
                var earlierId = earlier.ID;
                var otherBestId = otherBest.ID;
                var unassociatedId = unassociated.ID;
                var otherProfileId = otherProfile.ID;
                realm.Write(r =>
                {
                    r.Add(set);
                    foreach (var entry in new[] { current, earlier, otherBest, unassociated, otherProfile, obsolete })
                        r.Add(entry);
                });
                foreach (var id in new[] { currentId, earlierId, otherBestId })
                    profiles.AssociateNewScore(id, firstProfile, "osu", 0, "[]");
                profiles.AssociateNewScore(otherProfileId, secondProfile, "osu", 0, "[]");

                var ranks = profiles.GetResultRanks(currentId, mapId, "osu");
                Assert.That(ranks, Is.Not.Null);
                Assert.That((ranks!.BestPlay, ranks.AllMapScores, ranks.ProfileMapScores), Is.EqualTo((2, 3, 1)));
                Assert.That(ranks.CurrentPP, Is.GreaterThan(ranks.PreviousPP));
                Assert.That(ranks.CurrentPP, Is.EqualTo(profiles.GetStatistics(firstProfile, "osu").PP).Within(0.000001));
                var oldRanks = profiles.GetResultRanks(earlierId, mapId, "osu");
                Assert.That(oldRanks!.BestPlay, Is.Null);
                Assert.That((oldRanks.AllMapScores, oldRanks.ProfileMapScores), Is.EqualTo((4, 2)));
                Assert.That(oldRanks.CurrentPP, Is.EqualTo(oldRanks.PreviousPP).Within(0.000001));
                Assert.That(profiles.GetResultRanks(unassociatedId, mapId, "osu"), Is.Null);
                Assert.That(profiles.GetBestScores(firstProfile, "osu").Select(s => s.ID), Is.EqualTo(new[] { otherBestId, currentId }));
            }
        }

        [Test]
        public void TestBeatmapAndBeatmapSetExclusionsApplyToFutureScoresAndPersist()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "profile-exclusion-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid profileId = profiles.ActiveProfileId;
            var ruleset = CreateRuleset();
            var set = CreateBeatmapSet(ruleset);
            var mapA = set.Beatmaps[0];
            var mapBSet = CreateBeatmapSet(ruleset);
            var mapB = mapBSet.Beatmaps[0];
            var setC = CreateBeatmapSet(ruleset);
            var mapC = setC.Beatmaps[0];

            ScoreInfo createScore(BeatmapInfo beatmap, long total, double pp) => new ScoreInfo(beatmap, ruleset)
            {
                TotalScore = total,
                PP = pp,
            };

            var firstA = createScore(mapA, 100, 100);
            var firstB = createScore(mapB, 100, 200);
            var firstC = createScore(mapC, 100, 150);
            realm.Write(r =>
            {
                r.Add(set);
                r.Add(mapBSet);
                r.Add(setC);
                r.Add(firstA);
                r.Add(firstB);
                r.Add(firstC);
            });
            foreach (var score in new[] { firstA, firstB, firstC })
                profiles.AssociateNewScore(score.ID, profileId, "osu", 0, "[]");

            profiles.SetBeatmapScoresIncludedInPP(profileId, mapA.ID, false, set.ID);
            double ppWithoutA = profiles.GetStatistics(profileId, "osu").PP;
            Assert.That(profiles.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { firstB.ID, firstC.ID }));

            var secondA = createScore(mapA, 200, 500);
            var thirdA = createScore(mapA, 300, 600);
            var secondB = createScore(mapB, 200, 250);
            realm.Write(r =>
            {
                r.Add(secondA);
                r.Add(thirdA);
                r.Add(secondB);
            });
            foreach (var score in new[] { secondA, thirdA, secondB })
                profiles.AssociateNewScore(score.ID, profileId, "osu", 0, "[]");

            var afterPlays = profiles.GetStatistics(profileId, "osu");
            Assert.That(afterPlays.BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { secondB.ID, firstC.ID }));
            Assert.That(afterPlays.PP, Is.GreaterThan(ppWithoutA));
            Assert.That(afterPlays.RecentScores.Select(s => s.ID), Does.Contain(secondA.ID).And.Contain(thirdA.ID));
            Assert.That(afterPlays.PlayCount, Is.EqualTo(6));
            Assert.That(profiles.GetScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { firstA.ID, firstB.ID, firstC.ID, secondA.ID, thirdA.ID, secondB.ID }));
            Assert.That(profiles.IsScoreIncludedInPP(thirdA.ID), Is.False);
            var excludedPersonalBestRanks = profiles.GetResultRanks(thirdA.ID, mapA.ID, "osu");
            Assert.That(excludedPersonalBestRanks, Is.Not.Null);
            Assert.That(excludedPersonalBestRanks!.BestPlay, Is.Null);
            Assert.That(excludedPersonalBestRanks.CurrentPP, Is.EqualTo(excludedPersonalBestRanks.PreviousPP).Within(0.000001));

            profiles = new RitsuProfileManager(storage, realm);
            Assert.That(profiles.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { secondB.ID, firstC.ID }));

            profiles.SetBeatmapScoresIncludedInPP(profileId, mapA.ID, true, set.ID);
            Assert.That(profiles.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { thirdA.ID, secondB.ID, firstC.ID }));
            double ppWithA = profiles.GetStatistics(profileId, "osu").PP;
            profiles.SetBeatmapScoresIncludedInPP(profileId, mapA.ID, false, set.ID);
            var recalculated = profiles.GetStatistics(profileId, "osu");
            Assert.That(recalculated.BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { secondB.ID, firstC.ID }));
            Assert.That(recalculated.PP, Is.EqualTo(afterPlays.PP).Within(0.000001));
            Assert.That(ppWithA, Is.GreaterThan(recalculated.PP));

            profiles.SetBeatmapScoresIncludedInPP(profileId, mapB.ID, false, mapBSet.ID);
            var thirdB = createScore(mapB, 300, 800);
            realm.Write(r => r.Add(thirdB));
            profiles.AssociateNewScore(thirdB.ID, profileId, "osu", 0, "[]");
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { firstC.ID }));
            Assert.That(profiles.IsScoreIncludedInPP(thirdB.ID), Is.False);
            profiles.SetBeatmapScoresIncludedInPP(profileId, mapB.ID, true, mapBSet.ID);
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { thirdB.ID, firstC.ID }));
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Not.Contain(thirdA.ID));

            profiles.SetBeatmapSetScoresIncludedInPP(profileId, setC.ID, false);
            var secondC = createScore(mapC, 200, 700);
            realm.Write(r => r.Add(secondC));
            profiles.AssociateNewScore(secondC.ID, profileId, "osu", 0, "[]");
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Not.Contain(secondC.ID));
            Assert.That(profiles.GetScores(profileId, "osu").Select(s => s.ID), Does.Contain(secondC.ID));
            var afterSetExclusion = new RitsuProfileManager(storage, realm);
            Assert.That(afterSetExclusion.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Does.Not.Contain(secondC.ID));
            afterSetExclusion.SetBeatmapSetScoresIncludedInPP(profileId, setC.ID, true);
            Assert.That(afterSetExclusion.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Contain(secondC.ID));
        }

        [Test]
        public void TestLegacyScoreExclusionsMigrateToPersistentBeatmapRule()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "profile-exclusion-migration-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid profileId = profiles.ActiveProfileId;
            var ruleset = CreateRuleset();
            var set = CreateBeatmapSet(ruleset);
            var map = set.Beatmaps[0];
            var anotherMap = set.Beatmaps[1];
            var oldScore = new ScoreInfo(map, ruleset) { TotalScore = 100, PP = 100, Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
            var scoreSubmittedAfterExclusion = new ScoreInfo(map, ruleset) { TotalScore = 200, PP = 300, Date = oldScore.Date.AddDays(1) };
            var scoreOnOtherMap = new ScoreInfo(anotherMap, ruleset) { TotalScore = 100, PP = 150, Date = oldScore.Date };
            realm.Write(r =>
            {
                r.Add(set);
                r.Add(oldScore);
                r.Add(scoreSubmittedAfterExclusion);
                r.Add(scoreOnOtherMap);
            });
            profiles.AssociateNewScore(oldScore.ID, profileId, "osu", 0, "[]");
            profiles.AssociateNewScore(scoreSubmittedAfterExclusion.ID, profileId, "osu", 0, "[]");
            profiles.AssociateNewScore(scoreOnOtherMap.ID, profileId, "osu", 0, "[]");
            profiles.SetScoreIncludedInPP(oldScore.ID, false);

            string profileStorePath = storage.GetStorageForDirectory("ritsu").GetFullPath("profiles.json", true);
            string profileStore = File.ReadAllText(profileStorePath);
            Assert.That(profileStore, Does.Contain("\"Version\": 2"));
            File.WriteAllText(profileStorePath, profileStore.Replace("\"Version\": 2", "\"Version\": 1", StringComparison.Ordinal));

            var migratedProfiles = new RitsuProfileManager(storage, realm);
            Assert.That(migratedProfiles.IsScoreIncludedInPP(oldScore.ID), Is.False);
            Assert.That(migratedProfiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { scoreOnOtherMap.ID }));
            var laterScore = new ScoreInfo(map, ruleset) { TotalScore = 300, PP = 500, Date = scoreSubmittedAfterExclusion.Date.AddDays(1) };
            realm.Write(r => r.Add(laterScore));
            migratedProfiles.AssociateNewScore(laterScore.ID, profileId, "osu", 0, "[]");

            Assert.That(migratedProfiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { scoreOnOtherMap.ID }));
            Assert.That(migratedProfiles.GetScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { oldScore.ID, scoreSubmittedAfterExclusion.ID, scoreOnOtherMap.ID, laterScore.ID }));
        }
    }
}
