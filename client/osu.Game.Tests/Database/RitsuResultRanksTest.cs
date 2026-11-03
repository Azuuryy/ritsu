// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
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
    }
}
