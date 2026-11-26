// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.LocalPriority;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osuTK.Input;
using osu.Game.Screens.Play;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneDeathScoreFade : OsuGameTestScene
    {
        private LocalProfileManager manager => Game.Dependencies.Get<LocalProfileManager>();
        private LocalProfileOverlay overlay => Game.ChildrenOfType<LocalProfileOverlay>().Single();

        private LocalProfile profile = null!;

        /// <summary>
        /// The quick test set, with every .osu changed a little (new hashes), so each copy adds new beatmaps.
        /// </summary>
        private static string makeCopy(int copy)
        {
            string source = TestResources.GetQuickTestBeatmapForImport();
            string target = Path.Combine(Path.GetDirectoryName(source)!, $"copy{copy}-{Guid.NewGuid()}.osz");

            using (var input = ZipFile.OpenRead(source))
            using (var output = ZipFile.Open(target, ZipArchiveMode.Create))
            {
                foreach (var entry in input.Entries)
                {
                    using var read = entry.Open();
                    using var memory = new MemoryStream();
                    read.CopyTo(memory);
                    byte[] data = memory.ToArray();

                    if (entry.FullName.EndsWith(".osu", StringComparison.Ordinal))
                    {
                        string text = System.Text.Encoding.UTF8.GetString(data);
                        text = Regex.Replace(text, @"(?m)^Beatmap(Set)?ID:.*$", "");
                        text = Regex.Replace(text, @"(?m)^Version:(.*)$", $"Version:$1 copy {copy}");
                        data = System.Text.Encoding.UTF8.GetBytes(text);
                    }

                    using var write = output.CreateEntry(entry.FullName).Open();
                    write.Write(data);
                }
            }

            File.Delete(source);
            return target;
        }

        private object scoresPanel() => overlay.ChildrenOfType<Drawable>().Single(d => d.GetType().Name == "ScoresPanel");

        private List<Drawable> bestRows() => ((System.Collections.IEnumerable)scoresPanel().GetType()
                                                 .GetField("bestRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                                                 .GetValue(scoresPanel())!).Cast<Drawable>().ToList();

        private static bool isGoing(Drawable row) => row.Parent == null || row.LifetimeEnd < double.MaxValue;

        private int bestCounter()
        {
            var header = (Drawable)scoresPanel().GetType().GetField("bestHeader", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scoresPanel())!;
            return header.ChildrenOfType<CounterPill>().Single().Current.Value;
        }

        private int scoreCount;

        /// <summary>
        /// A Sudden Death profile with one PP score on each of about 20 beatmaps, at song select.
        /// </summary>
        private void createProfileWithScores(bool longerTimer)
        {
            AddStep("reset", () => scoreCount = 0);
            AddStep("set the timer", () => manager.LongerLastLook.Value = longerTimer);
            AddStep("create Sudden Death profile", () =>
            {
                profile = manager.CreateDefaultProfile(LocalDeathMode.SuddenDeath);
                manager.Switch(profile.Id);
            });
            AddStep("import 7 copies of a beatmap set", () =>
            {
                for (int i = 0; i < 7; i++)
                    Game.BeatmapManager.Import(new ImportTask(makeCopy(i))).WaitSafely();

                // Ranked, so they give PP (the copies have no online IDs).
                Game.Dependencies.Get<RealmAccess>().Write(r =>
                {
                    foreach (var b in r.All<BeatmapInfo>())
                        b.Status = BeatmapOnlineStatus.Ranked;
                });
            });
            AddStep("import a score on each beatmap", () =>
            {
                var realm = Game.Dependencies.Get<RealmAccess>();
                var ruleset = new OsuRuleset();
                var beatmaps = realm.Run(r => r.All<BeatmapInfo>().AsEnumerable().Where(b => b.Ruleset.ShortName == "osu").Detach().ToArray());

                foreach (var beatmap in beatmaps)
                {
                    var playable = Game.BeatmapManager.GetWorkingBeatmap(beatmap).GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());

                    if (playable.HitObjects.Count < 10)
                        continue;

                    var processor = ruleset.CreateScoreProcessor();
                    processor.ApplyBeatmap(playable);
                    var max = processor.MaximumStatistics;
                    var statistics = max.Where(k => !k.Key.IsBonus()).ToDictionary(k => k.Key, k => k.Value);
                    int misses = scoreCount % 5;
                    statistics[HitResult.Great] -= misses;
                    statistics[HitResult.Miss] = misses;

                    Game.ScoreManager.Import(new ScoreInfo
                    {
                        Hash = Guid.NewGuid().ToString(),
                        BeatmapInfo = beatmap,
                        BeatmapHash = beatmap.Hash,
                        Ruleset = ruleset.RulesetInfo,
                        User = new APIUser { Id = profile.UserId, Username = profile.Name },
                        Statistics = statistics,
                        MaximumStatistics = new Dictionary<HitResult, int>(max),
                        MaxCombo = 50,
                        Accuracy = 0.95 - misses / 100.0,
                        TotalScore = 500000 + scoreCount,
                        Rank = ScoreRank.A,
                        Date = DateTimeOffset.Now.AddMinutes(-scoreCount),
                    });

                    scoreCount++;
                }
            });
            AddAssert("more than 10 scores", () => scoreCount, () => Is.GreaterThan(14));
            PushAndConfirm(() => new SoloSongSelect());
            // Ritsu: Torii's first-run briefing would catch the keys pressed below.
            AddStep("close Torii briefing", () => { foreach (var o in Game.ChildrenOfType<osu.Game.Overlays.ToriiBriefing.ToriiBriefingOverlay>()) o.Hide(); });

        }

        [Test]
        public void TestFailedPlayShowsEveryBestScore()
        {
            createProfileWithScores(false);
            AddStep("play as the profile", () => ((DummyAPIAccess)API).LocalUser.Value = profile.ToUser());
            AddUntilStep("SD selected", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Count(), () => Is.EqualTo(1));
            AddStep("select an osu! difficulty", () =>
            {
                var beatmap = Game.Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapInfo>().AsEnumerable().Where(b => b.Ruleset.ShortName == "osu").Detach().ToArray())
                                  .First(b => Game.BeatmapManager.GetWorkingBeatmap(b).GetPlayableBeatmap(new OsuRuleset().RulesetInfo).HitObjects.Count >= 10);
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(beatmap);
            });
            AddUntilStep("osu! difficulty selected", () => Game.Beatmap.Value.BeatmapInfo.Ruleset.ShortName, () => Is.EqualTo("osu"));
            AddWaitStep("settle", 5);
            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("in gameplay", () => Game.ScreenStack.CurrentScreen is Player { IsLoaded: true });
            AddUntilStep("failed", () => ((Player)Game.ScreenStack.CurrentScreen).GameplayState.HasFailed);
            AddUntilStep("profile page opens", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("scores loaded", () => overlay.ChildrenOfType<Drawable>().Any(d => d.GetType().Name == "ScoresPanel") && bestRows().Count > 0);
            AddAssert("every best score shown", () => bestRows().Count, () => Is.EqualTo(scoreCount));
            AddAssert("no show more button under them", () => ((Drawable)scoresPanel()).ChildrenOfType<ShowMoreButton>(), () => Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBestScoresFadeAway(bool longerTimer)
        {
            List<Drawable> rows = null!;
            var removals = new List<(int Rank, double Time, bool Faded)>();
            var belowY = new List<float>();
            double end = 0;
            double start = 0;
            int counterAtStart = 0;
            double leftAtStart = 0;

            createProfileWithScores(longerTimer);

            AddStep("die", () => manager.BeginProfileDeath(profile.UserId));
            AddUntilStep("profile page opens", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("scores loaded", () => overlay.ChildrenOfType<Drawable>().Any(d => d.GetType().Name == "ScoresPanel") && bestRows().Count > 10);
            AddStep("record removals", () =>
            {
                rows = bestRows();
                end = manager.LastLookEndTime!.Value;
                start = manager.LastLookStartTime!.Value;
                counterAtStart = bestCounter();
                leftAtStart = end - overlay.Time.Current;

                var recentHeader = overlay.ChildrenOfType<Drawable>().Where(d => d.GetType().Name == "SubsectionHeader").ElementAt(1);

                overlay.OnUpdate += o =>
                {
                    if (rows.Count == 0 || o.Clock == null)
                        return;

                    if (removals.Count < rows.Count)
                        belowY.Add(recentHeader.ScreenSpaceDrawQuad.TopLeft.Y);

                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (removals.Any(r => r.Rank == i + 1) || !isGoing(rows[i]))
                            continue;

                        removals.Add((i + 1, o.Time.Current, rows[i].Parent != null));
                    }
                };
            });
            AddAssert("countdown length", () => end - start, () => Is.EqualTo(longerTimer ? 60_000 : 30_000).Within(0.01));
            AddAssert("opened in the grace period", () => end - leftAtStart - start, () => Is.LessThan(5000));
            AddAssert("counter showed them all", () => counterAtStart, () => Is.EqualTo(rows.Count));
            AddAssert("highest PP first", () => rows.Select(r => (double)r.GetType().GetField("pp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(r)!).ToArray(),
                () => Is.Ordered.Descending);

            AddUntilStep("first score gone after about 5 seconds", () => removals.Count > 0);
            AddAssert("timing", () => removals[0].Time - (start + 5000), () => Is.EqualTo(0).Within(150));
            AddAssert("one of the lowest went first", () => removals[0].Rank, () => Is.GreaterThan(rows.Count - 4));

            AddUntilStep("down to the top 10", () => removals.Count >= rows.Count - 10);
            AddAssert("all but the top 10 gone before the last 10 seconds", () => removals.Take(rows.Count - 10).Max(r => r.Time), () => Is.LessThan(end - 10_000));
            AddAssert("top 10 all still there", () => removals.All(r => r.Rank > 10));
            AddAssert("counter follows", bestCounter, () => Is.EqualTo(10));
            AddAssert("lowest first, neighbours shuffled", () =>
            {
                // Lowest first, at most 3 places out of order, and not simply in order.
                var order = removals.Take(rows.Count - 10).Select(r => r.Rank).ToArray();

                for (int i = 0; i < order.Length; i++)
                {
                    if (Math.Abs(order[i] - (rows.Count - i)) > 3)
                        return $"#{order[i]} went {i + 1}th: {string.Join(",", order)}";
                }

                return order.SequenceEqual(order.OrderByDescending(r => r)) ? "not shuffled" : "ok";
            }, () => Is.EqualTo("ok"));

            AddUntilStep("all gone", () => removals.Count == rows.Count);
            AddAssert("top 10 in order, #1 last", () => removals.Skip(rows.Count - 10).Select(r => r.Rank), () => Is.EqualTo(Enumerable.Range(1, 10).Reverse()));
            AddAssert("top 10 on the last ticks", () => removals.Skip(rows.Count - 10).Select(r => Math.Round((end - r.Time) / 1000)), () => Is.EqualTo(Enumerable.Range(1, 10).Reverse().Select(i => (double)i)));
            AddAssert("top 10 faded (in view)", () => removals.Skip(rows.Count - 10).All(r => r.Faded));
            AddAssert("what's below only moves up, never back down", () =>
            {
                for (int i = 1; i < belowY.Count; i++)
                {
                    if (belowY[i] > belowY[i - 1] + 0.01f)
                        return $"down by {belowY[i] - belowY[i - 1]} at frame {i}";
                }

                return belowY[^1] < belowY[0] ? "ok" : "never moved";
            }, () => Is.EqualTo("ok"));
            AddAssert("counter at zero", bestCounter, () => Is.EqualTo(0));
            AddUntilStep("closed", () => overlay.State.Value == Visibility.Hidden);
            AddUntilStep("profile deleted", () => manager.Store.GetProfile(profile.Id), () => Is.Null);
        }
    }
}
