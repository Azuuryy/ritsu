// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Tests.Resources;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.LocalPriority;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneProfileUnfinishedPlays : OsuGameTestScene
    {
        private LocalProfileManager manager => Game.Dependencies.Get<LocalProfileManager>();
        private LocalProfileOverlay overlay => Game.ChildrenOfType<LocalProfileOverlay>().Single();

        private OsuClickableContainer[] failedRows() => overlay.ChildrenOfType<OsuClickableContainer>()
                                                              .Where(d => d.GetType().Name == "ScoreRow")
                                                              .Where(r => ((osu.Game.Scoring.ScoreInfo)r.GetType().GetField("score", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                                                                                                         .GetValue(r)!).Rank == osu.Game.Scoring.ScoreRank.F)
                                                              .ToArray();

        [Test]
        public void TestClickingFailedPlayShowsItsBeatmap()
        {
            LocalProfile profile = null!;
            BeatmapInfo target = null!;

            AddStep("create profile", () =>
            {
                profile = manager.CreateDefaultProfile();
                manager.Switch(profile.Id);
            });
            // Ritsu offline keeps imported archives, so BeatmapImportHelper's "temporary file deleted" check can't pass here.
            AddStep("import beatmap", () => Game.BeatmapManager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).WaitSafely());
            AddStep("record a failed play on the last osu! difficulty, and one on a missing beatmap", () =>
            {
                target = Game.Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapInfo>().AsEnumerable().Where(b => b.Ruleset.ShortName == "osu").Detach().Last());

                foreach (string md5 in new[] { "0123456789abcdef0123456789abcdef", target.MD5Hash })
                {
                    manager.Store.RecordPlay(profile.UserId, "osu", 20, new Dictionary<HitResult, int> { [HitResult.Great] = 10 }, md5, new LocalUnfinishedPlay
                    {
                        UserId = profile.UserId,
                        Ruleset = "osu",
                        BeatmapMD5 = md5,
                        Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = 10, [HitResult.Miss] = 1 },
                        TotalScore = 12345,
                        MaxCombo = 10,
                        Accuracy = 0.9,
                        Date = DateTimeOffset.Now,
                    });
                }
            });
            AddAssert("another beatmap selected", () => Game.Beatmap.Value.BeatmapInfo.MD5Hash, () => Is.Not.EqualTo(target.MD5Hash));
            AddUntilStep("page ready", () => Game.ChildrenOfType<LocalProfileOverlay>().SingleOrDefault()?.IsLoaded == true);
            AddStep("open page", () => overlay.Show());
            AddUntilStep("two failed plays shown", () => failedRows().Length, () => Is.EqualTo(2));
            AddAssert("the one with a beatmap is clickable, the other isn't", () => failedRows().Select(r => r.Action != null).OrderBy(b => b),
                () => Is.EqualTo(new[] { false, true }));
            AddStep("click the one on the missing beatmap", () => failedRows().Single(r => r.Action == null).TriggerClick());
            AddWaitStep("wait", 5);
            AddAssert("nothing happened", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("click the failed play", () => failedRows().Single(r => r.Action != null).TriggerClick());
            AddUntilStep("at song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddUntilStep("its beatmap selected", () => Game.Beatmap.Value.BeatmapInfo.MD5Hash, () => Is.EqualTo(target.MD5Hash));
            AddAssert("no results screen", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
        }
    }
}
