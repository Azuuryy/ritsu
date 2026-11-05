using System.Reflection;
using osu.Framework;
using osu.Framework.Platform;

if (args.Contains("--catalogue-live"))
{
    using var catalogue = new osu.Game.Overlays.BeatmapListing.RitsuCatalogueClient();
    var first = await catalogue.SearchAsync("", null, CancellationToken.None);
    if (first.BeatmapSets.Count == 0 || string.IsNullOrEmpty(first.Cursor)) throw new Exception("Torii returned no results or cursor");
    var second = await catalogue.SearchAsync("", first.Cursor, CancellationToken.None);
    if (second.BeatmapSets.Count == 0) throw new Exception("Torii returned no second page");
    Console.WriteLine($"PASS: Torii returned {first.BeatmapSets.Count} and {second.BeatmapSets.Count} beatmapsets");
    return;
}

if (args.Contains("--catalogue-checks"))
{
    await CatalogueChecks.Run();
    return;
}

if (args.Contains("--audit-control"))
{
    using var controlAudit = new NetworkAudit();
    using var controlHttp = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
    try { await controlHttp.GetAsync("http://127.0.0.1:9/ritsu-audit-control"); }
    catch (HttpRequestException) { }
    catch (OperationCanceledException) { }
    controlAudit.Save(Path.Combine(AppContext.BaseDirectory, "audit-control.json"));
    if (controlAudit.AttemptCount == 0) throw new Exception("Network audit did not detect the loopback control request");
    Console.WriteLine("PASS: audit detects actual HTTP/socket activity");
    return;
}

if (args.Contains("--checks"))
{
    await OfflineChecks.Run(Path.Combine(AppContext.BaseDirectory, "offline-checks"));
    return;
}

if (args.Length == 2 && args[0] == "--catalogue-ui")
{
    using DesktopGameHost catalogueHost = Host.GetSuitableDesktopHost("ritsu-catalogue-validation", new HostOptions { PortableInstallation = true });
    catalogueHost.Run(new CatalogueSmokeGame(Path.GetFullPath(args[1])));
    return;
}
// Run the original desktop game in a fresh portable host. In particular, do not
// execute Torii's Program.Main(), which reads the existing Torii user storage.
using DesktopGameHost host = Host.GetSuitableDesktopHost("ritsu-validation", new HostOptions
{
    PortableInstallation = true,
    FriendlyGameName = "Torii — isolated Ritsu validation",
});
if (args.Length == 3 && args[0] is "--smoke" or "--reopen")
{
    using var smokeAudit = new NetworkAudit();
    host.Run(new SmokeGame(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), smokeAudit, args[0] == "--reopen"));
    return;
}

Assembly desktop = Assembly.Load("torii");
Type gameType = desktop.GetType("osu.Desktop.OsuGameDesktop", throwOnError: true)!;
var game = (osu.Framework.Game)Activator.CreateInstance(gameType, new object[] { Array.Empty<string>() })!;
host.Run(game);
