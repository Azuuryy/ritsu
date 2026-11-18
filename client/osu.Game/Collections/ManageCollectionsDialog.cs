// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Library;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Backdrops;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Offline;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Collections
{
    public partial class ManageCollectionsDialog : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private IDisposable? duckOperation;

        private BasicSearchTextBox searchTextBox = null!;
        private DrawableCollectionList list = null!;
        private Action? undo;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private RitsuLibrary? library { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogs { get; set; }

        internal void RemoveMembers(Guid collectionId, Guid? subgroupId, IEnumerable<string> hashes)
        {
            var requested = hashes.ToHashSet();
            if (subgroupId != null)
            {
                var subgroup = library?.Subgroups.FirstOrDefault(s => s.Id == subgroupId && s.CollectionId == collectionId);
                if (subgroup == null)
                    return;

                var removed = subgroup.Hashes.Where(requested.Contains).ToArray();
                if (removed.Length == 0)
                    return;
                library!.SaveSubgroup(subgroup with { Hashes = subgroup.Hashes.Where(h => !requested.Contains(h)).ToArray() });
                undo = () => restoreSubgroupMembers(subgroup.Id, removed);
                return;
            }

            var removedFromCollection = realm.Write(r =>
            {
                var collection = r.Find<BeatmapCollection>(collectionId);
                if (collection == null)
                    return Array.Empty<string>();

                var removed = collection.BeatmapMD5Hashes.Where(requested.Contains).ToArray();
                foreach (string hash in removed)
                    collection.BeatmapMD5Hashes.Remove(hash);
                if (removed.Length > 0)
                    collection.LastModified = DateTimeOffset.UtcNow;
                return removed;
            });

            if (removedFromCollection.Length == 0)
                return;

            var removedSet = removedFromCollection.ToHashSet();
            var subgroupMembers = library?.Subgroups.Where(s => s.CollectionId == collectionId)
                                          .Select(s => (s.Id, Hashes: s.Hashes.Where(removedSet.Contains).ToArray()))
                                          .Where(s => s.Hashes.Length > 0).ToArray();

            try
            {
                if (subgroupMembers is { Length: > 0 })
                    library!.RemoveSubgroupMembers(collectionId, removedSet);
            }
            catch
            {
                RitsuCollectionMenu.Add(realm, collectionId, removedFromCollection);
                throw;
            }

            undo = () =>
            {
                if (RitsuCollectionMenu.Add(realm, collectionId, removedFromCollection) && subgroupMembers != null)
                {
                    foreach (var (id, members) in subgroupMembers)
                        restoreSubgroupMembers(id, members);
                }
            };
        }

        private void restoreSubgroupMembers(Guid id, IEnumerable<string> hashes)
        {
            var subgroup = library?.Subgroups.FirstOrDefault(s => s.Id == id);
            if (subgroup != null)
                library!.SaveSubgroup(subgroup with { Hashes = subgroup.Hashes.Union(hashes).ToArray() });
        }

        internal void DeleteSubgroup(Guid id)
        {
            var subgroup = library?.Subgroups.FirstOrDefault(s => s.Id == id);
            if (subgroup == null)
                return;
            library!.DeleteSubgroup(id);
            undo = () => library.SaveSubgroup(subgroup);
        }

        internal void DeleteCollection(Guid id)
        {
            var snapshot = realm.Write(r =>
            {
                var collection = r.Find<BeatmapCollection>(id);
                if (collection == null)
                    return null;

                var copy = new DeletedCollection(collection.Name, collection.LastModified, collection.BeatmapMD5Hashes.ToArray());
                r.Remove(collection);
                return copy;
            });

            if (snapshot == null)
                return;

            undo = () => realm.Write(r =>
            {
                if (r.Find<BeatmapCollection>(id) != null)
                    return;

                var restored = r.Add(new BeatmapCollection(snapshot.Name) { ID = id, LastModified = snapshot.LastModified });
                foreach (string hash in snapshot.Hashes)
                    restored.BeatmapMD5Hashes.Add(hash);
            });
        }

        private sealed record DeletedCollection(string Name, DateTimeOffset LastModified, string[] Hashes);

        internal void UndoRemoval()
        {
            undo?.Invoke();
            undo = null;
        }

        [Resolved]
        private MusicController? musicController { get; set; }

        public ManageCollectionsDialog()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.5f, 0.8f);

            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Children = new Drawable[]
            {
                // torii dark glass: fondo del dialog de colecciones como vidrio
                new GlassBackdrop
                {
                    TintColour = colours.GreySeaFoamDark.Opacity(ThemeAware.GlassAlpha(0.8f)),
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Text = CollectionsStrings.ManageCollectionsTitle,
                                            Font = OsuFont.GetFont(size: 30),
                                            Padding = new MarginPadding { Vertical = 10 },
                                        },
                                        new IconButton
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            X = 10,
                                            Alpha = library == null ? 0 : 1,
                                            Icon = FontAwesome.Solid.Edit,
                                            TooltipText = "Edit collection contents",
                                            Action = () =>
                                            {
                                                if (library != null)
                                                    dialogs?.Push(new RitsuCollectionEditor(this, library));
                                            },
                                        },
                                        new IconButton
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            X = 43,
                                            Alpha = library == null ? 0 : 1,
                                            Icon = FontAwesome.Solid.Undo,
                                            TooltipText = "Undo last removal",
                                            Action = UndoRemoval,
                                        },
                                        new IconButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Icon = FontAwesome.Solid.Times,
                                            Colour = colours.GreySeaFoamDarker,
                                            Scale = new Vector2(0.8f),
                                            X = -10,
                                            Action = () => State.Value = Visibility.Hidden
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colours.GreySeaFoamDarker,
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding(10),
                                            Children = new Drawable[]
                                            {
                                                list = new DrawableCollectionList
                                                {
                                                    Padding = new MarginPadding { Vertical = 50 },
                                                    RelativeSizeAxes = Axes.Both,
                                                },
                                                searchTextBox = new BasicSearchTextBox
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 40,
                                                    ReleaseFocusOnCommit = false,
                                                    HoldFocus = true,
                                                    PlaceholderText = HomeStrings.SearchPlaceholder,
                                                },
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Children = new Drawable[]
                                                    {
                                                        new NewCollectionEntryItem()
                                                    }
                                                },
                                            }
                                        },
                                    }
                                }
                            },
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            searchTextBox.Current.BindValueChanged(_ =>
            {
                list.SearchTerm = searchTextBox.Current.Value;
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            duckOperation?.Dispose();
        }

        protected override void PopIn()
        {
            duckOperation = musicController?.Duck(new DuckParameters
            {
                DuckVolumeTo = 1,
                DuckDuration = 100,
                RestoreDuration = 100,
            });

            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            duckOperation?.Dispose();

            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);

            // Ensure that textboxes commit
            GetContainingFocusManager()?.TriggerFocusContention(this);
        }

        private partial class NewCollectionEntryItem : DrawableCollectionListItem
        {
            [Resolved]
            private RealmAccess realm { get; set; } = null!;

            public NewCollectionEntryItem()
                : base(new BeatmapCollection().ToLiveUnmanaged(), false)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                TextBox.OnCommit += (_, _) =>
                {
                    if (string.IsNullOrEmpty(TextBox.Text))
                        return;

                    realm.Write(r => r.Add(new BeatmapCollection(TextBox.Text)));
                    TextBox.Text = string.Empty;
                };
            }
        }
    }
}
