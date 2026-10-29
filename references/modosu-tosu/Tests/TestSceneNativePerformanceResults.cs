using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Rulesets.Modosu.Integration;
using osu.Game.Rulesets.Modosu.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Modosu.Tests.Screens
{
    public partial class TestSceneNativePerformanceResults : TestSceneOsuPlayer
    {
        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        protected override bool HasCustomSteps => true;
        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(false, true, false);

        [Test]
        public void TestNativePerformanceResultsScoreDate()
        {
            AddStep("import short native test map", () =>
            {
                var map = ModosuPerformanceExportTest.createState(false).Beatmap;
                using var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, leaveOpen: true))
                    new LegacyBeatmapEncoder(map, null, null).Encode(writer);
                stream.Position = 0;
                var imported = beatmapManager.Import(new ImportTask(stream, "native_results.osu")).GetResultSafely();
                Assert.That(imported, Is.Not.Null);
                map.BeatmapInfo = imported!.PerformRead(set => set.Beatmaps.Single()).Detach();

                var ruleset = CreatePlayerRuleset();
                Ruleset.Value = ruleset.RulesetInfo;
                Beatmap.Value = CreateWorkingBeatmap(map);
                SelectedMods.Value = new Mod[] { new OsuModFunSpacingAdjust(), ruleset.GetAutoplayMod()! };
                Player = CreatePlayer(ruleset);
                LoadScreen(Player);
            });
            AddUntilStep("player loaded", () => Player.IsLoaded && Player.Alpha == 1);
            AddUntilStep("results displayed", () => Stack.CurrentScreen is ResultsScreen);
            AddUntilStep("native results exported", () => readFrame() is { Phase: "results", TotalStars: > 0, Pp: not null });
            AddAssert("export uses the prepared score date", () =>
                Stack.CurrentScreen is ResultsScreen { Score: { } score } &&
                score.Date.Year > 2000 && DateTimeOffset.Parse(readFrame()!.Date) == score.Date);
            AddAssert("gameplay score still has its initial date", () => Player.Score.ScoreInfo.Date.Year == 1);
        }

        private static ModosuPerformanceFrame? readFrame()
        {
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "modosu-tosu", $"{Environment.ProcessId}.json");
                return JsonSerializer.Deserialize<ModosuPerformanceFrame>(File.ReadAllText(file), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
