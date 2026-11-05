using System.Net;
using osu.Game.Overlays.BeatmapListing;

internal static class CatalogueChecks
{
    public static async Task Run()
    {
        using var handler = new FakeHandler();
        using var catalogue = new RitsuCatalogueClient(handler);
        var torii = await catalogue.SearchAsync("a & b", "next cursor", CancellationToken.None);
        Require(torii.Total == 1 && torii.BeatmapSets.Count == 1 && torii.Cursor == "second", "Torii response and cursor");
        Require(handler.Requests[0].host == "lazer-api.shikkesora.com" && handler.Requests[0].query.Contains("q=a%20%26%20b") && handler.Requests[0].query.Contains("cursor_string=next%20cursor") && handler.Requests[0].authorization == null, "Torii query is escaped and anonymous");
        await catalogue.SearchAsync("blue zenith", null, CancellationToken.None, new Dictionary<string, string> { ["s"] = "ranked", ["m"] = "3" });
        Require(handler.Requests[1].query.Contains("s=ranked") && handler.Requests[1].query.Contains("m=3"), "Native filters sent to Torii");
        Require(osu.Game.Online.TrustedDomainOnlineStore.IsCatalogueMedia("https://assets.ppy.sh/beatmaps/1/covers/card.jpg"), "Beatmap covers permitted");
        Require(!osu.Game.Online.TrustedDomainOnlineStore.IsCatalogueMedia("https://assets.ppy.sh/menu-content.json"), "Unrelated online services remain blocked");
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
