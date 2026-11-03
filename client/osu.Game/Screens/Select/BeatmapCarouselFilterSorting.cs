// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;

namespace osu.Game.Screens.Select
{
    public class BeatmapCarouselFilterSorting : ICarouselFilter
    {
        public int BeatmapItemsCount { get; private set; }

        /// <summary>
        /// Used to sort by mod-adjusted star rating. When not set, an approximation is used for difficulty-altering mods.
        /// </summary>
        public BeatmapDifficultyCache? DifficultyCache { get; set; }

        /// <summary>
        /// The mod-adjusted star ratings used by the most recent sort, for each beatmap whose star rating was compared.
        /// </summary>
        public IReadOnlyDictionary<BeatmapInfo, double> LastEffectiveStarRatings { get; private set; } = new Dictionary<BeatmapInfo, double>();

        private readonly Func<FilterCriteria> getCriteria;

        public BeatmapCarouselFilterSorting(Func<FilterCriteria> getCriteria)
        {
            this.getCriteria = getCriteria;
        }

        public async Task<List<CarouselItem>> Run(IEnumerable<CarouselItem> items, CancellationToken cancellationToken) => await Task.Run(() =>
        {
            var criteria = getCriteria();

            bool groupedSets = BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria);

            BeatmapItemsCount = items.Count();

            var lookupEffectiveStarRating = criteria.CreateEffectiveStarRatingLookup(DifficultyCache);
            var effectiveStarRatings = new Dictionary<BeatmapInfo, double>(BeatmapItemsCount);

            // star rating adjusted for selected mods, cached per beatmap per run
            Func<BeatmapInfo, double> getEffectiveStarRating = beatmap =>
            {
                ref double stars = ref CollectionsMarshal.GetValueRefOrAddDefault(effectiveStarRatings, beatmap, out bool exists);
                if (!exists)
                    stars = lookupEffectiveStarRating(beatmap);

                return stars;
            };

            var list = items as List<CarouselItem> ?? items.ToList();

            Dictionary<BeatmapInfo, double?>? performance = null;
            if (criteria.Sort is SortMode.PerformancePoints or SortMode.Farmability)
            {
                performance = new Dictionary<BeatmapInfo, double?>();
                foreach (var item in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var beatmap = (BeatmapInfo)item.Model;
                    var result = criteria.PerformanceSession?.Get(beatmap) ?? new PerfectPerformanceResult(PerfectPerformanceState.Unavailable);
                    double? pp = result.State == PerfectPerformanceState.Available ? result.Value : null;
                    if (criteria.Sort == SortMode.Farmability)
                    {
                        double stars = getEffectiveStarRating(beatmap);
                        double rate = ModUtils.CalculateRateWithMods(criteria.Mods ?? Array.Empty<osu.Game.Rulesets.Mods.Mod>());
                        double seconds = beatmap.Length / 1000.0 / rate;
                        pp = CalculateFarmability(pp, seconds, stars);
                    }
                    performance[beatmap] = pp;
                }
            }

            list.Sort((a, b) =>
            {
                var ab = (BeatmapInfo)a.Model;
                var bb = (BeatmapInfo)b.Model;

                if (performance != null)
                {
                    double? left = performance[ab];
                    double? right = performance[bb];
                    if (!left.HasValue || !right.HasValue)
                        return left.HasValue ? -1 : right.HasValue ? 1 : ab.ID.CompareTo(bb.ID);
                    int result = right.Value.CompareTo(left.Value);
                    if (criteria.ReverseSort)
                        result = -result;
                    return result != 0 ? result : ab.ID.CompareTo(bb.ID);
                }

                if (groupedSets)
                {
                    if (ab.BeatmapSet!.Equals(bb.BeatmapSet))
                        return compareDifficulty(ab, bb, getEffectiveStarRating);

                    // If we're grouping by sets, all fallback sorts need to be aggregates for the set.
                    int result = compare(ab, bb, criteria.Sort, aggregate: true, getEffectiveStarRating);
                    return criteria.ReverseSort ? -Math.Sign(result) : result;
                }

                int ungroupedResult = compare(ab, bb, criteria.Sort, aggregate: false, getEffectiveStarRating);
                return criteria.ReverseSort ? -Math.Sign(ungroupedResult) : ungroupedResult;
            });

            // No longer modified now that sorting is done, so it can be shared as-is.
            LastEffectiveStarRatings = effectiveStarRatings;

            return list;
        }, cancellationToken).ConfigureAwait(false);

        internal static double? CalculateFarmability(double? pp, double seconds, double stars)
        {
            if (!pp.HasValue || !double.IsFinite(pp.Value) || pp.Value < 0 || seconds <= 0 || !double.IsFinite(seconds) || stars <= 0 || !double.IsFinite(stars))
                return null;
            double result = 1000 * pp.Value / seconds / (stars * stars);
            return double.IsFinite(result) ? result : null;
        }

        private static int compare(BeatmapInfo a, BeatmapInfo b, SortMode sort, bool aggregate, Func<BeatmapInfo, double> getEffectiveStarRating)
        {
            int comparison;

            switch (sort)
            {
                case SortMode.Artist:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Artist, b.BeatmapSet!.Metadata.Artist);
                    if (comparison == 0)
                        goto case SortMode.Title;
                    break;

                case SortMode.Title:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);
                    break;

                case SortMode.Author:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Author.Username, b.BeatmapSet!.Metadata.Author.Username);
                    break;

                case SortMode.Source:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Source, b.BeatmapSet!.Metadata.Source);
                    break;

                case SortMode.Difficulty:
                    comparison = getEffectiveStarRating(a).CompareTo(getEffectiveStarRating(b));
                    if (comparison == 0)
                        comparison = a.StarRating.CompareTo(b.StarRating);
                    break;

                case SortMode.DateAdded:
                    comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);
                    break;

                case SortMode.DateRanked:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateRanked, a.BeatmapSet!.DateRanked);
                    break;

                case SortMode.DateSubmitted:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateSubmitted, a.BeatmapSet!.DateSubmitted);
                    break;

                case SortMode.LastPlayed:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(b, a, static b => (b.LastPlayed ?? DateTimeOffset.MinValue).ToUnixTimeSeconds());
                    else
                        comparison = Nullable.Compare(b.LastPlayed, a.LastPlayed);
                    break;

                case SortMode.BPM:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(a, b, static b => b.BPM);
                    else
                        comparison = a.BPM.CompareTo(b.BPM);
                    break;

                case SortMode.Length:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(a, b, static b => b.Length);
                    else
                        comparison = a.Length.CompareTo(b.Length);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // If the initial sort could not differentiate, attempt to use Title then DateAdded to order sets in a stable fashion.
            if (comparison == 0)
                comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);

            // The directionality of this matches the current SortMode.DateAdded, but we may want to reconsider if that becomes a user decision (ie. asc / desc).
            if (comparison == 0)
                comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);

            // If both failed to break the tie, fallback to our internal GUID for stability.
            // This basically means it's a stable random sort.
            if (comparison == 0)
                comparison = b.BeatmapSet!.ID.CompareTo(a.BeatmapSet!.ID);

            // Difficulties from the same set can still tie. The sort is unstable, so break that tie by GUID too,
            // otherwise tied difficulties could swap places between filter runs.
            if (comparison == 0)
                comparison = b.ID.CompareTo(a.ID);

            return comparison;
        }

        private static int compareDifficulty(BeatmapInfo a, BeatmapInfo b, Func<BeatmapInfo, double> getEffectiveStarRating)
        {
            int comparison = a.Ruleset.CompareTo(b.Ruleset);

            if (comparison == 0)
                comparison = getEffectiveStarRating(a).CompareTo(getEffectiveStarRating(b));

            if (comparison == 0)
                comparison = a.StarRating.CompareTo(b.StarRating);

            // See the equivalent fallback in compare().
            if (comparison == 0)
                comparison = b.ID.CompareTo(a.ID);

            return comparison;
        }

        private static int compareUsingAggregateMax(BeatmapInfo a, BeatmapInfo b, Func<BeatmapInfo, double> func)
        {
            var aMatchedBeatmaps = a.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden);
            var bMatchedBeatmaps = b.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden);

            bool aAny = aMatchedBeatmaps.Any();
            bool bAny = bMatchedBeatmaps.Any();

            if (!aAny && !bAny) return 0;
            if (!aAny) return -1;
            if (!bAny) return 1;

            return aMatchedBeatmaps.Max(func).CompareTo(bMatchedBeatmaps.Max(func));
        }
    }
}
