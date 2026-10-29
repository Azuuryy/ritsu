using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Modosu.Integration;
using osu.Game.Rulesets.Modosu.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Modosu.Tests.Screens
{
    public partial class TestSceneNativePerformanceSelection : OsuTestScene
    {
        [Resolved] private BeatmapManager beatmapManager { get; set; } = null!;
        private OsuModFunSpacingAdjust fsa = null!;
        private OsuSpriteText values = null!;
        private string initialMods = string.Empty;
        private double initialStars;

        [Test]
        public void TestNativePerformanceSelectionSettingChanges()
        {
            AddStep("import and select native test map", () =>
            {
                var map = ModosuPerformanceExportTest.createState(false).Beatmap;
                using var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, leaveOpen: true))
                    new LegacyBeatmapEncoder(map, null, null).Encode(writer);
                stream.Position = 0;
                var imported = beatmapManager.Import(new ImportTask(stream, "native_selection.osu")).GetResultSafely();
                map.BeatmapInfo = imported!.PerformRead(set => set.Beatmaps.Single()).Detach();
                Ruleset.Value = new ModosuRuleset().RulesetInfo;
                Beatmap.Value = CreateWorkingBeatmap(map);
                fsa = new OsuModFunSpacingAdjust();
                SelectedMods.Value = new Mod[] { fsa };
                Add(new ModosuSelectionPerformanceExport());
                Add(new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = "Modosu selection export", Font = OsuFont.Default.With(size: 28) },
                        values = new OsuSpriteText { Text = "Calculating native selection...", Font = OsuFont.Default.With(size: 24) }
                    }
                });
            });
            AddUntilStep("stars and maximum exported before gameplay", () => readFrame() is { Phase: "selection", MaximumPp: > 0, TotalStars: > 0 });
            AddStep("capture native values", () =>
            {
                var frame = readFrame()!;
                initialMods = frame.ModsJson;
                initialStars = frame.TotalStars!.Value;
                values.Text = $"Selection: {frame.TotalStars:0.00} stars | SS {frame.MaximumPp:0.00} PP";
            });
            AddStep("edit existing FSA settings", () => { fsa.StartSpacing.Value = 2; fsa.EndSpacing.Value = 3; });
            AddUntilStep("new settings produce new difficulty", () => readFrame() is { Phase: "selection", MaximumPp: > 0 } frame &&
                frame.ModsJson != initialMods && Math.Abs(frame.TotalStars!.Value - initialStars) > 0.001);
            AddStep("show updated selection", () =>
            {
                var frame = readFrame()!;
                values.Text = $"FSA settings updated: {frame.TotalStars:0.00} stars | SS {frame.MaximumPp:0.00} PP";
            });
            AddWaitStep("keep selection heartbeat active", 2);
        }

        private static ModosuPerformanceFrame? readFrame()
        {
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "modosu-tosu", $"{Environment.ProcessId}.selection.json");
                return JsonSerializer.Deserialize<ModosuPerformanceFrame>(File.ReadAllText(file), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (IOException) { return null; }
        }
    }
}
