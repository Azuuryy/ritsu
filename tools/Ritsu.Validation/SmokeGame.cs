using System.Diagnostics;
using System.Text.Json;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using SixLabors.ImageSharp;

internal partial class SmokeGame : OsuGame
{
    private readonly string fixtures;
    private readonly string evidence;
    private readonly NetworkAudit audit;
    private readonly bool reopen;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly List<string> checks = new();
    private Task pending = Task.CompletedTask;
    private int stage;
    private Player? player;
    private Score? savedScore;
    private bool complete;
    private double nextAction;

    public SmokeGame(string fixtures, string evidence, NetworkAudit audit, bool reopen = false)
    {
        this.fixtures = fixtures;
        this.evidence = evidence;
        this.audit = audit;
        this.reopen = reopen;
        Directory.CreateDirectory(evidence);
    }

    public override void SetHost(GameHost host)
    {
        // A test run must not reuse the baseline host's imported library.
        Storage = host.GetStorage(Path.Combine(evidence, "profile"));
        base.SetHost(host);
        LocalConfig.SetValue(OsuSetting.ShowFirstRunSetup, false);
        LocalConfig.SetValue(OsuSetting.ShowToriiWelcome, false);
    }

    protected override void Update()
    {
        base.Update();
        if (complete) return;
        try
        {
            if (elapsed.Elapsed.TotalSeconds > 150) throw new TimeoutException($"Smoke test timed out at stage {stage} ({ScreenStack?.CurrentScreen?.GetType().Name})");
            if (!pending.IsCompleted || Time.Current < nextAction) return;
            pending.GetAwaiter().GetResult();
            switch (stage)
            {
                case 0:
                    if (ScreenStack?.CurrentScreen is not MainMenu menu || !menu.IsLoaded) return;
                    if (elapsed.Elapsed.TotalSeconds < 10) return;
                    Check(!API.IsLoggedIn, "menu available without an account");
                    if (reopen)
                    {
                        var persistedInfo = ScoreManager.Query(s => s.Hash != "") ?? throw new Exception("Score missing after restart");
                        savedScore = ScoreManager.GetScore(persistedInfo) ?? throw new Exception("Replay missing after restart");
                        Check(savedScore.Replay?.Frames.Count > 0, "score and replay persisted across process restart");
                        Beatmap.Value = BeatmapManager.GetWorkingBeatmap(persistedInfo.BeatmapInfo!);
                        Ruleset.Value = persistedInfo.Ruleset;
                        SelectedMods.Value = persistedInfo.Mods;
                        ScreenStack.Push(new SoloSongSelect());
                        stage = 30;
                        nextAction = Time.Current + 1500;
                        break;
                    }
                    pending = Capture("01-menu");
                    stage++;
                    break;
                case 1:
                    BeatmapManager.PresentImport = null;
                    pending = Task.WhenAll(BeatmapManager.Import(Path.Combine(fixtures, "ritsu-validation.osz")), SkinManager.Import(Path.Combine(fixtures, "ritsu-validation.osk")));
                    stage++;
                    break;
                case 2:
                    var beatmap = BeatmapManager.QueryBeatmap("Metadata.Title == $0", "Ritsu offline validation") ?? throw new Exception("Imported beatmap missing");
                    var skin = SkinManager.GetAllUsableSkins().FirstOrDefault(s => s.PerformRead(i => i.Name.Contains("ritsu-validation", StringComparison.Ordinal) || i.Name.Contains("Ritsu validation skin", StringComparison.Ordinal))) ?? throw new Exception("Imported skin missing");
                    Check(true, "beatmap and legacy skin imported locally");
                    Check(File.Exists(Path.Combine(fixtures, "ritsu-validation.osz")) && File.Exists(Path.Combine(fixtures, "ritsu-validation.osk")), "source archives are preserved after import");
                    LocalConfig.SetValue(OsuSetting.Skin, skin.PerformRead(s => s.ID.ToString()));
                    Beatmap.Value = BeatmapManager.GetWorkingBeatmap(beatmap);
                    Ruleset.Value = beatmap.Ruleset;
                    ScreenStack.Push(new SoloSongSelect());
                    stage++;
                    nextAction = Time.Current + 2000;
                    break;
                case 3:
                    if (ScreenStack.CurrentScreen is not SoloSongSelect select || !select.IsLoaded) return;
                    pending = Capture("02-selection");
                    stage++;
                    break;
                case 4:
                    SelectedMods.Value = new[] { new OsuModNoFail() };
                    player = new BackgroundSoloPlayer();
                    ScreenStack.Push(new BackgroundPlayerLoader(() => player));
                    stage++;
                    nextAction = Time.Current + 4500;
                    break;
                case 5:
                    if (ScreenStack.CurrentScreen is not Player p || !p.LoadedBeatmapSuccessfully) return;
                    Check(true, "normal solo gameplay loaded (NF, no synthetic input)");
                    pending = Capture("03-gameplay");
                    stage++;
                    break;
                case 6:
                    if (ScreenStack.CurrentScreen is not ResultsScreen results || !results.IsLoaded) return;
                    var scoreInfo = ScoreManager.Query(s => s.Hash != "") ?? throw new Exception("Local score was not saved");
                    savedScore = ScoreManager.GetScore(scoreInfo) ?? throw new Exception("Saved replay was not readable");
                    Check(savedScore.Replay != null && savedScore.Replay.Frames.Count > 0, "normal score and recorded replay saved");
                    stage = 18;
                    nextAction = Time.Current + 1800;
                    break;
                case 18:
                    pending = Capture("04-results");
                    stage = 7;
                    break;
                case 7:
                    pending = ScoreManager.Export(savedScore!.ScoreInfo);
                    stage++;
                    break;
                case 8:
                    string exportedReplay = Directory.GetFiles(Storage.GetFullPath("exports"), "*.osr", SearchOption.AllDirectories).FirstOrDefault() ?? throw new Exception("Exported replay missing");
                    Check(true, "replay exported to an OSR file");
                    pending = ScoreManager.Import(exportedReplay);
                    stage = 17;
                    break;
                case 17:
                    Check(true, "exported OSR re-import completed");
                    PerformFromScreen(screen => screen.Push(new BackgroundPlayerLoader(() => new BackgroundReplayPlayer(savedScore!))), new[] { typeof(SoloSongSelect) });
                    stage = 9;
                    nextAction = Time.Current + 5000;
                    break;
                case 9:
                    if (ScreenStack.CurrentScreen is not ReplayPlayer replay || !replay.LoadedBeatmapSuccessfully) return;
                    Check(true, "saved replay playback loaded");
                    pending = Capture("05-replay");
                    stage++;
                    break;
                case 10:
                    if (ScreenStack.CurrentScreen is not ResultsScreen) return;
                    PerformFromScreen(screen => screen.Push(new EditorLoader()), new[] { typeof(SoloSongSelect) });
                    stage++;
                    nextAction = Time.Current + 4000;
                    break;
                case 11:
                    if (ScreenStack.CurrentScreen is not Editor editor || !editor.IsLoaded) return;
                    Check(true, "existing beatmap editor loaded");
                    pending = Capture("06-beatmap-editor");
                    stage++;
                    break;
                case 12:
                    PerformFromScreen(_ => { Dependencies.Get<SkinEditorOverlay>().Show(); }, new[] { typeof(SoloSongSelect) });
                    stage++;
                    nextAction = Time.Current + 4000;
                    break;
                case 13:
                    var skinEditor = Dependencies.Get<SkinEditorOverlay>();
                    if (skinEditor.State.Value != osu.Framework.Graphics.Containers.Visibility.Visible) return;
                    Check(true, "existing skin layout editor loaded");
                    pending = Capture("07-skin-editor");
                    stage++;
                    break;
                case 14:
                    Dependencies.Get<SkinEditorOverlay>().Hide();
                    Dependencies.Get<SettingsOverlay>().Show();
                    stage++;
                    nextAction = Time.Current + 1500;
                    break;
                case 15:
                    pending = Capture("08-settings");
                    stage++;
                    break;
                case 16:
                    Check(audit.AttemptCount == 0, "no observed HTTP, DNS or socket attempt throughout the session");
                    Finish(null);
                    break;
                case 30:
                    if (ScreenStack.CurrentScreen is not SoloSongSelect) return;
                    ScreenStack.Push(new BackgroundPlayerLoader(() => new BackgroundReplayPlayer(savedScore!)));
                    stage = 31;
                    nextAction = Time.Current + 4500;
                    break;
                case 31:
                    if (ScreenStack.CurrentScreen is not ReplayPlayer restarted || !restarted.LoadedBeatmapSuccessfully) return;
                    Check(true, "replay playback loaded after process restart");
                    pending = Capture("09-replay-after-restart");
                    stage = 32;
                    nextAction = Time.Current + 1800;
                    break;
                case 32:
                    Check(audit.AttemptCount == 0, "no observed network attempt after restart");
                    Finish(null);
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish(exception.ToString());
        }
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks.Add(message);
        Console.WriteLine("PASS: " + message);
    }

    private Task Capture(string name) => Task.Run(async () =>
    {
        using var screenshot = await Host.TakeScreenshotAsync();
        await screenshot.SaveAsPngAsync(Path.Combine(evidence, name + ".png"));
    });

    private void Finish(string? error)
    {
        complete = true;
        audit.Save(Path.Combine(evidence, reopen ? "reopen-network.json" : "network.json"));
        File.WriteAllText(Path.Combine(evidence, reopen ? "reopen-result.json" : "result.json"), JsonSerializer.Serialize(new { success = error == null, stage, checks, error, seconds = elapsed.Elapsed.TotalSeconds }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = error == null ? 0 : 1;
        Console.WriteLine(error ?? "PASS: full offline smoke test");
        Host.Exit();
    }

    private partial class BackgroundPlayerLoader : PlayerLoader
    {
        public BackgroundPlayerLoader(Func<Player> createPlayer) : base(createPlayer)
        {
            WindowShouldBeActiveForGameplayStart = false;
        }
    }

    private partial class BackgroundSoloPlayer : SoloPlayer
    {
        protected override bool PauseOnFocusLost => false;
        public BackgroundSoloPlayer() : base(new PlayerConfiguration { AutomaticallySkipIntro = true, AllowUserInteraction = false }) { }
    }

    private partial class BackgroundReplayPlayer : ReplayPlayer
    {
        protected override bool PauseOnFocusLost => false;
        public BackgroundReplayPlayer(Score score) : base(score, new PlayerConfiguration { AutomaticallySkipIntro = true, AllowUserInteraction = false }) { }
    }
}
