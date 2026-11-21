// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Tags you give beatmaps yourself (instead of the stripped online tag voting), per difficulty.
    /// Kept in "osu-local/beatmap-tags.json", so the shared database is never changed.
    /// They show in song select's beatmap details, can be searched like voted tags (tag="jumps/cross-screen"! or plain text),
    /// and profiles can switch automatically on them.
    /// </summary>
    public class LocalBeatmapTags
    {
        private const string file = LocalProfileStore.DIRECTORY + @"/beatmap-tags.json";

        /// <summary>
        /// The tags of the running game (song select's filtering runs on background threads and reads them from here).
        /// </summary>
        public static LocalBeatmapTags? Current { get; internal set; }

        /// <summary>
        /// Invoked (on any thread) when tags change.
        /// </summary>
        public event Action? Changed;

        private readonly Storage storage;
        private readonly object sync = new object();
        private readonly Dictionary<string, string[]> tags;

        public LocalBeatmapTags(Storage storage)
        {
            this.storage = storage;
            tags = read() ?? new Dictionary<string, string[]>();
        }

        /// <summary>
        /// The local tags of a beatmap difficulty (keyed by its MD5 hash).
        /// </summary>
        public IReadOnlyList<string> Get(IBeatmapInfo? beatmap)
        {
            string? key = beatmap?.MD5Hash;

            if (string.IsNullOrEmpty(key))
                return Array.Empty<string>();

            // Called for every beatmap while song select filters: no copying (the stored arrays are never changed, only replaced).
            lock (sync)
                return tags.Count > 0 && tags.TryGetValue(key, out string[]? list) ? list : Array.Empty<string>();
        }

        public static string Normalise(string tag) => string.Join(' ', (tag ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        public void Add(IBeatmapInfo beatmap, string tag)
        {
            tag = Normalise(tag);

            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(beatmap.MD5Hash))
                return;

            lock (sync)
            {
                string[] list = tags.GetValueOrDefault(beatmap.MD5Hash) ?? Array.Empty<string>();

                if (list.Contains(tag))
                    return;

                tags[beatmap.MD5Hash] = list.Append(tag).ToArray();
                save();
            }

            Changed?.Invoke();
        }

        public void Remove(IBeatmapInfo beatmap, string tag)
        {
            lock (sync)
            {
                if (string.IsNullOrEmpty(beatmap.MD5Hash) || !tags.TryGetValue(beatmap.MD5Hash, out string[]? list) || !list.Contains(tag))
                    return;

                string[] remaining = list.Where(t => t != tag).ToArray();

                if (remaining.Length == 0)
                    tags.Remove(beatmap.MD5Hash);
                else
                    tags[beatmap.MD5Hash] = remaining;

                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Every local tag used so far (for suggestions).
        /// </summary>
        public IReadOnlyList<string> AllTags()
        {
            lock (sync)
                return tags.Values.SelectMany(t => t).Distinct().OrderBy(t => t).ToArray();
        }

        private Dictionary<string, string[]>? read()
        {
            try
            {
                return LocalDurableFile.Read(storage, file, JsonConvert.DeserializeObject<Dictionary<string, string[]>>);
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Ritsu: could not read {file}; starting without local beatmap tags.");
                return null;
            }
        }

        private void save()
        {
            try
            {
                LocalDurableFile.WriteText(storage, file, JsonConvert.SerializeObject(tags, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Ritsu: could not save {file}.");
            }
        }
    }
}
