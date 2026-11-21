// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Select;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Commands
{
    /// <summary>
    /// Editable score sheet. It follows the Modern V2 results layout (slanted wedges, a large headline value on the left,
    /// the rank ring on the right) but the headline is the estimated PP and the judgement counts are inputs.
    /// </summary>
    internal partial class RitsuSimulationScreen : OsuScreen, IKeyBindingHandler<GlobalAction>
    {
        // Same geometry as V2ResultsPanel so the two screens read as one family.
        private const float wedge_x = -60;
        private const float wedge_width = 710;
        private const float wedge_corner_radius = 10;
        private const float wedge_gap = 4;
        private const float header_height = 145;
        private const float strip_y = 128;
        private const float cell_width = 118;
        private const float cell_spacing = 6;

        private static readonly Color4 accent = new Color4(122, 233, 201, 255);

        private static readonly EdgeEffectParameters shadow = new EdgeEffectParameters
        {
            Type = EdgeEffectType.Shadow,
            Colour = Colour4.Black.Opacity(0.3f),
            Radius = 16,
            Offset = new Vector2(5, 4),
        };

        private readonly WorkingBeatmap working;
        private readonly UserModSelectOverlay modPicker;
        private readonly FillFlowContainer statRow;
        private readonly FillFlowContainer judgementFlow;
        private readonly OsuSpriteText ppValue;
        private readonly OsuSpriteText status;
        private readonly BeatmapTitleWedge.Statistic lengthStat = new BeatmapTitleWedge.Statistic(OsuIcon.Clock);
        private readonly BeatmapTitleWedge.Statistic bpmStat = new BeatmapTitleWedge.Statistic(OsuIcon.Metronome);
        private readonly BeatmapTitleWedge.DifficultyStatisticsDisplay countDisplay = new BeatmapTitleWedge.DifficultyStatisticsDisplay(true);
        private readonly BeatmapTitleWedge.DifficultyStatisticsDisplay difficultyDisplay = new BeatmapTitleWedge.DifficultyStatisticsDisplay(true);
        private readonly FillFlowContainer indicators = new FillFlowContainer { AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(8, 0) };
        private readonly FillFlowContainer iconAndStars = new FillFlowContainer { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(6, 0) };
        private StarRatingDisplay? starDisplay;
        private readonly OsuSpriteText difficultyName;
        private OsuSpriteText mapperText = null!;
        private readonly Container ringHolder = new Container { Name = "Accuracy circle", Size = new Vector2(414), Y = 85 };
        private readonly ModDisplay modsDisplay;
        private readonly OsuSpriteText modsHeader;
        private ScreenFooterButton? resetButton;
        private ScreenFooterButton? modsButton;
        private bool resetEnabled;
        private readonly RoundedButton applyMods;
        private readonly Dictionary<HitResult, OsuTextBox> fields = new Dictionary<HitResult, OsuTextBox>();
        private readonly Dictionary<HitResult, CountDisplay> counts = new Dictionary<HitResult, CountDisplay>();
        private readonly Container hero = new Container { Name = "Headline value", Position = new Vector2(48, 222), Size = new Vector2(412, 92) };
        private FillFlowContainer heroFlow = null!;
        private OsuTextBox? accuracy;
        private OsuTextBox? combo;
        private CountDisplay? fixedCombo;
        private ScheduledDelegate? queuedCalculation;
        private CancellationTokenSource? preparation;
        private RitsuSimulation? model;
        private RulesetInfo mode = null!;
        private Mod[] mods = Array.Empty<Mod>();
        private OsuColour colours = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        public override string Title => "Score simulation";

        public RitsuSimulationScreen(WorkingBeatmap working)
        {
            this.working = working;
            modPicker = new UserModSelectOverlay { Depth = -1, Beatmap = { Value = working } };

            statRow = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Horizontal };
            judgementFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(cell_spacing, 8),
            };
            ppValue = new OsuSpriteText { Font = OsuFont.Numeric.With(size: 104, weight: FontWeight.Light), Spacing = new Vector2(-2, 0), Shadow = false, Text = "—" };
            status = new OsuSpriteText { Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold), Shadow = false };
            difficultyName = new OsuSpriteText
            {
                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                Shadow = false,
                Text = working.BeatmapInfo.DifficultyName,
            };
            modsDisplay = new ModDisplay(false) { Scale = new Vector2(0.87f), ExpansionMode = ExpansionMode.ExpandOnHover };
            modsHeader = new OsuSpriteText { Text = "MODS", Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold), Shadow = false, Alpha = 0 };
            applyMods = new RoundedButton { Width = 160, Height = 35, Text = "Apply mods", Action = modPicker.Hide };

            // Same footer modules as Song Select (Mods, Rewind), with Reset in place of the random buttons.
            FooterContent.Value = ScreenFooterContent.Argon(leftButtons: createFooterButtons);
        }

        private IReadOnlyList<ScreenFooterButton> createFooterButtons()
        {
            resetButton = new ScreenFooterButton
            {
                Text = "Reset",
                Icon = FontAwesome.Solid.Undo,
                AccentColour = colours.Blue1,
                TooltipText = "Reset to a perfect score (F2)",
                Action = resetScore,
            };
            resetButton.Enabled.Value = resetEnabled;

            return new[]
            {
                modsButton = new ScreenFooterButton
                {
                    Text = "Mods",
                    Icon = FontAwesome.Solid.ExchangeAlt,
                    AccentColour = colours.Lime1,
                    TooltipText = "Edit mods and rate (F1)",
                    Action = toggleMods,
                },
                resetButton,
            };
        }

        // Shortcuts press the footer module so they play the same click sound as a mouse click.
        private void pressMods()
        {
            if (modsButton is { IsLoaded: true, IsPresent: true })
                modsButton.TriggerClickWithSound();
            else
                toggleMods();
        }

        private void pressReset()
        {
            if (resetButton is { IsLoaded: true, IsPresent: true } && resetButton.Enabled.Value)
                resetButton.TriggerClickWithSound();
            else
                resetScore();
        }

        private void toggleMods()
        {
            if (modPicker.State.Value == Visibility.Visible)
                modPicker.Hide();
            else
                modPicker.Show();
        }

        private void resetScore()
        {
            if (model != null)
                rebuildInputs(model);
        }

        private void setResetEnabled(bool enabled)
        {
            resetEnabled = enabled;

            if (resetButton != null)
                resetButton.Enabled.Value = enabled;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            this.colours = colours;
            var titleBackground = colourProvider.Background3;
            var stripBackground = colourProvider.Background5;
            var bodyBackground = colourProvider.Background6;
            var metadata = working.BeatmapInfo.Metadata;

            AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(20, 23, 30, 255) });
            // Same parallax as the menus: ParallaxContainer follows the user's menu parallax settings (off means no movement).
            AddInternal(new ParallaxContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new BeatmapBackgroundSprite(working)
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0.3f,
                },
            });
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(Colour4.Black.Opacity(0.3f), Colour4.Black.Opacity(0.5f)),
            });

            var leftColumn = new Container { RelativeSizeAxes = Axes.Y, Width = 700 };
            var rightColumn = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = 473,
            };

            status.Position = new Vector2(50, 214);
            modsHeader.Position = new Vector2(66, 538);

            leftColumn.AddRange(new Drawable[]
            {
                new Container
                {
                    Name = "Wedges",
                    Position = new Vector2(wedge_x, 0),
                    RelativeSizeAxes = Axes.Y,
                    Width = wedge_width,
                    Shear = OsuGame.SHEAR,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Name = "Title wedge",
                            RelativeSizeAxes = Axes.X,
                            Height = header_height - 17,
                            CornerRadius = wedge_corner_radius,
                            Masking = true,
                            EdgeEffect = shadow,
                            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = titleBackground.Opacity(0.95f) },
                        },
                        new Container
                        {
                            Name = "Score wedge",
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = header_height + wedge_gap - 17, Bottom = 6 },
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                CornerRadius = wedge_corner_radius,
                                Masking = true,
                                EdgeEffect = shadow,
                                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = bodyBackground.Opacity(0.85f) },
                            },
                        },
                        new Container
                        {
                            Name = "Difficulty and map statistics",
                            Size = new Vector2(wedge_width - 10, 88),
                            Y = strip_y,
                            CornerRadius = wedge_corner_radius,
                            Masking = true,
                            EdgeEffect = shadow,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.X, Height = 34, Colour = stripBackground },
                                new Box { RelativeSizeAxes = Axes.X, Y = 34, Height = 54, Colour = stripBackground.Darken(0.45f) },
                            },
                        },
                    },
                },
                new Container
                {
                    Name = "Simulation badge",
                    Position = new Vector2(66, 18),
                    Scale = new Vector2(1.3f),
                    AutoSizeAxes = Axes.Both,
                    CornerRadius = 7,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = accent },
                        new OsuSpriteText
                        {
                            Margin = new MarginPadding { Horizontal = 6, Vertical = 1 },
                            Text = "SCORE SIMULATION",
                            Font = OsuFont.Default.With(size: 10, weight: FontWeight.Bold),
                            Colour = Colour4.FromHex("#20322A"),
                            Shadow = false,
                        },
                    },
                },
                new TruncatingSpriteText
                {
                    Position = new Vector2(66, 35),
                    MaxWidth = 480,
                    Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                    Font = OsuFont.TorusAlternate.With(size: 32, weight: FontWeight.SemiBold),
                    Shadow = false,
                },
                new TruncatingSpriteText
                {
                    Position = new Vector2(66, 68),
                    MaxWidth = 470,
                    Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                    Font = OsuFont.Default.With(size: 22),
                    Shadow = false,
                },
                indicators.With(d =>
                {
                    d.Position = new Vector2(61, strip_y + 17);
                    d.Origin = Anchor.CentreLeft;
                    d.Children = new Drawable[]
                    {
                        iconAndStars,
                        new FillFlowContainer
                        {
                            Name = "Difficulty name and mapper",
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(5, 0),
                            Children = new Drawable[]
                            {
                                difficultyName,
                                mapperText = new OsuSpriteText
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    Text = $"mapped by {metadata.Author.Username}",
                                    Font = OsuFont.Default.With(size: 14),
                                    Shadow = false,
                                },
                            },
                        },
                    };
                }),
                new FillFlowContainer
                {
                    Name = "Length and BPM",
                    Position = new Vector2(66, 96),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(14, 0),
                    Children = new Drawable[] { lengthStat, bpmStat },
                },
                countDisplay.With(d =>
                {
                    d.Position = new Vector2(46, 168);
                    d.Scale = Vector2.One;
                }),
                difficultyDisplay.With(d =>
                {
                    d.Anchor = Anchor.TopLeft;
                    d.Origin = Anchor.TopRight;
                    d.Position = new Vector2(598, 168);
                    d.Scale = Vector2.One;
                }),
                status,
                hero.With(h => h.Child = heroFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Colour = ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")),
                    Children = new Drawable[]
                    {
                        ppValue,
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Y = -22,
                            Text = "pp",
                            Font = OsuFont.Numeric.With(size: 40, weight: FontWeight.Light),
                            Shadow = false,
                        },
                    },
                }),
                new FillFlowContainer
                {
                    Name = "Statistics",
                    Position = new Vector2(50, 326),
                    Width = 495,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[] { statRow, judgementFlow },
                },
                modsHeader,
                new Container
                {
                    Name = "Mods area",
                    Position = new Vector2(66, 552),
                    Size = new Vector2(380, 36),
                    Masking = true,
                    Child = modsDisplay,
                },
            });

            rightColumn.Add(ringHolder);
            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Top = Toolbar.HEIGHT },
                Child = new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1280, 620),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = new Container { RelativeSizeAxes = Axes.Both, Children = new Drawable[] { leftColumn, rightColumn } },
                },
            });

            AddInternal(modPicker);

            applyMods.Anchor = Anchor.BottomRight;
            applyMods.Origin = Anchor.BottomRight;
            applyMods.Margin = new MarginPadding(16);
            applyMods.Depth = -2;
            applyMods.Alpha = 0;
            AddInternal(applyMods);
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
            });

            Ruleset.BindValueChanged(change =>
            {
                var selected = change.NewValue;
                var native = working.BeatmapInfo.Ruleset;
                var next = native.OnlineID == 0 || native.Equals(selected) ? selected : native;
                if (mode != null && next.Equals(mode))
                    return;

                mode = next;
                mods = mode.Equals(selected) ? Mods.Value.Select(mod => mod.DeepClone()).ToArray() : Array.Empty<Mod>();
                modPicker.Ruleset.Value = mode;
                modPicker.SelectedMods.Value = mods.Select(mod => mod.DeepClone()).ToArray();
                prepare();
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
            queuedCalculation?.Cancel();
            statRow.Clear();
            judgementFlow.Clear();
            fields.Clear();
            counts.Clear();
            accuracy = null;
            combo = null;
            fixedCombo = null;
            lengthStat.Text = "—";
            bpmStat.Text = "—";
            countDisplay.Statistics = Array.Empty<BeatmapTitleWedge.StatisticDifficulty.Data>();
            difficultyDisplay.Statistics = Array.Empty<BeatmapTitleWedge.StatisticDifficulty.Data>();
            ppValue.Text = "—";
            setStatus("Calculating difficulty and perfect PP…", false);
            modsDisplay.Current.Value = selectedMods;
            modsHeader.Alpha = selectedMods.Length > 0 ? 1 : 0;
            setResetEnabled(false);
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
                    lengthStat.Text = prepared.LengthText;
                    bpmStat.Text = prepared.BpmText;
                    countDisplay.Statistics = prepared.Counts;
                    difficultyDisplay.Statistics = prepared.Attributes;
                    showDifficulty(prepared);
                    rebuildInputs(prepared);
                    setResetEnabled(true);
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
                            setStatus($"Cannot calculate PP for {selectedMode.ShortName}: {error.Message}", true);
                    });
            }
        }

        private void showDifficulty(RitsuSimulation simulation)
        {
            var colour = colours.ForStarDifficulty(simulation.StarRating);
            iconAndStars.Clear();
            iconAndStars.AddRange(new Drawable[]
            {
                mode.CreateInstance().CreateIcon().With(d =>
                {
                    d.Anchor = d.Origin = Anchor.CentreLeft;
                    d.Size = new Vector2(20);
                    d.Colour = colour;
                }),
                starDisplay = new StarRatingDisplay(new StarDifficulty(simulation.StarRating, 0), StarRatingDisplaySize.Small)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
            });
        }

        private void setStatus(string text, bool error)
        {
            status.Text = text;
            status.Colour = error ? colours.Red1 : Colour4.White.Opacity(0.75f);
        }

        private void rebuildInputs(RitsuSimulation simulation)
        {
            queuedCalculation?.Cancel();
            statRow.Clear();
            judgementFlow.Clear();
            fields.Clear();
            counts.Clear();

            var accuracyField = textBox("100%", 28, 146);
            accuracy = accuracyField.Input;
            accuracy.Name = "Target accuracy";
            statRow.Add(statColumn("ACCURACY", accuracyField, 170, true, 0));

            combo = null;
            fixedCombo = null;
            if (mode.ShortName != "mania")
            {
                var comboField = textBox($"{simulation.MaxCombo}x", 28, 146);
                comboField.Suffix = $"/{simulation.MaxCombo}";
                combo = comboField.Input;
                combo.Name = "Maximum combo";
                statRow.Add(statColumn("COMBO", comboField, 184, true, 16));
            }
            else
            {
                fixedCombo = new CountDisplay(34, "x", true);
                fixedCombo.Set($"{simulation.MaxCombo}");
                statRow.Add(statColumn("COMBO", fixedCombo, 184, true, 16));
            }

            var perfect = new CountDisplay(34, "pp", false);
            perfect.Set($"{simulation.PerfectPP:0.##}");
            statRow.Add(statColumn("PERFECT PP", perfect, 178, false, 16));

            int index = 0;
            int total = simulation.Groups.Sum(g => 1 + g.Editable.Length);
            bool separator() => index % 4 != 3 && index < total - 1;

            foreach (var group in simulation.Groups)
            {
                bool main = group.Best <= HitResult.Perfect;
                var best = new CountDisplay(38, main ? "×" : $"/{group.Count}", main);
                counts[group.Best] = best;
                judgementFlow.Add(cell(RitsuSimulation.BestLabel(mode.ShortName, group.Best), group.Best, best, separator()));
                index++;

                foreach (var judgement in group.Editable)
                {
                    var field = textBox("0", 26, 84);
                    field.Input.Name = RitsuSimulation.JudgementLabel(mode.ShortName, judgement);
                    fields.Add(judgement, field.Input);
                    judgementFlow.Add(cell(field.Input.Name, judgement, field, separator()));
                    index++;
                }
            }

            calculateScore();
        }

        private static Drawable slantedSeparator(bool visible) => new Box
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            Position = new Vector2(-4, 6),
            Size = new Vector2(1.5f, 28),
            Rotation = 10,
            Colour = Colour4.White.Opacity(0.3f),
            Alpha = visible ? 1 : 0,
            BypassAutoSizeAxes = Axes.Both,
        };

        private static Drawable statColumn(string header, Drawable value, float width, bool separator, float leftPadding) => new Container
        {
            Width = width,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2),
                    Padding = new MarginPadding { Left = leftPadding, Right = 14 },
                    Children = new[]
                    {
                        (Drawable)new OsuSpriteText
                        {
                            Text = header,
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                            Shadow = false,
                        },
                        value,
                    },
                },
                slantedSeparator(separator),
            },
        };

        private Drawable cell(string label, HitResult result, Drawable value, bool separator) => new Container
        {
            Width = cell_width,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 1),
                    Padding = new MarginPadding { Right = 10 },
                    Children = new[]
                    {
                        (Drawable)new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = label.ToUpperInvariant(),
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                            Colour = colours.ForHitResult(result),
                            Shadow = false,
                        },
                        value,
                    },
                },
                slantedSeparator(separator),
            },
        };

        private ParallelogramField textBox(string hint, float size, float width)
        {
            var field = new ParallelogramField(size, width) { Input = { PlaceholderText = hint } };
            field.Input.Current.BindValueChanged(_ =>
            {
                queuedCalculation?.Cancel();
                queuedCalculation = Scheduler.AddDelayed(calculateScore, 150);
            });
            field.Input.OnCommit += (_, _) =>
            {
                queuedCalculation?.Cancel();
                calculateScore();
            };
            return field;
        }

        private void calculateScore()
        {
            if (model == null || accuracy == null)
                return;

            try
            {
                var inputs = fields.Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Text))
                                   .ToDictionary(pair => pair.Key, pair => parseCount(pair.Value.Text));
                double? target = null;
                if (!string.IsNullOrWhiteSpace(accuracy.Text))
                {
                    string accuracyText = accuracy.Text.Trim();
                    if (accuracyText.EndsWith('%'))
                        accuracyText = accuracyText[..^1];

                    if ((!double.TryParse(accuracyText, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                         && !double.TryParse(accuracyText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        || !double.IsFinite(value) || value < 0 || value > 100)
                        throw new ArgumentException("Enter target accuracy between 0 and 100%.");

                    target = value / 100;
                }

                int? achievedCombo = null;
                if (combo is { Text: var comboText } && !string.IsNullOrWhiteSpace(comboText))
                {
                    comboText = comboText.Trim();
                    if (comboText.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                        comboText = comboText[..^1];

                    achievedCombo = parseCount(comboText);
                }

                var calculated = model.Calculate(inputs, target, achievedCombo);
                ppValue.Text = $"{calculated.PP:0.##}";
                accuracy.PlaceholderText = $"{calculated.Accuracy:P2}";
                if (combo != null)
                    combo.PlaceholderText = $"{calculated.MaxCombo}x";
                if (fixedCombo != null)
                    fixedCombo.Set($"{calculated.MaxCombo}");

                setStatus(string.Empty, false);
                foreach (var (hit, text) in counts)
                    text.Set(calculated.Statistics.TryGetValue(hit, out int count) ? count : 0);

                showCircle(calculated);
            }
            catch (ArgumentException error)
            {
                ppValue.Text = "—";
                setStatus(error.Message, true);
            }
        }

        /// <summary>Shows the original results accuracy ring and rank for the simulated score.</summary>
        private void showCircle(ScoreInfo score)
        {
            ringHolder.Clear();
            ringHolder.Add(new AccuracyCircle(score) { UseV2Style = true, RelativeSizeAxes = Axes.Both });
        }

        private static int parseCount(string text) =>
            int.TryParse(text, out int count) && count >= 0 ? count : throw new ArgumentException("Judgements and combo must be nonnegative whole numbers.");

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!this.IsCurrentScreen())
                return false;

            switch (e.Action)
            {
                // F1 and F2 are bound to the Song Select actions Mods and Random. Claiming those actions here also
                // stops the global F1 "previous track" binding, which comes later in the same key binding list.
                case GlobalAction.ToggleModSelection:
                    if (!e.Repeat)
                        pressMods();
                    return true;

                case GlobalAction.SelectNextRandom:
                    if (!e.Repeat && modPicker.State.Value != Visibility.Visible)
                        pressReset();
                    return true;

                case GlobalAction.Back:
                    if (modPicker.State.Value == Visibility.Visible)
                        modPicker.Hide();
                    else
                        this.Exit();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // F1 / F2 alone: mods and reset, matching the footer modules. Combinations are left alone.
            if (!e.Repeat && !e.ControlPressed && !e.AltPressed && !e.ShiftPressed && !e.SuperPressed)
            {
                if (e.Key == Key.F1)
                {
                    pressMods();
                    return true;
                }

                if (e.Key == Key.F2 && modPicker.State.Value != Visibility.Visible)
                {
                    pressReset();
                    return true;
                }
            }

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
                queuedCalculation?.Cancel();
                model?.Dispose();
                model = null;
            }

            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            // Like the Song Select info panel: the text follows the star display's readable colour,
            // not the raw difficulty colour, which can be too dark against the dark panels.
            if (starDisplay != null)
            {
                var readable = starDisplay.ForegroundTextColour;
                difficultyName.Colour = readable;
                mapperText.Colour = readable;
                countDisplay.AccentColour = readable;
                difficultyDisplay.AccentColour = readable;
            }

            // Same rule as the results score counter: the headline shrinks to fit its column.
            if (heroFlow.IsLoaded)
                heroFlow.Scale = new Vector2(Math.Min(1, hero.DrawWidth / Math.Max(1, heroFlow.DrawWidth / heroFlow.Scale.X)));
        }

        /// <summary>Numeric readout in the Modern V2 statistic style: a light value followed by a smaller suffix.</summary>
        private partial class CountDisplay : FillFlowContainer
        {
            private readonly OsuSpriteText value;
            private readonly OsuSpriteText suffix;

            /// <param name="size">Font size of the value.</param>
            /// <param name="suffixText">Suffix such as "×" or "/23".</param>
            /// <param name="largeSuffix">Whether the suffix uses the value size, like the "×" after main judgements.</param>
            public CountDisplay(float size, string suffixText, bool largeSuffix)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Children = new Drawable[]
                {
                    value = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Font = OsuFont.Numeric.With(size: size, weight: FontWeight.Light),
                        Shadow = false,
                    },
                    suffix = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Text = suffixText,
                        Font = OsuFont.Numeric.With(size: largeSuffix ? size : size * 0.5f, weight: largeSuffix ? FontWeight.Light : FontWeight.SemiBold),
                        Shadow = false,
                        Margin = new MarginPadding { Bottom = largeSuffix ? 0 : 3 },
                    },
                };
            }

            public void Set(string text) => value.Text = text;

            public void Set(int count) => value.Text = count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A numeric input drawn as a parallelogram, matching the slanted wedges and separators of the results screen.
        /// Only the plate is sheared; the text stays upright.
        /// </summary>
        private partial class ParallelogramField : CompositeDrawable
        {
            public OsuTextBox Input => input;

            public string Suffix
            {
                set => suffix.Text = value;
            }

            private readonly FieldInput input;
            private readonly Box plate;
            private readonly OsuSpriteText suffix;

            /// <param name="size">Font size of the value; the height follows it.</param>
            /// <param name="width">Width of the plate in design pixels.</param>
            public ParallelogramField(float size, float width)
            {
                RelativeSizeAxes = Axes.X;
                Height = size + 14;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = width,
                    Margin = new MarginPadding { Left = 4 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Width = 1f,
                            Shear = new Vector2(0.18f, 0),
                            CornerRadius = 5,
                            Masking = true,
                            Child = plate = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.White.Opacity(0.07f) },
                        },
                        input = new FieldInput(size) { RelativeSizeAxes = Axes.Both, Size = Vector2.One },
                        suffix = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -18,
                            Font = OsuFont.Numeric.With(size: size * 0.5f, weight: FontWeight.SemiBold),
                            Colour = Colour4.White.Opacity(0.55f),
                            Shadow = false,
                        },
                    },
                };

                input.FocusChanged += focused => plate.FadeColour(Colour4.White.Opacity(focused ? 0.16f : 0.07f), 150);
            }

            private partial class FieldInput : OsuTextBox
            {
                public event Action<bool>? FocusChanged;

                public FieldInput(float size)
                {
                    FontSize = size;
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    BackgroundUnfocused = Colour4.Transparent;
                    BackgroundFocused = Colour4.Transparent;
                    BackgroundCommit = Colour4.Transparent;
                }

                protected override void OnFocus(FocusEvent e)
                {
                    base.OnFocus(e);
                    FocusChanged?.Invoke(true);
                }

                protected override void OnFocusLost(FocusLostEvent e)
                {
                    base.OnFocusLost(e);
                    FocusChanged?.Invoke(false);
                }
            }
        }
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
        public string BpmText { get; }
        public string LengthText { get; }
        public IReadOnlyList<BeatmapTitleWedge.StatisticDifficulty.Data> Counts { get; }
        public IReadOnlyList<BeatmapTitleWedge.StatisticDifficulty.Data> Attributes { get; }
        public double StarRating => difficulty.StarRating;

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
            BpmText = ((int)Math.Round(60000 / working.Beatmap.GetMostCommonBeatLength() * rate)).ToString(CultureInfo.InvariantCulture);
            LengthText = TimeSpan.FromMilliseconds(working.BeatmapInfo.Length / rate).ToString(@"m\:ss");
            Counts = buildCounts();
            Attributes = ruleset.GetBeatmapAttributesForDisplay(working.BeatmapInfo, mods).Select(a => new BeatmapTitleWedge.StatisticDifficulty.Data(a)).ToArray();
            Summary = $"{mode.ShortName} · {(mods.Length == 0 ? "NM" : string.Join(", ", mods.Select(m => m.Acronym)))} · {difficulty.StarRating:0.00}★ · "
                      + $"{60000 / working.Beatmap.GetMostCommonBeatLength() * rate:0.##} BPM · {TimeSpan.FromMilliseconds(working.BeatmapInfo.Length / rate):m\\:ss} · {MaxCombo}x · "
                      + $"CS {effective.CircleSize:0.##} · AR {effective.ApproachRate:0.##} · OD {effective.OverallDifficulty:0.##} · HP {effective.DrainRate:0.##}";
        }

        /// <summary>Object counts shown like the Song Select info panel. Zero counts are left out.</summary>
        private IReadOnlyList<BeatmapTitleWedge.StatisticDifficulty.Data> buildCounts()
        {
            var objects = working.Beatmap.HitObjects;
            int total = objects.Count;
            int timed = objects.Count(o => o is IHasDuration);
            int sliders = objects.Count(o => o is IHasPath);
            var result = new List<BeatmapTitleWedge.StatisticDifficulty.Data>();

            void add(LocalisableString label, int count)
            {
                if (count <= 0)
                    return;

                float bar = count / (float)Math.Max(1, total);
                result.Add(new BeatmapTitleWedge.StatisticDifficulty.Data(label, bar, bar, 1, count.ToString(CultureInfo.InvariantCulture)));
            }

            switch (working.BeatmapInfo.Ruleset.ShortName)
            {
                case "osu":
                    add(BeatmapStatisticStrings.Circles, total - timed);
                    add(BeatmapStatisticStrings.Sliders, sliders);
                    break;

                case "taiko":
                    add(BeatmapStatisticStrings.Hits, total - timed);
                    add(BeatmapStatisticStrings.Drumrolls, timed);
                    break;

                case "fruits":
                    add(BeatmapStatisticStrings.Fruits, total - timed);
                    add(BeatmapStatisticStrings.JuiceStreams, timed);
                    break;

                case "mania":
                    add(BeatmapStatisticStrings.Notes, total - timed);
                    add(BeatmapStatisticStrings.HoldNotes, timed);
                    break;

                default:
                    add(BeatmapStatisticStrings.Circles, total - timed);
                    add(BeatmapStatisticStrings.Sliders, timed);
                    break;
            }

            return result;
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

        public static string BestLabel(string mode, HitResult hit) => (mode, hit) switch
        {
            ("osu", HitResult.Great) => "300",
            ("osu", HitResult.LargeTickHit) => "Slider tick",
            ("osu", HitResult.SliderTailHit) => "Slider end",
            ("fruits", HitResult.Great) => "Fruits",
            ("fruits", HitResult.LargeTickHit) => "Droplets",
            ("fruits", HitResult.SmallTickHit) => "Tiny droplets",
            _ => hit.ToString(),
        };

        public static string JudgementLabel(string mode, HitResult hit) => (mode, hit) switch
        {
            ("fruits", HitResult.Miss) => "Fruit misses",
            ("fruits", HitResult.LargeTickMiss) => "Droplet misses",
            ("fruits", HitResult.SmallTickMiss) => "Tiny droplet misses",
            ("osu", HitResult.Ok) => "100",
            ("osu", HitResult.Meh) => "50",
            ("osu", HitResult.LargeTickMiss) => "Tick misses",
            ("osu", HitResult.IgnoreMiss) => "Slider end misses",
            _ => hit.ToString(),
        };
    }
}
