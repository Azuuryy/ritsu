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
        private readonly FillFlowContainer<BeatmapCard> results;
        private readonly RoundedButton next;
        private readonly Container resultsArea;
        private CancellationTokenSource? detailCancellation;
        private float gridWidth;

        [Resolved]
        private BeatmapSetOverlay details { get; set; } = null!;
        private CancellationTokenSource? pending;
        private ScheduledDelegate? debounce;
        private string? cursor;
        private int generation;

        public RitsuBeatmapCatalogue()
        {
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
                        Child = results = new FillFlowContainer<BeatmapCard>
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(10),
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
            results.Clear();
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
            if (!more) results.Clear();
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
                    var existing = results.Select(c => c.BeatmapSet.OnlineID).ToHashSet();
                    foreach (var set in data.BeatmapSets.Where(s => existing.Add(s.OnlineID)))
                    {
                        var card = new BeatmapCardNormal(set, allowExpansion: false);
                        card.Action = () => _ = showDetailsAsync(set.OnlineID);
                        results.Add(card);
                    }
                    gridWidth = 0;
                    cursor = data.Cursor;
                    next.Alpha = string.IsNullOrEmpty(cursor) ? 0 : 1;
                    status.Text = $"";
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

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            float width = resultsArea.DrawWidth;
            if (width <= 0 || Math.Abs(width - gridWidth) < 0.1f) return;
            gridWidth = width;
            int columns = Math.Max(1, (int)((width + 10) / (BeatmapCard.WIDTH + 10)));
            float scale = Math.Min(1.25f, (width - (columns - 1) * 10) / (columns * BeatmapCard.WIDTH));
            results.Width = columns * BeatmapCard.WIDTH * scale + (columns - 1) * 10 + 0.1f;
            foreach (var card in results) card.Scale = new Vector2(scale);
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
