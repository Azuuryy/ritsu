// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.BeatmapListing
{
    public sealed class RitsuCatalogueClient : IDisposable
    {
        private readonly HttpClient client;

        public RitsuCatalogueClient(HttpMessageHandler? handler = null)
        {
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
        }

        public async Task<RitsuCatalogueResponse> SearchAsync(string query, string? cursor, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? filters = null)
        {
            const string host = "https://lazer-api.shikkesora.com";
            string url = $"{host}/api/v2/beatmapsets/search?q={Uri.EscapeDataString(query)}";
            if (filters == null) url += "&s=any&sort=updated_desc";
            else foreach (var filter in filters) url += $"&{Uri.EscapeDataString(filter.Key)}={Uri.EscapeDataString(filter.Value)}";
            if (!string.IsNullOrEmpty(cursor))
                url += $"&cursor_string={Uri.EscapeDataString(cursor)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<RitsuCatalogueResponse>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))
                   ?? throw new InvalidOperationException("Invalid catalogue response.");
        }

        public async Task<APIBeatmapSet> GetBeatmapSetAsync(int id, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync($"https://lazer-api.shikkesora.com/api/v2/beatmapsets/{id}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<APIBeatmapSet>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidOperationException("Invalid beatmap response.");
        }

        public void Dispose() => client.Dispose();

    }

    public sealed class RitsuCatalogueResponse
    {
        [JsonProperty("beatmapsets")]
        public List<APIBeatmapSet> BeatmapSets { get; set; } = new List<APIBeatmapSet>();

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("cursor_string")]
        public string? Cursor { get; set; }
    }
}
