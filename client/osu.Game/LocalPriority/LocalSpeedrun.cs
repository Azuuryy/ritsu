// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// A Speedrun profile's goal and time: PP and / or an estimated global rank to reach, timed by how long the profile was active.
    /// </summary>
    public class LocalSpeedrun
    {
        /// <summary>
        /// The ruleset whose PP counts (the one selected when the profile was made).
        /// </summary>
        public string Ruleset { get; set; } = @"osu";

        public double? TargetPP { get; set; }

        /// <summary>
        /// Estimated global rank to reach.
        /// </summary>
        public int? TargetRank { get; set; }

        /// <summary>
        /// How long the profile was active so far, in seconds; the final time once <see cref="Completed"/>.
        /// </summary>
        public double Seconds { get; set; }

        /// <summary>
        /// When the goal was reached, or null while the run goes on.
        /// </summary>
        public DateTimeOffset? Completed { get; set; }

        public LocalSpeedrun Clone() => (LocalSpeedrun)MemberwiseClone();

        /// <summary>
        /// Whether a PP total reaches every goal that is set. PP counts as shown on the profile page (whole numbers).
        /// </summary>
        public bool IsReachedBy(double pp)
        {
            if (TargetPP == null && TargetRank == null)
                return false;

            if (TargetPP is double targetPP && Math.Round(pp) < targetPP)
                return false;

            if (TargetRank is int targetRank && (pp <= 0 || !(LocalPriorityPolicy.EstimateGlobalRank(Ruleset, pp) <= targetRank)))
                return false;

            return true;
        }

        /// <summary>
        /// "#10,000 • 8,550pp": the goal, with the other half estimated (the rank for a PP goal, the PP for a rank goal).
        /// </summary>
        public string DescribeGoal() => DescribeGoal(Ruleset, TargetPP, TargetRank);

        public static string DescribeGoal(string ruleset, double? targetPP, int? targetRank)
        {
            int? rank = targetRank ?? (targetPP is double p && p > 0 ? LocalPriorityPolicy.EstimateGlobalRank(ruleset, p) : null);
            double? pp = targetPP ?? (targetRank is int r ? LocalPriorityPolicy.EstimatePPForRank(ruleset, r) : null);

            string ppText = pp is double ppValue ? $@"{ppValue:N0}pp" : string.Empty;
            string rankText = rank is int rankValue ? $@"#{rankValue:N0}" : string.Empty;
            return ppText.Length > 0 && rankText.Length > 0 ? $@"{rankText} • {ppText}" : ppText + rankText;
        }

        /// <summary>
        /// Speedrun-timer style: "4:07.32", "1:04:07.32".
        /// </summary>
        public static string FormatTime(double seconds)
        {
            long centiseconds = (long)Math.Floor(Math.Max(0, seconds) * 100);
            return FormatCentiseconds(centiseconds);
        }

        public static string FormatCentiseconds(long centiseconds)
        {
            long totalSeconds = centiseconds / 100;
            long hours = totalSeconds / 3600;
            long minutes = totalSeconds / 60 % 60;
            long secs = totalSeconds % 60;
            long cs = centiseconds % 100;

            return hours > 0
                ? $@"{hours}:{minutes:00}:{secs:00}.{cs:00}"
                : $@"{minutes}:{secs:00}.{cs:00}";
        }
    }

    /// <summary>
    /// The small window in the middle of the profile page after "New 'Speedrun' profile": the run's goal (PP and / or estimated rank).
    /// The profile is made once a goal is confirmed; cancelling makes nothing.
    /// </summary>
    public partial class LocalSpeedrunGoalWindow : VisibilityContainer, IKeyBindingHandler<GlobalAction>
    {
        private readonly RulesetInfo ruleset;
        private readonly Action<LocalSpeedrun> start;

        private Container panel = null!;
        private OsuTextFlowContainer description = null!;
        private GoalTextBox goalBox = null!;
        private OsuSpriteText estimate = null!;
        private OsuSpriteText error = null!;

        private bool rankAvailable => LocalPriorityPolicy.HasRankEstimate(ruleset.ShortName);

        public LocalSpeedrunGoalWindow(RulesetInfo ruleset, Action<LocalSpeedrun> start)
        {
            this.ruleset = ruleset;
            this.start = start;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.5f),
                },
                panel = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 380,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(0.4f),
                        Radius = 16,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(20),
                            Spacing = new Vector2(0, 10),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8, 0),
                                    Children = new Drawable[]
                                    {
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Icon = OsuIcon.ModClassic,
                                            Size = new Vector2(28),
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = @"New 'Speedrun' profile",
                                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                        },
                                    },
                                },
                                description = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Alpha = 0.75f,
                                },
                                field(rankAvailable ? @"PP, or #rank" : $@"PP ({ruleset.Name})", goalBox = new GoalTextBox
                                {
                                    RelativeSizeAxes = Axes.X,
                                    PlaceholderText = rankAvailable ? @"1000 or #10000" : @"1000",
                                }),
                                // One fixed-height line under the box, so the window never resizes while typing: the typed goal with its
                                // estimated other half ("#10,000 • 8,550pp"), "#NaN • NaNpp" while there's no valid goal, or the error
                                // in its place after a failed start.
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 18,
                                    Children = new Drawable[]
                                    {
                                        estimate = new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                            Colour = colourProvider.Content2,
                                        },
                                        error = new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                            Colour = colours.Red1,
                                            Alpha = 0,
                                        },
                                    },
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.Absolute, 10),
                                        new Dimension(),
                                    },
                                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new RoundedButton
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = @"Start the run",
                                                Action = tryStart,
                                            },
                                            Empty(),
                                            new RoundedButton
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = @"Cancel",
                                                BackgroundColour = colours.Gray5,
                                                Action = Hide,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            description.AddText(@"Set a goal. This profile will have a timer counting every second its ");
            description.AddText(@"active", t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold));
            description.AddText(@" until goal is reached");

            goalBox.OnCommit += (_, _) => tryStart();
            goalBox.Current.BindValueChanged(_ => updateEstimate(), true);
        }

        private void updateEstimate()
        {
            estimate.Text = tryParseGoal(out double? pp, out int? rank, out _)
                ? LocalSpeedrun.DescribeGoal(ruleset.ShortName, pp, rank)
                : rankAvailable ? @"#NaN • NaNpp" : @"NaNpp";

            // Typing again replaces a shown error with the estimate.
            error.FadeOut(100);
            estimate.FadeIn(100);
        }

        private static Drawable field(string label, Drawable control) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 4),
            Children = new[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                },
                control,
            },
        };

        private bool started;

        private void tryStart()
        {
            if (started)
                return;

            if (!tryParseGoal(out double? pp, out int? rank, out string? problem))
            {
                showError(problem);
                return;
            }

            started = true;

            start(new LocalSpeedrun
            {
                Ruleset = ruleset.ShortName,
                TargetPP = pp,
                TargetRank = rank,
            });

            Hide();
        }

        /// <summary>
        /// One goal: a number is PP, "#" and a number is an estimated global rank (PP and rank go hand in hand anyway).
        /// </summary>
        private bool tryParseGoal(out double? pp, out int? rank, [NotNullWhen(false)] out string? problem)
        {
            string text = goalBox.Text.Trim();
            bool isRank = text.StartsWith('#');
            string number = isRank ? text[1..] : text;

            pp = null;
            rank = null;
            problem = null;

            if (text.Length == 0)
                problem = rankAvailable ? @"Type in PP, or a rank after a #." : @"Type in the PP to reach.";
            else if (isRank && !rankAvailable)
                problem = @"The rank isn't estimated for this ruleset.";
            else if (number.Length == 0 || !number.All(char.IsAsciiDigit))
                problem = isRank ? @"Type the rank as #10000." : @"PP must be a whole number.";
            else if (isRank)
            {
                if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= 1)
                    rank = value;
                else
                    problem = @"Rank must be #1 or lower.";
            }
            else
            {
                if (double.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out double value) && value > 0)
                    pp = value;
                else
                    problem = @"PP must be a number above 0.";
            }

            return problem == null;
        }

        private void showError(string text)
        {
            error.Text = text;
            error.FadeIn(100);
            estimate.FadeOut(100);
            panel.Shake();
        }

        protected override void PopIn()
        {
            this.FadeIn(150, Easing.OutQuint);
            panel.ScaleTo(0.9f).ScaleTo(1, 400, Easing.OutElasticHalf);
            Schedule(() => GetContainingFocusManager()?.ChangeFocus(goalBox));
        }

        protected override void PopOut()
        {
            this.FadeOut(150, Easing.OutQuint).Expire();
        }

        // Everything behind the window waits until it is closed.
        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnClick(ClickEvent e)
        {
            // A click next to the window cancels it.
            if (!panel.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                Hide();

            return true;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat || State.Value != Visibility.Visible)
                return false;

            if (e.Action == GlobalAction.Back)
            {
                Hide();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// Digits, and "#" for a rank.
        /// </summary>
        private partial class GoalTextBox : OsuTextBox
        {
            protected override bool CanAddCharacter(char character) => char.IsAsciiDigit(character) || character == '#';

            // The typed goal (and the placeholder) sit in the middle of the box and grow out from it evenly.
            protected override void LoadComplete()
            {
                base.LoadComplete();

                TextContainer.Anchor = Anchor.Centre;
                TextContainer.Origin = Anchor.Centre;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // The framework keeps the text a padding's width in from the left edge (and scrolls it for long text, which
                // a goal never is); centred, that would push it off the middle.
                TextContainer.X = 0;
            }
        }
    }

    /// <summary>
    /// A Speedrun profile's timer on its page ("1:04:07.32"): fixed-width digits like the dying profiles' countdown, with a little room
    /// around each ":". Only the parts that changed are updated.
    /// </summary>
    public partial class LocalSpeedrunTimeDisplay : FillFlowContainer
    {
        /// <summary>
        /// Room on each side of a ":".
        /// </summary>
        private const float colon_margin = 4;

        private readonly OsuSpriteText hours;
        private readonly OsuSpriteText hoursColon;
        private readonly OsuSpriteText minutes;
        private readonly OsuSpriteText seconds;
        private readonly OsuSpriteText centiseconds;

        private long shown = -1;

        /// <summary>
        /// The time as shown.
        /// </summary>
        public string Text { get; private set; } = string.Empty;

        public LocalSpeedrunTimeDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
            // The same spacing as between the characters inside each part, so only the ":" get their extra room.
            Spacing = new Vector2(-4, 0);

            Children = new Drawable[]
            {
                hours = part(),
                hoursColon = part(@":", colon_margin),
                minutes = part(),
                part(@":", colon_margin),
                seconds = part(),
                part(@"."),
                centiseconds = part(),
            };
        }

        private static OsuSpriteText part(string text = @"", float margin = 0) => new OsuSpriteText
        {
            Text = text,
            Margin = new MarginPadding { Horizontal = margin },
            Font = OsuFont.GetFont(size: 40, weight: FontWeight.Bold, fixedWidth: true),
            // Fixed-width digits (so the numbers don't jiggle), pulled a little closer together.
            Spacing = new Vector2(-4, 0),
        };

        public void SetTime(long totalCentiseconds)
        {
            if (totalCentiseconds == shown)
                return;

            shown = totalCentiseconds;

            long totalSeconds = totalCentiseconds / 100;
            long h = totalSeconds / 3600;

            hours.Alpha = hoursColon.Alpha = h > 0 ? 1 : 0;
            hours.Text = h.ToString(CultureInfo.InvariantCulture);
            minutes.Text = h > 0 ? $@"{totalSeconds / 60 % 60:00}" : $@"{totalSeconds / 60 % 60}";
            seconds.Text = $@"{totalSeconds % 60:00}";
            centiseconds.Text = $@"{totalCentiseconds % 100:00}";

            Text = LocalSpeedrun.FormatCentiseconds(totalCentiseconds);
        }
    }

    /// <summary>
    /// "DONE!": a Speedrun profile reached its goal (big classic icon, the final time below).
    /// </summary>
    public partial class LocalSpeedrunCompletedNotification : SimpleNotification
    {
        private readonly string message;

        /// <summary>
        /// The line under "DONE!" ("You've completed the run in 4:07.32!").
        /// </summary>
        public override LocalisableString Text
        {
            get => message;
            set { }
        }

        public LocalSpeedrunCompletedNotification(double seconds)
        {
            message = $@"You've completed the run in {LocalSpeedrun.FormatTime(seconds)}!";

            Icon = OsuIcon.ModClassic;
            IconDrawable.Size = new Vector2(30);

            TextFlow.TextAnchor = Anchor.TopCentre;
            TextFlow.AddText(@"DONE!", t => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold));
            TextFlow.NewLine();
            TextFlow.AddText(message, t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.Medium));
        }
    }
}
