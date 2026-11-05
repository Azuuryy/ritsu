// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Configuration;
using osu.Game.Offline;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RitsuPlaytimePersistenceTest
    {
        [Test]
        public void TestSelectedMethodPersists()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "playtime-config-" + Guid.NewGuid().ToString("N")));
            using (var config = new OsuConfigManager(storage))
                config.SetValue(OsuSetting.RitsuPlaytimeMode, RitsuPlaytimeMode.DrainLength);
            using (var reloaded = new OsuConfigManager(storage))
                Assert.That(reloaded.Get<RitsuPlaytimeMode>(OsuSetting.RitsuPlaytimeMode), Is.EqualTo(RitsuPlaytimeMode.DrainLength));
        }

        [Test]
        public void TestAssociatedScoresAfterRestartAndNewSubmission()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "playtime-test-" + Guid.NewGuid().ToString("N")));
            Guid profile;
            Guid mapId;
            IBeatmap map() => new Beatmap { HitObjects = { new HitCircle { StartTime = 0 }, new HitCircle { StartTime = 120000 } } };
            double total(RitsuProfileManager manager) => new RitsuPlaytime(_ => map()).Calculate(manager.GetStatistics(profile, "osu").RecentScores, RitsuPlaytimeMode.MapLength).Seconds;
            using (var realm = new RealmAccess(storage, "scores"))
            {
                var profiles = new RitsuProfileManager(storage, realm);
                profile = profiles.ActiveProfileId;
                var ruleset = new RulesetInfo("osu", "osu!", string.Empty, 0) { Available = true };
                var info = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata()) { Hash = "map" };
                mapId = info.ID;
                var first = new ScoreInfo(info, ruleset) { TotalScore = 100 };
                var firstId = first.ID;
                realm.Write(r => r.Add(first));
                profiles.AssociateNewScore(firstId, profile, "osu", 0, "[]");
                Assert.That(total(profiles), Is.EqualTo(120));
            }
            using (var realm = new RealmAccess(storage, "scores"))
            {
                var profiles = new RitsuProfileManager(storage, realm);
                Assert.That(total(profiles), Is.EqualTo(120));
                Guid nextId = Guid.Empty;
                realm.Write(r =>
                {
                    var info = r.Find<BeatmapInfo>(mapId)!;
                    var next = new ScoreInfo(info, info.Ruleset) { TotalScore = 100 };
                    nextId = next.ID;
                    r.Add(next);
                });
                profiles.AssociateNewScore(nextId, profile, "osu", 0, "[]");
                profiles.AssociateNewScore(nextId, profile, "osu", 0, "[]");
                Assert.That(total(profiles), Is.EqualTo(240));
            }
            using (var realm = new RealmAccess(storage, "scores"))
                Assert.That(total(new RitsuProfileManager(storage, realm)), Is.EqualTo(240));
        }
    }
}
