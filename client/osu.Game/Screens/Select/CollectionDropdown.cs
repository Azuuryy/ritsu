// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Offline;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osuTK.Input;
using osuTK;
using Realms;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A dropdown to select the collection to be used to filter results.
    /// </summary>
    public partial class CollectionDropdown : ShearedDropdown<CollectionFilterMenuItem> // TODO: partial class under FilterControl?
    {
        /// <summary>
        /// Whether to show the "manage collections..." menu item in the dropdown.
        /// </summary>
        protected virtual bool ShowManageCollectionsItem => true;

        private readonly BindableList<CollectionFilterMenuItem> filters = new BindableList<CollectionFilterMenuItem>();
        private readonly Bindable<string> configCollectionFilter = new Bindable<string>();

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        [Resolved(canBeNull: true)]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IDisposable? realmSubscription;
        private bool reverseCollections;
        private bool synchronising;

        [Resolved(canBeNull: true)]
        private RitsuLibrary? library { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogs { get; set; }

        private readonly CollectionFilterMenuItem allBeatmapsItem = new AllBeatmapsCollectionFilterMenuItem();

        public CollectionDropdown()
            : base(CollectionsStrings.Collection)
        {
            ItemSource = filters;
            Current.Value = allBeatmapsItem;
            AlwaysShowSearchBar = true;
            MiddleClickAction = () =>
            {
                reverseCollections = !reverseCollections;
                SetReversedSortIndicator(reverseCollections, CollectionsStrings.Collection);
                refresh();
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager configManager)
        {
            configManager.BindWith(OsuSetting.SongSelectCollectionFilter, configCollectionFilter);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            subscribeToCollections();

            Current.BindValueChanged(selectionChanged);
            configCollectionFilter.BindValueChanged(_ => refreshSelection());
            if (library != null)
                library.Changed += onLibraryChanged;
        }

        private void subscribeToCollections() =>
            realmSubscription = realm.RegisterForNotifications(r => r.All<BeatmapCollection>().OrderBy(c => c.Name),
                (items, changes) => refresh(changes?.NewModifiedIndices.Select(index => items[index].ID).ToHashSet()));

        private void onLibraryChanged() => Schedule(() => refresh());

        private CollectionFilterMenuItem[] availableCollections()
        {
            var native = realm.Realm.All<BeatmapCollection>().OrderBy(c => c.Name).AsEnumerable()
                              .Select(c => new CollectionFilterMenuItem(c.ToLive(realm))).ToArray();
            if (library == null)
                return reverseCollections ? native.Reverse().ToArray() : native;

            var ids = native.Select(item => item.Collection!.ID).ToHashSet();
            var subgroups = library.Subgroups.Where(s => ids.Contains(s.CollectionId))
                                   .Select(s => new RitsuCollectionFilterMenuItem(s.Id, $"↳ {s.Name}"));
            var smart = library.SmartCollections.Select(c => new RitsuCollectionFilterMenuItem(c.Id, $"⚡ {c.Name}"));
            var items = native.Concat(subgroups).Concat(smart);
            if (reverseCollections)
                items = items.Reverse();
            return library.MenuOrder(RitsuMenuOrdering.Collections).Apply(items, scope, item => item.CollectionName.ToString());
        }

        private void refresh(IReadOnlySet<Guid>? modified = null)
        {
            synchronising = true;
            try
            {
                var entries = new List<CollectionFilterMenuItem> { allBeatmapsItem };
                entries.AddRange(availableCollections());
                if (ShowManageCollectionsItem)
                    entries.Add(new ManageCollectionsFilterMenuItem());

                var selected = entries.FirstOrDefault(item => scope(item) == configCollectionFilter.Value) ?? allBeatmapsItem;
                if (Current.Value?.Equals(selected) == true && Current.Value.CollectionName != selected.CollectionName)
                    Current.Value = allBeatmapsItem;

                for (int i = filters.Count - 1; i >= 0; i--)
                {
                    if (!entries.Contains(filters[i]))
                        filters.RemoveAt(i);
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    int existingIndex = filters.IndexOf(entry);
                    if (existingIndex == i && filters[i].CollectionName == entry.CollectionName
                        && (entry.Collection is not { } collection || modified?.Contains(collection.ID) != true))
                        continue;

                    if (existingIndex >= 0)
                        filters.RemoveAt(existingIndex);
                    filters.Insert(i, entry);
                }

                Current.Value = selected;
                if (ReferenceEquals(selected, allBeatmapsItem))
                    configCollectionFilter.Value = string.Empty;
            }
            finally
            {
                synchronising = false;
            }
        }

        private void refreshSelection()
        {
            if (synchronising)
                return;

            synchronising = true;
            try
            {
                Current.Value = filters.FirstOrDefault(item => scope(item) == configCollectionFilter.Value) ?? allBeatmapsItem;
            }
            finally
            {
                synchronising = false;
            }
        }

        private static string scope(CollectionFilterMenuItem item) => item switch
        {
            RitsuCollectionFilterMenuItem virtualCollection => virtualCollection.Id.ToString(),
            _ => item.Collection?.ID.ToString() ?? string.Empty,
        };

        private void selectionChanged(ValueChangedEvent<CollectionFilterMenuItem> filter)
        {
            if (synchronising || filter.NewValue.IsNull())
                return;

            if (filter.NewValue is ManageCollectionsFilterMenuItem)
            {
                Current.Value = filter.OldValue;
                if (songSelect != null)
                    songSelect.ManageCollections();
                else
                    manageCollectionsDialog?.Show();
                return;
            }

            string selection = scope(filter.NewValue);
            configCollectionFilter.Value = selection;
            if (selection.Length > 0 && library != null && Menu.State == MenuState.Open)
                RitsuMenuOrdering.RecordUse(library, RitsuMenuOrdering.Collections, selection);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Right || library == null || dialogs == null)
                return base.OnMouseDown(e);

            dialogs.Push(new RitsuMenuOrdering.OrderDialog(RitsuMenuOrdering.Collections, library,
                availableCollections().Select(item => new RitsuMenuOrdering.Entry(scope(item), item.CollectionName.ToString())).ToArray()));
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            realmSubscription?.Dispose();
            if (library != null)
                library.Changed -= onLibraryChanged;
        }

        protected override LocalisableString GenerateItemText(CollectionFilterMenuItem item) => item.CollectionName;

        protected sealed override DropdownMenu CreateMenu() => CreateCollectionMenu();

        protected virtual ShearedCollectionDropdownMenu CreateCollectionMenu() => new ShearedCollectionDropdownMenu();

        protected partial class ShearedCollectionDropdownMenu : ShearedDropdownMenu
        {
            public ShearedCollectionDropdownMenu()
            {
                MaxHeight = 200;
            }

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new DrawableCollectionMenuItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour
            };
        }

        protected partial class DrawableCollectionMenuItem : ShearedDropdownMenu.ShearedMenuItem
        {
            private IconButton addOrRemoveButton = null!;

            private bool beatmapInCollection;

            private readonly Live<BeatmapCollection>? collection;

            [Resolved]
            private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

            public DrawableCollectionMenuItem(MenuItem item)
                : base(item)
            {
                collection = ((DropdownMenuItem<CollectionFilterMenuItem>)item).Value.Collection;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(addOrRemoveButton = new NoFocusChangeIconButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Shear = -OsuGame.SHEAR,
                    X = -OsuScrollContainer.SCROLL_BAR_WIDTH,
                    Scale = new Vector2(0.65f),
                    Action = addOrRemove,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (collection != null)
                {
                    beatmap.BindValueChanged(_ =>
                    {
                        beatmapInCollection = collection.PerformRead(c => c.BeatmapMD5Hashes.Contains(beatmap.Value.BeatmapInfo.MD5Hash));

                        addOrRemoveButton.Enabled.Value = !beatmap.IsDefault;
                        addOrRemoveButton.Icon = beatmapInCollection ? FontAwesome.Solid.MinusSquare : FontAwesome.Solid.PlusSquare;
                        addOrRemoveButton.TooltipText = beatmapInCollection ? CollectionsStrings.RemoveSelectedBeatmap : CollectionsStrings.AddSelectedBeatmap;

                        updateButtonVisibility();
                    }, true);
                }

                updateButtonVisibility();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateButtonVisibility();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateButtonVisibility();
                base.OnHoverLost(e);
            }

            protected override void OnSelectChange()
            {
                base.OnSelectChange();
                updateButtonVisibility();
            }

            private void updateButtonVisibility()
            {
                if (collection == null)
                    addOrRemoveButton.Alpha = 0;
                else
                    addOrRemoveButton.Alpha = IsHovered || IsPreSelected || beatmapInCollection ? 1 : 0;
            }

            [Resolved(canBeNull: true)]
            private RitsuLibrary? library { get; set; }

            [Resolved(canBeNull: true)]
            private ManageCollectionsDialog? manager { get; set; }

            private void addOrRemove()
            {
                Debug.Assert(collection != null);
                if (library != null && manager != null && collection.PerformRead(c => c.BeatmapMD5Hashes.Contains(beatmap.Value.BeatmapInfo.MD5Hash)))
                {
                    manager.RemoveMembers(collection.ID, null, new[] { beatmap.Value.BeatmapInfo.MD5Hash });
                    return;
                }

                Task.Run(() => collection.PerformWrite(c =>
                {
                    if (!c.BeatmapMD5Hashes.Remove(beatmap.Value.BeatmapInfo.MD5Hash))
                        c.BeatmapMD5Hashes.Add(beatmap.Value.BeatmapInfo.MD5Hash);
                }));
            }

            protected override Drawable CreateContent() => (Content)base.CreateContent();

            private partial class NoFocusChangeIconButton : IconButton
            {
                public override bool ChangeFocusOnClick => false;
            }
        }
    }
}
