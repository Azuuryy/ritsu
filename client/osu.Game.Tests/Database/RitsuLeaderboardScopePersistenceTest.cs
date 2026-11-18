// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RitsuLeaderboardScopePersistenceTest
    {
        [Test]
        public void TestSelectedScopePersists()
        {
            var storage = new NativeStorage(Path.Combine(Environment.CurrentDirectory, ".cache", "leaderboard-scope-" + Guid.NewGuid().ToString("N")));
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<BeatmapLeaderboardScope>(OsuSetting.RitsuLeaderboardScope), Is.EqualTo(BeatmapLeaderboardScope.Local));
                config.SetValue(OsuSetting.RitsuLeaderboardScope, BeatmapLeaderboardScope.LocalAll);
            }

            using (var reloaded = new OsuConfigManager(storage))
                Assert.That(reloaded.Get<BeatmapLeaderboardScope>(OsuSetting.RitsuLeaderboardScope), Is.EqualTo(BeatmapLeaderboardScope.LocalAll));
        }
    }
}
