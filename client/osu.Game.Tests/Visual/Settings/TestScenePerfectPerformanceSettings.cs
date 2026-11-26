using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Rulesets.Objects;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestScenePerfectPerformanceSettings : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colours = new OverlayColourProvider(OverlayColourScheme.Purple);

        protected override bool UseFreshStoragePerRun => true;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Test]
        public void TestAutomaticVariantsShareConversionAndPreserveMaxPerformance()
        {
            Task comparison = null!;
            AddStep("compare warmup and direct Max PP", () => comparison = compare());
            AddUntilStep("wait for comparison", () => comparison.IsCompleted);
            AddStep("check the comparison result", () => comparison.GetAwaiter().GetResult());

            async Task compare()
            {
                string path = TestResources.GetQuickTestBeatmapForImport();
                try
                {
                    var imported = await beatmapManager.Import(new ImportTask(path)).ConfigureAwait(false);
                    var map = imported!.PerformRead(set => set.Beatmaps.First(beatmap => beatmap.Ruleset.ShortName == "osu").Detach());
                    var working = new Lazy<WorkingBeatmap>(() => beatmapManager.GetWorkingBeatmap(map));

                    var variants = PerfectPerformanceWarmup.CreateVariants(map.Ruleset, false);
                    var groups = PerfectPerformanceWarmup.GroupVariantsForConversion(variants, false).ToArray();
                    Assert.That(groups, Has.Length.EqualTo(2));

                    var sliderEvents = new SliderEventGenerator.EventCache();
                    foreach (var group in groups)
                    {
                        Assert.That(group, Has.Length.EqualTo(4));
                        var playable = new Lazy<BeatmapDifficultyCache.PlayableCachedWorkingBeatmap>(() => new BeatmapDifficultyCache.PlayableCachedWorkingBeatmap(working.Value, shareHidden: true, sliderEvents: sliderEvents));
                        foreach (var mods in group)
                        {
                            var lookup = difficultyCache.CreatePerfectPerformanceLookup(map.Ruleset, mods);
                            double? warmup = await lookup.CalculateAsync(map, CancellationToken.None, background: true, workingBeatmap: working, sharedPlayableBeatmap: playable).ConfigureAwait(false);
                            var preview = await difficultyCache.GetDifficultyPreviewAsync(map, map.Ruleset, mods, CancellationToken.None).ConfigureAwait(false);
                            Assert.That(preview.HasValue, Is.True);
                            Assert.That(preview?.DifficultyAttributes, Is.Null);

                            var expected = await difficultyCache.GetDifficultyAsync(map, map.Ruleset, mods).ConfigureAwait(false);
                            Assert.That(warmup, Is.Not.Null);
                            Assert.That(warmup, Is.EqualTo(expected?.PerformanceAttributes?.Total));
                            Assert.That(preview?.Stars, Is.EqualTo(expected?.Stars));
                            Assert.That(preview?.MaxCombo, Is.EqualTo(expected?.MaxCombo));

                            var shouldNotLoad = new Lazy<WorkingBeatmap>(() => throw new InvalidOperationException("A cached result does not need a working beatmap."));
                            double? cached = await lookup.CalculateAsync(map, CancellationToken.None, background: true, workingBeatmap: shouldNotLoad).ConfigureAwait(false);
                            Assert.That(cached, Is.EqualTo(warmup));
                        }

                        Assert.That(playable.Value.SharedSkillResults, Has.Count.EqualTo(2));
                    }

                    Assert.That(working.IsValueCreated, Is.True);
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void TestPauseAndResumeAutomaticPrecalculationExcludesMapsBelowMinimumStars()
        {
            PerfectPerformanceWarmup warmup = null!;
            PerfectPerformanceSettings settings = null!;
            var notifications = new TestNotifications();
            ProgressNotification? initialNotification = null;
            SettingsButtonV2 button() => settings.ChildrenOfType<SettingsButtonV2>().Single();

            AddStep("load Max PP settings", () =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(4);
                set.Beatmaps[0].StarRating = 0;
                set.Beatmaps[1].StarRating = 0.099;
                set.Beatmaps[2].StarRating = 0.1;
                set.Beatmaps[3].StarRating = -1;
                Realm.Write(r => r.Add(set));
                Dependencies.Cache(Realm);
                Dependencies.Cache(warmup = new PerfectPerformanceWarmup());
                Add(warmup);
                Add(settings = new PerfectPerformanceSettings());
            });

            AddStep("collect unreferenced bindables", () => GC.Collect());

            AddStep("start automatic precalculation", () =>
            {
                warmup.SetPlaying(true);
                warmup.StartAutomatic(1, LocalStorage.GetFullPath("."), notifications);
            });
            AddUntilStep("automatic progress includes only eligible maps", () => notifications.Progress?.Text.ToString(),
                () => Does.StartWith("Base Max PP precalculation: 0/16 · Elapsed:"));
            AddAssert("Maintenance offers to stop the automatic lot", () => button().Text.ToString(), () => Is.EqualTo("Stop Max PP precalculation"));
            AddStep("stop automatic precalculation via notification", () =>
            {
                initialNotification = notifications.Progress!;
                initialNotification.Close(false);
                assertStopping();
            });
            AddUntilStep("wait for automatic precalculation to stop", () => button().Enabled.Value && notifications.Progress?.State == ProgressNotificationState.Cancelled);

            AddStep("resume the same lot", () =>
            {
                Assert.That(button().Text.ToString(), Is.EqualTo("Resume Max PP precalculation"));
                button().Action!.Invoke();
            });
            AddUntilStep("resumed progress is posted", () => notifications.Progress != null && notifications.Progress != initialNotification);
            AddAssert("notification still tracks only the base grid", () => notifications.Progress!.Text.ToString(),
                () => Does.StartWith("Base Max PP precalculation: 0/16 · Elapsed:"));
            AddStep("pause resumed lot", () => button().Action!.Invoke());
            AddUntilStep("wait for resumed lot to stop", () => button().Enabled.Value && notifications.Progress?.State == ProgressNotificationState.Cancelled);

            void assertStopping()
            {
                Assert.That(button().Text.ToString(), Is.EqualTo("Stopping Max PP precalculation…"));
                Assert.That(button().Enabled.Value, Is.False);
            }
        }

        private sealed class TestNotifications : INotificationOverlay
        {
            public ProgressNotification? Progress { get; private set; }
            public IBindable<int> UnreadCount { get; } = new Bindable<int>();
            public IEnumerable<Notification> AllNotifications => Progress == null ? Array.Empty<Notification>() : new Notification[] { Progress };

            public void Post(Notification notification) => Progress = (ProgressNotification)notification;
            public void Hide()
            {
            }
        }
    }
}
