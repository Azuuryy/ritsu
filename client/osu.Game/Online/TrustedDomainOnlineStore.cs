// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using osu.Framework.IO.Stores;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore, IResourceStore<byte[]>
    {
        private static readonly HttpClient media = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public static bool IsCatalogueMedia(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return false;
            return (uri.Host == "assets.ppy.sh" && uri.AbsolutePath.StartsWith("/beatmaps/", StringComparison.Ordinal))
                || (uri.Host == "b.ppy.sh" && uri.AbsolutePath.StartsWith("/preview/", StringComparison.Ordinal))
                || (uri.Host == "lazer-api.shikkesora.com" && uri.AbsolutePath.StartsWith("/file/beatmapsets/", StringComparison.Ordinal));
        }

        public override byte[] Get(string url) => GetAsync(url).GetAwaiter().GetResult();

        public new async Task<byte[]> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            if (!IsCatalogueMedia(url)) return null!;
            try { return await media.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false); }
            catch (HttpRequestException) { return null!; }
            catch (OperationCanceledException) { return null!; }
        }
    }
}
