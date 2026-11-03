// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Utils;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A component which performs and acts as a central cache for difficulty calculations of beatmap/ruleset/mod combinations.
    /// Currently not persisted between game sessions.
    /// </summary>
    public partial class BeatmapDifficultyCache : MemoryCachingComponent<BeatmapDifficultyCache.DifficultyCacheLookup, StarDifficulty?>
    {
        // Too many simultaneous updates can lead to stutters. One thread seems to work fine for song select display purposes.
        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(BeatmapDifficultyCache));

        /// <summary>
        /// All bindables that should be updated along with the current ruleset + mods.
        /// </summary>
        private readonly WeakList<BindableStarDifficulty> trackedBindables = new WeakList<BindableStarDifficulty>();

        /// <summary>
        /// Cancellation sources used by tracked bindables.
        /// </summary>
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();

        /// <summary>
        /// Lock to be held when operating on <see cref="trackedBindables"/> or <see cref="linkedCancellationSources"/>.
        /// </summary>
        private readonly Lock bindableUpdateLock = new Lock();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        private const string performance_cache_file = "ritsu/perfect-pp-cache-v2.json";
        private const string legacy_performance_cache_file = "ritsu/perfect-pp-cache-v1.json";
        private const int performance_algorithm_version = 1;
        private Dictionary<string, double> legacyPerformanceCache = new Dictionary<string, double>();
        private readonly object performanceCacheLock = new object();
        private Dictionary<string, double> performanceCache = new Dictionary<string, double>();
        private Storage performanceCacheStorage = null!;
        private int performanceCacheChanges;

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            performanceCacheStorage = storage;
            performanceCache = loadPerformanceCache(storage, performance_cache_file);
            legacyPerformanceCache = loadPerformanceCache(storage, legacy_performance_cache_file);
            Logger.Log($"Ritsu PP cache loaded from {storage.GetFullPath(performance_cache_file)}: {performanceCache.Count} entries, {legacyPerformanceCache.Count} legacy entries, algorithm={performance_algorithm_version}");
        }

        internal static Dictionary<string, double> loadPerformanceCache(Storage storage, string filename)
        {
            try
            {
                if (!storage.Exists(filename)) return new Dictionary<string, double>();
                using var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open);
                using var reader = new StreamReader(stream);
                return JsonConvert.DeserializeObject<Dictionary<string, double>>(reader.ReadToEnd()) ?? new Dictionary<string, double>();
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                Logger.Error(e, $"Failed to load Ritsu PP cache {filename}");
                return new Dictionary<string, double>();
            }
        }

        internal static string CreateStablePerformanceKey(int difficultyVersion, string ruleset, string beatmapHash, string mods)
            => JsonConvert.SerializeObject(new[] { performance_algorithm_version.ToString(), difficultyVersion.ToString(), ruleset, beatmapHash, mods });

        internal static void WritePerformanceCache(Storage storage, string filename, IReadOnlyDictionary<string, double> entries)
        {
            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(entries));
        }

        internal static IEnumerable<Mod> FilterPerformanceMods(IEnumerable<Mod>? mods)
            => mods?.Where(mod => ModUtils.FlattenMods(new[] { mod }).Any(StarRatingApproximator.ModAffectsDifficulty)) ?? Enumerable.Empty<Mod>();

        internal PerfectPerformanceLookup CreatePerfectPerformanceLookup(RulesetInfo? ruleset, IReadOnlyList<Mod>? mods)
            => new PerfectPerformanceLookup(this, ruleset, mods);

        internal sealed class PerfectPerformanceLookup
        {
            private readonly BeatmapDifficultyCache owner;
            private readonly RulesetInfo? ruleset;
            private readonly Mod[] mods;
            private readonly string modKey;
            private readonly string gameVersion = typeof(BeatmapDifficultyCache).Assembly.ManifestModule.ModuleVersionId.ToString();
            private readonly Dictionary<string, (int difficultyVersion, string legacyVersion)> versions = new Dictionary<string, (int, string)>();

            internal PerfectPerformanceLookup(BeatmapDifficultyCache owner, RulesetInfo? ruleset, IReadOnlyList<Mod>? mods)
            {
                this.owner = owner;
                this.ruleset = ruleset;
                this.mods = FilterPerformanceMods(mods).OrderBy(m => m.Acronym).Select(m => m.DeepClone()).ToArray();
                modKey = JsonConvert.SerializeObject(this.mods.Select(m => new { Type = m.GetType().FullName, Settings = new APIMod(m) }));
            }

            private (string? stable, string? legacy) getKeys(BeatmapInfo beatmap)
            {
                if (string.IsNullOrEmpty(beatmap.Hash)) return (null, null);
                var info = ruleset ?? beatmap.Ruleset;
                (int difficultyVersion, string legacyVersion) version;
                lock (versions)
                {
                    if (!versions.TryGetValue(info.ShortName, out version))
                    {
                        var instance = info.CreateInstance();
                        version = (instance.CreateDifficultyCalculator(owner.beatmapManager.GetWorkingBeatmap(beatmap)).Version, instance.GetType().Assembly.ManifestModule.ModuleVersionId.ToString());
                        versions[info.ShortName] = version;
                    }
                }
                string stable = CreateStablePerformanceKey(version.difficultyVersion, info.ShortName, beatmap.Hash, modKey);
                string legacy = JsonConvert.SerializeObject(new[] { gameVersion, version.legacyVersion, info.ShortName, beatmap.Hash, modKey });
                return (stable, legacy);
            }

            internal bool TryGet(BeatmapInfo beatmap, out double value)
            {
                var (stable, legacy) = getKeys(beatmap);
                lock (owner.performanceCacheLock)
                {
                    if (stable != null && owner.performanceCache.TryGetValue(stable, out value) && double.IsFinite(value) && value >= 0)
                        return true;
                    if (stable != null && legacy != null && owner.legacyPerformanceCache.TryGetValue(legacy, out value) && double.IsFinite(value) && value >= 0)
                    {
                        owner.performanceCache[stable] = value;
                        if (++owner.performanceCacheChanges >= 200) owner.savePerformanceCache();
                        return true;
                    }
                }
                value = 0;
                return false;
            }

            internal double? Calculate(BeatmapInfo beatmap, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryGet(beatmap, out double cached)) return cached;
                double? pp = owner.GetDifficultyAsync(beatmap, ruleset ?? beatmap.Ruleset, mods, cancellationToken).GetAwaiter().GetResult()?.PerformanceAttributes?.Total;
                if (!pp.HasValue || !double.IsFinite(pp.Value) || pp.Value < 0) return null;
                var (stable, _) = getKeys(beatmap);
                if (stable != null)
                {
                    lock (owner.performanceCacheLock)
                    {
                        owner.performanceCache[stable] = pp.Value;
                        if (++owner.performanceCacheChanges >= 200) owner.savePerformanceCache();
                    }
                }
                return pp;
            }
        }

        internal void FlushPerfectPerformanceCache()
        {
            lock (performanceCacheLock)
                savePerformanceCache();
        }

        private void savePerformanceCache()
        {
            if (performanceCacheChanges == 0)
                return;
            try
            {
                WritePerformanceCache(performanceCacheStorage, performance_cache_file, performanceCache);
                performanceCacheChanges = 0;
                Logger.Log($"Ritsu PP cache saved: {performanceCache.Count} entries");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Logger.Error(e, "Failed to save Ritsu perfect PP cache");
            }
        }

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        /// <summary>
        /// Invoked on the update thread whenever a bindable from <see cref="GetBindableDifficulty"/> receives a difficulty for the current ruleset and mods.
        /// Invoked once per bindable update, so a beatmap tracked by multiple bindables is reported once for each of them.
        /// </summary>
        internal event Action<IBeatmapInfo, StarDifficulty>? TrackedDifficultyUpdated;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentRuleset.BindValueChanged(_ => Scheduler.AddOnce(updateTrackedBindables));

            currentMods.BindValueChanged(mods =>
            {
                // A change in bindable here doesn't guarantee that mods have actually changed.
                // However, we *do* want to make sure that the mod *references* are the same;
                // `SequenceEqual()` without a comparer would fall back to `IEquatable`.
                // Failing to ensure reference equality can cause setting change tracking to fail later.
                if (mods.OldValue.SequenceEqual(mods.NewValue, ReferenceEqualityComparer.Instance))
                    return;

                modSettingChangeTracker?.Dispose();

                Scheduler.AddOnce(updateTrackedBindables);

                modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                modSettingChangeTracker.SettingChanged += _ =>
                {
                    lock (bindableUpdateLock)
                    {
                        debouncedModSettingsChange?.Cancel();
                        debouncedModSettingsChange = Scheduler.AddDelayed(updateTrackedBindables, 100);
                    }
                };
            }, true);
        }

        /// <summary>
        /// Notify this cache that a beatmap has been invalidated/updated.
        /// </summary>
        /// <param name="oldBeatmap">The old beatmap model.</param>
        /// <param name="newBeatmap">The updated beatmap model.</param>
        public void Invalidate(IBeatmapInfo oldBeatmap, IBeatmapInfo newBeatmap)
        {
            base.Invalidate(lookup => lookup.BeatmapInfo.Equals(oldBeatmap));

            lock (bindableUpdateLock)
            {
                bool trackedBindablesRefreshRequired = false;

                foreach (var bsd in trackedBindables.Where(bsd => bsd.BeatmapInfo.Equals(oldBeatmap)))
                {
                    bsd.BeatmapInfo = newBeatmap;
                    trackedBindablesRefreshRequired = true;
                }

                if (trackedBindablesRefreshRequired)
                    Scheduler.AddOnce(updateTrackedBindables);
            }
        }

        /// <summary>
        /// Retrieves a bindable containing the star difficulty of a <see cref="BeatmapInfo"/> that follows the currently-selected ruleset and mods.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="BeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops updating the star difficulty for the given <see cref="BeatmapInfo"/>.</param>
        /// <param name="computationDelay">A delay in milliseconds before performing the </param>
        /// <returns>A bindable that is updated to contain the star difficulty when it becomes available. May be an approximation while in an initial calculating state.</returns>
        public IBindable<StarDifficulty> GetBindableDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var bindable = new BindableStarDifficulty(beatmapInfo, cancellationToken)
            {
                // Start with the mod-adjusted star rating (cached exact value or approximation) instead of zero.
                Value = new StarDifficulty(getEffectiveStarRating(beatmapInfo), 0)
            };

            lock (bindableUpdateLock)
            {
                var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, cancellationToken);
                linkedCancellationSources.Add(linkedSource);

                updateBindable(bindable, currentRuleset.Value, currentMods.Value, linkedSource, computationDelay);

                trackedBindables.Add(bindable);
            }

            return bindable;
        }

        /// <summary>
        /// Retrieves the difficulty of a <see cref="IBeatmapInfo"/>.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="IBeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="rulesetInfo">The <see cref="IRulesetInfo"/> to get the difficulty with.</param>
        /// <param name="mods">The <see cref="Mod"/>s to get the difficulty with.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops computing the star difficulty.</param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        /// <returns>
        /// The requested <see cref="StarDifficulty"/>, if non-<see langword="null"/>.
        /// A <see langword="null"/> return value indicates that the difficulty process failed or was interrupted early,
        /// and as such there is no usable star difficulty value to be returned.
        /// </returns>
        public virtual Task<StarDifficulty?> GetDifficultyAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            rulesetInfo ??= beatmapInfo.Ruleset;

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = rulesetInfo as RulesetInfo;

            // Difficulty can only be computed if the beatmap and ruleset are locally available.
            if (localBeatmapInfo == null || localRulesetInfo == null)
            {
                // If not, fall back to the existing star difficulty (e.g. from an online source).
                return Task.FromResult<StarDifficulty?>(new StarDifficulty(beatmapInfo.StarRating, (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0));
            }

            return GetAsync(new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods), cancellationToken, computationDelay);
        }

        /// <summary>
        /// Retrieves the currently cached difficulty if available without performing an asynchronous computation.
        /// </summary>
        /// <param name="beatmapInfo">The beatmap to check.</param>
        /// <param name="rulesetInfo">The ruleset to check with.</param>
        /// <param name="mods">The mods to check with.</param>
        /// <returns>The cached <see cref="StarDifficulty"/> if available; otherwise <c>null</c>.</returns>
        public StarDifficulty? GetExistingDifficulty(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null)
        {
            rulesetInfo ??= beatmapInfo.Ruleset;

            if (beatmapInfo is not BeatmapInfo localBeatmapInfo || rulesetInfo is not RulesetInfo localRulesetInfo)
                return null;

            var lookup = new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods);
            return CheckExists(lookup, out var existing) ? existing : null;
        }

        /// <summary>
        /// Retrieves the effective star rating for a beatmap, considering active mods and cached difficulties.
        /// If not yet computed in the difficulty cache, falls back to a fast approximation for difficulty-altering mods.
        /// </summary>
        /// <param name="beatmapInfo">The beatmap.</param>
        /// <param name="rulesetInfo">The ruleset.</param>
        /// <param name="mods">The mods.</param>
        /// <returns>The effective star rating.</returns>
        public double GetEffectiveStarRating(BeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null)
        {
            if (mods == null || !ModUtils.FlattenMods(mods).Any(StarRatingApproximator.ModAffectsDifficulty))
                return beatmapInfo.StarRating;

            var existing = GetExistingDifficulty(beatmapInfo, rulesetInfo, mods);
            if (existing != null)
                return existing.Value.Stars;

            return StarRatingApproximator.Approximate(beatmapInfo, rulesetInfo, mods);
        }

        /// <summary>
        /// Creates a lookup which returns the same values as <see cref="GetEffectiveStarRating"/> for many beatmaps with the same ruleset and mods.
        /// The mods are ordered, copied and checked for difficulty adjustments once, rather than for every beatmap.
        /// </summary>
        /// <param name="rulesetInfo">The ruleset.</param>
        /// <param name="mods">The mods. Their settings must not change while the lookup is in use.</param>
        internal EffectiveStarRatingLookup CreateEffectiveStarRatingLookup(IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods)
            => new EffectiveStarRatingLookup(this, rulesetInfo, mods);

        private double getEffectiveStarRating(IBeatmapInfo beatmapInfo) => beatmapInfo is BeatmapInfo localBeatmapInfo
            ? GetEffectiveStarRating(localBeatmapInfo, currentRuleset.Value, currentMods.Value)
            : beatmapInfo.StarRating;

        protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeDifficulty(lookup, cancellationToken);
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        protected override bool CacheNullValues => false;

        public Task<List<TimedDifficultyAttributes>> GetTimedDifficultyAttributesAsync(IWorkingBeatmap beatmap, Ruleset ruleset, Mod[] mods, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() => ruleset.CreateDifficultyCalculator(beatmap).CalculateTimed(mods, cancellationToken),
                cancellationToken,
                TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                updateScheduler);
        }

        /// <summary>
        /// Updates all tracked <see cref="BindableStarDifficulty"/> using the current ruleset and mods.
        /// </summary>
        private void updateTrackedBindables()
        {
            lock (bindableUpdateLock)
            {
                cancelTrackedBindableUpdate();

                foreach (var b in trackedBindables)
                {
                    // Cancelled bindables belong to consumers that have moved on (e.g. pooled carousel panels now showing another beatmap).
                    if (!b.CancellationToken.IsCancellationRequested)
                    {
                        double seed = getEffectiveStarRating(b.BeatmapInfo);

                        // Only seed when the star rating actually changes, so non-difficulty mod changes (e.g. NF) don't drop the attributes.
                        if (seed != b.Value.Stars)
                            b.Value = new StarDifficulty(seed, b.Value.MaxCombo);
                    }

                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, b.CancellationToken);
                    linkedCancellationSources.Add(linkedSource);

                    updateBindable(b, currentRuleset.Value, currentMods.Value, linkedSource);
                }
            }
        }

        /// <summary>
        /// Cancels the existing update of all tracked <see cref="BindableStarDifficulty"/> via <see cref="updateTrackedBindables"/>.
        /// </summary>
        private void cancelTrackedBindableUpdate()
        {
            lock (bindableUpdateLock)
            {
                debouncedModSettingsChange?.Cancel();
                debouncedModSettingsChange = null;

                trackedUpdateCancellationSource.Cancel();
                trackedUpdateCancellationSource = new CancellationTokenSource();

                foreach (var c in linkedCancellationSources)
                    c.Dispose();

                linkedCancellationSources.Clear();
            }
        }

        /// <summary>
        /// Updates the value of a <see cref="BindableStarDifficulty"/> with a given ruleset + mods.
        /// </summary>
        /// <param name="bindable">The <see cref="BindableStarDifficulty"/> to update.</param>
        /// <param name="rulesetInfo">The <see cref="IRulesetInfo"/> to update with.</param>
        /// <param name="mods">The <see cref="Mod"/>s to update with.</param>
        /// <param name="linkedCancellationTokenSource">
        /// A cancellation token source that may be used to cancel this update.
        /// This token will be cancelled in one of two scenarios:
        /// <list type="bullet">
        /// <item>The owner of the bindable has requested the cancellation.</item>
        /// <item>An <see cref="Invalidate"/> call has been issued, and as such ongoing calculations must be aborted to avoid stale values being potentially written to bindables.</item>
        /// </list>
        /// </param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        private void updateBindable(BindableStarDifficulty bindable, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods, CancellationTokenSource linkedCancellationTokenSource, int computationDelay = 0)
        {
            // The bindable's beatmap may be replaced by Invalidate() before this completes, so report the beatmap this computation was for.
            var beatmapInfo = bindable.BeatmapInfo;

            // GetDifficultyAsync will fall back to existing data from IBeatmapInfo if not locally available
            // (contrary to GetAsync)
            GetDifficultyAsync(beatmapInfo, rulesetInfo, mods, linkedCancellationTokenSource.Token, computationDelay)
                .ContinueWith(task =>
                    {
                        // We're on a threadpool thread, but we should exit back to the update thread so consumers can safely handle value-changed events.
                        Schedule(() =>
                        {
                            if (!linkedCancellationTokenSource.IsCancellationRequested)
                            {
                                StarDifficulty? starDifficulty = task.GetResultSafely();

                                if (starDifficulty != null)
                                {
                                    bindable.Value = starDifficulty.Value;
                                    TrackedDifficultyUpdated?.Invoke(beatmapInfo, starDifficulty.Value);
                                }
                            }

                            // Once the linked cancellation token source is of no remaining use to anybody, clean it up.
                            lock (bindableUpdateLock)
                            {
                                linkedCancellationSources.Remove(linkedCancellationTokenSource);
                                linkedCancellationTokenSource.Dispose();
                            }
                        });
                    },
                    // This continuation MUST run even if the antecedent `GetDifficultyAsync()` call was canceled in order to clean up `linkedCancellationTokenSource`.
                    // Due to this, `ContinueWith()` CANNOT accept `linkedCancellationTokenSource.Token` here, because if it did, then in an event of a cancellation,
                    // the continuation would never be scheduled for execution.
                    CancellationToken.None);
        }

        /// <summary>
        /// Computes the difficulty defined by a <see cref="DifficultyCacheLookup"/> key, and stores it to the timed cache.
        /// </summary>
        /// <param name="key">The <see cref="DifficultyCacheLookup"/> that defines the computation parameters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The <see cref="StarDifficulty"/>.</returns>
        private StarDifficulty? computeDifficulty(in DifficultyCacheLookup key, CancellationToken cancellationToken = default)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            var beatmapInfo = key.BeatmapInfo;
            var rulesetInfo = key.Ruleset;

            try
            {
                var ruleset = rulesetInfo.CreateInstance();
                Debug.Assert(ruleset != null);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(key.BeatmapInfo));
                IBeatmap playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, key.OrderedMods, cancellationToken);

                var difficulty = ruleset.CreateDifficultyCalculator(workingBeatmap).Calculate(key.OrderedMods, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var performanceCalculator = ruleset.CreatePerformanceCalculator();
                if (performanceCalculator == null)
                    return new StarDifficulty(difficulty, new PerformanceAttributes());

                ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
                scoreProcessor.Mods.Value = key.OrderedMods;
                scoreProcessor.ApplyBeatmap(playableBeatmap);
                cancellationToken.ThrowIfCancellationRequested();

                ScoreInfo perfectScore = new ScoreInfo(key.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Passed = true,
                    Accuracy = 1,
                    Mods = key.OrderedMods,
                    MaxCombo = scoreProcessor.MaximumCombo,
                    Combo = scoreProcessor.MaximumCombo,
                    TotalScore = scoreProcessor.MaximumTotalScore,
                    Statistics = scoreProcessor.MaximumStatistics,
                    MaximumStatistics = scoreProcessor.MaximumStatistics
                };

                var performance = performanceCalculator.Calculate(perfectScore, difficulty);
                cancellationToken.ThrowIfCancellationRequested();

                return new StarDifficulty(difficulty, performance);
            }
            catch (OperationCanceledException)
            {
                // no need to log, cancellations are expected as part of normal operation.
                return null;
            }
            catch (BeatmapInvalidForRulesetException invalidForRuleset)
            {
                if (rulesetInfo.Equals(beatmapInfo.Ruleset))
                    Logger.Error(invalidForRuleset, $"Failed to convert {beatmapInfo.OnlineID} to the beatmap's default ruleset ({beatmapInfo.Ruleset}).");

                return null;
            }
            catch (Exception unknownException)
            {
                Logger.Error(unknownException, "Failed to calculate beatmap difficulty");

                return null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            updateScheduler.Dispose();
        }

        public readonly struct DifficultyCacheLookup : IEquatable<DifficultyCacheLookup>
        {
            public readonly BeatmapInfo BeatmapInfo;
            public readonly RulesetInfo Ruleset;
            public readonly Mod[] OrderedMods;

            public DifficultyCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo? ruleset, IEnumerable<Mod>? mods)
                : this(beatmapInfo, ruleset, OrderMods(mods))
            {
            }

            /// <param name="beatmapInfo">The beatmap.</param>
            /// <param name="ruleset">The ruleset.</param>
            /// <param name="orderedMods">Mods returned by <see cref="OrderMods"/>. Used as-is, without ordering or copying.</param>
            private DifficultyCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo? ruleset, Mod[] orderedMods)
            {
                BeatmapInfo = beatmapInfo;
                // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
                Ruleset = ruleset ?? BeatmapInfo.Ruleset;
                OrderedMods = orderedMods;
            }

            /// <summary>
            /// Creates a lookup from mods which were already prepared by <see cref="OrderMods"/>, so many lookups can share one array.
            /// </summary>
            internal static DifficultyCacheLookup FromOrderedMods(BeatmapInfo beatmapInfo, RulesetInfo? ruleset, Mod[] orderedMods)
                => new DifficultyCacheLookup(beatmapInfo, ruleset, orderedMods);

            /// <summary>
            /// Orders and copies mods into the form stored in <see cref="OrderedMods"/>.
            /// </summary>
            internal static Mod[] OrderMods(IEnumerable<Mod>? mods)
                => mods?.OrderBy(m => m.Acronym).Select(mod => mod.DeepClone()).ToArray() ?? Array.Empty<Mod>();

            public bool Equals(DifficultyCacheLookup other)
                => BeatmapInfo.Equals(other.BeatmapInfo)
                   && Ruleset.Equals(other.Ruleset)
                   && OrderedMods.SequenceEqual(other.OrderedMods);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(BeatmapInfo.ID);
                hashCode.Add(Ruleset.ShortName);

                foreach (var mod in OrderedMods)
                    hashCode.Add(mod);

                return hashCode.ToHashCode();
            }
        }

        /// <summary>
        /// Returns the same values as <see cref="GetEffectiveStarRating"/> for a fixed ruleset and set of mods.
        /// </summary>
        internal sealed class EffectiveStarRatingLookup
        {
            private readonly BeatmapDifficultyCache cache;
            private readonly IRulesetInfo? rulesetInfo;
            private readonly Mod[] orderedMods;
            private readonly Mod[] flattenedMods;
            private readonly double rate;
            private readonly bool modsAffectDifficulty;

            public EffectiveStarRatingLookup(BeatmapDifficultyCache cache, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods)
            {
                this.cache = cache;
                this.rulesetInfo = rulesetInfo;

                if (mods != null)
                {
                    flattenedMods = ModUtils.FlattenMods(mods).ToArray();
                    modsAffectDifficulty = flattenedMods.Any(StarRatingApproximator.ModAffectsDifficulty);
                    rate = ModUtils.CalculateRateWithMods(mods);
                }
                else
                {
                    flattenedMods = Array.Empty<Mod>();
                    modsAffectDifficulty = false;
                    rate = 1.0;
                }

                orderedMods = modsAffectDifficulty ? DifficultyCacheLookup.OrderMods(mods) : Array.Empty<Mod>();
            }

            public double GetEffectiveStarRating(BeatmapInfo beatmapInfo)
            {
                if (!modsAffectDifficulty)
                    return beatmapInfo.StarRating;

                // same as GetExistingDifficulty, but sharing one ordered copy of the mods across all lookups
                if ((rulesetInfo ?? beatmapInfo.Ruleset) is RulesetInfo localRulesetInfo
                    && cache.CheckExists(DifficultyCacheLookup.FromOrderedMods(beatmapInfo, localRulesetInfo, orderedMods), out var existing)
                    && existing != null)
                    return existing.Value.Stars;

                return StarRatingApproximator.Approximate(beatmapInfo, rulesetInfo, rate, flattenedMods);
            }
        }

        private class BindableStarDifficulty : Bindable<StarDifficulty>
        {
            public IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;

            public BindableStarDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken)
            {
                BeatmapInfo = beatmapInfo;
                CancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// A working beatmap that caches its playable representation.
        /// This is intended as single-use for when it is guaranteed that the playable beatmap can be reused.
        /// </summary>
        private class PlayableCachedWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private IBeatmap? playable;

            public PlayableCachedWorkingBeatmap(IWorkingBeatmap working)
            {
                this.working = working;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => working.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => working.BeatmapLoaded;
            bool IWorkingBeatmap.TrackLoaded => working.TrackLoaded;
            IBeatmap IWorkingBeatmap.Beatmap => working.Beatmap;
            Texture IWorkingBeatmap.GetBackground() => working.GetBackground();
            Texture IWorkingBeatmap.GetPanelBackground() => working.GetPanelBackground();
            Waveform IWorkingBeatmap.Waveform => working.Waveform;
            Storyboard IWorkingBeatmap.Storyboard => working.Storyboard;
            ISkin IWorkingBeatmap.Skin => working.Skin;
            Track IWorkingBeatmap.Track => working.Track;
            Track IWorkingBeatmap.LoadTrack() => working.LoadTrack();
            Stream IWorkingBeatmap.GetStream(string storagePath) => working.GetStream(storagePath);
            void IWorkingBeatmap.BeginAsyncLoad() => working.BeginAsyncLoad();
            void IWorkingBeatmap.CancelAsyncLoad() => working.CancelAsyncLoad();
            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double? offsetFromPreviewPoint) => working.PrepareTrackForPreview(looping, offsetFromPreviewPoint);
        }
    }
}
