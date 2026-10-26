using osu.Framework.IO.Network;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Offline;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Skinning;
using osu.Game.IO.Serialization;
using Newtonsoft.Json;

internal static class OfflineChecks
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var audit = new NetworkAudit();
        var storage = new NativeStorage(directory);
        using var config = new OsuConfigManager(storage);
        config.SetValue(OsuSetting.Token, "test-token-that-must-not-be-used");
        using var api = new TestOfflineProvider(config);
        Require(config.Get<string>(OsuSetting.Token) == "", "Saved credentials were cleared");
        api.Login("test-user", "test-password");
        Require(!api.IsLoggedIn && api.State.Value == APIState.Offline && api.AccessToken == "", "Login stays offline");

        int failures = 0;
        var request = new NeverTransportRequest();
        request.Failure += _ => failures++;
        api.Queue(request);
        api.Perform(request); // Must still complete exactly once.
        api.Drain();
        Require(failures == 1 && request.CompletionState == APIRequestCompletionState.Failed, "Request rejection completes once");
        Require(!request.TransportCreated, "Rejected API request never creates a transport");
        var asyncRequest = new NeverTransportRequest();
        await api.PerformAsync(asyncRequest);
        Require(!asyncRequest.TransportCreated && asyncRequest.CompletionState == APIRequestCompletionState.Failed, "Async dispatch rejects the request without an unobserved task exception");
        Require(api.GetHubConnector("test", "https://localhost") == null && !api.NotificationsClient.IsConnected.Value, "No real-time connections");

        WebRequest.DisableNetworkAccess();
        foreach (bool synchronous in new[] { false, true })
        {
            int failureCallbacks = 0;
            using var web = new WebRequest("http://127.0.0.1:9/ritsu-offline-check");
            web.Failed += _ => failureCallbacks++;
            try
            {
                if (synchronous) web.Perform(); else await web.PerformAsync();
                throw new Exception("Framework request unexpectedly succeeded");
            }
            catch (InvalidOperationException e) when (e.Message.Contains("disabled network")) { }
            Require(web.Completed && failureCallbacks == 1, "HTTP rejection completes once: " + synchronous);
        }

        new LoginOverlay().Show(); // An accidental shortcut must not expose authentication.
        Require(audit.AttemptCount == 0, "No HTTP, socket or DNS attempt during rejected operations");
        audit.Save(Path.Combine(directory, "network-checks.json"));

        // Persisted Torii component identifiers must remain loadable after the fork.
        Type[] components = SerialisedDrawableInfo.GetAllToriiSkinComponents(null);
        Require(components.Length > 0, "Torii skin components are retained");
        foreach (Type type in components)
        {
            var original = new SerialisedDrawableInfo { Type = type };
            var copy = original.Serialize().Deserialize<SerialisedDrawableInfo>();
            Require(copy?.Type == type, "Skin component roundtrip: " + type.Name);
        }
        Console.WriteLine("PASS: all offline, completion and skin identifier checks");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private sealed class NeverTransportRequest : APIRequest
    {
        protected override string Target => "must-not-connect";
        public bool TransportCreated { get; private set; }
        protected override WebRequest CreateWebRequest()
        {
            TransportCreated = true;
            throw new Exception("Network transport must not be constructed");
        }
    }

    private partial class TestOfflineProvider : OfflineAPIProvider
    {
        public TestOfflineProvider(OsuConfigManager config) : base(config) { }
        public void Drain() => Scheduler.Update();
    }
}
