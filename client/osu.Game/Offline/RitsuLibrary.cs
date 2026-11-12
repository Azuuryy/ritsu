// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Offline
{
    internal sealed class RitsuLibrary
    {
        private const string filename = "library.json";
        private readonly Storage storage;
        private LibraryData data;
        private readonly object sync = new object();

        public event Action? Changed;

        public RitsuLibrary(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory("ritsu");
            data = new LibraryData();
            try
            {
                if (!this.storage.Exists(filename))
                    return;

                using var stream = this.storage.GetStream(filename, FileAccess.Read, FileMode.Open);
                using var reader = new StreamReader(stream);
                data = JsonConvert.DeserializeObject<LibraryData>(reader.ReadToEnd()) ?? new LibraryData();
            }
            catch (Exception e) when (e is JsonException or IOException)
            {
                Logger.Error(e, "Could not read Ritsu library preferences.");
            }
        }

        public RitsuPinnedSearch[] Searches => read(d => d.Searches.ToArray());
        public RitsuSmartCollection[] SmartCollections => read(d => d.SmartCollections.ToArray());
        public RitsuSubgroup[] Subgroups => read(d => d.Subgroups.ToArray());
        public RitsuSessionPreset[] Presets => read(d => d.Presets.ToArray());

        public void SaveSearch(Guid id, string name, RitsuSearchState state)
        {
            change(d =>
            {
                int index = d.Searches.FindIndex(s => s.Id == id);
                var search = new RitsuPinnedSearch(id, name.Trim(), state);
                if (index == -1)
                    d.Searches.Add(search);
                else
                    d.Searches[index] = search;
            });
        }

        public void DeleteSearch(Guid id) => change(d =>
        {
            d.Searches.RemoveAll(s => s.Id == id);
            d.SmartCollections.RemoveAll(c => c.SearchId == id);
        });

        public void AddSmartCollection(Guid searchId, string name) => change(d =>
        {
            if (d.Searches.Any(s => s.Id == searchId))
                d.SmartCollections.Add(new RitsuSmartCollection(Guid.NewGuid(), searchId, name.Trim()));
        });

        public void DeleteSmartCollection(Guid id) => change(d => d.SmartCollections.RemoveAll(c => c.Id == id));

        public void SaveSubgroup(RitsuSubgroup subgroup) => change(d =>
        {
            int index = d.Subgroups.FindIndex(s => s.Id == subgroup.Id);
            if (index == -1)
                d.Subgroups.Add(subgroup);
            else
                d.Subgroups[index] = subgroup;
        });

        public void DeleteSubgroup(Guid id) => change(d => d.Subgroups.RemoveAll(s => s.Id == id));

        public void RemoveSubgroupMembers(Guid collectionId, IReadOnlySet<string> hashes) => change(d =>
        {
            for (int i = 0; i < d.Subgroups.Count; i++)
            {
                var subgroup = d.Subgroups[i];
                if (subgroup.CollectionId == collectionId && subgroup.Hashes.Any(hashes.Contains))
                    d.Subgroups[i] = subgroup with { Hashes = subgroup.Hashes.Where(h => !hashes.Contains(h)).ToArray() };
            }
        });

        public void SavePreset(RitsuSessionPreset preset) => change(d =>
        {
            int index = d.Presets.FindIndex(p => p.Id == preset.Id);
            if (index == -1)
                d.Presets.Add(preset);
            else
                d.Presets[index] = preset;
        });

        public void DeletePreset(Guid id) => change(d => d.Presets.RemoveAll(p => p.Id == id));

        public Guid[] GetMarkedScores(Guid profileId) =>
            read(d => d.MarkedScores.TryGetValue(profileId, out var scores) ? scores.ToArray() : Array.Empty<Guid>());

        public void ToggleMarkedScore(Guid profileId, Guid scoreId) => change(d =>
        {
            if (!d.MarkedScores.TryGetValue(profileId, out var scores))
                d.MarkedScores.Add(profileId, scores = new HashSet<Guid>());
            if (!scores.Remove(scoreId))
                scores.Add(scoreId);
        });

        public RitsuMemo? GetMemo(Guid profileId, string hash) =>
            read(d => d.Memos.TryGetValue(profileId, out var memos) ? memos.GetValueOrDefault(hash) : null);

        public void SaveMemo(Guid profileId, string hash, RitsuMemo memo) => change(d =>
        {
            if (!d.Memos.TryGetValue(profileId, out var memos))
                d.Memos.Add(profileId, memos = new Dictionary<string, RitsuMemo>());
            if (string.IsNullOrWhiteSpace(memo.Text) && memo.ReplayId == null)
                memos.Remove(hash);
            else
                memos[hash] = memo;
        });

        public RitsuMenuOrder MenuOrder(string menu) => read(d => d.MenuOrders.GetValueOrDefault(menu) ?? new RitsuMenuOrder());

        public void SetMenuOrder(string menu, RitsuMenuOrder order) => change(d => d.MenuOrders[menu] = order);

        private T read<T>(Func<LibraryData, T> read)
        {
            lock (sync)
                return read(data);
        }

        private void change(Action<LibraryData> update)
        {
            lock (sync)
            {
                var next = data.Copy();
                update(next);
                string json = JsonConvert.SerializeObject(next, Formatting.Indented);
                string temporary = $".library-{Guid.NewGuid():N}.tmp";

                try
                {
                    using (var stream = storage.GetStream(temporary, FileAccess.Write, FileMode.CreateNew))
                    using (var writer = new StreamWriter(stream))
                        writer.Write(json);
                    storage.Move(temporary, filename);
                }
                finally
                {
                    if (storage.Exists(temporary))
                        storage.Delete(temporary);
                }

                data = next;
            }
            Changed?.Invoke();
        }

        private sealed class LibraryData
        {
            public List<RitsuPinnedSearch> Searches { get; set; } = new List<RitsuPinnedSearch>();
            public List<RitsuSmartCollection> SmartCollections { get; set; } = new List<RitsuSmartCollection>();
            public List<RitsuSubgroup> Subgroups { get; set; } = new List<RitsuSubgroup>();
            public List<RitsuSessionPreset> Presets { get; set; } = new List<RitsuSessionPreset>();
            public Dictionary<Guid, HashSet<Guid>> MarkedScores { get; set; } = new Dictionary<Guid, HashSet<Guid>>();
            public Dictionary<Guid, Dictionary<string, RitsuMemo>> Memos { get; set; } = new Dictionary<Guid, Dictionary<string, RitsuMemo>>();
            public Dictionary<string, RitsuMenuOrder> MenuOrders { get; set; } = new Dictionary<string, RitsuMenuOrder>();

            public LibraryData Copy() => new LibraryData
            {
                Searches = new List<RitsuPinnedSearch>(Searches),
                SmartCollections = new List<RitsuSmartCollection>(SmartCollections),
                Subgroups = new List<RitsuSubgroup>(Subgroups),
                Presets = new List<RitsuSessionPreset>(Presets),
                MarkedScores = MarkedScores.ToDictionary(p => p.Key, p => new HashSet<Guid>(p.Value)),
                Memos = Memos.ToDictionary(p => p.Key, p => new Dictionary<string, RitsuMemo>(p.Value)),
                MenuOrders = new Dictionary<string, RitsuMenuOrder>(MenuOrders),
            };
        }
    }

    internal sealed record RitsuSearchState(string Query, string Collection, double MinimumStars, double MaximumStars,
                                            double MinimumPP, double MaximumPP, bool Converted, SortMode Sort, GroupMode Group,
                                            bool ReverseSort, bool ReverseGroup, int PPGroupInterval, bool CalculatePerformance);

    internal sealed record RitsuPinnedSearch(Guid Id, string Name, RitsuSearchState State);
    internal sealed record RitsuSmartCollection(Guid Id, Guid SearchId, string Name);
    internal sealed record RitsuSubgroup(Guid Id, Guid CollectionId, string Name, string[] Hashes);
    internal sealed record RitsuMemo(string Text, Guid? ReplayId, double? Timestamp);
    internal sealed record RitsuSessionPreset(Guid Id, string Name, string Ruleset, string Mods, string Skin, int HudVisibility, bool KeyOverlay, RitsuSearchState Search);

    internal enum RitsuMenuOrderMode
    {
        Manual,
        Alphabetical,
        MostUsed,
    }

    internal sealed record RitsuMenuOrder
    {
        public RitsuMenuOrderMode Mode { get; init; }
        public string[] Order { get; init; } = Array.Empty<string>();
        public string[] Pins { get; init; } = Array.Empty<string>();
        public Dictionary<string, int> Uses { get; init; } = new Dictionary<string, int>();

        public T[] Apply<T>(IEnumerable<T> items, Func<T, string> key, Func<T, string> label)
        {
            var ordered = items.OrderByDescending(item => Pins.Contains(key(item)));
            return (Mode switch
            {
                RitsuMenuOrderMode.Alphabetical => ordered.ThenBy(label, StringComparer.CurrentCultureIgnoreCase),
                RitsuMenuOrderMode.MostUsed => ordered.ThenByDescending(item => Uses.GetValueOrDefault(key(item))),
                _ => ordered.ThenBy(item => Array.IndexOf(Order, key(item)) is int index && index >= 0 ? index : int.MaxValue),
            }).ToArray();
        }
    }
}
