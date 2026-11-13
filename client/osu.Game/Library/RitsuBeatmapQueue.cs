// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Library
{
    internal sealed record RitsuQueueItem(string Hash, string Title);

    // Accessed on the update thread, including after navigation to Song Select.
    internal sealed class RitsuBeatmapQueue
    {
        private readonly List<RitsuQueueItem> items = new List<RitsuQueueItem>();

        public event Action? Changed;

        public RitsuQueueItem[] Snapshot() => items.ToArray();

        public bool CanAdd(BeatmapInfo? beatmap) =>
            IsAvailable(beatmap) && items.All(item => item.Hash != beatmap!.MD5Hash);

        public bool Add(BeatmapInfo beatmap)
        {
            if (!CanAdd(beatmap))
                return false;

            items.Add(new RitsuQueueItem(beatmap.MD5Hash, $"{beatmap.Metadata.Title} [{beatmap.DifficultyName}]"));
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string hash)
        {
            if (items.RemoveAll(item => item.Hash == hash) == 0)
                return false;

            Changed?.Invoke();
            return true;
        }

        public bool Move(string hash, int direction)
        {
            int from = items.FindIndex(item => item.Hash == hash);
            int to = from + direction;
            if (from < 0 || to < 0 || to >= items.Count)
                return false;

            (items[from], items[to]) = (items[to], items[from]);
            Changed?.Invoke();
            return true;
        }

        public bool SelectNext(Func<string, BeatmapInfo?> resolve, Action<BeatmapInfo> select)
        {
            while (items.Count > 0)
            {
                var item = items[0];
                var beatmap = resolve(item.Hash);
                if (IsAvailable(beatmap))
                {
                    select(beatmap!);
                    Remove(item.Hash);
                    return true;
                }

                Remove(item.Hash);
            }

            return false;
        }

        internal static bool IsAvailable(BeatmapInfo? beatmap) =>
            beatmap is { Hidden: false, BeatmapSet: { DeletePending: false, Protected: false } }
            && !string.IsNullOrEmpty(beatmap.MD5Hash);
    }
}
