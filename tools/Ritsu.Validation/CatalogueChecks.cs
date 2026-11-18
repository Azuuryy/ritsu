using System.Net;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Offline;
using osu.Game.Online;

internal static class CatalogueChecks
{
    public static async Task Run()
    {
        using var handler = new FakeHandler();
        using var catalogue = new RitsuCatalogueClient(handler);
        var first = await catalogue.SearchAsync("test", null, CancellationToken.None);
        Require(first.Cursor == "second", "Catalogue returns cursor");
        await catalogue.SearchAsync("test", first.Cursor, CancellationToken.None);
        Require(handler.Requests[1].query.Contains("cursor_string=second"), "Next page sends cursor");
        Require(handler.Requests.All(r => r.authorization == null), "Catalogue needs no authentication");
        using var media = new HttpClient(handler, false);
        using var store = new TrustedDomainOnlineStore(media);
        Require(await store.GetAsync("https://assets.ppy.sh/beatmaps/1/covers/card.jpg") != null, "Trusted catalogue media allowed");
        Require(await store.GetAsync("https://example.com/image.jpg") == null, "Untrusted media rejected");
        Console.WriteLine("PASS: catalogue transport checks");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public readonly List<(string host, string path, string query, string? authorization)> Requests = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.Host, request.RequestUri.AbsolutePath, request.RequestUri.Query, request.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"beatmapsets\":[{\"id\":123}],\"total\":1,\"cursor_string\":\"second\"}") });
        }
    }
}
