// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Input.Events;
using osuTK.Input;
using osu.Game.Library;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Offline;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    internal partial class SearchHistoryControls : CompositeDrawable, IHasPopover
    {
        public const float WIDTH = 28;

        private readonly FilterControl.SongSelectSearchTextBox search;
        private readonly IconButton favouriteButton;
        private readonly IHasPopover target;
        private readonly Bindable<string> query = new Bindable<string>();
        private readonly Bindable<bool> isFavourite = new Bindable<bool>();
        private readonly BindableInt selectedSuggestion = new BindableInt();
        private RitsuSearchCompletion.Completion[] suggestions = Array.Empty<RitsuSearchCompletion.Completion>();
        private Action<RitsuSearchCompletion.Completion>? applySuggestion;

        public bool SuggestionsOpen => this.FindClosestParent<PopoverContainer>()?.CurrentTarget == this;

        public void Complete(string text, int caret, Action<RitsuSearchCompletion.Completion> apply)
        {
            if (SuggestionsOpen)
                this.HidePopover();
            suggestions = RitsuSearchCompletion.Get(text, caret);
            selectedSuggestion.Value = 0;
            applySuggestion = apply;
            if (suggestions.Length > 0)
                this.ShowPopover();
        }

        public bool HandleSuggestionKey(KeyDownEvent e)
        {
            if (!SuggestionsOpen)
                return false;

            switch (e.Key)
            {
                case Key.Down:
                    selectedSuggestion.Value = (selectedSuggestion.Value + 1) % suggestions.Length;
                    return true;

                case Key.Up:
                    selectedSuggestion.Value = (selectedSuggestion.Value + suggestions.Length - 1) % suggestions.Length;
                    return true;

                case Key.Enter:
                case Key.KeypadEnter:
                case Key.Tab:
                    acceptSuggestion(selectedSuggestion.Value);
                    return true;

                case Key.Escape:
                    this.HidePopover();
                    return true;

                default:
                    return false;
            }
        }

        public Popover GetPopover() => new SuggestionsPopover(suggestions, selectedSuggestion, acceptSuggestion);

        private void acceptSuggestion(int index)
        {
            this.HidePopover();
            applySuggestion?.Invoke(suggestions[index]);
        }

        private partial class SuggestionsPopover : OsuPopover
        {
            public override bool RequestsFocus => false;
            public override bool AcceptsFocus => false;

            public SuggestionsPopover(RitsuSearchCompletion.Completion[] suggestions, BindableInt selection, Action<int> apply)
            {
                AllowableAnchors = new[] { Anchor.BottomLeft, Anchor.BottomRight };
                Child = new FillFlowContainer
                {
                    Width = 180,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    ChildrenEnumerable = suggestions.Select((suggestion, index) => new SuggestionButton(suggestion.Label, index, selection, apply)),
                };
            }

            private partial class SuggestionButton : OsuButton
            {
                private readonly BindableNumber<int> selection;

                public SuggestionButton(string label, int index, BindableInt selection, Action<int> apply)
                {
                    this.selection = selection.GetBoundCopy();
                    Text = label;
                    RelativeSizeAxes = Axes.X;
                    Height = 25;
                    Action = () => apply(index);
                    this.selection.BindValueChanged(value =>
                    {
                        BackgroundColour = value.NewValue == index ? new Color4(0.28f, 0.34f, 0.44f, 1f) : Color4.Transparent;
                    }, true);
                }
            }
        }

        public SearchHistoryControls(FilterControl.SongSelectSearchTextBox search, IHasPopover target)
        {
            this.search = search;
            this.target = target;
            Size = new Vector2(WIDTH, 26);
            InternalChild = favouriteButton = new IconButton
            {
                Size = new Vector2(WIDTH, 26),
                Action = search.ToggleCurrentFavourite,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            query.BindTo(search.Current);
            isFavourite.BindTo(search.CurrentIsFavourite);
            query.BindValueChanged(_ =>
            {
                updateFavourite();

                var bar = (Drawable)target;
                if (bar.FindClosestParent<PopoverContainer>()?.CurrentTarget == target)
                    bar.HidePopover();
            }, true);
            isFavourite.BindValueChanged(_ => updateFavourite());
        }

        public void ShowHistory()
        {
            if (SuggestionsOpen)
                this.HidePopover();

            if (((Drawable)target).FindClosestParent<PopoverContainer>() is { } container && container.CurrentTarget != target)
                target.ShowPopover();
        }

        private void updateFavourite()
        {
            bool saved = isFavourite.Value;
            favouriteButton.Enabled.Value = !string.IsNullOrWhiteSpace(query.Value);
            favouriteButton.Icon = saved ? FontAwesome.Solid.Star : FontAwesome.Regular.Star;
            favouriteButton.TooltipText = saved ? "Remove search from favourites" : "Add search to favourites (Ctrl+Left Alt to complete a filter)";
        }

        internal partial class HistoryPopover : OsuPopover
        {
            private const float row_height = 26;

            private readonly FilterControl filter;
            private readonly FilterControl.SongSelectSearchTextBox search;

            [Resolved(canBeNull: true)]
            private RitsuLibrary? library { get; set; }

            [Resolved(canBeNull: true)]
            private IDialogOverlay? dialogs { get; set; }

            private readonly float width;

            public override bool RequestsFocus => false;
            public override bool AcceptsFocus => false;

            public HistoryPopover(FilterControl filter, FilterControl.SongSelectSearchTextBox search, Drawable bar)
                : base(withPadding: false)
            {
                this.filter = filter;
                this.search = search;
                width = bar.DrawWidth;
                Body.X = -bar.DrawHeight * bar.Shear.X / 2;
                Body.Margin = new MarginPadding { Top = 3 };
                Body.Shear = OsuGame.SHEAR;
                AllowableAnchors = new[] { Anchor.BottomCentre };
            }

            [BackgroundDependencyLoader(true)]
            private void load(OsuColour colours, OverlayColourProvider? colourProvider)
            {
                var rows = new FillFlowContainer<Drawable>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(8),
                    Spacing = new Vector2(0, 3),
                };

                var headingColour = colourProvider?.Content2 ?? colours.GrayC;
                IconButton? clearButton = search.Recent.Count == 0 ? null : new IconButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Margin = new MarginPadding { Right = 4 },
                    Size = new Vector2(row_height),
                    Shear = -OsuGame.SHEAR,
                    Icon = FontAwesome.Solid.Trash,
                    IconScale = new Vector2(0.75f),
                    IconColour = colours.Red1,
                    IconHoverColour = colours.Red0,
                    TooltipText = "Clear history",
                    Action = () =>
                    {
                        search.ClearHistory();
                        this.HidePopover();
                    },
                };

                if (library != null)
                {
                    var addButton = new IconButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Size = new Vector2(row_height),
                        Icon = FontAwesome.Solid.Plus,
                        TooltipText = "Pin current search",
                        Action = () => editSearch(null),
                    };

                    var pinned = library.Searches;
                    addSection(rows, "Pinned searches", pinned.Select(item => item.Name), headingColour, addButton, index => pinnedRow(pinned[index]));
                }

                addSection(rows, SongSelectStrings.Favourites, search.Favourites, headingColour);
                addSection(rows, "Recent searches", search.Recent.Where(query => !search.Favourites.Contains(query)), headingColour, clearButton);

                Child = new OsuScrollContainer
                {
                    Width = width,
                    Height = Math.Min(320, rows.Padding.TotalVertical + rows.Sum(row => row.Height + row.Margin.TotalVertical) + (rows.Count - 1) * rows.Spacing.Y),
                    Child = rows,
                };
            }

            private Drawable pinnedRow(RitsuPinnedSearch item)
            {
                var menu = new List<OsuMenuItem>
                {
                    new OsuMenuItem("Rename", MenuItemType.Standard, () => editSearch(item)),
                    new OsuMenuItem("Replace with current filters", MenuItemType.Standard, () =>
                    {
                        var current = filter.CaptureSearch();
                        this.HidePopover();
                        if (library?.SmartCollections.Any(c => c.SearchId == item.Id) == true && isSmartScope(current.Collection))
                        {
                            dialogs?.Push(new ConfirmDialog("Choose a native collection or subgroup as the source.", () => { }));
                            return;
                        }
                        library?.SaveSearch(item.Id, item.Name, current);
                    }),
                    new OsuMenuItem("Delete", MenuItemType.Destructive, () =>
                    {
                        var dependent = library?.SmartCollections.Where(c => c.SearchId == item.Id).Select(c => c.Name).ToArray();
                        string message = dependent is { Length: > 0 } ? $"Delete {item.Name} and these smart collections?\n{string.Join(", ", dependent)}" : $"Delete {item.Name}?";
                        this.HidePopover();
                        dialogs?.Push(new ConfirmDialog(message, () => library?.DeleteSearch(item.Id)));
                    }),
                };

                if (!isSmartScope(item.State.Collection))
                {
                    menu.Insert(2, new OsuMenuItem("Create smart collection", MenuItemType.Standard, () =>
                    {
                        this.HidePopover();
                        dialogs?.Push(new RitsuInputDialog("Create smart collection", item.Name, name =>
                        {
                            if (name.Length == 0)
                                return "Enter a collection name.";
                            library?.AddSmartCollection(item.Id, name);
                            return null;
                        }, "This collection follows changes to the pinned search."));
                    }));
                }

                var linked = library?.SmartCollections.Where(c => c.SearchId == item.Id).ToArray();
                if (linked is { Length: > 0 })
                {
                    menu.Add(new OsuMenuItem("Delete a smart collection")
                    {
                        Items = linked.Select(c => new OsuMenuItem(c.Name, MenuItemType.Destructive, () =>
                        {
                            this.HidePopover();
                            dialogs?.Push(new ConfirmDialog($"Delete {c.Name}?", () => library?.DeleteSmartCollection(c.Id)));
                        })).ToArray(),
                    });
                }

                return new OsuContextMenuContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = row_height,
                    Child = new QueryButton(item.Name, () =>
                    {
                        this.HidePopover();
                        filter.RestoreSearch(item.State);
                    }) { ContextMenuItems = menu.ToArray() },
                };
            }

            private bool isSmartScope(string scope) => library?.SmartCollections.Any(c => c.Id.ToString() == scope) == true;

            private void editSearch(RitsuPinnedSearch? item)
            {
                var state = item?.State ?? filter.CaptureSearch();
                this.HidePopover();
                dialogs?.Push(new RitsuInputDialog(item == null ? "Pin search" : "Rename pinned search", item?.Name ?? state.Query, name =>
                {
                    if (name.Length == 0)
                        return "Enter a name for this search.";

                    library?.SaveSearch(item?.Id ?? Guid.NewGuid(), name, state);
                    return null;
                }));
            }

            private void addSection(FillFlowContainer<Drawable> rows, LocalisableString heading, IEnumerable<string> queries, Color4 headingColour,
                                    IconButton? action = null, Func<int, Drawable>? customRow = null)
            {
                var header = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = row_height,
                    Margin = new MarginPadding { Top = rows.Count == 0 ? 0 : 5 },
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 7,
                        Shear = -OsuGame.SHEAR,
                        Text = heading,
                        Font = OsuFont.Style.Caption1.With(weight: FontWeight.Bold),
                        Colour = headingColour,
                    },
                };

                if (action != null)
                    header.Add(action);

                rows.Add(header);
                rows.Add(new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Colour = headingColour,
                    Alpha = 0.22f,
                });

                foreach (var (query, index) in queries.DefaultIfEmpty(string.Empty).Select((query, index) => (query, index)))
                {
                    if (query.Length == 0)
                        rows.Add(new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = row_height,
                            Child = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 7,
                                Shear = -OsuGame.SHEAR,
                                Text = "None yet",
                                Font = OsuFont.Style.Caption1,
                            },
                        });
                    else if (customRow != null)
                        rows.Add(customRow(index));
                    else
                        rows.Add(new QueryButton(query, () =>
                        {
                            this.HidePopover();
                            search.Current.Value = query;
                        }));
                }
            }

            private partial class QueryButton : OsuButton, IHasTooltip, IHasContextMenu
            {
                public LocalisableString TooltipText => Text;
                public MenuItem[] ContextMenuItems { get; init; } = Array.Empty<MenuItem>();

                public QueryButton(string text, Action action)
                {
                    RelativeSizeAxes = Axes.X;
                    Height = row_height;
                    BackgroundColour = Color4.Transparent;
                    Text = text;
                    Action = action;
                    SpriteText.Anchor = SpriteText.Origin = Anchor.CentreLeft;
                    SpriteText.RelativeSizeAxes = Axes.X;
                    SpriteText.Width = 0.93f;
                    SpriteText.X = 7;
                    SpriteText.Shear = -OsuGame.SHEAR;
                    SpriteText.Truncate = true;
                    SpriteText.Font = OsuFont.Style.Caption1;
                }
            }
        }
    }
}
