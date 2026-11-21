// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    internal partial class ScoreProgressionGraph : CompositeDrawable
    {
        private const int sample_count = 161;

        private static readonly Color4[] series_colours =
        {
            new Color4(111, 204, 250, 255),
            new Color4(106, 229, 187, 255),
            new Color4(218, 172, 255, 255),
            new Color4(240, 130, 184, 255),
            new Color4(254, 122, 116, 255),
            new Color4(174, 220, 100, 255),
            new Color4(255, 204, 103, 255),
        };

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private readonly ScoreInfo score;
        private readonly FillFlowContainer view;
        private readonly OsuSpriteText status;
        private readonly bool[] visible = { true, true, false, false, false, false };
        private readonly List<Plot> plots = new List<Plot>();
        private readonly LocalisableString[] labels;
        private readonly RoundedButton[] toggles = new RoundedButton[6];
        private CancellationTokenSource? cancellation;
        private Progression? progression;
        private OsuTextFlowContainer? readout;
        private bool started;

        public ScoreProgressionGraph(ScoreInfo score)
        {
            this.score = score;
            var ruleset = score.Ruleset.CreateInstance();
            labels = new LocalisableString[]
            {
                "Accuracy", "PP", "HP", "Misses",
                ruleset.GetDisplayNameForHitResult(HitResult.Ok),
                ruleset.GetDisplayNameForHitResult(HitResult.Meh),
            };

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChild = view = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 9),
                Child = status = new OsuSpriteText { Text = "Loading play progression…" },
            };
        }

        public async void Start()
        {
            if (started)
                return;

            started = true;
            if (score.HitEvents.Count == 0)
            {
                status.Text = "Play progression data unavailable.";
                return;
            }

            var token = (cancellation = new CancellationTokenSource()).Token;
            try
            {
                var snapshot = score.DeepClone();
                var working = beatmapManager.GetWorkingBeatmap(snapshot.BeatmapInfo);
                var prepared = await Task.Run(() => Compute(working, snapshot, token), token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                    Schedule(() =>
                    {
                        if (!token.IsCancellationRequested)
                            showProgression(prepared);
                    });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                Logger.Error(error, "Could not calculate play progression.");
                if (!token.IsCancellationRequested)
                    Schedule(() => status.Text = "Play progression data unavailable.");
            }
        }

        internal static Progression Compute(WorkingBeatmap working, ScoreInfo snapshot, CancellationToken token)
        {
            var ruleset = snapshot.Ruleset.CreateInstance();
            var mods = snapshot.Mods;
            var playable = working.GetPlayableBeatmap(snapshot.Ruleset, mods, token);
            var events = snapshot.HitEvents;
            double start = Math.Min(playable.HitObjects.First().StartTime, eventTime(events[0]));
            double end = Math.Max(start + 1, events.Max(eventTime));
            if (snapshot.Passed)
                end = Math.Max(end, playable.HitObjects.Max(hit => hit.GetEndTime()));

            var calculator = ruleset.CreatePerformanceCalculator();
            var difficulty = calculator == null ? null : ruleset.CreateDifficultyCalculator(working).CalculateTimed(mods, token);
            bool fullCombo = snapshot.Passed && snapshot.GetMaximumAchievableCombo() > 0 && snapshot.IsPerfect();

            double[]?[] values =
            {
                new double[sample_count],
                difficulty?.Count > 0 ? new double[sample_count] : null,
                !fullCombo && snapshot.Passed && difficulty?.Count > 0 && FullComboScoreInfo.Create(snapshot) != null ? new double[sample_count] : null,
                LegacyHealthGraph.SampleHealth(snapshot, playable, start, end, sample_count, token),
                new double[sample_count],
                ruleset.GetHitResultsForDisplay().Any(item => item.result == HitResult.Ok) ? new double[sample_count] : null,
                ruleset.GetHitResultsForDisplay().Any(item => item.result == HitResult.Meh) ? new double[sample_count] : null,
            };

            using var processor = ruleset.CreateScoreProcessor();
            processor.TrackHitEvents = false;
            processor.Mods.Value = mods;
            processor.ScoreV1Active |= snapshot.IsLegacyScore;
            foreach (var mod in mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(processor);
            processor.ApplyBeatmap(playable);

            var prefix = new ScoreInfo(snapshot.BeatmapInfo, snapshot.Ruleset) { Mods = mods };
            var maximum = new Dictionary<HitResult, int>();
            double[] times = new double[sample_count];
            int nextEvent = 0;
            double lastObjectTime = double.NegativeInfinity;

            for (int i = 0; i < sample_count; i++)
            {
                token.ThrowIfCancellationRequested();
                double time = times[i] = i == sample_count - 1 ? end : start + (end - start) * i / (sample_count - 1);
                while (nextEvent < events.Count && eventTime(events[nextEvent]) <= time)
                {
                    var hit = events[nextEvent++];
#pragma warning disable CS0618
                    if (hit.Result == HitResult.LegacyComboIncrease)
                        continue;
#pragma warning restore CS0618

                    var judgement = new JudgementResult(hit.HitObject, hit.HitObject.Judgement) { Type = hit.Result, TimeOffset = hit.TimeOffset };
                    processor.ApplyResult(judgement);
                    lastObjectTime = judgement.HitObject.GetEndTime();
                    HitResult best = judgement.Judgement.MaxResult;
                    maximum[best] = maximum.GetValueOrDefault(best) + 1;
                }

                values[0]![i] = processor.Accuracy.Value * 100;
                values[4]![i] = processor.Statistics.GetValueOrDefault(HitResult.Miss);
                if (values[5] is { } ok) ok[i] = processor.Statistics.GetValueOrDefault(HitResult.Ok);
                if (values[6] is { } meh) meh[i] = processor.Statistics.GetValueOrDefault(HitResult.Meh);

                if (values[1] is not { } pp || nextEvent == 0 || difficulty == null || calculator == null)
                    continue;

                int matchingAttribute = difficulty.BinarySearch(new TimedDifficultyAttributes(lastObjectTime, null));
                int attributeIndex = Math.Clamp(matchingAttribute < 0 ? ~matchingAttribute - 1 : matchingAttribute, 0, difficulty.Count - 1);

                processor.PopulateScore(prefix);
                prefix.MaximumStatistics.Clear();
                foreach (var (result, count) in maximum)
                    prefix.MaximumStatistics[result] = count;
                var attributes = difficulty[attributeIndex].Attributes;
                pp[i] = finitePP(calculator.Calculate(prefix, attributes).Total);

                if (values[2] is { } ifFullCombo)
                {
                    var corrected = FullComboScoreInfo.Create(prefix);
                    ifFullCombo[i] = corrected == null ? pp[i] : finitePP(calculator.Calculate(corrected, attributes).Total);
                }
            }

            for (int i = 0; i < sample_count; i++)
                values[3]![i] *= 100;

            return new Progression(times, values, fullCombo);
        }

        private static double finitePP(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

        private static double eventTime(HitEvent hit) => hit.HitObject.GetEndTime() + hit.TimeOffset;

        private void showProgression(Progression data)
        {
            progression = data;
            view.Clear();

            var buttons = new FillFlowContainer<RoundedButton>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(6),
            };
            for (int i = 0; i < toggles.Length; i++)
            {
                int index = i;
                var button = toggles[i] = new RoundedButton
                {
                    Text = labels[i],
                    Width = 82,
                    Height = 26,
                    Action = () =>
                    {
                        visible[index] = !visible[index];
                        refreshPlots();
                    },
                };
                buttons.Add(button);
            }

            view.Add(buttons);
            readout = new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 12)) { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            view.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 34,
                Child = readout,
            });

            foreach (string title in new[] { "Accuracy / HP", "Estimated PP", "Judgements" })
            {
                var plot = new Plot(title, hover);
                plots.Add(plot);
                view.Add(plot);
            }

            refreshPlots();
        }

        private void refreshPlots()
        {
            var data = progression!;
            int[] indices = { 0, 1, 3, 4, 5, 6 };
            for (int i = 0; i < toggles.Length; i++)
            {
                toggles[i].BackgroundColour = visible[i] ? series_colours[indices[i]].Darken(0.66f) : new Color4(62, 63, 68, 255);
                toggles[i].Enabled.Value = data.Values[indices[i]] != null;
            }

            updatePlot(plots[0], "Accuracy / HP", 100, "%", false, visible[0] ? 0 : -1, visible[2] ? 3 : -1);
            updatePlot(plots[1], "Estimated PP", 0, " pp", false, visible[1] ? 1 : -1, visible[1] ? 2 : -1);
            updatePlot(plots[2], "Judgements", 0, "", true, visible[3] ? 4 : -1, visible[4] ? 5 : -1, visible[5] ? 6 : -1);
            hover(null);
        }

        private void updatePlot(Plot plot, string title, double fixedMax, string unit, bool stepped, params int[] indices)
        {
            var data = progression!;
            var lines = indices.Where(index => index >= 0 && data.Values[index] != null)
                               .Select(index => (Values: data.Values[index]!, Colour: series_colours[index]))
                               .ToArray();
            double maximum = fixedMax > 0 ? fixedMax : Math.Max(1, Math.Ceiling(lines.SelectMany(line => line.Values).DefaultIfEmpty().Max()));
            plot.SetLines($"{title} · 0–{maximum:0}{unit}", lines, maximum, stepped);
        }

        private void hover(float? position)
        {
            var data = progression!;
            foreach (var plot in plots)
                plot.SetCursor(position);

            if (position is not { } x)
            {
                string duration = TimeSpan.FromMilliseconds(data.Times[^1] - data.Times[0]).ToString(@"m\:ss");
                string ppLegend = data.FullCombo ? "FC PP" : data.Values[2] != null ? "Play PP (green) · If FC (purple)" : "Play PP";
                readout!.Text = $"{(visible[1] && data.Values[1] != null ? $"{ppLegend}   ·   " : "")}0:00–{duration}";
                return;
            }

            int index = Math.Clamp((int)(x * (sample_count - 1)), 0, sample_count - 1);
            var entries = new List<string>
            {
                TimeSpan.FromMilliseconds(data.Times[index] - data.Times[0]).ToString(@"m\:ss"),
            };

            void add(int toggle, int series, string name, string format)
            {
                if (visible[toggle] && data.Values[series] is { } values)
                    entries.Add($"{name} {values[index].ToString(format)}");
            }

            add(0, 0, "Acc", "0.00'%'");
            add(1, 1, data.FullCombo ? "FC" : "PP", "0.#");
            add(1, 2, "If FC", "0.#");
            add(2, 3, "HP", "0'%'");
            add(3, 4, labels[3].ToString(), "0");
            add(4, 5, labels[4].ToString(), "0");
            add(5, 6, labels[5].ToString(), "0");
            readout!.Text = string.Join("   ·   ", entries);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                cancellation?.Cancel();
                cancellation?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        internal sealed record Progression(double[] Times, double[]?[] Values, bool FullCombo);

        private partial class Plot : CompositeDrawable
        {
            private readonly OsuSpriteText label;
            private readonly PlotArea area;

            public Plot(string title, Action<float?> hover)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 3),
                    Children = new Drawable[]
                    {
                        label = new OsuSpriteText { Text = title, Font = OsuFont.GetFont(size: 13) },
                        area = new PlotArea(hover),
                    },
                };
            }

            public void SetLines(string title, (double[] Values, Color4 Colour)[] lines, double maximum, bool stepped)
            {
                label.Text = title;
                area.SetLines(lines, maximum, stepped);
                Alpha = lines.Length > 0 ? 1 : 0;
            }

            public void SetCursor(float? position) => area.SetCursor(position);
        }

        private partial class PlotArea : CompositeDrawable
        {
            private readonly Action<float?> hover;
            private readonly Container paths;
            private readonly Box cursor;
            private (double[] Values, Color4 Colour)[] lines = Array.Empty<(double[], Color4)>();
            private double maximum;
            private bool stepped;
            private float previousWidth = -1;

            public PlotArea(Action<float?> hover)
            {
                this.hover = hover;
                RelativeSizeAxes = Axes.X;
                Height = 65;
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(35, 39, 43, 255) },
                    gridLine(Anchor.TopLeft),
                    gridLine(Anchor.CentreLeft),
                    gridLine(Anchor.BottomLeft),
                    paths = new Container { RelativeSizeAxes = Axes.Both },
                    cursor = new Box { RelativeSizeAxes = Axes.Y, RelativePositionAxes = Axes.X, Width = 1, Alpha = 0 },
                };
            }

            private static Box gridLine(Anchor anchor) => new Box
            {
                Anchor = anchor,
                Origin = anchor,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(83, 91, 99, 255),
            };

            public void SetLines((double[] Values, Color4 Colour)[] newLines, double newMaximum, bool useSteps)
            {
                lines = newLines;
                maximum = newMaximum;
                stepped = useSteps;
                previousWidth = -1;
            }

            public void SetCursor(float? position)
            {
                cursor.Alpha = position.HasValue ? 0.65f : 0;
                cursor.X = position ?? 0;
            }

            protected override bool OnMouseMove(MouseMoveEvent e)
            {
                if (DrawWidth > 0)
                    hover(Math.Clamp(e.MousePosition.X / DrawWidth, 0, 1));
                return base.OnMouseMove(e);
            }

            protected override bool OnHover(HoverEvent e) => true;

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover(null);
                base.OnHoverLost(e);
            }

            protected override void Update()
            {
                base.Update();
                if (DrawWidth == previousWidth)
                    return;

                previousWidth = DrawWidth;
                paths.Clear();

                foreach (var (values, colour) in lines)
                {
                    var vertices = new List<Vector2>(values.Length * (stepped ? 2 : 1));
                    for (int i = 0; i < values.Length; i++)
                    {
                        float x = 2 + (DrawWidth - 4) * i / (values.Length - 1);
                        float y = 2 + (DrawHeight - 4) * (1 - (float)Math.Clamp(values[i] / maximum, 0, 1));
                        if (stepped && i > 0)
                            vertices.Add(new Vector2(x, vertices[^1].Y));
                        vertices.Add(new Vector2(x, y));
                    }

                    var path = new Path { Vertices = vertices, PathRadius = 1, Colour = colour };
                    path.Position = -path.PositionInBoundingBox(Vector2.Zero);
                    paths.Add(path);
                }
            }
        }
    }
}
