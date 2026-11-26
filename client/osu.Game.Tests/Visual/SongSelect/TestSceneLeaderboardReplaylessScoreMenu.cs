// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneLeaderboardReplaylessScoreMenu : SongSelectComponentsTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider { get; set; } = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private BeatmapLeaderboardScore row = null!;

        private string[] menuTexts() => ((IHasContextMenu)row).ContextMenuItems!
                                                                .Where(i => i is not OsuMenuItemSpacer)
                                                                .Select(i => i.Text.Value.ToString()).ToArray();

        private void show(System.Func<ScoreInfo> score) => AddStep("show score", () => Child = new PopoverContainer
        {
            RelativeSizeAxes = osu.Framework.Graphics.Axes.Both,
            Child = row = new BeatmapLeaderboardScore(score()) { Rank = 1 },
        });

        [Test]
        public void TestSavedScoreWithoutReplay()
        {
            ScoreInfo saved = null!;

            AddStep("save score without replay", () =>
            {
                var score = TestResources.CreateTestScoreInfo();

                // Stored like ScoreImporter stores them.
                score.StatisticsJson = Newtonsoft.Json.JsonConvert.SerializeObject(score.Statistics);
                score.MaximumStatisticsJson = Newtonsoft.Json.JsonConvert.SerializeObject(score.MaximumStatistics);

                Realm.Write(r =>
                {
                    var ruleset = r.Find<RulesetInfo>(score.Ruleset.ShortName)!;
                    score.Ruleset = ruleset;
                    score.BeatmapInfo!.Ruleset = ruleset;
                    score.BeatmapInfo.BeatmapSet!.Beatmaps.Add(score.BeatmapInfo);
                    r.Add(score.BeatmapInfo.BeatmapSet);
                    r.Add(score);
                });

                saved = Realm.Run(r => r.Find<ScoreInfo>(score.ID)!.Detach());
            });

            AddAssert("in database, no replay file", () => Realm.Run(r => r.Find<ScoreInfo>(saved.ID)!.Files.Count == 0));
            show(() => saved);
            AddAssert("delete offered", () => menuTexts().Contains("Delete"));
            AddAssert("transfer offered once", () => menuTexts().Count(t => t == "Transfer score"), () => Is.EqualTo(1));
            AddAssert("no export / replay", () => !menuTexts().Contains("Export") && !menuTexts().Contains("Watch replay"));
        }

        [Test]
        public void TestOnlineScore()
        {
            show(() => TestResources.CreateTestScoreInfo());
            // Ritsu offers "Transfer score" on every leaderboard score, so only Delete is checked here.
            AddAssert("no delete", () => !menuTexts().Contains("Delete"));
        }
    }
}
