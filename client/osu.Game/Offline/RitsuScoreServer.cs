// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Offline
{
    internal static class RitsuScoreServer
    {
        private static readonly Uri? endpoint = getEndpoint();
        private static readonly HttpClient transport = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(3),
        };

        public static bool Enabled => endpoint != null;

        public static BeatmapLeaderboardScope[] Scopes => Enabled
            ? new[] { BeatmapLeaderboardScope.Local, BeatmapLeaderboardScope.LocalAll, BeatmapLeaderboardScope.Global }
            : new[] { BeatmapLeaderboardScope.Local, BeatmapLeaderboardScope.LocalAll };

        internal static bool IsBeatmapHash(string? hash) => hash is { Length: 32 } && hash.All(Uri.IsHexDigit);

        private static Uri? getEndpoint()
        {
            string? configuredUrl = Environment.GetEnvironmentVariable("RITSU_SCORE_SERVER_URL");
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp
                || uri.Host is not ("localhost" or "127.0.0.1") || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0
                || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                return null;

            return new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", uri.Port, "/api/v2/").Uri;
        }

        internal static async Task FetchAsync(GetScoresRequest request)
        {
            try
            {
                string json = await transport.GetStringAsync(new Uri(endpoint!, request.LocalServerTarget)).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<APIScoresCollection>(json) ?? throw new JsonSerializationException("Missing leaderboard response.");
                request.TriggerSuccess(result);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
            {
                request.TriggerFailure(error is OperationCanceledException ? new TimeoutException("The local score server did not respond.", error) : error);
            }
        }

        internal static async Task<bool> SubmitAsync(ScoreInfo score, Guid profileId, string hash)
        {
            try
            {
                var payload = JObject.FromObject(SoloScoreInfo.ForSubmission(score));
                payload["ended_at"] = score.Date;
                payload["pp"] = score.PP is { } pp && double.IsFinite(pp) ? new JValue(pp) : JValue.CreateNull();

                var submission = new JObject
                {
                    ["profile_id"] = profileId,
                    ["username"] = score.User.Username,
                    ["score"] = payload,
                };
                using var content = new StringContent(submission.ToString(Formatting.None), Encoding.UTF8, "application/json");
                using var response = await transport.PutAsync(new Uri(endpoint!, $"beatmaps/{hash}/solo/scores/{score.ID}"), content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
            {
                Logger.Log($"Could not submit the score to the local Ritsu server: {error.Message}", LoggingTarget.Network);
                return false;
            }
        }
    }
}
