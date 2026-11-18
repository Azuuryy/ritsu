// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public partial class RitsuSharedCalculationTest
    {
        [Test]
        public async Task ConcurrentRequestsShareCalculationAndCancelIndependently()
        {
            using var cache = new TestCache();
            using var cancellation = new CancellationTokenSource();

            var cancelled = cache.Get("map", cancellation.Token);
            var waiting = cache.Get("map");
            Assert.That(cache.Calculations, Has.Count.EqualTo(1));

            cancellation.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await cancelled);
            cache.Calculations[0].SetResult("difficulty");
            Assert.That(await waiting, Is.EqualTo("difficulty"));
            Assert.That(await cache.Get("map"), Is.EqualTo("difficulty"));
            Assert.That(cache.Calculations, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task InvalidatedCalculationCannotReplaceFreshValue()
        {
            using var cache = new TestCache();
            var obsolete = cache.Get("map");
            cache.Invalidate("map");
            Assert.That(await obsolete, Is.Null);

            var fresh = cache.Get("map");
            Assert.That(cache.Calculations, Has.Count.EqualTo(2));
            cache.Calculations[1].SetResult("new");
            Assert.That(await fresh, Is.EqualTo("new"));
            cache.Calculations[0].SetResult("old");
            Assert.That(await cache.Get("map"), Is.EqualTo("new"));
        }

        private partial class TestCache : MemoryCachingComponent<string, string>
        {
            public readonly List<TaskCompletionSource<string?>> Calculations = new List<TaskCompletionSource<string?>>();

            protected override bool SharePendingComputations => true;

            public Task<string?> Get(string key, CancellationToken cancellationToken = default) => GetAsync(key, cancellationToken);

            public void Invalidate(string key) => base.Invalidate(candidate => candidate == key);

            protected override Task<string?> ComputeValueAsync(string lookup, CancellationToken cancellationToken = default)
            {
                var calculation = new TaskCompletionSource<string?>();
                Calculations.Add(calculation);
                return calculation.Task;
            }
        }
    }
}
