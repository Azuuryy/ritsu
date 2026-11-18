// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Commands;
using osu.Game.Database;
using osu.Game.Offline;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game
{
    public partial class OsuGame
    {
        private IEnumerable<RitsuCommand> ritsuLocalCommands()
        {
            yield return command("map.find", "/map find", "Find an imported difficulty", "<title, ID or link>", input => findRitsuMap(input, false));
            yield return command("map.simulate", "/simulate map", "Simulate an imported difficulty", "<title, ID or link>", input => findRitsuMap(input, true));
            yield return command("map.favorite", "/favorite", "Toggle local favorite", "", _ => toggleRitsuFavourite(), () => !Beatmap.IsDefault);
            yield return command("map.attributes", "/attributes", "Inspect attributes with selected mods", "", _ => ShowRitsuMap(false), () => !Beatmap.IsDefault);
            yield return command("map.strains", "/strains", "Inspect difficulty over time", "", _ => showRitsuDifficultyCurve(), () => !Beatmap.IsDefault);
            yield return command("scores.compare", "/compare score", "Compare scores on the selected difficulty", "", _ => showRitsuCompare(Beatmap.Value.BeatmapInfo), () => !Beatmap.IsDefault);
            yield return command("scores.compare.top", "/compare top", "Compare best scores between local profiles", "", _ => compareRitsuProfiles(true));
            yield return command("scores.pinned", "/pinned", "Browse marked local scores", "", _ =>
            {
                var profiles = Dependencies.Get<RitsuProfileManager>();
                var marked = Dependencies.Get<Library.RitsuLibrary>().GetMarkedScores(profiles.ActiveProfileId).ToHashSet();
                showRitsuScoreList("Marked scores", scores => scores.Where(score => marked.Contains(score.ID)).OrderByDescending(score => score.PP));
            });
            yield return command("scores.map", "/scores", "Browse scores on the selected difficulty", "", _ => showRitsuScoreList("Scores on this difficulty", scores => scores
                .Where(s => s.BeatmapHash == Beatmap.Value.BeatmapInfo.Hash).OrderByDescending(s => s.TotalScore)), () => !Beatmap.IsDefault);
            yield return command("scores.history", "/history", "Browse local score history", "", _ => showRitsuScoreList("Score history", scores => scores.OrderByDescending(s => s.Date)));
            yield return command("scores.mapper", "/mapper top", "Browse scores by mapper", "<mapper>", input => showRitsuScoreList($"Top scores by {input}", scores => scores
                .Where(s => s.BeatmapInfo?.Metadata.Author.Username.Contains(input, StringComparison.OrdinalIgnoreCase) == true).OrderByDescending(s => s.PP)));
            yield return command("scores.mostplayed", "/mostplayed", "Show most played local maps", "", _ => showRitsuMostPlayed());
            yield return command("profiles.compare", "/compare profile", "Compare two local profiles", "", _ => compareRitsuProfiles());
            yield return command("scores.nochoke", "/nochoke", "Estimate a full combo for a local score", "", _ => chooseRitsuProjection());
            yield return command("scores.whatif", "/whatif", "Project profile PP after a new score", "<score PP>", input => projectRitsuProfilePP(input, false), () => !Beatmap.IsDefault);
            yield return command("scores.reach", "/reach", "Find the score PP needed to reach a profile total", "<target profile PP>", input => projectRitsuProfilePP(input, true), () => !Beatmap.IsDefault);
            yield return command("map.recommend", "/recommend", "Show recommended local difficulties", "", _ => recommendRitsuMaps());
        }

        private static RitsuCommand command(string id, string alias, string title, string options, Action<string> run, Func<bool>? available = null) =>
            new RitsuCommand(id, alias, title, options, available ?? (() => true), input =>
                (options.Length == 0) == (input.Length == 0)
                    ? new RitsuCommandInvocation(() => run(input))
                    : RitsuCommandInvocation.Invalid($"Usage: {alias} {options}".TrimEnd()));

        private void findRitsuMap(string input, bool simulate)
        {
            var maps = BeatmapManager.GetAllUsableBeatmapSets().Where(set => !set.Protected).SelectMany(set => set.Beatmaps).Where(map => !map.Hidden);
            int id = parseRitsuBeatmapId(input);
            var matches = (id > 0
                ? maps.Where(map => map.OnlineID == id)
                : maps.Where(map => $"{map.Metadata.Artist} {map.Metadata.Title} {map.DifficultyName}".Contains(input, StringComparison.CurrentCultureIgnoreCase)))
                .OrderBy(map => map.Metadata.Title).ThenBy(map => map.DifficultyName).Take(100).ToArray();

            void open(BeatmapInfo map)
            {
                var current = ResolveRitsuQueueBeatmap(map.MD5Hash);
                if (current == null)
                    postRitsuNotice("This difficulty is no longer available.");
                else if (simulate)
                    ShowRitsuMap(true, current);
                else
                    withRitsuSongSelect(select => select.SelectQueuedBeatmap(current));
            }

            switch (matches.Length)
            {
                case 0:
                    postRitsuNotice("No imported difficulty matches that search.");
                    break;

                case 1:
                    open(matches[0]);
                    break;

                default:
                    dialogOverlay.Push(new RitsuLocalChoiceDialog<BeatmapInfo>("Choose imported difficulty", matches,
                        map => $"{map.Metadata.Artist} - {map.Metadata.Title} [{map.DifficultyName}]", open));
                    break;
            }
        }

        private static int parseRitsuBeatmapId(string input)
        {
            if (int.TryParse(input, out int id))
                return id;

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Host != "osu.ppy.sh")
                return 0;

            var match = Regex.Match(uri.AbsolutePath, @"^/(?:b|beatmaps)/(\d+)/?$");
            if (!match.Success && uri.AbsolutePath.StartsWith("/beatmapsets/", StringComparison.Ordinal))
                match = Regex.Match(uri.Fragment, @"^#(?:osu|taiko|fruits|mania)/(\d+)$");

            return match.Success && int.TryParse(match.Groups[1].Value, out id) ? id : 0;
        }

        private void toggleRitsuFavourite()
        {
            string hash = Beatmap.Value.BeatmapInfo.MD5Hash;
            if (string.IsNullOrEmpty(hash))
                return;

            bool added = false;
            Dependencies.Get<RealmAccess>().Write(realm =>
            {
                var favourites = realm.All<BeatmapCollection>().FirstOrDefault(c => c.Name == "Ritsu favourites")
                                 ?? realm.Add(new BeatmapCollection("Ritsu favourites"));
                added = !favourites.BeatmapMD5Hashes.Remove(hash);
                if (added)
                    favourites.BeatmapMD5Hashes.Add(hash);
                favourites.LastModified = DateTimeOffset.UtcNow;
            });
            postRitsuNotice(added ? "Added to Ritsu favourites." : "Removed from Ritsu favourites.");
        }

        private void showRitsuScoreList(string title, Func<IEnumerable<ScoreInfo>, IEnumerable<ScoreInfo>> filter)
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            Guid profile = profiles.ActiveProfileId;
            var scores = filter(profiles.GetScores(profile, Ruleset.Value.ShortName)).Take(100).ToArray();
            if (scores.Length == 0)
            {
                postRitsuNotice("No local scores match.");
                return;
            }

            dialogOverlay.Push(new RitsuLocalChoiceDialog<ScoreInfo>(title, scores,
                score => $"{score.BeatmapInfo?.Metadata.Title ?? "Unknown map"} · {score.Date:g} · {score.Accuracy:P2} · {score.PP:0.##} PP", score =>
                {
                    if (profiles.ActiveProfileId == profile && ScoreManager.Query(s => s.ID == score.ID && !s.DeletePending) != null)
                        PresentScoreFromLocalProfile(score);
                }));
        }

        private void showRitsuMostPlayed()
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            var maps = profiles.GetScores(profiles.ActiveProfileId, Ruleset.Value.ShortName)
                               .Where(score => score.BeatmapInfo != null).GroupBy(score => score.BeatmapHash)
                               .OrderByDescending(group => group.Count()).Take(15)
                               .Select(group => $"{group.First().BeatmapInfo!.Metadata.Title} [{group.First().BeatmapInfo!.DifficultyName}] · {group.Count()} plays").ToArray();
            showRitsuInformation("Most played local maps", maps);
        }

        private void compareRitsuProfiles(bool compareTop = false)
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            var current = profiles.GetActiveProfile();
            var others = profiles.GetProfiles().Where(p => p.Id != current.Id).ToArray();
            if (others.Length == 0)
            {
                postRitsuNotice("Create a second local profile to compare profiles.");
                return;
            }

            string mode = Ruleset.Value.ShortName;
            dialogOverlay.Push(new RitsuLocalChoiceDialog<RitsuProfile>("Compare local profiles", others, profile => profile.Name, other =>
            {
                var first = profiles.GetStatistics(current.Id, mode);
                var second = profiles.GetStatistics(other.Id, mode);
                if (compareTop)
                {
                    var otherByMap = second.BestScores.ToDictionary(score => score.BeatmapHash);
                    var shared = first.BestScores.Where(score => otherByMap.ContainsKey(score.BeatmapHash)).Take(15)
                                      .Select(score => $"{score.BeatmapInfo?.Metadata.Title ?? "Unknown map"} · {score.PP:0.##} / {otherByMap[score.BeatmapHash].PP:0.##} PP").ToArray();
                    showRitsuInformation($"{current.Name} / {other.Name} · shared best maps", shared);
                    return;
                }

                showRitsuInformation($"Local profiles · {mode}", new[]
                {
                    $"{current.Name}: {first.PP:0.##} PP · {first.DisplayAccuracy} · {first.PlayCount} plays",
                    $"{other.Name}: {second.PP:0.##} PP · {second.DisplayAccuracy} · {second.PlayCount} plays",
                });
            }));
        }

        private void chooseRitsuProjection()
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            var scores = profiles.GetScores(profiles.ActiveProfileId, Ruleset.Value.ShortName)
                                 .Where(score => score.Passed && score.BeatmapInfo != null).OrderByDescending(score => score.Date).Take(100).ToArray();
            if (scores.Length == 0)
            {
                postRitsuNotice("No complete local scores with a beatmap are available.");
                return;
            }

            dialogOverlay.Push(new RitsuLocalChoiceDialog<ScoreInfo>("Choose a score for an FC projection", scores,
                score => $"{score.BeatmapInfo!.Metadata.Title} · {score.Date:g} · {score.Accuracy:P2}", calculateRitsuFullCombo));
        }

        private async void calculateRitsuFullCombo(ScoreInfo original)
        {
            try
            {
                var working = BeatmapManager.GetWorkingBeatmap(original.BeatmapInfo!);
                var projection = await Task.Run(() =>
                {
                    var score = FullComboScoreInfo.Create(original);
                    return score?.Ruleset.CreateInstance().CreatePerformanceCalculator()?.Calculate(score, working).Total;
                }).ConfigureAwait(false);
                Schedule(() => postRitsuNotice(projection.HasValue && double.IsFinite(projection.Value)
                    ? $"{original.PP:0.##} PP → {projection.Value:0.##} PP at full combo"
                    : "This score does not contain enough data for a full combo projection."));
            }
            catch (Exception)
            {
                Schedule(() => postRitsuNotice("The full combo projection could not be calculated."));
            }
        }

        private void projectRitsuProfilePP(string input, bool reach)
        {
            if ((!double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                 && !double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                || !double.IsFinite(value) || value < 0 || value > 1_000_000)
            {
                postRitsuNotice("Enter a positive finite PP value.");
                return;
            }

            var profiles = Dependencies.Get<RitsuProfileManager>();
            string hash = Beatmap.Value.BeatmapInfo.Hash;
            var best = profiles.GetBestScores(profiles.ActiveProfileId, Ruleset.Value.ShortName);
            var previous = best.FirstOrDefault(score => score.BeatmapHash == hash)?.PP ?? 0;
            double total = RitsuProfileManager.CalculateWeightedPP(best.Select(score => score.PP!.Value));
            double after(double newScore)
            {
                if (newScore <= previous)
                    return total;

                return RitsuProfileManager.CalculateWeightedPP(best.Where(score => score.BeatmapHash != hash)
                    .Select(score => score.PP!.Value).Append(newScore).OrderByDescending(pp => pp));
            }

            if (!reach)
            {
                postRitsuNotice($"Profile projection: {total:0.##} → {after(value):0.##} PP.");
                return;
            }

            if (value <= total)
            {
                postRitsuNotice($"This profile has already reached {value:0.##} PP.");
                return;
            }

            double low = previous;
            double high = value;
            for (int i = 0; i < 48; i++)
            {
                double mid = (low + high) / 2;
                if (after(mid) < value)
                    low = mid;
                else
                    high = mid;
            }

            postRitsuNotice($"To reach {value:0.##} profile PP, this difficulty needs about {Math.Ceiling(high * 100) / 100:0.##} score PP.");
        }

        private void recommendRitsuMaps()
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            string mode = Ruleset.Value.ShortName;
            var scores = profiles.GetScores(profiles.ActiveProfileId, mode);
            var played = scores.Select(score => score.BeatmapHash).ToHashSet();
            double reference = scores.Select(score => score.BeatmapInfo?.StarRating ?? 0).Where(stars => stars > 0).DefaultIfEmpty(3).Average();
            var maps = BeatmapManager.GetAllUsableBeatmapSets().Where(set => !set.Protected).SelectMany(set => set.Beatmaps)
                                     .Where(map => !map.Hidden && map.Ruleset.ShortName == mode && !played.Contains(map.Hash) && map.StarRating > 0)
                                     .OrderBy(map => Math.Abs(map.StarRating - reference)).Take(20).ToArray();
            if (maps.Length == 0)
            {
                postRitsuNotice("No unplayed imported difficulties with known star ratings.");
                return;
            }

            dialogOverlay.Push(new RitsuLocalChoiceDialog<BeatmapInfo>("Recommended from your local library", maps,
                map => $"{map.Metadata.Title} [{map.DifficultyName}] · {map.StarRating:0.00}★",
                map => withRitsuSongSelect(select => select.SelectQueuedBeatmap(map))));
        }

        private async void showRitsuDifficultyCurve()
        {
            var working = Beatmap.Value;
            var ruleset = Ruleset.Value.CreateInstance();
            var mods = SelectedMods.Value.Select(mod => mod.DeepClone()).ToArray();
            try
            {
                var values = await Task.Run(() => ruleset.CreateDifficultyCalculator(working).CalculateTimed(mods)).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (Beatmap.Value != working || dialogOverlay.CurrentDialog != null)
                        return;

                    var points = values.Where((_, i) => i % Math.Max(1, values.Count / 12) == 0).Take(13)
                                       .Select(point => $"{TimeSpan.FromMilliseconds(point.Time):m\\:ss} · {point.Attributes.StarRating:0.00}★").ToArray();
                    showRitsuInformation("Difficulty over time", points);
                });
            }
            catch (Exception)
            {
                Schedule(() => postRitsuNotice("Could not calculate a difficulty curve for this map and mode."));
            }
        }

        private void showRitsuInformation(string title, string[] lines)
        {
            dialogOverlay.Push(new ConfirmDialog(title, () => { })
            {
                BodyText = lines.Length > 0 ? string.Join('\n', lines) : "No local data available.",
                Buttons = new[] { new PopupDialogCancelButton { Text = "Close" } },
            });
        }
    }
}

namespace osu.Game.Commands
{
    internal partial class RitsuLocalChoiceDialog<T> : PopupDialog
    {
        public RitsuLocalChoiceDialog(string title, T[] items, Func<T, string> label, Action<T> choose)
        {
            HeaderText = title;
            var choices = items.Select(item => new Choice(item, label(item))).ToArray();
            var dropdown = new osu.Game.Graphics.UserInterface.OsuDropdown<Choice>
            {
                RelativeSizeAxes = osu.Framework.Graphics.Axes.X, Items = choices, Current = { Value = choices[0] },
            };
            MainContent.Add(dropdown);
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "Open", HideDialogOnAction = false, Action = () => { Hide(); choose(dropdown.Current.Value.Item); } },
                new PopupDialogCancelButton { Text = "Close" },
            };
        }

        private sealed record Choice(T Item, string Label)
        {
            public override string ToString() => Label;
        }
    }
}
