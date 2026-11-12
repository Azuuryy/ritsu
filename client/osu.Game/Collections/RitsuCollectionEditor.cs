// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Offline;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.Collections
{
    internal partial class RitsuCollectionEditor : PopupDialog
    {
        private readonly ManageCollectionsDialog manager;
        private readonly RitsuLibrary library;
        private readonly OsuDropdown<Entry> collections;
        private readonly OsuDropdown<Entry> subgroups;
        private readonly BasicSearchTextBox search;
        private readonly FillFlowContainer rows;
        private readonly OsuButton remove;
        private readonly OsuButton addSubgroup;
        private readonly OsuTextBox subgroupName;
        private readonly OsuButton deleteSubgroup;
        private readonly HashSet<string> selected = new HashSet<string>();
        private readonly Entry allMembers = new Entry(Guid.Empty, "All difficulties");
        private (string Hash, string Label)[] difficulties = Array.Empty<(string, string)>();

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public RitsuCollectionEditor(ManageCollectionsDialog manager, RitsuLibrary library)
        {
            this.manager = manager;
            this.library = library;

            HeaderText = "Collection contents";
            Icon = FontAwesome.Solid.FolderOpen;
            MainContent.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Padding = new MarginPadding { Horizontal = 35, Vertical = 8 },
                Children = new Drawable[]
                {
                    collections = new OsuDropdown<Entry> { RelativeSizeAxes = Axes.X },
                    subgroups = new OsuDropdown<Entry> { RelativeSizeAxes = Axes.X },
                    search = new BasicSearchTextBox { RelativeSizeAxes = Axes.X, Height = 32, PlaceholderText = "Find difficulties" },
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 215,
                        Child = rows = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                        },
                    },
                    subgroupName = new OsuTextBox { RelativeSizeAxes = Axes.X, PlaceholderText = "Name a new subgroup", ReleaseFocusOnCommit = false },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            remove = new RoundedButton { Width = 135, Height = 28, Text = "Remove selected", Action = removeSelected },
                            addSubgroup = new RoundedButton { Width = 110, Height = 28, Text = "New subgroup", Action = addGroup },
                            deleteSubgroup = new RoundedButton { Width = 115, Height = 28, Text = "Delete subgroup", Action = deleteGroup },
                            new RoundedButton { Width = 65, Height = 28, Text = "Undo", Action = () => { manager.UndoRemoval(); refresh(); } },
                        },
                    },
                },
            });
            Buttons = new PopupDialogButton[] { new PopupDialogOkButton { Text = "Done" } };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refresh();
            collections.Current.ValueChanged += _ => refreshSubgroups();
            subgroups.Current.ValueChanged += _ => refreshDifficulties();
            search.Current.ValueChanged += _ => refreshRows();
            subgroupName.Current.ValueChanged += _ => updateSubgroupButton();
            subgroupName.OnCommit += (_, _) => addGroup();
        }

        private void refresh()
        {
            var entries = realm.Run(r => r.All<BeatmapCollection>().OrderBy(c => c.Name).AsEnumerable().Select(c => new Entry(c.ID, c.Name)).ToArray());
            updateDropdown(collections, entries);
            refreshSubgroups();
        }

        private void refreshSubgroups()
        {
            var collection = collections.Current.Value;
            updateDropdown(subgroups, new[] { allMembers }.Concat(library.Subgroups
                .Where(s => s.CollectionId == collection?.Id)
                .Select(s => new Entry(s.Id, s.Name))).ToArray());
            refreshDifficulties();
        }

        private static void updateDropdown(OsuDropdown<Entry> dropdown, Entry[] entries)
        {
            var current = dropdown.Items.ToArray();
            if (current.SequenceEqual(entries))
                return;

            var selected = dropdown.Current.Value;
            foreach (var item in current)
                dropdown.RemoveDropdownItem(item);
            foreach (var item in entries)
                dropdown.AddDropdownItem(item);
            dropdown.Current.Value = entries.Contains(selected) ? selected : entries.FirstOrDefault();
        }

        private void refreshDifficulties()
        {
            selected.Clear();
            var collection = collections.Current.Value;
            var subgroup = library.Subgroups.FirstOrDefault(s => s.Id == subgroups.Current.Value?.Id);
            deleteSubgroup.Enabled.Value = subgroup != null;
            updateSubgroupButton();
            difficulties = collection == null ? Array.Empty<(string, string)>() : realm.Run(r =>
            {
                var hashes = r.Find<BeatmapCollection>(collection.Id)?.BeatmapMD5Hashes.AsEnumerable() ?? Enumerable.Empty<string>();
                if (subgroup != null)
                    hashes = hashes.Intersect(subgroup.Hashes);

                return hashes.Distinct().Select(hash =>
                {
                    var map = r.All<BeatmapInfo>().FirstOrDefault(b => b.MD5Hash == hash);
                    string label = map == null ? $"Unavailable difficulty · {hash}" : $"{map.Metadata.Title} [{map.DifficultyName}]";
                    return (hash, label);
                }).OrderBy(b => b.label, StringComparer.CurrentCultureIgnoreCase).ToArray();
            });
            refreshRows();
        }

        private void refreshRows()
        {
            rows.Clear();
            foreach (var (hash, label) in difficulties.Where(d => d.Label.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)))
            {
                var row = new RoundedButton { RelativeSizeAxes = Axes.X, Height = 26 };
                void update() => row.Text = $"{(selected.Contains(hash) ? "☑" : "☐")}  {label}";
                row.Action = () =>
                {
                    if (!selected.Add(hash))
                        selected.Remove(hash);
                    remove.Enabled.Value = selected.Count > 0;
                    update();
                };
                update();
                rows.Add(row);
            }
            remove.Enabled.Value = selected.Count > 0;
        }

        private void removeSelected()
        {
            if (collections.Current.Value is not { } collection || selected.Count == 0)
                return;

            Guid subgroup = subgroups.Current.Value?.Id ?? Guid.Empty;
            manager.RemoveMembers(collection.Id, subgroup == Guid.Empty ? null : subgroup, selected);
            refreshDifficulties();
        }

        private void addGroup()
        {
            string name = subgroupName.Text.Trim();
            if (collections.Current.Value is not { } collection || name.Length == 0)
                return;

            var group = new RitsuSubgroup(Guid.NewGuid(), collection.Id, name, selected.ToArray());
            library.SaveSubgroup(group);
            subgroupName.Text = string.Empty;
            refreshSubgroups();
            subgroups.Current.Value = subgroups.Items.Single(s => s.Id == group.Id);
        }

        private void updateSubgroupButton() =>
            addSubgroup.Enabled.Value = collections.Current.Value != null && !string.IsNullOrWhiteSpace(subgroupName.Text);

        private void deleteGroup()
        {
            if (subgroups.Current.Value is not { } group || group.Id == Guid.Empty)
                return;

            manager.DeleteSubgroup(group.Id);
            refreshSubgroups();
        }

        private sealed record Entry(Guid Id, string Label)
        {
            public override string ToString() => Label;
        }
    }
}
