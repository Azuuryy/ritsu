using System.Reflection;
using osu.Framework;
using osu.Framework.Platform;

if (args is ["--measure-profile-writes"])
{
    ProfileWriteMeasurements.Run();
    return;
}

if (args.Length > 0 && args[0] == "--measure-decoys")
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: --measure-decoys <path-to-osu-file>");
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        DecoyMeasurements.Run(args[1]);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
    {
        Console.Error.WriteLine($"Cannot measure beatmap: {exception.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

if (args.Contains("--catalogue-live") || args.Contains("--catalogue-ui"))
{
    Console.WriteLine("The online catalogue is disabled in the offline milestone.");
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

// Run the original desktop game in a fresh portable host. In particular, do not
// execute Torii's Program.Main(), which reads the existing Torii user storage.
using DesktopGameHost host = Host.GetSuitableDesktopHost("ritsu-validation", new HostOptions
{
    PortableInstallation = true,
    FriendlyGameName = "Torii — isolated Ritsu validation",
});
if (args.Length == 3 && args[0] is "--smoke" or "--reopen" or "--linked-smoke" or "--linked-reopen" or "--frame-baseline" or "--frame-instrumented" or "--linked-frame-baseline" or "--linked-frame-instrumented")
{
    using var smokeAudit = new NetworkAudit();
    bool linked = args[0].StartsWith("--linked-", StringComparison.Ordinal);
    bool? frameBenchmark = args[0].EndsWith("frame-instrumented", StringComparison.Ordinal) ? true
        : args[0].EndsWith("frame-baseline", StringComparison.Ordinal) ? false : null;
    bool reopen = frameBenchmark.HasValue || args[0].EndsWith("reopen", StringComparison.Ordinal);
    host.Run(new SmokeGame(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), smokeAudit, reopen, linked, frameBenchmark));
    return;
}

Assembly desktop = Assembly.Load("torii");
Type gameType = desktop.GetType("osu.Desktop.OsuGameDesktop", throwOnError: true)!;
var game = (osu.Framework.Game)Activator.CreateInstance(gameType, new object[] { Array.Empty<string>() })!;
host.Run(game);
