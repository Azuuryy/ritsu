// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Statistics;

namespace osu.Game.Database
{
    /// <summary>
    /// A component which performs lookups (or calculations) and caches the results.
    /// Currently not persisted between game sessions.
    /// </summary>
    public abstract partial class MemoryCachingComponent<TLookup, TValue> : Component
        where TLookup : notnull
    {
        private readonly ConcurrentDictionary<TLookup, TValue?> cache = new ConcurrentDictionary<TLookup, TValue?>();

        private readonly GlobalStatistic<MemoryCachingStatistics> statistics;

        private readonly object pendingLock = new object();
        private readonly Dictionary<TLookup, TaskCompletionSource<TValue?>> pending = new Dictionary<TLookup, TaskCompletionSource<TValue?>>();

        protected virtual bool CacheNullValues => true;

        protected virtual bool SharePendingComputations => false;
        protected virtual bool CacheComputedValue(TLookup lookup) => true;

        protected MemoryCachingComponent()
        {
            statistics = GlobalStatistics.Get<MemoryCachingStatistics>(nameof(MemoryCachingComponent<,>), GetType().ReadableName());
            statistics.Value = new MemoryCachingStatistics();
        }

        /// <summary>
        /// Retrieve the cached value for the given lookup.
        /// </summary>
        /// <param name="lookup">The lookup to retrieve.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        protected async Task<TValue?> GetAsync(TLookup lookup, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            if (CheckExists(lookup, out TValue? existing))
            {
                statistics.Value.HitCount++;
                return existing;
            }

            if (computationDelay > 0)
                await Task.Delay(computationDelay, cancellationToken).ConfigureAwait(false);

            if (SharePendingComputations)
                return await getSharedAsync(lookup, cancellationToken).ConfigureAwait(false);

            var computed = await ComputeValueAsync(lookup, cancellationToken).ConfigureAwait(false);

            statistics.Value.MissCount++;

            if (CacheComputedValue(lookup) && (computed != null || CacheNullValues))
            {
                cache[lookup] = computed;
                statistics.Value.Usage = cache.Count;
            }

            return computed;
        }

        private Task<TValue?> getSharedAsync(TLookup lookup, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<TValue?> computation;
            bool start = false;
            lock (pendingLock)
            {
                if (CheckExists(lookup, out TValue? existing))
                    return Task.FromResult(existing);

                if (!pending.TryGetValue(lookup, out var existingComputation))
                {
                    computation = new TaskCompletionSource<TValue?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    pending.Add(lookup, computation);
                    start = true;
                }
                else
                    computation = existingComputation;
            }

            if (start)
                _ = computeSharedAsync(lookup, computation);

            return computation.Task.WaitAsync(cancellationToken);
        }

        private async Task computeSharedAsync(TLookup lookup, TaskCompletionSource<TValue?> computation)
        {
            try
            {
                var result = await ComputeValueAsync(lookup).ConfigureAwait(false);
                lock (pendingLock)
                {
                    if (pending.TryGetValue(lookup, out var current) && ReferenceEquals(current, computation))
                    {
                        pending.Remove(lookup);
                        statistics.Value.MissCount++;
                        if (CacheComputedValue(lookup) && (result != null || CacheNullValues))
                            cache[lookup] = result;
                        statistics.Value.Usage = cache.Count;
                        computation.TrySetResult(result);
                    }
                }
            }
            catch (Exception e)
            {
                lock (pendingLock)
                {
                    if (pending.TryGetValue(lookup, out var current) && ReferenceEquals(current, computation))
                        pending.Remove(lookup);

                    if (e is OperationCanceledException cancellation)
                        computation.TrySetCanceled(cancellation.CancellationToken);
                    else if (computation.TrySetException(e))
                        _ = computation.Task.Exception;
                }
            }
        }

        /// <summary>
        /// Invalidate all entries matching a provided predicate.
        /// </summary>
        /// <param name="matchKeyPredicate">The predicate to decide which keys should be invalidated.</param>
        protected void Invalidate(Func<TLookup, bool> matchKeyPredicate)
        {
            lock (pendingLock)
            {
                foreach (var kvp in cache)
                {
                    if (matchKeyPredicate(kvp.Key))
                        cache.TryRemove(kvp.Key, out _);
                }

                foreach (var key in pending.Keys.Where(matchKeyPredicate).ToArray())
                {
                    pending[key].TrySetResult(default);
                    pending.Remove(key);
                }

                statistics.Value.Usage = cache.Count;
            }
        }

        /// <summary>
        /// Completely purge the cache.
        /// </summary>
        public virtual void Clear()
        {
            lock (pendingLock)
            {
                cache.Clear();
                foreach (var computation in pending.Values)
                    computation.TrySetResult(default);
                pending.Clear();
                statistics.Value.Usage = 0;
            }
        }

        protected bool CheckExists(TLookup lookup, [MaybeNullWhen(false)] out TValue value) =>
            cache.TryGetValue(lookup, out value);

        /// <summary>
        /// Called on cache miss to compute the value for the specified lookup.
        /// </summary>
        /// <param name="lookup">The lookup to retrieve.</param>
        /// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>The computed value.</returns>
        protected abstract Task<TValue?> ComputeValueAsync(TLookup lookup, CancellationToken token = default);

        private class MemoryCachingStatistics
        {
            /// <summary>
            /// Total number of cache hits.
            /// </summary>
            public int HitCount;

            /// <summary>
            /// Total number of cache misses.
            /// </summary>
            public int MissCount;

            /// <summary>
            /// Total number of cached entities.
            /// </summary>
            public int Usage;

            public override string ToString()
            {
                int totalAccesses = HitCount + MissCount;
                double hitRate = totalAccesses == 0 ? 0 : (double)HitCount / totalAccesses;

                return $"i:{Usage} h:{HitCount} m:{MissCount} {hitRate:0%}";
            }
        }
    }
}
