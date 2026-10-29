using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Modosu.Integration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Modosu.Tests.Screens
{
    /// <summary>Verifies the native producer and atomic file without depending on a HUD skin.</summary>
    public partial class TestSceneNativePerformanceExport : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;
        private ModosuPerformanceExport? export;
        private GameplayState state = null!;
        private OsuSpriteText values = null!;
        private double time;

        [Test]
        public void TestNativePerformanceExportWithFsa()
        {
            AddStep("create native FSA session", () =>
            {
                Add(new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = "Modosu native performance export", Font = OsuFont.Default.With(size: 30) },
                        values = new OsuSpriteText { Text = "Loading game difficulty attributes...", Font = OsuFont.Default.With(size: 24) }
                    }
                });
                state = ModosuPerformanceExportTest.createState(true);
                export = new ModosuPerformanceExport(state, () => time);
            });
            AddUntilStep("native difficulty ready", () => export!.Ready.IsCompletedSuccessfully);
            AddStep("apply actual score judgements", () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    var hitObject = state.Beatmap.HitObjects[i];
                    time = hitObject.StartTime;
                    state.ScoreProcessor.ApplyResult(new JudgementResult(hitObject, hitObject.CreateJudgement()) { Type = HitResult.Great });
                }
                export!.Update(force: true);
            });
            AddUntilStep("atomic frame contains game PP", () => readFrame() is { Phase: "play", Pp: > 0, TotalStars: > 0, Hits.Great: 20 });
            AddAssert("current process and FSA retained", () => readFrame() is { } frame && frame.ProcessId == Environment.ProcessId && frame.ModsJson.Contains("FSA"));
            AddStep("show native values", () =>
            {
                var frame = readFrame()!;
                values.Text = $"FSA | {frame.Pp:0.00} PP | {frame.LiveStars:0.00} / {frame.TotalStars:0.00} stars | {frame.Hits.Great} hits";
            });
            AddWaitStep("keep producer active for reader", 15);
        }

        protected override void Update()
        {
            base.Update();
            export?.Update();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing) export?.Dispose();
            base.Dispose(isDisposing);
        }

        private static ModosuPerformanceFrame? readFrame()
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "modosu-tosu", $"{Environment.ProcessId}.json");
                return JsonSerializer.Deserialize<ModosuPerformanceFrame>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
