// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Extensions;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class RitsuBeatmapCatalogue : CompositeDrawable
    {
        private readonly RitsuCatalogueClient catalogue = new RitsuCatalogueClient();
        private readonly BeatmapListingSearchControl filters;
        private readonly BeatmapListingSortTabControl sorting;
        private readonly OsuSpriteText status;
        private const float card_spacing = 10;
        private const int overscan_rows = 2;
        private readonly Drawable viewport;
        private readonly Container results;
        private readonly List<APIBeatmapSet> beatmapSets = new List<APIBeatmapSet>();
        private readonly HashSet<int> resultIds = new HashSet<int>();
        private readonly Dictionary<int, BeatmapCardNormal> visibleCards = new Dictionary<int, BeatmapCardNormal>();
        private readonly RoundedButton next;
        private readonly Container resultsArea;
        private CancellationTokenSource? detailCancellation;
        private float gridWidth;
        private int firstVisibleIndex = -1;
        private int lastVisibleIndex = -1;

        [Resolved]
        private BeatmapSetOverlay details { get; set; } = null!;
        private CancellationTokenSource? pending;
        private ScheduledDelegate? debounce;
        private string? cursor;
        private int generation;
        private int emptyPageCount;

        public RitsuBeatmapCatalogue(Drawable viewport)
        {
            this.viewport = viewport;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    filters = new BeatmapListingSearchControl(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0.1f, 0.12f, 0.14f, 1f) },
                            sorting = new BeatmapListingSortTabControl { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Margin = new MarginPadding { Left = 20 } },
                        },
                    },
                    status = new OsuSpriteText { Text = "Search Torii to browse beatmaps." },
                    resultsArea = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Child = results = new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Width = BeatmapCard.WIDTH,
                        },
                    },
                    next = new RoundedButton { RelativeSizeAxes = Axes.X, Height = 40, Text = "Load more", Alpha = 0, Action = () => startSearch(true) },
                }
            };
            filters.SetToriiOptions(true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            filters.Query.BindValueChanged(_ => { sorting.Reset(filters.Category.Value, !string.IsNullOrEmpty(filters.Query.Value)); queueSearch(); });
            filters.Category.BindValueChanged(_ => { sorting.Reset(filters.Category.Value, !string.IsNullOrEmpty(filters.Query.Value)); queueSearch(); });
            filters.General.CollectionChanged += (_, _) => queueSearch();
            filters.Ruleset.BindValueChanged(_ => queueSearch());
            filters.Genre.BindValueChanged(_ => queueSearch());
            filters.Language.BindValueChanged(_ => queueSearch());
            filters.Extra.CollectionChanged += (_, _) => queueSearch();
            filters.Ranks.CollectionChanged += (_, _) => queueSearch();
            filters.Played.BindValueChanged(_ => queueSearch());
            filters.Downloaded.BindValueChanged(_ => queueSearch());
            filters.ExplicitContent.BindValueChanged(_ => queueSearch());
            sorting.Current.BindValueChanged(_ => queueSearch());
            sorting.SortDirection.BindValueChanged(_ => queueSearch());
            queueSearch();
        }

        private void queueSearch()
        {
            resetResults();
            debounce = Scheduler.AddDelayed(() => startSearch(false), 500);
        }

        private void resetResults()
        {
            debounce?.Cancel();
            cancelSearch();
            detailCancellation?.Cancel();
            clearResults();
            next.Alpha = 0;
            status.Text = "Search Torii. Downloads are imported into the currently selected beatmap library.";
        }

        private void cancelSearch()
        {
            generation++;
            pending?.Cancel();
            pending?.Dispose();
            pending = null;
            cursor = null;
        }

        private void startSearch(bool more)
        {
            debounce?.Cancel();
            string text = filters.Query.Value ?? string.Empty;
            string? page = more ? cursor : null;
            cancelSearch();
            if (!more)
            {
                clearResults();
                emptyPageCount = 0;
            }
            next.Alpha = 0;
            if (filters.Category.Value is SearchCategory.Mine or SearchCategory.Favourites || filters.Played.Value != SearchPlayed.Any || filters.Ranks.Any() || filters.Downloaded.Value != SearchDownloaded.Any || filters.General.Contains(SearchGeneral.Recommended) || filters.General.Contains(SearchGeneral.Follows))
            {
                status.Text = "Account and downloaded-map filters are unavailable in this catalogue.";
                return;
            }
            var parameters = new Dictionary<string, string>
            {
                ["s"] = filters.Category.Value.ToString().ToLowerInvariant(),
                ["sort"] = $"{sorting.Current.Value.ToString().ToLowerInvariant()}_{(sorting.SortDirection.Value == SortDirection.Descending ? "desc" : "asc")}",
                ["nsfw"] = filters.ExplicitContent.Value == SearchExplicit.Show ? "true" : "false",
            };
            if (filters.Ruleset.Value?.OnlineID >= 0) parameters["m"] = filters.Ruleset.Value.OnlineID.ToString();
            if (filters.Genre.Value != SearchGenre.Any) parameters["g"] = ((int)filters.Genre.Value).ToString();
            if (filters.Language.Value != SearchLanguage.Any) parameters["l"] = ((int)filters.Language.Value).ToString();
            if (filters.Extra.Any()) parameters["e"] = string.Join('.', filters.Extra.Select(e => e.ToString().ToLowerInvariant()));
            var general = filters.General.Where(g => g != SearchGeneral.ToriiExclusive && g != SearchGeneral.AiGenerated).ToArray();
            if (general.Length > 0) parameters["c"] = string.Join('.', general.Select(g => g.ToString().ToSnakeCase()));
            if (!filters.General.Contains(SearchGeneral.ToriiExclusive)) parameters["hide_local"] = "true";
            if (!filters.General.Contains(SearchGeneral.AiGenerated)) parameters["hide_ai"] = "true";
            pending = new CancellationTokenSource();
            int current = generation;
            status.Text = "Searching...";
            _ = searchAsync(text, page, parameters, current, pending.Token);
        }

        private async Task searchAsync(string text, string? page, IReadOnlyDictionary<string, string> parameters, int current, CancellationToken cancellation)
        {
            try
            {
                var data = await catalogue.SearchAsync(text, page, cancellation, parameters).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (current != generation) return;
                    int previousCount = beatmapSets.Count;
                    beatmapSets.AddRange(data.BeatmapSets.Where(set => resultIds.Add(set.OnlineID)));
                    int added = beatmapSets.Count - previousCount;
                    cursor = data.Cursor;
                    if (added == 0 && !string.IsNullOrEmpty(cursor) && cursor != page && ++emptyPageCount <= 5)
                    {
                        startSearch(true);
                        return;
                    }
                    emptyPageCount = 0;
                    if (cursor == page && !string.IsNullOrEmpty(cursor))
                    {
                        next.Alpha = 1;
                        status.Text = "No new maps returned. Try loading again.";
                        return;
                    }
                    next.Alpha = string.IsNullOrEmpty(cursor) ? 0 : 1;
                    status.Text = added == 0 ? "No new maps found." : string.Empty;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                Schedule(() =>
                {
                    if (current != generation) return;
                    cursor = page;
                    next.Alpha = string.IsNullOrEmpty(cursor) ? 0 : 1;
                    status.Text = "Search failed. Check your connection and try again.";
                });
            }
        }

        private void clearResults()
        {
            beatmapSets.Clear();
            resultIds.Clear();
            visibleCards.Clear();
            results.Clear();
            results.Height = 0;
            firstVisibleIndex = lastVisibleIndex = -1;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            if (beatmapSets.Count == 0)
                return;

            float width = resultsArea.DrawWidth;
            if (width <= 0)
                return;

            int columns = Math.Max(1, (int)((width + card_spacing) / (BeatmapCard.WIDTH + card_spacing)));
            float scale = Math.Min(1.25f, (width - (columns - 1) * card_spacing) / (columns * BeatmapCard.WIDTH));
            float rowHeight = BeatmapCardNormal.HEIGHT * scale + card_spacing;
            int rows = (beatmapSets.Count + columns - 1) / columns;
            results.Width = columns * (BeatmapCard.WIDTH * scale + card_spacing) - card_spacing;
            results.Height = rows * rowHeight - card_spacing;

            var bounds = viewport.ScreenSpaceDrawQuad;
            float top = results.ToLocalSpace(bounds.TopLeft).Y;
            float bottom = results.ToLocalSpace(bounds.BottomLeft).Y;
            int first = Math.Clamp((int)Math.Floor(top / rowHeight) - overscan_rows, 0, rows) * columns;
            int last = Math.Min(beatmapSets.Count, Math.Clamp((int)Math.Ceiling(bottom / rowHeight) + overscan_rows, 0, rows) * columns);
            if (first == firstVisibleIndex && last == lastVisibleIndex && width == gridWidth)
                return;

            firstVisibleIndex = first;
            lastVisibleIndex = last;
            gridWidth = width;

            foreach (int index in visibleCards.Keys.Where(index => index < first || index >= last).ToArray())
            {
                results.Remove(visibleCards[index], true);
                visibleCards.Remove(index);
            }

            for (int index = first; index < last; index++)
            {
                if (!visibleCards.TryGetValue(index, out var card))
                {
                    var set = beatmapSets[index];
                    card = new BeatmapCardNormal(set, allowExpansion: false);
                    card.Action = () => _ = showDetailsAsync(set.OnlineID);
                    visibleCards.Add(index, card);
                    results.Add(card);
                }

                card.Position = new Vector2(index % columns * (BeatmapCard.WIDTH * scale + card_spacing), index / columns * rowHeight);
                card.Scale = new Vector2(scale);
            }
        }

        private async Task showDetailsAsync(int id)
        {
            detailCancellation?.Cancel();
            detailCancellation?.Dispose();
            detailCancellation = new CancellationTokenSource();
            var token = detailCancellation.Token;
            status.Text = "Loading beatmap details...";
            try
            {
                var set = await catalogue.GetBeatmapSetAsync(id, token).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (token.IsCancellationRequested) return;
                    details.ShowBeatmapSet(set);
                    status.Text = $"{results.Count} beatmapsets loaded.";
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                Schedule(() => { if (!token.IsCancellationRequested) status.Text = "Could not load beatmap details. Click the card to retry."; });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            debounce?.Cancel();
            detailCancellation?.Cancel();
            detailCancellation?.Dispose();
            cancelSearch();
            if (isDisposing) catalogue.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
