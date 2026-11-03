// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Collections;
using osu.Game.Graphics.Carousel;
using osu.Game.Scoring;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Resources;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public class PerfectPerformanceSessionTest
    {
        [Test]
        public void DisabledCalculationUsesOnlyCachedValues()
        {
            var cached = new BeatmapInfo { ID = Guid.NewGuid() };
            var missing = new BeatmapInfo { ID = Guid.NewGuid() };
            int calls = 0;
            using var session = new PerfectPerformanceSession(
                b => b.ID == cached.ID ? (true, 321d) : (false, 0d),
                (_, _) => { calls++; return 100; }, () => { }, _ => { }, allowCalculations: false);
            Assert.That(session.Get(cached), Is.EqualTo(new PerfectPerformanceResult(PerfectPerformanceState.Available, 321)));
            Assert.That(session.Get(missing).State, Is.EqualTo(PerfectPerformanceState.Unavailable));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public async Task CachedAndMissingResultsAreAvailableWithoutBlocking()
        {
            var cached = new BeatmapInfo { ID = Guid.NewGuid() };
            var missing = new BeatmapInfo { ID = Guid.NewGuid() };
            using var release = new ManualResetEventSlim();
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int calculations = 0;
            using var session = new PerfectPerformanceSession(
                beatmap => beatmap.ID == cached.ID ? (true, 123d) : (false, 0d),
                (_, token) => { Interlocked.Increment(ref calculations); release.Wait(token); return 250; },
                () => { },
                value => { var progress = value.Progress; if (progress.total == 2 && progress.completed == 2) finished.TrySetResult(); });

            Assert.That(session.Get(cached), Is.EqualTo(new PerfectPerformanceResult(PerfectPerformanceState.Available, 123)));
            Assert.That(session.Get(missing).State, Is.EqualTo(PerfectPerformanceState.Pending));
            Assert.That(session.Get(missing).State, Is.EqualTo(PerfectPerformanceState.Pending));
            release.Set();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(session.Get(missing), Is.EqualTo(new PerfectPerformanceResult(PerfectPerformanceState.Available, 250)));
            Assert.That(calculations, Is.EqualTo(1));
        }

        [Test]
        public async Task GroupingDisplaysCachedAndPendingMapsBeforeCalculationFinishes()
        {
            var set = TestResources.CreateTestBeatmapSetInfo(2);
            var cached = set.Beatmaps[0];
            var missing = set.Beatmaps[1];
            using var release = new ManualResetEventSlim();
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var session = new PerfectPerformanceSession(
                b => b.ID == cached.ID ? (true, 150d) : (false, 0d),
                (_, token) => { release.Wait(token); return 250; }, () => { },
                value => { if (value.Progress.completed == 2) finished.TrySetResult(); });
            var criteria = new FilterCriteria { Group = GroupMode.PerformancePoints100, PerformanceSession = session };
            var grouping = new BeatmapCarouselFilterGrouping
            {
                GetCriteria = () => criteria,
                GetCollections = () => new List<BeatmapCollection>(),
                GetLocalUserTopRanks = _ => new Dictionary<Guid, ScoreRank>(),
                GetFavouriteBeatmapSets = () => new HashSet<int>(),
            };
            var items = set.Beatmaps.Select(b => new CarouselItem(b)).ToList();
            var initial = await grouping.Run(items, CancellationToken.None);
            var titles = initial.Select(i => i.Model).OfType<GroupDefinition>().Select(g => g.Title.ToString()).ToArray();
            Assert.That(titles, Does.Contain("100 - 200 pp"));
            Assert.That(titles, Does.Contain("PP calculating..."));
            Assert.That(initial.Select(i => i.Model).OfType<GroupedBeatmap>().Count(), Is.EqualTo(2));
            release.Set();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var final = await grouping.Run(items, CancellationToken.None);
            Assert.That(final.Select(i => i.Model).OfType<GroupDefinition>().Select(g => g.Title.ToString()), Does.Contain("200 - 300 pp"));
        }

        [Test]
        public async Task LargeLibraryCanBeQueuedWithoutWaitingForFirstCalculation()
        {
            var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var session = new PerfectPerformanceSession(
                _ => (false, 0),
                (_, token) => { token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested(); return 0; },
                () => flushed.TrySetResult(), _ => { });
            for (int i = 0; i < 5000; i++)
                Assert.That(session.Get(new BeatmapInfo { ID = Guid.NewGuid() }).State, Is.EqualTo(PerfectPerformanceState.Pending));
            Assert.That(session.Progress.total, Is.EqualTo(5000));
            Assert.That(session.Progress.completed, Is.Zero);
            session.Dispose();
            await flushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task FailureIsDistinctFromPendingAndCancellationFlushes()
        {
            var failed = new BeatmapInfo { ID = Guid.NewGuid() };
            var waiting = new BeatmapInfo { ID = Guid.NewGuid() };
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var flushing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var session = new PerfectPerformanceSession(
                _ => (false, 0),
                (beatmap, token) => { if (beatmap.ID == failed.ID) return null; token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested(); return 1; },
                () => flushing.TrySetResult(),
                value => { if (value.Progress.failures == 1) changed.TrySetResult(); });

            Assert.That(session.Get(failed).State, Is.EqualTo(PerfectPerformanceState.Pending));
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(session.Get(failed).State, Is.EqualTo(PerfectPerformanceState.Unavailable));
            Assert.That(session.Get(waiting).State, Is.EqualTo(PerfectPerformanceState.Pending));
            session.Dispose();
            await flushing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
