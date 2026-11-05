using osu.Framework.Allocation;
using System.Diagnostics;
using System.Reflection;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Screens.Menu;
using SixLabors.ImageSharp;

internal partial class CatalogueSmokeGame : OsuGame
{
    private readonly string output;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private int stage;
    private double nextAction;
    private Task pending = Task.CompletedTask;
    private osu.Game.Beatmaps.Drawables.Cards.Buttons.PlayButton? previewButton;
    private bool inputDisabled;

    public CatalogueSmokeGame(string output) => this.output = output;

    public override void SetHost(GameHost host)
    {
        Storage = host.GetStorage(Path.Combine(output, "profile"));
        base.SetHost(host);
        LocalConfig.SetValue(OsuSetting.ShowFirstRunSetup, false);
        LocalConfig.SetValue(OsuSetting.ShowToriiWelcome, false);
    }

    protected override void Update()
    {
        base.Update();
        if (stage == 9) return;
        if (!inputDisabled && !Host.AvailableInputHandlers.IsDefaultOrEmpty)
        {
            foreach (var handler in Host.AvailableInputHandlers) handler.Enabled.Value = false;
            inputDisabled = true;
        }
        try
        {
            if (elapsed.Elapsed.TotalSeconds > 150) throw new TimeoutException($"Catalogue smoke timed out at stage {stage}");
            if (!pending.IsCompleted || Time.Current < nextAction) return;
            pending.GetAwaiter().GetResult();
            if (stage == 0)
            {
                if (ScreenStack?.CurrentScreen is not MainMenu menu || !menu.IsLoaded || elapsed.Elapsed.TotalSeconds < 10) return;
                ToggleBeatmapListing();
                stage = 1;
                nextAction = Time.Current + 2500;
                return;
            }
            var overlay = (BeatmapListingOverlay?)typeof(OsuGame).GetField("beatmapListing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this);
            if (overlay == null || !overlay.IsLoaded) return;
            var panel = (Container)typeof(BeatmapListingOverlay).GetField("panelTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            var catalogue = (RitsuBeatmapCatalogue)panel.Child;
            if (!catalogue.IsLoaded) return;
            if (stage == 1)
            {
                pending = Capture("catalogue-before-search.png");
                stage = 2;
                return;
            }
            if (stage == 2)
            {
                var search = (BeatmapListingSearchControl)typeof(RitsuBeatmapCatalogue).GetField("filters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(catalogue)!;
                search.General.Clear();
                search.Extra.Add(SearchExtra.Video);
                typeof(RitsuBeatmapCatalogue).GetMethod("startSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(catalogue, new object[] { false });
                stage = 3;
                nextAction = Time.Current + 2000;
                return;
            }
            var results = (FillFlowContainer<BeatmapCard>)typeof(RitsuBeatmapCatalogue).GetField("results", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(catalogue)!;
            if (stage == 3)
            {
                if (results.Count == 0) return;
                stage = 4;
                nextAction = Time.Current + 5000;
                return;
            }
            if (stage == 4)
            {
                pending = Capture("catalogue-results.png");
                stage = 5;
                return;
            }
            if (stage == 5)
            {
                var card = results.First(c => !c.BeatmapSet.IsLocal && c.BeatmapSet.HasVideo);
                if (!card.ContextMenuItems.Any(i => i.Text.Value.ToString().Contains("Copy osu!")) || !card.ContextMenuItems.Any(i => i.Text.Value.ToString().Contains("Copy Torii"))) throw new Exception("Missing beatmap link actions");
                if (card.Scale.X <= 1 || Math.Abs(results.ScreenSpaceDrawQuad.Centre.X - results.Parent!.ScreenSpaceDrawQuad.Centre.X) > 2) throw new Exception("Grid is not enlarged and centred");
                card.Action!();
                stage = 6;
                nextAction = Time.Current + 3000;
                return;
            }
            if (stage == 6)
            {
                var detail = Dependencies.Get<BeatmapSetOverlay>();
                if (detail.State.Value != Visibility.Visible || !detail.Header.HeaderContent.DownloadButtonsVisible) return;
                var buttons = (FillFlowContainer)typeof(osu.Game.Overlays.BeatmapSet.BeatmapSetHeaderContent).GetField("downloadButtonsContainer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(detail.Header.HeaderContent)!;
                if (buttons.Count != 2) throw new Exception("Expected with-video and no-video buttons");
                var content = detail.Header.HeaderContent;
                var author = (osu.Framework.Graphics.Drawable)typeof(osu.Game.Overlays.BeatmapSet.BeatmapSetHeaderContent).GetField("author", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(content)!;
                if (buttons.ScreenSpaceDrawQuad.TopLeft.X < author.ScreenSpaceDrawQuad.TopRight.X - 1 || Math.Abs(buttons.ScreenSpaceDrawQuad.Centre.Y - author.ScreenSpaceDrawQuad.Centre.Y) > 10) throw new Exception("Download buttons are not beside the mapper");
                var rating = (osu.Framework.Graphics.Drawable)typeof(osu.Game.Overlays.BeatmapSet.Details).GetField("ratingBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(content.Details)!;
                if (rating.IsPresent) throw new Exception("Unavailable user rating remains visible");

                Console.WriteLine($"Details: mode={detail.Header.RulesetSelector.Current.Value?.OnlineID}, maps={detail.Header.HeaderContent.Picker.BeatmapSet?.Beatmaps.Length}, selected={detail.Header.HeaderContent.Picker.Beatmap.Value?.OnlineID}");
                stage = 7;
                nextAction = Time.Current + 3000;
                return;
            }
            if (stage == 7)
            {
                if (Dependencies.Get<BeatmapSetOverlay>().Header.HeaderContent.Picker.Beatmap.Value == null) throw new Exception("No difficulty selected in beatmap details");
                pending = Capture("catalogue-details.png");
                stage = 8;
                return;
            }
            if (stage == 8)
            {
                Console.WriteLine("PASS: centred enlarged cards, context links, native details and two video download choices");
                stage = 9;
                Environment.ExitCode = 0;
                Host.Exit();
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            Environment.ExitCode = 1;
            Host.Exit();
            stage = 9;
        }
    }

    private Task Capture(string name) => Task.Run(async () =>
    {
        using var image = await Host.TakeScreenshotAsync();
        await image.SaveAsPngAsync(Path.Combine(output, name));
    });
}
