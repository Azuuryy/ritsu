// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using osuTK.Input;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Commands
{
    internal partial class RitsuSimulationScreen : OsuScreen, IKeyBindingHandler<GlobalAction>
    {
        private readonly WorkingBeatmap working;
        private readonly UserModSelectOverlay modPicker;
        private readonly FillFlowContainer inputs;
        private readonly OsuSpriteText summary;
        private readonly OsuSpriteText result;
        private readonly RoundedButton calculate;
        private readonly RoundedButton applyMods;
        private readonly Dictionary<HitResult, OsuTextBox> fields = new Dictionary<HitResult, OsuTextBox>();
        private OsuTextBox accuracy = textBox("Blank = use counts");
        private OsuTextBox combo = textBox("Blank = automatic");
        private CancellationTokenSource? preparation;
        private RitsuSimulation? model;
        private RulesetInfo mode;
        private Mod[] mods;

        public override string Title => "Score simulation";

        public RitsuSimulationScreen(WorkingBeatmap working, RulesetInfo mode, Mod[] mods, RulesetInfo[] modes)
        {
            this.working = working;
            this.mode = mode;
            this.mods = mods;

            modPicker = new UserModSelectOverlay { Depth = -1, Beatmap = { Value = working }, Ruleset = { Value = mode } };
            modPicker.SelectedMods.Value = mods.Select(m => m.DeepClone()).ToArray();

            var modePicker = new OsuDropdown<ModeChoice>
            {
                Width = 230,
                Items = modes.Where(candidate => working.BeatmapInfo.Ruleset.OnlineID == 0 || candidate.OnlineID == working.BeatmapInfo.Ruleset.OnlineID).Select(candidate => new ModeChoice(candidate)).ToArray(),
                Current = { Value = new ModeChoice(mode) },
            };

            modePicker.Current.BindValueChanged(change =>
            {
                this.mode = change.NewValue.Ruleset;
                modPicker.Ruleset.Value = this.mode;
                modPicker.SelectedMods.Value = Array.Empty<Mod>();
                this.mods = Array.Empty<Mod>();
                prepare();
            });

            AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(23, 21, 31, 255) });
            AddInternal(new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 36, Vertical = 56 },
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = "Score simulation · Ritsu lazer PP" },
                        new OsuSpriteText { Text = $"{working.BeatmapInfo.Metadata.Artist} - {working.BeatmapInfo.Metadata.Title} [{working.BeatmapInfo.DifficultyName}]" },
                        summary = new OsuSpriteText(),
                        result = new OsuSpriteText(),
                        modePicker,
                        button("Edit mods, clock rate and attributes", () => modPicker.Show()),
                        inputs = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 5) },
                        calculate = button("Calculate PP", calculateScore),
                        button("Reset to perfect", () => { if (model != null) rebuildInputs(model); }),
                        button("Back to Song Select", () => this.Exit()),
                    },
                },
            });
            AddInternal(modPicker);
            AddInternal(applyMods = new RoundedButton
            {
                Anchor = Anchor.BottomRight, Origin = Anchor.BottomRight, Margin = new MarginPadding(16),
                Depth = -2, Width = 160, Height = 35, Text = "Apply mods", Action = modPicker.Hide,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            modPicker.State.BindValueChanged(change =>
            {
                applyMods.Alpha = change.NewValue == Visibility.Visible ? 1 : 0;
                if (change.NewValue == Visibility.Hidden)
                {
                    mods = modPicker.SelectedMods.Value.Select(m => m.DeepClone()).ToArray();
                    prepare();
                }
            }, true);
        }

        private async void prepare()
        {
            cancelPreparation();
            preparation = new CancellationTokenSource();
            var token = preparation.Token;
            var selectedMode = mode;
            var selectedMods = mods;
            model?.Dispose();
            model = null;
            inputs.Clear();
            result.Text = "Calculating difficulty and perfect PP…";
            calculate.Enabled.Value = false;
            try
            {
                var prepared = await Task.Run(() => new RitsuSimulation(working, selectedMode, selectedMods, token), token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    prepared.Dispose();
                    return;
                }

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        prepared.Dispose();
                        return;
                    }

                    model = prepared;
                    summary.Text = prepared.Summary;
                    rebuildInputs(prepared);
                    calculate.Enabled.Value = true;
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                Logger.Error(error, "Ritsu could not prepare this score simulation.");
                if (!token.IsCancellationRequested)
                    Schedule(() =>
                    {
                        if (!token.IsCancellationRequested)
                            result.Text = $"Cannot calculate PP for {selectedMode.ShortName}: {error.Message}";
                    });
            }
        }

        private void rebuildInputs(RitsuSimulation simulation)
        {
            inputs.Clear();
            fields.Clear();
            accuracy = textBox("Blank = use counts");
            combo = textBox("Blank = automatic");
            inputs.Add(labelled("Target accuracy % (optional)", accuracy));
            if (mode.ShortName != "mania")
                inputs.Add(labelled("Maximum combo achieved (optional)", combo));

            foreach (var group in simulation.Groups)
            {
                foreach (var judgement in group.Editable)
                {
                    var field = textBox($"Auto (0–{group.Count})");
                    fields.Add(judgement, field);
                    inputs.Add(labelled(RitsuSimulation.JudgementLabel(mode.ShortName, judgement), field));
                }
            }

            calculateScore();
        }

        private void calculateScore()
        {
            if (model == null)
                return;

            try
            {
                var counts = fields.Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Text))
                                   .ToDictionary(pair => pair.Key, pair => parseCount(pair.Value.Text));
                double? target = null;
                if (!string.IsNullOrWhiteSpace(accuracy.Text))
                {
                    if ((!double.TryParse(accuracy.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                         && !double.TryParse(accuracy.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        || !double.IsFinite(value) || value < 0 || value > 100)
                        throw new ArgumentException("Enter target accuracy between 0 and 100%.");

                    target = value / 100;
                }

                int? achievedCombo = mode.ShortName == "mania" || string.IsNullOrWhiteSpace(combo.Text) ? null : parseCount(combo.Text);
                var calculated = model.Calculate(counts, target, achievedCombo);
                result.Text = $"{calculated.Rank} · {calculated.Accuracy:P2} · {calculated.MaxCombo}/{model.MaxCombo}x · {calculated.PP:0.##} / {model.PerfectPP:0.##} PP"
                              + $"   |   {string.Join(" · ", calculated.Statistics.Where(pair => pair.Value > 0 && !pair.Key.IsBonus()).Select(pair => $"{RitsuSimulation.JudgementLabel(mode.ShortName, pair.Key)}: {pair.Value}"))}";
            }
            catch (ArgumentException error)
            {
                result.Text = error.Message;
            }
        }

        private static int parseCount(string text) =>
            int.TryParse(text, out int count) && count >= 0 ? count : throw new ArgumentException("Judgements and combo must be nonnegative whole numbers.");

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != GlobalAction.Back || !this.IsCurrentScreen())
                return false;

            if (modPicker.State.Value == Visibility.Visible)
                modPicker.Hide();
            else
                this.Exit();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape && !e.Repeat)
            {
                if (modPicker.State.Value == Visibility.Visible)
                    modPicker.Hide();
                else
                    this.Exit();
                return true;
            }

            return base.OnKeyDown(e);
        }

        private void cancelPreparation()
        {
            var active = Interlocked.Exchange(ref preparation, null);
            active?.Cancel();
            active?.Dispose();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (modPicker.State.Value == Visibility.Visible)
            {
                modPicker.Hide();
                return true;
            }

            cancelPreparation();
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                cancelPreparation();
                model?.Dispose();
                model = null;
            }

            base.Dispose(isDisposing);
        }

        private sealed record ModeChoice(RulesetInfo Ruleset)
        {
            public override string ToString() => Ruleset.Name;
        }

        private static OsuTextBox textBox(string hint) => new OsuTextBox { Width = 220, PlaceholderText = hint };

        private static Drawable labelled(string label, Drawable input) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Horizontal, Spacing = new Vector2(10, 0),
            Children = new[] { input, new OsuSpriteText { Text = label } },
        };

        private static RoundedButton button(string title, Action action) => new RoundedButton { Width = 275, Height = 32, Text = title, Action = action };
    }

    internal sealed class RitsuSimulation : IDisposable
    {
        internal sealed record JudgementGroup(HitResult Best, int Count, HitResult[] Editable);

        private readonly WorkingBeatmap working;
        private readonly RulesetInfo mode;
        private readonly Mod[] mods;
        private readonly ScoreProcessor processor;
        private readonly PerformanceCalculator calculator;
        private readonly DifficultyAttributes difficulty;
        private readonly Dictionary<HitResult, int> maximum;
        private readonly double accuracyMaximum;
        public IReadOnlyList<JudgementGroup> Groups { get; }
        public int MaxCombo => processor.MaximumCombo;
        public double PerfectPP { get; }
        public string Summary { get; }

        public RitsuSimulation(WorkingBeatmap working, RulesetInfo mode, Mod[] mods, CancellationToken token)
        {
            this.working = working;
            this.mode = mode;
            this.mods = mods;

            var ruleset = mode.CreateInstance();
            if (!ModUtils.CheckValidForGameplay(mods, out _))
                throw new ArgumentException("The selected mods are incompatible.");

            var playable = working.GetPlayableBeatmap(mode, mods, token);
            difficulty = ruleset.CreateDifficultyCalculator(working).Calculate(mods, token);
            processor = ruleset.CreateScoreProcessor();
            processor.Mods.Value = mods;
            processor.ApplyBeatmap(playable);
            token.ThrowIfCancellationRequested();
            calculator = ruleset.CreatePerformanceCalculator() ?? throw new ArgumentException("This mode has no performance calculator.");
            maximum = processor.MaximumStatistics;
            accuracyMaximum = accuracyPoints(maximum);

            var groups = new List<JudgementGroup>();
            void group(HitResult best, params HitResult[] rest)
            {
                if (maximum.TryGetValue(best, out int count) && count > 0)
                    groups.Add(new JudgementGroup(best, count, rest));
            }

            switch (mode.ShortName)
            {
                case "osu":
                    group(HitResult.Great, HitResult.Ok, HitResult.Meh, HitResult.Miss);
                    group(HitResult.LargeTickHit, HitResult.LargeTickMiss);
                    group(HitResult.SliderTailHit, HitResult.IgnoreMiss);
                    break;

                case "taiko":
                    group(HitResult.Great, HitResult.Ok, HitResult.Miss);
                    break;

                case "fruits":
                    group(HitResult.Great, HitResult.Miss);
                    group(HitResult.LargeTickHit, HitResult.LargeTickMiss);
                    group(HitResult.SmallTickHit, HitResult.SmallTickMiss);
                    break;

                case "mania":
                    group(HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss);
                    break;
            }

            Groups = groups;
            PerfectPP = buildScore(maximum, MaxCombo).PP!.Value;
            double rate = ModUtils.CalculateRateWithMods(mods);
            var effective = playable.Difficulty;
            Summary = $"{mode.ShortName} · {(mods.Length == 0 ? "NM" : string.Join(", ", mods.Select(m => m.Acronym)))} · {difficulty.StarRating:0.00}★ · "
                      + $"{60000 / working.Beatmap.GetMostCommonBeatLength() * rate:0.##} BPM · {TimeSpan.FromMilliseconds(working.BeatmapInfo.Length / rate):m\\:ss} · {MaxCombo}x · "
                      + $"CS {effective.CircleSize:0.##} · AR {effective.ApproachRate:0.##} · OD {effective.OverallDifficulty:0.##} · HP {effective.DrainRate:0.##}";
        }

        public ScoreInfo Calculate(IReadOnlyDictionary<HitResult, int> changes, double? target, int? combo)
        {
            var stats = new Dictionary<HitResult, int>(maximum);
            if (target.HasValue && Groups.SelectMany(g => g.Editable).Any(hit => hit is (HitResult.Perfect or HitResult.Great or HitResult.Good or HitResult.Ok or HitResult.Meh) && changes.ContainsKey(hit)))
                throw new ArgumentException("Clear the hit counts before using target accuracy. Misses and slider fields may be kept.");

            foreach (var group in Groups)
            {
                long replaced = group.Editable.Sum(hit => (long)changes.GetValueOrDefault(hit));
                if (replaced < 0 || replaced > group.Count)
                    throw new ArgumentException($"{group.Best} replacements must total between 0 and {group.Count}.");

                stats[group.Best] = group.Count - (int)replaced;
                foreach (var hit in group.Editable)
                    stats[hit] = changes.GetValueOrDefault(hit);
            }

            if (target.HasValue)
                distributeAccuracy(stats, target.Value, changes);

            int broken = stats.GetValueOrDefault(HitResult.Miss) + stats.GetValueOrDefault(HitResult.LargeTickMiss);
            int comboLimit = Math.Max(0, MaxCombo - broken);
            int achieved = combo ?? comboLimit;
            if (achieved < 0 || achieved > comboLimit)
                throw new ArgumentException($"Combo must be between 0 and {comboLimit} for these judgements.");

            return buildScore(stats, achieved);
        }

        private void distributeAccuracy(Dictionary<HitResult, int> stats, double target, IReadOnlyDictionary<HitResult, int> changes)
        {
            double remaining = accuracyPoints(stats) - target * accuracyMaximum;
            if (remaining < -0.001)
                throw new ArgumentException("The target accuracy exceeds what is possible with these misses and slider judgements.");

            int maximumRoundingLoss = 0;
            foreach (var group in Groups)
            {
                int best = processor.GetBaseScoreForResult(group.Best);
                var replacements = group.Editable.Where(hit => hit is HitResult.Great or HitResult.Good or HitResult.Ok or HitResult.Meh
                                                                 || hit != HitResult.IgnoreMiss && mode.ShortName == "fruits")
                                        .Where(hit => !changes.ContainsKey(hit) && processor.ResultAffectsAccuracy(hit))
                                        .Select(hit => (Hit: hit, Loss: best - processor.GetBaseScoreForResult(hit)))
                                        .Where(pair => pair.Loss > 0).OrderByDescending(pair => pair.Loss).ToArray();
                if (replacements.Length > 0)
                    maximumRoundingLoss = Math.Max(maximumRoundingLoss, replacements[^1].Loss);

                foreach (var (hit, loss) in replacements)
                {
                    int count = (int)Math.Min(stats[group.Best], Math.Max(0, Math.Floor(remaining / loss)));
                    stats[group.Best] -= count;
                    stats[hit] += count;
                    remaining -= count * loss;
                }

                if (stats[group.Best] > 0 && replacements.Length > 0 && remaining > replacements[^1].Loss / 2.0)
                {
                    stats[group.Best]--;
                    stats[replacements[^1].Hit]++;
                    remaining -= replacements[^1].Loss;
                }
            }

            if (remaining > maximumRoundingLoss / 2.0 + 0.001)
                throw new ArgumentException("This target requires more misses or different slider judgements.");
        }

        private double accuracyPoints(IReadOnlyDictionary<HitResult, int> stats) =>
            stats.Where(pair => processor.ResultAffectsAccuracy(pair.Key)).Sum(pair => (double)pair.Value * processor.GetBaseScoreForResult(pair.Key));

        private ScoreInfo buildScore(Dictionary<HitResult, int> stats, int combo)
        {
            double accuracy = accuracyMaximum == 0 ? 1 : accuracyPoints(stats) / accuracyMaximum;
            var rank = processor.RankFromScore(accuracy, stats);
            foreach (var mod in mods.OfType<IApplicableToScoreProcessor>())
                rank = mod.AdjustRank(rank, accuracy);

            var score = new ScoreInfo(working.BeatmapInfo, mode)
            {
                Passed = true, Accuracy = accuracy, Rank = rank, Mods = mods, MaxCombo = combo, Combo = combo,
                Statistics = stats, MaximumStatistics = maximum,
            };
            double pp = calculator.Calculate(score, difficulty).Total;
            if (!double.IsFinite(pp))
                throw new ArgumentException("The performance calculator returned an invalid PP value.");

            score.PP = pp;
            return score;
        }

        public void Dispose() => processor.Dispose();

        public static string JudgementLabel(string mode, HitResult hit) => (mode, hit) switch
        {
            ("fruits", HitResult.Miss) => "Fruit misses",
            ("fruits", HitResult.LargeTickMiss) => "Droplet misses",
            ("fruits", HitResult.SmallTickMiss) => "Tiny droplet misses",
            ("osu", HitResult.LargeTickMiss) => "Large ticks missed",
            ("osu", HitResult.IgnoreMiss) => "Slider ends missed",
            _ => hit.ToString(),
        };
    }
}
