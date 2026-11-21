// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Offline;
using osu.Game.Scoring;

namespace osu.Game.Collections
{
    internal static class RitsuCollectionMenu
    {
        public static OsuMenuItem[] Create(RealmAccess realm, ManageCollectionsDialog? manager, BeatmapInfo beatmap,
                                           RitsuLibrary? library = null, RitsuProfileManager? profiles = null, ScoreInfo? score = null,
                                           OsuGame? game = null)
        {
            if (library == null)
            {
                return realm.Realm.All<BeatmapCollection>().OrderBy(c => c.Name).AsEnumerable()
                            .Select(c => (OsuMenuItem)new CollectionToggleMenuItem(c.ToLive(realm), beatmap))
                            .Append(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, () => manager?.Show()))
                            .ToArray();
            }

            var collections = realm.Run(r => r.All<BeatmapCollection>().OrderBy(c => c.Name).AsEnumerable()
                                              .Select(c => (c.ID, c.Name, Contains: c.BeatmapMD5Hashes.Contains(beatmap.MD5Hash))).ToArray());
            var items = library.MenuOrder(RitsuMenuOrdering.Collections).Apply(collections, c => c.ID.ToString(), c => c.Name)
                               .Select(collection =>
                               {
                                   var actions = new List<OsuMenuItem>
                                   {
                                       new OsuMenuItem(collection.Contains ? "Remove difficulty" : "Add difficulty", MenuItemType.Standard, () =>
                                       {
                                           if (collection.Contains)
                                               manager?.RemoveMembers(collection.ID, null, new[] { beatmap.MD5Hash });
                                           else
                                               Add(realm, collection.ID, new[] { beatmap.MD5Hash });
                                           RitsuMenuOrdering.RecordUse(library, RitsuMenuOrdering.Collections, collection.ID.ToString());
                                       }),
                                       new OsuMenuItem("Add beatmap set", MenuItemType.Standard, () =>
                                       {
                                           AddSet(realm, collection.ID, beatmap);
                                           RitsuMenuOrdering.RecordUse(library, RitsuMenuOrdering.Collections, collection.ID.ToString());
                                       }),
                                   };

                                   foreach (var subgroup in library.Subgroups.Where(s => s.CollectionId == collection.ID))
                                       actions.Add(createSubgroupAction(realm, library, subgroup, beatmap.MD5Hash, $"Add difficulty to {subgroup.Name}"));

                                   return new OsuMenuItem(collection.Name) { Items = actions };
                               }).ToList();

            if (score != null && profiles != null && realm.Run(r => r.Find<ScoreInfo>(score.ID) is { DeletePending: false }))
            {
                Guid profile = profiles.ActiveProfileId;
                bool marked = library.GetMarkedScores(profile).Contains(score.ID);
                items.Add(new OsuMenuItem(marked ? "Unmark score for analysis" : "Mark score for analysis", MenuItemType.Standard, () =>
                {
                    if (profiles.ActiveProfileId == profile)
                        library.ToggleMarkedScore(profile, score.ID);
                }));
            }

            if (game != null && profiles != null)
                items.Add(CreateMemoAction(library, profiles, beatmap, game, score));

            items.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, () => manager?.Show()));
            return items.ToArray();
        }

        internal static OsuMenuItem? CreateSubgroupMenu(RealmAccess realm, RitsuLibrary library, BeatmapInfo beatmap)
        {
            var subgroups = library.Subgroups;
            if (subgroups.Length == 0)
                return null;

            var groupNames = realm.Run(r => r.All<BeatmapCollection>().AsEnumerable().ToDictionary(collection => collection.ID, collection => collection.Name));
            var items = subgroups.Where(subgroup => groupNames.ContainsKey(subgroup.CollectionId))
                                 .Select(subgroup => createSubgroupAction(realm, library, subgroup, beatmap.MD5Hash, $"{groupNames[subgroup.CollectionId]} · {subgroup.Name}"))
                                 .ToArray();
            return items.Length == 0 ? null : new OsuMenuItem("Add to subgroup") { Items = items };
        }

        private static OsuMenuItem createSubgroupAction(RealmAccess realm, RitsuLibrary library, RitsuSubgroup subgroup, string hash, string label)
            => new OsuMenuItem(label, MenuItemType.Standard, () =>
            {
                if (Add(realm, subgroup.CollectionId, new[] { hash }))
                    library.SaveSubgroup(subgroup with { Hashes = subgroup.Hashes.Append(hash).Distinct().ToArray() });
            });

        internal static OsuMenuItem CreateMemoAction(RitsuLibrary library, RitsuProfileManager profiles, BeatmapInfo beatmap, OsuGame game, ScoreInfo? score = null)
        {
            var memo = library.GetMemo(profiles.ActiveProfileId, beatmap.MD5Hash);
            string label = string.IsNullOrEmpty(memo?.Text) ? "Difficulty memo" : $"Memo · {memo.Text[..Math.Min(memo.Text.Length, 45)]}";
            return new OsuMenuItem(label, MenuItemType.Standard, () => game.ShowRitsuMemo(beatmap, score));
        }

        private static void AddSet(RealmAccess realm, Guid collection, BeatmapInfo beatmap)
        {
            var hashes = realm.Run(r =>
            {
                var set = beatmap.BeatmapSet == null ? null : r.Find<BeatmapSetInfo>(beatmap.BeatmapSet.ID);
                return set is { DeletePending: false, Protected: false }
                    ? set.Beatmaps.AsEnumerable().Where(b => !b.Hidden).Select(b => b.MD5Hash).ToArray()
                    : Array.Empty<string>();
            });

            Add(realm, collection, hashes);
        }

        internal static bool Add(RealmAccess realm, Guid collectionId, IEnumerable<string> hashes) => realm.Write(r =>
        {
            var collection = r.Find<BeatmapCollection>(collectionId);
            if (collection == null)
                return false;

            var existing = collection.BeatmapMD5Hashes.ToHashSet();
            foreach (string hash in hashes)
            {
                if (!string.IsNullOrEmpty(hash) && existing.Add(hash))
                    collection.BeatmapMD5Hashes.Add(hash);
            }

            collection.LastModified = DateTimeOffset.UtcNow;
            return true;
        });
    }
}
