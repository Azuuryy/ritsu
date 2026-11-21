// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Collections
{
    internal partial class RitsuCollectionEditor : CompositeDrawable
    {
        private readonly ManageCollectionsDialog manager;
        private readonly RitsuLibrary library;
        private readonly Guid collectionId;
        private readonly OsuDropdown<Entry> subgroups;
        private readonly BasicSearchTextBox search;
        private readonly FillFlowContainer rows;
        private readonly OsuButton remove;
        private readonly OsuButton addSubgroup;
        private readonly OsuTextBox subgroupName;
        private readonly OsuButton deleteSubgroup;
        private int visibleLimit = 100;
        private readonly HashSet<string> selected = new HashSet<string>();
        private readonly Entry allMembers = new Entry(Guid.Empty, "All difficulties");
        private (string Hash, string Label)[] difficulties = Array.Empty<(string, string)>();

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public RitsuCollectionEditor(ManageCollectionsDialog manager, RitsuLibrary library, Guid collectionId)
        {
            this.manager = manager;
            this.library = library;

            this.collectionId = collectionId;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            AddInternal(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Padding = new MarginPadding { Horizontal = 35, Vertical = 8 },
                Children = new Drawable[]
                {
                    subgroups = new OsuDropdown<Entry> { RelativeSizeAxes = Axes.X },
                    search = new BasicSearchTextBox { RelativeSizeAxes = Axes.X, Height = 32, PlaceholderText = "Find difficulties" },
                    subgroupName = new OsuTextBox { RelativeSizeAxes = Axes.X, PlaceholderText = "Name a new subgroup", ReleaseFocusOnCommit = false },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            remove = new RoundedButton { Width = 135, Height = 28, Text = "Remove selected", Action = removeSelected },
                            addSubgroup = new RoundedButton { Width = 110, Height = 28, Text = "New subgroup", Action = addGroup },
                            deleteSubgroup = new RoundedButton { Width = 115, Height = 28, Text = "Delete subgroup", Action = deleteGroup },
                            new RoundedButton { Width = 65, Height = 28, Text = "Undo", Action = manager.UndoRemoval },
                        },
                    },
                    rows = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
            subgroups.Current.ValueChanged += _ => refreshDifficulties();
            search.Current.ValueChanged += _ =>
            {
                visibleLimit = 100;
                refreshRows();
            };
            manager.RemovalUndone += Refresh;
            subgroupName.Current.ValueChanged += _ => updateSubgroupButton();
            subgroupName.OnCommit += (_, _) => addGroup();
        }

        internal void Refresh()
        {
            updateDropdown(subgroups, new[] { allMembers }.Concat(library.Subgroups
                .Where(s => s.CollectionId == collectionId)
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
            visibleLimit = 100;
            var subgroup = library.Subgroups.FirstOrDefault(s => s.Id == subgroups.Current.Value?.Id);
            deleteSubgroup.Enabled.Value = subgroup != null;
            updateSubgroupButton();
            difficulties = realm.Run(r =>
            {
                var hashes = r.Find<BeatmapCollection>(collectionId)?.BeatmapMD5Hashes.AsEnumerable() ?? Enumerable.Empty<string>();
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
            var filtered = difficulties.Where(d => d.Label.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            foreach (var (hash, label) in filtered.Take(visibleLimit))
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
            if (filtered.Length > visibleLimit)
                rows.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 28,
                    Text = "Show more difficulties",
                    Action = () =>
                    {
                        visibleLimit += 100;
                        refreshRows();
                    },
                });

            remove.Enabled.Value = selected.Count > 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                manager.RemovalUndone -= Refresh;
            base.Dispose(isDisposing);
        }

        private void removeSelected()
        {
            if (selected.Count == 0)
                return;

            Guid subgroup = subgroups.Current.Value?.Id ?? Guid.Empty;
            manager.RemoveMembers(collectionId, subgroup == Guid.Empty ? null : subgroup, selected);
            refreshDifficulties();
        }

        private void addGroup()
        {
            string name = subgroupName.Text.Trim();
            if (name.Length == 0)
                return;

            var group = new RitsuSubgroup(Guid.NewGuid(), collectionId, name, selected.ToArray());
            library.SaveSubgroup(group);
            subgroupName.Text = string.Empty;
            Refresh();
            subgroups.Current.Value = subgroups.Items.Single(s => s.Id == group.Id);
        }

        private void updateSubgroupButton() =>
            addSubgroup.Enabled.Value = !string.IsNullOrWhiteSpace(subgroupName.Text);

        private void deleteGroup()
        {
            if (subgroups.Current.Value is not { } group || group.Id == Guid.Empty)
                return;

            manager.DeleteSubgroup(group.Id);
            Refresh();
        }

        private sealed record Entry(Guid Id, string Label)
        {
            public override string ToString() => Label;
        }
    }
}
