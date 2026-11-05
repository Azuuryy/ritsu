// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Online.API.Requests
{
    public class DownloadBeatmapSetRequest : ArchiveDownloadRequest<IBeatmapSetInfo>
    {
        private readonly bool noVideo;

        public DownloadBeatmapSetRequest(IBeatmapSetInfo set, bool noVideo)
            : base(set)
        {
            this.noVideo = noVideo;
        }

        public async Task PerformCatalogueAsync(IAPIProvider api)
        {
            AttachAPI(api);
            if (CompletionState != APIRequestCompletionState.Waiting) return;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            void cancel(Exception _) => cancellation.Cancel();
            Failure += cancel;
            string filename = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".osz");
            bool handedOff = false;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                using var response = await client.GetAsync($"https://lazer-api.shikkesora.com/api/v2/beatmapsets/{Model.OnlineID}/download?noVideo={(noVideo ? "1" : "0")}", HttpCompletionOption.ResponseHeadersRead, cancellation.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using (var input = await response.Content.ReadAsStreamAsync(cancellation.Token).ConfigureAwait(false))
                await using (var output = File.Create(filename))
                {
                    byte[] buffer = new byte[65536];
                    long received = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, cancellation.Token).ConfigureAwait(false)) > 0)
                    {
                        if (CompletionState != APIRequestCompletionState.Waiting) throw new OperationCanceledException();
                        await output.WriteAsync(buffer.AsMemory(0, count), cancellation.Token).ConfigureAwait(false);
                        received += count;
                        if (response.Content.Headers.ContentLength is long total && total > 0)
                        {
                            float progress = Math.Min(0.99f, (float)received / total);
                            api.Schedule(() => SetProgress(progress));
                        }
                    }
                }
                if (CompletionState != APIRequestCompletionState.Waiting) throw new OperationCanceledException();
                api.Schedule(() => SetProgress(1));
                TriggerSuccess(filename);
                handedOff = true;
            }
            catch (Exception e) { Fail(e); }
            finally
            {
                Failure -= cancel;
                if (!handedOff && File.Exists(filename)) File.Delete(filename);
            }
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Timeout = 60000;
            return req;
        }

        protected override string FileExtension => ".osz";

        protected override string Target => $@"beatmapsets/{Model.OnlineID}/download{(noVideo ? "?noVideo=1" : "")}";
    }
}
