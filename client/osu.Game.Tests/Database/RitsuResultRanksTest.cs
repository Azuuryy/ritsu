// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using NUnit.Framework;
using osu.Game.Offline;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
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
        [TestCase(97.2741, "97.27%")]
        [TestCase(0, "0.00%")]
        [TestCase(100, "100.00%")]
        [SetCulture("en-US")]
        public void TestProfileAccuracyDisplayUsesPercentagePoints(double accuracy, string expected)
        {
            var stats = new RitsuProfileStatistics(0, 0, accuracy, null, Array.Empty<ScoreInfo>(), Array.Empty<ScoreInfo>(), Array.Empty<RitsuPpPoint>());
            Assert.That(stats.DisplayAccuracy, Is.EqualTo(expected));
        }

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
        public Task TestTransferScoreBetweenProfiles() => Task.Run(() =>
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "transfer-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid source = profiles.ActiveProfileId;
            profiles.AddProfile("Destination");
            Guid target = profiles.ActiveProfileId;
            var ruleset = CreateRuleset();
            var set = CreateBeatmapSet(ruleset);
            var score = new ScoreInfo(set.Beatmaps[0], ruleset) { TotalScore = 100, PP = 150 };
            Guid scoreId = score.ID;
            realm.Write(r =>
            {
                r.Add(set);
                r.Add(score);
            });
            profiles.AssociateNewScore(scoreId, source, "osu", 42, "[]");
            profiles.SetScoreIncludedInPP(scoreId, false);
            int changes = 0;
            profiles.Changed += () => changes++;

            var dialogs = new TransferTestDialogOverlay();
            Assert.That(RitsuScoreTransferMenu.Create(profiles, dialogs, Guid.NewGuid()), Is.Null);
            var menu = RitsuScoreTransferMenu.Create(profiles, dialogs, scoreId);
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu!.Items.Select(item => item.Text.Value.ToString()), Is.EqualTo(new[] { "Destination" }));
            menu.Items.Single().Action.Value!();
            Assert.That(dialogs.CurrentDialog, Is.InstanceOf<ConfirmDialog>());

            Assert.That(profiles.TransferScore(scoreId, source, source), Is.False);
            Assert.That(profiles.TransferScore(scoreId, source, Guid.NewGuid()), Is.False);
            Assert.That(profiles.TransferScore(Guid.NewGuid(), source, target), Is.False);
            Assert.That(profiles.TransferScore(scoreId, source, target), Is.True);
            Assert.That(profiles.TransferScore(scoreId, source, target), Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(profiles.GetScores(source, "osu"), Is.Empty);
            Assert.That(profiles.GetScores(target, "osu").Select(s => s.ID), Is.EqualTo(new[] { scoreId }));
            Assert.That(profiles.IsScoreIncludedInPP(scoreId), Is.False);

            var reloaded = new RitsuProfileManager(storage, realm);
            Assert.That(reloaded.GetScoreProfileId(scoreId), Is.EqualTo(target));
            Assert.That(reloaded.IsScoreIncludedInPP(scoreId), Is.False);
            Assert.That(realm.Run(r => r.Find<ScoreInfo>(scoreId)?.TotalScore), Is.EqualTo(100));
        });

        [Test]
        public Task TestAssignStoredScoresToExistingAndNewProfiles() => Task.Run(() =>
        {
            using var storage = new TemporaryNativeStorage("RitsuAssignStoredScoreTest");
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid existingProfileId = profiles.ActiveProfileId;

            var ruleset = CreateRuleset();
            var set = CreateBeatmapSet(ruleset);
            var first = new ScoreInfo(set.Beatmaps[0], ruleset) { TotalScore = 100, PP = 150 };
            var second = new ScoreInfo(set.Beatmaps[0], ruleset) { TotalScore = 200 };
            var deleted = new ScoreInfo(set.Beatmaps[0], ruleset) { DeletePending = true };
            Guid firstId = first.ID, secondId = second.ID, deletedId = deleted.ID;
            realm.Write(r =>
            {
                r.Add(set);
                r.Add(first);
                r.Add(second);
                r.Add(deleted);
            });

            var dialogs = new TransferTestDialogOverlay();
            Assert.That(RitsuScoreTransferMenu.Create(profiles, dialogs, deletedId), Is.Null);
            var menu = RitsuScoreTransferMenu.Create(profiles, dialogs, firstId);
            Assert.That(menu?.Text.Value.ToString(), Is.EqualTo("Assign score to profile"));
            Assert.That(menu!.Items.Count, Is.EqualTo(2));
            menu.Items.Last().Action.Value!();
            Assert.That(dialogs.CurrentDialog, Is.InstanceOf<RitsuInputDialog>());

            Assert.That(profiles.AssignStoredScore(firstId, Guid.NewGuid()), Is.False);
            Assert.That(profiles.AssignStoredScore(firstId, existingProfileId), Is.True);
            Assert.That(profiles.CreateProfileForStoredScore(firstId, "Duplicate"), Is.False);
            Assert.Throws<ArgumentException>(() => profiles.CreateProfileForStoredScore(secondId, profiles.GetActiveProfile().Name));
            Assert.That(profiles.CreateProfileForStoredScore(secondId, "Imported plays"), Is.True);
            Assert.That(profiles.CreateProfileForStoredScore(deletedId, "Deleted"), Is.False);

            var reloaded = new RitsuProfileManager(storage, realm);
            var destination = reloaded.GetProfiles().Single(p => p.Name == "Imported plays");
            Assert.That(reloaded.GetProfiles(), Has.Count.EqualTo(2));
            Assert.That(reloaded.GetScores(existingProfileId, "osu").Single().ID, Is.EqualTo(firstId));
            Assert.That(reloaded.GetScores(destination.Id, "osu").Single().ID, Is.EqualTo(secondId));
            Assert.That(reloaded.ActiveProfileId, Is.EqualTo(existingProfileId));
            Assert.That(realm.Run(r => r.Find<ScoreInfo>(firstId)!.PP), Is.EqualTo(150));
            Assert.That(realm.Run(r => r.All<ScoreInfo>().Count()), Is.EqualTo(3));
        });

        private sealed class TransferTestDialogOverlay : IDialogOverlay
        {
            public PopupDialog? CurrentDialog { get; private set; }
            public void Push(PopupDialog dialog) => CurrentDialog = dialog;
        }

        [Test]
        public Task TestRepeatedScoreSavesAndProfileReads() => Task.Run(() =>
        {
            using var storage = new TemporaryNativeStorage("RitsuProfileSessionTest");
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            var profileId = profiles.ActiveProfileId;
            var ruleset = CreateRuleset();
            var set = CreateBeatmapSet(ruleset);
            var mapId = set.Beatmaps[0].ID;
            realm.Write(r => r.Add(set));
            for (int i = 0; i < 100; i++)
            {
                Guid scoreId = Guid.NewGuid();
                realm.Write(r =>
                {
                    var score = new ScoreInfo(r.Find<BeatmapInfo>(mapId), r.Find<osu.Game.Rulesets.RulesetInfo>("osu"))
                    {
                        ID = scoreId,
                        Hash = Guid.NewGuid().ToString(),
                        TotalScore = 1000 + i,
                        PP = 100 + i,
                        Accuracy = 0.99,
                        Date = DateTimeOffset.UtcNow.AddSeconds(i),
                    };
                    r.Add(score);
                });
                profiles.AssociateNewScore(scoreId, profileId, "osu", 0, "[]");
                var stats = profiles.GetStatistics(profileId, "osu");
                Assert.That(stats.PlayCount, Is.EqualTo(i + 1));
                Assert.That(stats.BestScores.Single().ID, Is.EqualTo(scoreId));
                var ranks = profiles.GetResultRanks(scoreId, mapId, "osu");
                Assert.That(ranks, Is.Not.Null);
                Assert.That(ranks!.ProfileMapScores, Is.EqualTo(1));
                if ((i + 1) % 25 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            var reloaded = new RitsuProfileManager(storage, realm);
            Assert.That(reloaded.GetStatistics(profileId, "osu").PlayCount, Is.EqualTo(100));
        });

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
        public Task TestLocalBeatmapStatusRespectsProfileExclusions() => Task.Run(() =>
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "local-status-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid first = profiles.ActiveProfileId;
            var set = CreateBeatmapSet(CreateRuleset());
            set.Status = BeatmapOnlineStatus.Ranked;
            set.Beatmaps[0].Status = BeatmapOnlineStatus.Ranked;
            Guid setId = set.ID;
            Guid mapId = set.Beatmaps[0].ID;
            realm.Write(r => r.Add(set));
            set = realm.Run(r => r.Find<BeatmapSetInfo>(setId)!.Detach());

            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(set.Beatmaps[0], profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Ranked));
            profiles.SetBeatmapScoresIncludedInPP(first, mapId, false, setId);
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(set.Beatmaps[0], profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Unranked));
            Assert.That(RitsuLocalBeatmapStatus.ForSet(set, profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Ranked));
            profiles.AddProfile("Other");
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(set.Beatmaps[0], profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Ranked));
            Assert.That(set.Beatmaps[0].Status, Is.EqualTo(BeatmapOnlineStatus.Ranked));

            Guid second = profiles.ActiveProfileId;
            profiles.SetBeatmapSetScoresIncludedInPP(second, setId, false);
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(set.Beatmaps[0], profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Unranked));
            profiles.SetBeatmapScoresIncludedInPP(second, mapId, true, setId);
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(set.Beatmaps[0], profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Ranked));
            Assert.That(RitsuLocalBeatmapStatus.ForSet(set, profiles.GetActiveProfile()), Is.EqualTo(BeatmapOnlineStatus.Ranked));
            Assert.That(set.Status, Is.EqualTo(BeatmapOnlineStatus.Ranked));
        });

        [TestCase(BeatmapOnlineStatus.Unranked)]
        [TestCase(BeatmapOnlineStatus.None)]
        [TestCase(BeatmapOnlineStatus.Blacklisted)]
        public void TestLocalApprovalFollowsExclusions(BeatmapOnlineStatus status)
        {
            var set = CreateBeatmapSet(CreateRuleset());
            var first = set.Beatmaps[0];
            var second = set.Beatmaps[1];
            set.Status = first.Status = second.Status = status;
            var profile = new RitsuProfile();

            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(first, null), Is.EqualTo(BeatmapOnlineStatus.Approved));
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(first, profile), Is.EqualTo(BeatmapOnlineStatus.Approved));
            Assert.That(RitsuLocalBeatmapStatus.ForSet(set, profile), Is.EqualTo(BeatmapOnlineStatus.Approved));

            profile.ExcludedBeatmapSetIds.Add(set.ID);
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(first, profile), Is.EqualTo(BeatmapOnlineStatus.Unranked));
            Assert.That(RitsuLocalBeatmapStatus.ForSet(set, profile), Is.EqualTo(BeatmapOnlineStatus.Unranked));

            profile.IncludedBeatmapIds.Add(first.ID);
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(first, profile), Is.EqualTo(BeatmapOnlineStatus.Approved));
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(second, profile), Is.EqualTo(BeatmapOnlineStatus.Unranked));
            Assert.That(RitsuLocalBeatmapStatus.ForSet(set, profile), Is.EqualTo(BeatmapOnlineStatus.Approved));

            profile.ExcludedBeatmapIds.Add(first.ID);
            Assert.That(RitsuLocalBeatmapStatus.ForBeatmap(first, profile), Is.EqualTo(BeatmapOnlineStatus.Unranked));
            Assert.That(RitsuLocalBeatmapStatus.ForSet(set, profile), Is.EqualTo(BeatmapOnlineStatus.Unranked));
            Assert.That(first.Status, Is.EqualTo(status));
            Assert.That(set.Status, Is.EqualTo(status));
        }

        [TestCase(BeatmapOnlineStatus.Unranked)]
        [TestCase(BeatmapOnlineStatus.None)]
        [TestCase(BeatmapOnlineStatus.Blacklisted)]
        public Task TestLocalProfileCountsPPForEveryMapStatus(BeatmapOnlineStatus status) => Task.Run(() =>
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "status-pp-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            var ruleset = CreateRuleset();
            var set = CreateBeatmapSet(ruleset);
            set.Status = status;
            var beatmap = set.Beatmaps[0];
            beatmap.Status = status;
            var score = new ScoreInfo(beatmap, ruleset) { TotalScore = 100, PP = 125 };
            Guid scoreId = score.ID;
            Guid mapId = beatmap.ID;
            Guid setId = set.ID;
            realm.Write(r => { r.Add(set); r.Add(score); });
            Guid profileId = profiles.ActiveProfileId;
            profiles.AssociateNewScore(scoreId, profileId, "osu", 0, "[]");
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Contain(scoreId));
            Assert.That(profiles.GetStatistics(profileId, "osu").PP, Is.GreaterThan(0));
            profiles.SetBeatmapScoresIncludedInPP(profileId, mapId, false, setId);
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Not.Contain(scoreId));
        });

        [Test]
        public void TestBeatmapSetPPStatusUsesLocalDifficulties()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "profile-set-status-test-" + Guid.NewGuid().ToString("N")));
            using var realm = new RealmAccess(storage, "scores");
            var profiles = new RitsuProfileManager(storage, realm);
            Guid profileId = profiles.ActiveProfileId;
            var set = CreateBeatmapSet(CreateRuleset());
            Guid setId = set.ID;
            Guid beatmapId = set.Beatmaps[0].ID;
            realm.Write(r => r.Add(set));

            Assert.That(profiles.AreBeatmapSetScoresIncludedInPP(profileId, setId), Is.True);

            profiles.SetBeatmapScoresIncludedInPP(profileId, beatmapId, false, setId);
            Assert.That(profiles.AreBeatmapSetScoresIncludedInPP(profileId, setId), Is.False);

            profiles.SetBeatmapSetScoresIncludedInPP(profileId, setId, true);
            Assert.That(profiles.AreBeatmapSetScoresIncludedInPP(profileId, setId), Is.True);
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
            mapA.Hash = "map-a";
            mapB.Hash = "map-b";
            mapC.Hash = "map-c";

            ScoreInfo createScore(BeatmapInfo beatmap, long total, double pp) => new ScoreInfo(beatmap, ruleset)
            {
                TotalScore = total,
                PP = pp,
            };

            var firstA = createScore(mapA, 100, 100);
            var firstB = createScore(mapB, 100, 200);
            var firstC = createScore(mapC, 100, 150);
            Guid mapAId = mapA.ID, mapBId = mapB.ID, mapCId = mapC.ID;
            Guid setAId = set.ID, setBId = mapBSet.ID, setCId = setC.ID;
            Guid firstAId = firstA.ID, firstBId = firstB.ID, firstCId = firstC.ID;
            realm.Write(r =>
            {
                r.Add(set);
                r.Add(mapBSet);
                r.Add(setC);
                r.Add(firstA);
                r.Add(firstB);
                r.Add(firstC);
            });
            foreach (Guid scoreId in new[] { firstAId, firstBId, firstCId })
                profiles.AssociateNewScore(scoreId, profileId, "osu", 0, "[]");

            profiles.SetBeatmapScoresIncludedInPP(profileId, mapAId, false, setAId);
            double ppWithoutA = profiles.GetStatistics(profileId, "osu").PP;
            Assert.That(profiles.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { firstBId, firstCId }));

            Guid secondAId = addScore(mapAId, 200, 500);
            Guid thirdAId = addScore(mapAId, 300, 600);
            Guid secondBId = addScore(mapBId, 200, 250);

            var afterPlays = profiles.GetStatistics(profileId, "osu");
            Assert.That(afterPlays.BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { secondBId, firstCId }));
            Assert.That(afterPlays.PP, Is.GreaterThan(ppWithoutA));
            Assert.That(profiles.GetPerformancePoints(profileId, "osu"), Is.EqualTo(afterPlays.PP));
            Assert.That(afterPlays.RecentScores.Select(s => s.ID), Does.Contain(secondAId).And.Contain(thirdAId));
            Assert.That(afterPlays.PlayCount, Is.EqualTo(6));
            Assert.That(profiles.GetScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { firstAId, firstBId, firstCId, secondAId, thirdAId, secondBId }));
            Assert.That(profiles.IsScoreIncludedInPP(thirdAId), Is.False);
            var excludedPersonalBestRanks = profiles.GetResultRanks(thirdAId, mapAId, "osu");
            Assert.That(excludedPersonalBestRanks, Is.Not.Null);
            Assert.That(excludedPersonalBestRanks!.BestPlay, Is.Null);
            Assert.That(excludedPersonalBestRanks.CurrentPP, Is.EqualTo(excludedPersonalBestRanks.PreviousPP).Within(0.000001));

            profiles = new RitsuProfileManager(storage, realm);
            Assert.That(profiles.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { secondBId, firstCId }));

            profiles.SetBeatmapScoresIncludedInPP(profileId, mapAId, true, setAId);
            Assert.That(profiles.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { thirdAId, secondBId, firstCId }));
            double ppWithA = profiles.GetStatistics(profileId, "osu").PP;
            profiles.SetBeatmapScoresIncludedInPP(profileId, mapAId, false, setAId);
            var recalculated = profiles.GetStatistics(profileId, "osu");
            Assert.That(recalculated.BestScores.Select(s => s.ID), Is.EquivalentTo(new[] { secondBId, firstCId }));
            Assert.That(recalculated.PP, Is.EqualTo(afterPlays.PP).Within(0.000001));
            Assert.That(ppWithA, Is.GreaterThan(recalculated.PP));

            profiles.SetBeatmapScoresIncludedInPP(profileId, mapBId, false, setBId);
            Guid thirdBId = addScore(mapBId, 300, 800);
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { firstCId }));
            Assert.That(profiles.IsScoreIncludedInPP(thirdBId), Is.False);
            profiles.SetBeatmapScoresIncludedInPP(profileId, mapBId, true, setBId);
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { thirdBId, firstCId }));
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Not.Contain(thirdAId));

            profiles.SetBeatmapSetScoresIncludedInPP(profileId, setCId, false);
            Guid secondCId = addScore(mapCId, 200, 700);
            Assert.That(profiles.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Not.Contain(secondCId));
            Assert.That(profiles.GetScores(profileId, "osu").Select(s => s.ID), Does.Contain(secondCId));
            var afterSetExclusion = new RitsuProfileManager(storage, realm);
            Assert.That(afterSetExclusion.GetStatistics(profileId, "osu").BestScores.Select(s => s.ID), Does.Not.Contain(secondCId));
            afterSetExclusion.SetBeatmapSetScoresIncludedInPP(profileId, setCId, true);
            Assert.That(afterSetExclusion.GetBestScores(profileId, "osu").Select(s => s.ID), Does.Contain(secondCId));

            Guid addScore(Guid beatmapId, long total, double pp)
            {
                Guid id = Guid.NewGuid();
                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(beatmapId)!;
                    r.Add(new ScoreInfo(beatmap, beatmap.Ruleset) { ID = id, TotalScore = total, PP = pp });
                });
                profiles.AssociateNewScore(id, profileId, "osu", 0, "[]");
                return id;
            }
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
            Guid mapId = map.ID;
            Guid oldScoreId = oldScore.ID, submittedAfterExclusionId = scoreSubmittedAfterExclusion.ID, otherMapScoreId = scoreOnOtherMap.ID;
            DateTimeOffset laterDate = scoreSubmittedAfterExclusion.Date.AddDays(1);
            realm.Write(r =>
            {
                r.Add(set);
                r.Add(oldScore);
                r.Add(scoreSubmittedAfterExclusion);
                r.Add(scoreOnOtherMap);
            });
            profiles.AssociateNewScore(oldScoreId, profileId, "osu", 0, "[]");
            profiles.AssociateNewScore(submittedAfterExclusionId, profileId, "osu", 0, "[]");
            profiles.AssociateNewScore(otherMapScoreId, profileId, "osu", 0, "[]");
            profiles.SetScoreIncludedInPP(oldScoreId, false);

            string profileStorePath = storage.GetStorageForDirectory("ritsu").GetFullPath("profiles.json", true);
            string profileStore = File.ReadAllText(profileStorePath);
            Assert.That(profileStore, Does.Contain("\"Version\": 2"));
            File.WriteAllText(profileStorePath, profileStore.Replace("\"Version\": 2", "\"Version\": 1", StringComparison.Ordinal));

            var migratedProfiles = new RitsuProfileManager(storage, realm);
            Assert.That(migratedProfiles.IsScoreIncludedInPP(oldScoreId), Is.False);
            Assert.That(migratedProfiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { otherMapScoreId }));
            Guid laterScoreId = Guid.NewGuid();
            realm.Write(r =>
            {
                var beatmap = r.Find<BeatmapInfo>(mapId)!;
                r.Add(new ScoreInfo(beatmap, beatmap.Ruleset) { ID = laterScoreId, TotalScore = 300, PP = 500, Date = laterDate });
            });
            migratedProfiles.AssociateNewScore(laterScoreId, profileId, "osu", 0, "[]");

            Assert.That(migratedProfiles.GetBestScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { otherMapScoreId }));
            Assert.That(migratedProfiles.GetScores(profileId, "osu").Select(s => s.ID), Is.EquivalentTo(new[] { oldScoreId, submittedAfterExclusionId, otherMapScoreId, laterScoreId }));
        }
    }
}
