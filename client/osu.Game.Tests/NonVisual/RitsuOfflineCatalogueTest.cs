// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class RitsuOfflineCatalogueTest
    {
        [Test]
        public async Task CataloguePassesCursorWithoutAuthentication()
        {
            using var handler = new RecordingHandler();
            using var catalogue = new RitsuCatalogueClient(handler);
            var result = await catalogue.SearchAsync("test", "next page", CancellationToken.None);
            Assert.That(result.Cursor, Is.EqualTo("second"));
            Assert.That(handler.LastRequest!.RequestUri!.Query, Does.Contain("cursor_string=next%20page"));
            Assert.That(handler.LastRequest.Headers.Authorization, Is.Null);
        }

        [Test]
        public async Task OnlyTrustedCatalogueMediaReachesTransport()
        {
            using var handler = new RecordingHandler();
            using var client = new HttpClient(handler);
            using var store = new TrustedDomainOnlineStore(client);
            Assert.That(await store.GetAsync("https://assets.ppy.sh/beatmaps/1/covers/card.jpg"), Is.Not.Null);
            Assert.That(await store.GetAsync("https://example.com/image.jpg"), Is.Null);
            Assert.That(handler.Count, Is.EqualTo(1));
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public int Count { get; private set; }
            public HttpRequestMessage? LastRequest { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Count++;
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"beatmapsets\":[],\"cursor_string\":\"second\"}") });
            }
        }
    }
}
