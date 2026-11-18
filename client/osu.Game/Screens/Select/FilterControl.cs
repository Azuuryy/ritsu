// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Platform;
using osu.Framework.Logging;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Offline;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public sealed partial class FilterControl : OverlayContainer
    {
        // taken from draw visualiser. used for carousel alignment purposes.
        public const float HEIGHT_FROM_SCREEN_TOP = 141 - corner_radius;

        private const float corner_radius = 10;

        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

        private SongSelectSearchTextBox searchTextBox = null!;
        private SearchHistoryControls searchHistoryControls = null!;
        private ShearedToggleButton showConvertedBeatmapsButton = null!;
        private DifficultyRangeSlider difficultyRangeSlider = null!;
        private PerformanceRangeSlider performanceRangeSlider = null!;
        private Container performanceOptions = null!;
        private ShearedDropdown<int> performanceIntervalDropdown = null!;
        private ShearedToggleButton performanceCalculationToggle = null!;
        private readonly BindableDouble minimumPerformance = new BindableDouble(0) { MinValue = 0, MaxValue = 3000, Precision = 10 };
        private readonly BindableDouble maximumPerformance = new BindableDouble(3000) { MinValue = 0, MaxValue = 3000, Precision = 10 };
        private ShearedDropdown<SortMode> sortDropdown = null!;
        private bool reverseSort;
        private bool reverseGroup;
        private ShearedDropdown<GroupModeDropdownItem> groupDropdown = null!;
        private readonly Bindable<string> configCollectionFilter = new Bindable<string>();

        /// <summary>
        /// An optional method which can force certain criteria adjustments.
        /// </summary>
        public Action<FilterCriteria>? ApplyRequiredCriteria { get; set; }

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IBindable<APIUser> localUser = null!;
        private readonly IBindableList<int> localUserFavouriteBeatmapSets = new BindableList<int>();

        public LocalisableString StatusText
        {
            get => searchTextBox.StatusText;
            set => searchTextBox.StatusText = value;
        }

        public event Action<FilterCriteria>? CriteriaChanged;

        private FilterCriteria currentCriteria = null!;

        private IDisposable? collectionsSubscription;
        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;
        private ScheduledDelegate? sliderDebounce;
        private bool restoringSearch;
        private IDisposable? scoreSubscription;
        private ScheduledDelegate? weekCheck;
        private DateTimeOffset weekStart;
        private (Guid Profile, string Ruleset, DateTimeOffset Week)? historyKey;
        private Lazy<RitsuPlayHistory>? playHistory;

        [Resolved(canBeNull: true)]
        private RitsuLibrary? library { get; set; }

        [Resolved(canBeNull: true)]
        private RitsuProfileManager? profiles { get; set; }

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Shear = OsuGame.SHEAR;
            Margin = new MarginPadding { Top = -corner_radius, Right = -40 };

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = corner_radius,
                    Masking = true,
                    Child = new WedgeBackground
                    {
                        Anchor = Anchor.TopRight,
                        Scale = new Vector2(-1, 1),
                        Glass = true, // torii dark glass
                    }
                },
                new ReverseChildIDFillFlowContainer<Drawable>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Top = corner_radius + 5, Bottom = 2, Right = 40f, Left = 2f },
                    Children = new Drawable[]
                    {
                        new ReverseChildIDFillFlowContainer<Drawable>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0f, 5f),
                            Children = new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Shear = -OsuGame.SHEAR,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, SearchHistoryControls.WIDTH) },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            searchTextBox = new SongSelectSearchTextBox
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                HoldFocus = true,
                                                ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                                                SearchClicked = () => searchHistoryControls.ShowHistory(),
                                                CreatePopover = CreateSearchHistoryPopover,
                                                CompletionControls = () => searchHistoryControls,
                                            },
                                            searchHistoryControls = CreateSearchHistoryControls(searchTextBox),
                                        },
                                    },
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Shear = -OsuGame.SHEAR,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.Absolute), // can probably be removed?
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            difficultyRangeSlider = new DifficultyRangeSlider
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                MinRange = 0.1f,
                                                RightClickAction = () => performanceOptions.Alpha = performanceOptions.Alpha == 0 ? 1 : 0,
                                            },
                                            Empty(),
                                            showConvertedBeatmapsButton = new ShearedToggleButton
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                AutoSizeAxes = Axes.X,
                                                Text = UserInterfaceStrings.ShowConverts,
                                                Height = 30f,
                                            },
                                        },
                                    }
                                },
                                performanceOptions = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 30,
                                    Shear = -OsuGame.SHEAR,
                                    Alpha = 0,
                                    Child = new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 5), new Dimension(GridSizeMode.Absolute, 140), new Dimension(GridSizeMode.Absolute, 5), new Dimension(GridSizeMode.Absolute, 95) },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                performanceRangeSlider = new PerformanceRangeSlider
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    LowerBound = minimumPerformance,
                                                    UpperBound = maximumPerformance,
                                                    MinRange = 10,
                                                },
                                                Empty(),
                                                performanceIntervalDropdown = new ShearedDropdown<int>("PP step")
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Items = new[] { 10, 20, 50, 100, 200, 500, 1000 },
                                                    Current = { Value = 100 },
                                                },
                                                Empty(),
                                                performanceCalculationToggle = new PerformanceCalculationButton
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 30,
                                                    Text = "PP calc",
                                                },
                                            },
                                        },
                                    },
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 30,
                                    Shear = -OsuGame.SHEAR,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(maxSize: 180),
                                        new Dimension(GridSizeMode.Absolute, 5),
                                        new Dimension(maxSize: 180),
                                        new Dimension(GridSizeMode.Absolute, 5),
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            sortDropdown = new ShearedDropdown<SortMode>(SongSelectStrings.Sort)
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Items = Enum.GetValues<SortMode>(),
                                                RitsuOrderKey = RitsuMenuOrdering.Sort,
                                                MiddleClickAction = () => { reverseSort = !reverseSort; sortDropdown.SetReversedSortIndicator(reverseSort, SongSelectStrings.Sort); updateCriteria(clearScopedSet: false); },
                                            },
                                            Empty(),
                                            groupDropdown = new GroupModeDropdown(SongSelectStrings.Group)
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                RitsuOrderKey = RitsuMenuOrdering.Group,
                                                MiddleClickAction = () => { reverseGroup = !reverseGroup; groupDropdown.SetReversedSortIndicator(reverseGroup, SongSelectStrings.Group); updateCriteria(); },
                                            },
                                            Empty(),
                                            new CollectionDropdown
                                            {
                                                RelativeSizeAxes = Axes.X,
                                            },
                                        }
                                    }
                                },
                            },
                        },
                        new ScopedBeatmapSetDisplay
                        {
                            ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                        }
                    },
                },
            };

            localUser = api.LocalUser.GetBoundCopy();
            localUserFavouriteBeatmapSets.BindTo(api.LocalUserState.FavouriteBeatmapSets);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            difficultyRangeSlider.LowerBound = config.GetBindable<double>(OsuSetting.DisplayStarsMinimum);
            difficultyRangeSlider.UpperBound = config.GetBindable<double>(OsuSetting.DisplayStarsMaximum);
            config.BindWith(OsuSetting.ShowConvertedBeatmaps, showConvertedBeatmapsButton.Active);
            config.BindWith(OsuSetting.SongSelectSortingMode, sortDropdown.Current);
            config.BindWith(OsuSetting.RitsuPerformanceGroupInterval, performanceIntervalDropdown.Current);
            config.BindWith(OsuSetting.RitsuCalculateMapPerformance, performanceCalculationToggle.Active);
            config.BindWith(OsuSetting.SongSelectCollectionFilter, configCollectionFilter);

            ruleset.BindValueChanged(_ => updateCriteria());
            mods.BindValueChanged(m =>
            {
                // Settings are tracked per mod instance, so tracking must be refreshed even if the new mods are equal by value.
                trackModSettings(m.NewValue);

                // The following is a note carried from old song select and may not be a valid reason anymore:
                // // Mods are updated once by the mod select overlay when song select is entered,
                // // regardless of if there are any mods or any changes have taken place.
                // // Updating the criteria here so early triggers a re-ordering of panels on song select, via... some mechanism.
                // // Todo: Investigate/fix and potentially remove this.
                // TODO: this might be simply removable with the new song select & carousel code.
                if (m.NewValue.SequenceEqual(m.OldValue))
                    return;

                var rulesetCriteria = currentCriteria.RulesetCriteria;

                // Sorting, grouping and star rating filters use mod-adjusted star ratings,
                // so ordering, group membership or matches may change when difficulty-altering mods are toggled.
                bool difficultyCriteriaMayChange = (performanceCriteriaActive || difficultyCriteriaActive) && difficultyModsChanged(m.OldValue, m.NewValue);

                if (rulesetCriteria?.FilterMayChangeFromMods(currentCriteria, m) == true || difficultyCriteriaMayChange)
                    updateCriteria();
            });

            trackModSettings(mods.Value);

            searchTextBox.Current.BindValueChanged(_ => updateCriteria());


            // For slider dragging (where input events can arrive very often), even creating criteria can have
            // overhead, especially when a collection is selected (see ToImmutableHashSet() call).
            void debouncedUpdateCriteria()
            {
                if (restoringSearch)
                    return;

                sliderDebounce?.Cancel();
                sliderDebounce = Scheduler.AddDelayed(() => updateCriteria(), 50);
            }

            difficultyRangeSlider.LowerBound.BindValueChanged(_ => debouncedUpdateCriteria());
            difficultyRangeSlider.UpperBound.BindValueChanged(_ => debouncedUpdateCriteria());
            minimumPerformance.BindValueChanged(_ => debouncedUpdateCriteria());
            maximumPerformance.BindValueChanged(_ => debouncedUpdateCriteria());

            showConvertedBeatmapsButton.Active.BindValueChanged(_ => updateCriteria());
            sortDropdown.Current.BindValueChanged(_ => updateCriteria(clearScopedSet: false));
            groupDropdown.Current.BindValueChanged(_ => updateCriteria());
            performanceIntervalDropdown.Current.BindValueChanged(_ => updateCriteria());
            performanceCalculationToggle.Active.BindValueChanged(_ => updateCriteria());
            configCollectionFilter.BindValueChanged(_ => updateCriteria());
            collectionsSubscription = realm.RegisterForNotifications(r => r.All<BeatmapCollection>(), (_, changeSet) =>
            {
                if (changeSet != null && (groupDropdown.Current.Value.Value == GroupMode.Collections || configCollectionFilter.Value.Length > 0))
                    updateCriteria(clearScopedSet: false);
            });

            if (library != null)
                library.Changed += libraryChanged;
            if (library != null && profiles != null)
            {
                profiles.Changed += historyChanged;
                scoreSubscription = realm.RegisterForNotifications(r => r.All<ScoreInfo>(), (_, changes) =>
                {
                    if (changes != null)
                        historyChanged();
                });
                weekStart = RitsuPlayHistory.StartOfWeek(DateTimeOffset.Now, TimeZoneInfo.Local);
                weekCheck = Scheduler.AddDelayed(checkWeek, 30000, true);
            }

            localUser.BindValueChanged(_ => updateCriteria());
            localUserFavouriteBeatmapSets.BindCollectionChanged((_, _) => updateCriteria());
            ScopedBeatmapSet.BindValueChanged(_ => updateCriteria(clearScopedSet: false));

            updateCriteria();
        }

        /// <summary>
        /// Whether the current sorting, grouping or star rating filters use mod-adjusted star ratings.
        /// When beatmaps are grouped by set, difficulties within each set are also ordered by mod-adjusted star rating.
        /// </summary>
        private bool performanceCriteriaActive => currentCriteria.UsesPerformanceFilter || currentCriteria.Sort == SortMode.Farmability || currentCriteria.Sort == SortMode.PerformancePoints || currentCriteria.Group == GroupMode.PerformancePoints100;

        private bool difficultyCriteriaActive => currentCriteria.Sort == SortMode.Difficulty
                                                 || currentCriteria.Group == GroupMode.Difficulty
                                                 || currentCriteria.StarDifficulty.HasFilter
                                                 || currentCriteria.UserStarDifficulty.HasFilter
                                                 || currentCriteria.SmartCollectionCriteria?.StarDifficulty.HasFilter == true
                                                 || currentCriteria.SmartCollectionCriteria?.UserStarDifficulty.HasFilter == true
                                                 || BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(currentCriteria);

        private static bool difficultyModsChanged(IReadOnlyList<Mod> oldMods, IReadOnlyList<Mod> newMods) =>
            ModUtils.FlattenMods(oldMods.Except(newMods).Concat(newMods.Except(oldMods))).Any(StarRatingApproximator.ModAffectsDifficulty);

        private void trackModSettings(IReadOnlyList<Mod> currentMods)
        {
            modSettingChangeTracker?.Dispose();
            modSettingChangeTracker = new ModSettingChangeTracker(currentMods);
            modSettingChangeTracker.SettingChanged += mod =>
            {
                // Variant grouping also reads mods, but is invariant to rate and other setting changes today, so it isn't checked here.
                if (!StarRatingApproximator.ModAffectsDifficulty(mod) || (!performanceCriteriaActive && !difficultyCriteriaActive))
                    return;

                // Settings can change very often (e.g. when dragging a rate slider), so debounce re-filtering.
                debouncedModSettingsChange?.Cancel();
                debouncedModSettingsChange = Scheduler.AddDelayed(() => updateCriteria(), 100);
            };
        }

        private void libraryChanged() => Scheduler.AddOnce(refreshCollectionCriteria);

        private void refreshCollectionCriteria()
        {
            if (configCollectionFilter.Value.Length > 0)
                updateCriteria(clearScopedSet: false);
        }

        private void historyChanged() => Scheduler.AddOnce(refreshHistoryCriteria);

        private void refreshHistoryCriteria()
        {
            historyKey = null;
            if (currentCriteria.UsesProfileHistory || currentCriteria.SmartCollectionCriteria?.UsesProfileHistory == true)
                updateCriteria(clearScopedSet: false);
        }

        private void checkWeek()
        {
            var currentWeek = RitsuPlayHistory.StartOfWeek(DateTimeOffset.Now, TimeZoneInfo.Local);
            if (currentWeek == weekStart)
                return;

            weekStart = currentWeek;
            refreshHistoryCriteria();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            collectionsSubscription?.Dispose();
            scoreSubscription?.Dispose();
            weekCheck?.Cancel();
            sliderDebounce?.Cancel();
            if (library != null)
                library.Changed -= libraryChanged;
            if (profiles != null)
                profiles.Changed -= historyChanged;
            modSettingChangeTracker?.Dispose();
            debouncedModSettingsChange?.Cancel();
        }

        /// <summary>
        /// Creates a <see cref="FilterCriteria"/> based on the current state of the controls.
        /// </summary>
        public FilterCriteria CreateCriteria()
        {
            var state = CaptureSearch();
            var rulesetInfo = ruleset.Value;
            var currentMods = mods.Value.Select(m => m.DeepClone()).ToArray();
            Lazy<RitsuPlayHistory>? history = getPlayHistory(rulesetInfo.ShortName);
            var smartCollection = library?.SmartCollections.FirstOrDefault(c => c.Id.ToString() == state.Collection);
            var criteria = createCriteria(state, rulesetInfo, currentMods, history);
            criteria.SelectedBeatmapSet = ScopedBeatmapSet.Value;

            if (smartCollection != null)
            {
                var source = library!.Searches.FirstOrDefault(s => s.Id == smartCollection.SearchId);
                if (source != null)
                {
                    criteria.CollectionBeatmapMD5Hashes = null;
                    criteria.SmartCollectionCriteria = createCriteria(source.State, rulesetInfo, currentMods, history);
                }
            }

            ApplyRequiredCriteria?.Invoke(criteria);
            return criteria;
        }

        private Lazy<RitsuPlayHistory>? getPlayHistory(string mode)
        {
            if (profiles == null || library == null)
                return null;

            var now = DateTimeOffset.Now;
            var key = (profiles.ActiveProfileId, mode, RitsuPlayHistory.StartOfWeek(now, TimeZoneInfo.Local));
            if (historyKey != key)
            {
                var manager = profiles;
                historyKey = key;
                playHistory = new Lazy<RitsuPlayHistory>(() => new RitsuPlayHistory(manager.GetScores(key.ActiveProfileId, mode), now, TimeZoneInfo.Local));
            }

            return playHistory;
        }

        private FilterCriteria createCriteria(RitsuSearchState state, RulesetInfo rulesetInfo, IReadOnlyList<Mod> currentMods, Lazy<RitsuPlayHistory>? history)
        {
            bool isValidUser = localUser.Value.Id > 1;
            var criteria = new FilterCriteria
            {
                Sort = state.Sort,
                ReverseSort = state.ReverseSort,
                ReverseGroup = state.ReverseGroup,
                PerformanceGroupInterval = state.PPGroupInterval,
                CalculateMapPerformance = state.CalculatePerformance,
                Group = state.Group,
                AllowConvertedBeatmaps = state.Converted,
                Ruleset = rulesetInfo,
                Mods = currentMods,
                CollectionBeatmapMD5Hashes = getCollectionHashes(state.Collection),
                LocalUserId = isValidUser ? localUser.Value.Id : null,
                LocalUserUsername = isValidUser ? localUser.Value.Username : null,
                RulesetCriteria = rulesetInfo.CreateInstance().CreateRulesetFilterCriteria(),
                PlayHistory = history,
            };

            if (state.MinimumStars != difficultyRangeSlider.LowerBound.Default)
                criteria.UserStarDifficulty.Min = state.MinimumStars;
            if (state.MaximumStars != difficultyRangeSlider.UpperBound.Default)
                criteria.UserStarDifficulty.Max = state.MaximumStars;
            if (state.MinimumPP != minimumPerformance.Default)
                criteria.UserPerformance.Min = state.MinimumPP;
            if (state.MaximumPP != maximumPerformance.Default)
                criteria.UserPerformance.Max = state.MaximumPP;

            FilterQueryParser.ApplyQueries(criteria, state.Query);
            return criteria;
        }

        private IEnumerable<string>? getCollectionHashes(string scope)
        {
            if (!Guid.TryParse(scope, out var id))
                return null;

            var subgroup = library?.Subgroups.FirstOrDefault(s => s.Id == id);
            return realm.Run(r =>
            {
                var collection = r.Find<BeatmapCollection>(subgroup?.CollectionId ?? id);
                if (collection == null)
                    return ImmutableHashSet<string>.Empty;
                return subgroup == null ? collection.BeatmapMD5Hashes.ToImmutableHashSet() :
                    collection.BeatmapMD5Hashes.Intersect(subgroup.Hashes).ToImmutableHashSet();
            });
        }

        private void updateCriteria(bool clearScopedSet = true)
        {
            if (restoringSearch)
                return;

            if (clearScopedSet && ScopedBeatmapSet.Value != null)
            {
                songSelect?.UnscopeBeatmapSet();
                // because `ScopedBeatmapSet` has a value change callback bound to it that calls `updateCriteria()` again,
                // we can just do nothing other than clear it to avoid extra work and duplicated `CriteriaChanged` invocations
                return;
            }

            // New criteria include the latest mod settings, so any pending mod setting re-filter is redundant.
            debouncedModSettingsChange?.Cancel();

            currentCriteria = CreateCriteria();
            CriteriaChanged?.Invoke(currentCriteria);
        }

        /// <summary>
        /// Set the query to the search text box.
        /// </summary>
        /// <param name="query">The string to search.</param>
        public void Search(string query)
        {
            searchTextBox.Current.Value = query;
        }

        /// <summary>
        /// Set the query to the search text box.
        /// </summary>
        /// <param name="query">The string to search.</param>
        public void AddToSearch(string query)
        {
            string existingQuery = searchTextBox.Current.Value;

            if (existingQuery.Contains(query))
                return;

            searchTextBox.Current.Value = string.IsNullOrWhiteSpace(existingQuery)
                ? query
                : string.Join(' ', existingQuery.Trim(), query);
        }

        /// <summary>
        /// Torii: the live search query bindable, exposed so the legacy (stable-style) song-select
        /// search box can bind two-way to the same source the modern filter uses.
        /// </summary>
        public Bindable<string> SearchQuery => searchTextBox.Current;

        internal SearchHistoryControls CreateSearchHistoryControls(IHasPopover target) => new SearchHistoryControls(searchTextBox, target);

        internal Popover CreateSearchHistoryPopover(Drawable bar) => new SearchHistoryControls.HistoryPopover(this, searchTextBox, bar);

        internal RitsuSearchState CaptureSearch() => new RitsuSearchState(
            SearchQuery.Value, configCollectionFilter.Value, difficultyRangeSlider.LowerBound.Value, difficultyRangeSlider.UpperBound.Value,
            minimumPerformance.Value, maximumPerformance.Value, showConvertedBeatmapsButton.Active.Value, sortDropdown.Current.Value,
            groupDropdown.Current.Value?.Value ?? GroupMode.None, reverseSort, reverseGroup, performanceIntervalDropdown.Current.Value,
            performanceCalculationToggle.Active.Value);

        internal bool HasCollection(string collection)
        {
            if (string.IsNullOrEmpty(collection))
                return true;
            if (!Guid.TryParse(collection, out var id))
                return false;
            if (library?.SmartCollections.Any(c => c.Id == id) == true)
                return true;

            var subgroup = library?.Subgroups.FirstOrDefault(s => s.Id == id);
            return realm.Run(r => r.Find<BeatmapCollection>(subgroup?.CollectionId ?? id) != null);
        }

        internal void RestoreSearch(RitsuSearchState state)
        {
            bool collectionAvailable = HasCollection(state.Collection);
            if (!collectionAvailable)
                notifications?.Post(new SimpleNotification { Text = "The saved collection is unavailable. Choose another collection in Song Select." });

            sliderDebounce?.Cancel();
            restoringSearch = true;
            try
            {
                if (ScopedBeatmapSet.Value != null)
                    songSelect?.UnscopeBeatmapSet();

                SearchQuery.Value = state.Query;
                if (collectionAvailable)
                    configCollectionFilter.Value = state.Collection;
                difficultyRangeSlider.UpperBound.SetDefault();
                difficultyRangeSlider.LowerBound.Value = state.MinimumStars;
                difficultyRangeSlider.UpperBound.Value = state.MaximumStars;
                maximumPerformance.SetDefault();
                minimumPerformance.Value = state.MinimumPP;
                maximumPerformance.Value = state.MaximumPP;
                showConvertedBeatmapsButton.Active.Value = state.Converted;
                sortDropdown.Current.Value = state.Sort;
                config.SetValue(OsuSetting.SongSelectGroupMode, state.Group);
                reverseSort = state.ReverseSort;
                reverseGroup = state.ReverseGroup;
                sortDropdown.SetReversedSortIndicator(reverseSort, SongSelectStrings.Sort);
                groupDropdown.SetReversedSortIndicator(reverseGroup, SongSelectStrings.Group);
                performanceIntervalDropdown.Current.Value = state.PPGroupInterval;
                performanceCalculationToggle.Active.Value = state.CalculatePerformance;
            }
            finally
            {
                restoringSearch = false;
            }

            updateCriteria(clearScopedSet: false);
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(150, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        internal partial class SongSelectSearchTextBox : ShearedFilterTextBox, IHasContextMenu, IHasPopover
        {
            public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

            private const string history_file = "ritsu/search-history.json";
            private Storage storage = null!;
            private SearchHistory history = new SearchHistory();
            private Scheduler historyScheduler = null!;
            private ScheduledDelegate? historyDelay;
            private readonly Bindable<bool> currentIsFavourite = new Bindable<bool>();

            internal Bindable<bool> CurrentIsFavourite => currentIsFavourite;
            internal IReadOnlyList<string> Favourites => history.Favourites;
            internal IReadOnlyList<string> Recent => history.Recent;
            internal Action? SearchClicked { get; init; }
            internal Func<Drawable, Popover>? CreatePopover { get; init; }
            internal Func<SearchHistoryControls>? CompletionControls { get; init; }

            [BackgroundDependencyLoader]
            private void load(Storage storage, GameHost host)
            {
                this.storage = storage;
                historyScheduler = host.UpdateThread.Scheduler;
                try
                {
                    if (storage.Exists(history_file))
                    {
                        using var stream = storage.GetStream(history_file, FileAccess.Read, FileMode.Open);
                        using var reader = new StreamReader(stream);
                        history = JsonConvert.DeserializeObject<SearchHistory>(reader.ReadToEnd()) ?? new SearchHistory();
                        history.Recent ??= new List<string>();
                        history.Favourites ??= new List<string>();
                    }
                }
                catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
                {
                    Logger.Error(e, "Failed to load search history");
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateFavouriteState();
                Current.BindValueChanged(value =>
                {
                    updateFavouriteState();
                    historyDelay?.Cancel();
                    string query = value.NewValue.Trim();
                    if (query.Length == 0)
                        return;
                    historyDelay = historyScheduler.AddDelayed(() =>
                    {
                        history.Recent.RemoveAll(item => item == query);
                        history.Recent.Insert(0, query);
                        if (history.Recent.Count > 30)
                            history.Recent.RemoveRange(30, history.Recent.Count - 30);
                        saveHistory();
                    }, 1500);
                });
            }

            protected override void Dispose(bool isDisposing)
            {
                historyDelay?.Cancel();
                base.Dispose(isDisposing);
            }

            public Popover? GetPopover() => CreatePopover?.Invoke(this);

            public MenuItem[] ContextMenuItems
            {
                get
                {
                    string query = Current.Value.Trim();
                    var items = new List<MenuItem>();
                    if (query.Length > 0)
                    {
                        bool saved = currentIsFavourite.Value;
                        items.Add(new OsuMenuItem(saved ? "Remove from favourites" : "Add to favourites", MenuItemType.Standard, ToggleCurrentFavourite));
                    }
                    if (history.Favourites.Count > 0)
                        items.Add(new OsuMenuItem("Favourites") { Items = history.Favourites.Select(value => new OsuMenuItem(value, MenuItemType.Standard, () => Current.Value = value)).ToArray() });
                    if (history.Recent.Count > 0)
                    {
                        items.Add(new OsuMenuItem("Recent searches") { Items = history.Recent.Select(value => new OsuMenuItem(value, MenuItemType.Standard, () => Current.Value = value)).ToArray() });
                        items.Add(new OsuMenuItem("Clear history", MenuItemType.Destructive, ClearHistory));
                    }
                    if (items.Count == 0)
                        items.Add(new OsuMenuItem("No saved searches") { Action = { Disabled = true } });
                    return items.ToArray();
                }
            }

            internal void ToggleCurrentFavourite()
            {
                string query = Current.Value.Trim();
                if (query.Length == 0)
                    return;

                if (!history.Favourites.Remove(query))
                    history.Favourites.Add(query);

                updateFavouriteState();
                saveHistory();
            }

            internal void ClearHistory()
            {
                historyDelay?.Cancel();
                history.Recent.Clear();
                saveHistory();
            }

            private void updateFavouriteState() => currentIsFavourite.Value = history.Favourites.Contains(Current.Value.Trim());

            private void saveHistory()
            {
                try
                {
                    using var stream = storage.CreateFileSafely(history_file);
                    using var writer = new StreamWriter(stream);
                    writer.Write(JsonConvert.SerializeObject(history));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Logger.Error(e, "Failed to save search history");
                }
            }

            private sealed class SearchHistory
            {
                public List<string> Recent { get; set; } = new List<string>();
                public List<string> Favourites { get; set; } = new List<string>();
            }

            protected override InnerSearchTextBox CreateInnerTextBox() => new InnerTextBox
            {
                ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                SearchClicked = () => SearchClicked?.Invoke(),
                CompletionControls = () => CompletionControls?.Invoke(),
            };

            private partial class InnerTextBox : InnerFilterTextBox
            {
                public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

                public Action? SearchClicked { get; init; }

                public Func<SearchHistoryControls?>? CompletionControls { get; init; }

                public override bool HandleLeftRightArrows => false;

                protected override bool OnKeyDown(KeyDownEvent e)
                {
                    var controls = CompletionControls?.Invoke();
                    if (controls?.HandleSuggestionKey(e) == true)
                        return true;

                    if (this.FindClosestParent<SongSelect>()?.TryOpenRitsuQueue(e) == true)
                        return true;

                    if (controls != null && RitsuOffline.Enabled && HasFocus && !e.Repeat && ((e.Key == Key.AltLeft && e.ControlPressed)
                        || (e.Key is Key.ControlLeft or Key.ControlRight && e.CurrentState.Keyboard.Keys.IsPressed(Key.AltLeft))))
                    {
                        MoveCursorBy(0);
                        ExpandSelectionBy(Text.Length);
                        int caret = Text.Length - SelectedText.Length;
                        MoveCursorBy(caret - Text.Length);
                        controls.Complete(Text, caret, completion =>
                        {
                            Text = completion.Query;
                            MoveCursorBy(completion.Caret - Text.Length);
                        });
                        return true;
                    }

                    return base.OnKeyDown(e);
                }

                protected override bool OnClick(ClickEvent e)
                {
                    if (e.Button == MouseButton.Left)
                        SearchClicked?.Invoke();

                    return base.OnClick(e);
                }

                public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
                {
                    if (e.Action == GlobalAction.Back && ScopedBeatmapSet.Value != null)
                        return false;

                    return base.OnPressed(e);
                }

                public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
                {
                    // Conflicts with default group navigation keys (shift-left shift-right).
                    if (e.Action == PlatformAction.SelectBackwardChar || e.Action == PlatformAction.SelectForwardChar)
                        return false;

                    // the "cut" platform key binding (shift-delete) conflicts with the beatmap deletion action.
                    if (e.Action == PlatformAction.Cut && e.ShiftPressed && e.CurrentState.Keyboard.Keys.IsPressed(Key.Delete))
                        return false;

                    return base.OnPressed(e);
                }
            }
        }

        private partial class PerformanceCalculationButton : ShearedToggleButton, IHasTooltip
        {
            public override LocalisableString TooltipText => "Calculate PP for maps. When disabled, only cached values are used.";
        }

        private partial class GroupModeDropdown : ShearedDropdown<GroupModeDropdownItem>
        {
            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            private Bindable<GroupMode> configGroupMode { get; } = new Bindable<GroupMode>();

            public GroupModeDropdown(LocalisableString label)
                : base(label)
            {
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                config.BindWith(OsuSetting.SongSelectGroupMode, configGroupMode);
            }

            protected override void LoadAsyncComplete()
            {
                // Bindings set up intentionally in `LoadAsyncComplete()` rather than `LoadComplete()` as normal
                // to avoid double-filter on entering song select with the "variant" group mode engaged.
                ruleset.BindValueChanged(_ =>
                {
                    updateAvailableItems();
                    updateCurrentFromConfig();
                }, true);
                configGroupMode.BindValueChanged(_ => updateCurrentFromConfig());
                Current.BindValueChanged(currentChanged);

                // Ordering important - base call must run *after* the above bindings
                // because the bindings set up `Items`, and the base call is responsible for calling `GenerateItemText()`.
                base.LoadAsyncComplete();
            }

            private void updateAvailableItems()
            {
                var rulesetInstance = ruleset.Value.CreateInstance();
                var items = new List<GroupModeDropdownItem>();

                foreach (var item in Enum.GetValues<GroupMode>())
                {
                    switch (item)
                    {
                        default:
                            items.Add(new GroupModeDropdownItem(item, item.GetLocalisableDescription()));
                            break;

                        case GroupMode.Variant:
                            if (rulesetInstance.GameplayVariants.Count() <= 1)
                                break;

                            items.Add(new GroupModeDropdownItem(GroupMode.Variant, rulesetInstance.VariantDescription));
                            break;
                    }
                }

                Items = items.ToArray();
            }

            private bool synchronisingBindables;

            private void updateCurrentFromConfig()
            {
                if (synchronisingBindables)
                    return;

                synchronisingBindables = true;
                // Only rulesets that actually have variants expose and support the "variant" grouping mode.
                // If it's missing, default to no grouping.
                Current.Value = Items.SingleOrDefault(i => i.Value == configGroupMode.Value) ?? Items.Single(i => i.Value == GroupMode.None);
                synchronisingBindables = false;
            }

            private void currentChanged(ValueChangedEvent<GroupModeDropdownItem> current)
            {
                if (synchronisingBindables)
                    return;

                // Only rulesets that actually have variants expose and support the "variant" grouping mode.
                // If it's missing, code above will revert to no grouping, which also incurs a `Current` change.
                // However, for better user experience, don't write the new value out to config in this scenario
                // so that the variant grouping is re-engaged on switching to a ruleset that has variant support
                // (this also persists across game restarts).
                if (current.OldValue.Value == GroupMode.Variant && Items.All(i => i.Value != GroupMode.Variant))
                    return;

                synchronisingBindables = true;
                configGroupMode.Value = current.NewValue.Value;
                synchronisingBindables = false;
            }

            protected override LocalisableString GenerateItemText(GroupModeDropdownItem item) => item.Text;
        }

        private record GroupModeDropdownItem(GroupMode Value, LocalisableString Text);
    }
}
