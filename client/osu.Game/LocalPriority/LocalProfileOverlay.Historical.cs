// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Overlays.Profile.Sections.Historical;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LocalPriority
{
    public partial class LocalProfileOverlay
    {
        /// <summary>
        /// "Historical", like the website's profile: play history (per month, or per week) and the most played beatmaps.
        /// </summary>
        private partial class HistoricalPanel : ProfileSection
        {
            public override LocalisableString Title => @"Historical";

            public override string Identifier => @"historical";

            private static readonly int[] expansion_steps = { ScoresPanel.COMPACT_COUNT, 50, 100, 150, LocalProfileStatistics.MOST_PLAYED_LIMIT };

            private readonly LocalProfileStatistics stats;
            private readonly Action<BeatmapInfo> presentBeatmap;

            private readonly Bindable<bool> byWeek = new Bindable<bool>();

            private Container chartContainer = null!;
            private FillFlowContainer mostPlayedFlow = null!;

            private const string most_played_list = @"mostPlayed";

            private readonly IDictionary<string, int> expandedLists;

            public HistoricalPanel(LocalProfileStatistics stats, Action<BeatmapInfo> presentBeatmap, IDictionary<string, int> expandedLists)
            {
                this.stats = stats;
                this.presentBeatmap = presentBeatmap;
                this.expandedLists = expandedLists;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // The game's own profile section (title with underline, rounded background), like the website profile.
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new HistoricalHeader(@"Play History", null),
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(6, 0),
                                        Children = new Drawable[]
                                        {
                                            new PeriodButton(@"Months", false, byWeek),
                                            new PeriodButton(@"Weeks", true, byWeek),
                                        },
                                    },
                                },
                            },
                            chartContainer = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Top = 10, Left = 20, Right = 40, Bottom = 10 },
                            },
                            new HistoricalHeader(@"Most Played Beatmaps", stats.MostPlayedCount)
                            {
                                Margin = new MarginPadding { Top = 15 },
                            },
                            mostPlayedFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                            },
                        },
                    },
                };

                // As far as it was expanded last time (see LocalProfileOverlay.expandedLists).
                fillMostPlayed(Math.Clamp(expandedLists.TryGetValue(most_played_list, out int step) ? step : 0, 0, expansion_steps.Length - 1));
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                byWeek.BindValueChanged(_ => updateChart(), true);
            }

            private void updateChart()
            {
                var values = (byWeek.Value ? stats.WeeklyPlays : stats.MonthlyPlays).ToArray();

                if (values.Length < 2)
                {
                    chartContainer.Child = empty(@"Play history appears after your first counted plays.");
                    return;
                }

                chartContainer.Child = new ProfileLineChart(@"Plays")
                {
                    TickDateFormat = byWeek.Value ? @"d MMM yyyy" : @"MMM yyyy",
                    TooltipDateFormat = byWeek.Value ? @"'Week of' d MMMM yyyy" : @"MMMM yyyy",
                    Values = values,
                };
            }

            [Resolved]
            private osu.Game.Database.RealmAccess realm { get; set; } = null!;

            private void fillMostPlayed(int step)
            {
                mostPlayedFlow.Clear();

                if (stats.MostPlayed.Count == 0)
                {
                    mostPlayedFlow.Add(empty(@"No plays yet in this mode."));
                    return;
                }

                int shown = Math.Min(stats.MostPlayed.Count, expansion_steps[step]);

                // The first rows were loaded with the statistics; "show more" loads the next beatmaps here.
                LocalBeatmapEntry.Load(stats.MostPlayed.Take(shown), realm);

                foreach (var entry in stats.MostPlayed.Take(shown))
                {
                    var beatmap = entry.Beatmap;

                    // Beatmaps no longer in the database can't be shown (their plays still count towards the total).
                    if (beatmap == null)
                        continue;

                    mostPlayedFlow.Add(new MostPlayedRow(beatmap, entry.Plays) { Action = () => presentBeatmap(beatmap) });
                }

                if (shown < stats.MostPlayed.Count && step + 1 < expansion_steps.Length)
                {
                    mostPlayedFlow.Add(new ShowMoreButton
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Margin = new MarginPadding { Top = 6 },
                        Action = () =>
                        {
                            expandedLists[most_played_list] = step + 1;
                            fillMostPlayed(step + 1);
                        },
                    });
                }
            }

            private static Drawable empty(string text) => new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.GetFont(size: 14),
                Alpha = 0.7f,
                Margin = new MarginPadding { Left = 10, Vertical = 6 },
            };
        }

        /// <summary>
        /// Accent bar + title (+ counter), the same size as the Ranks section's headers.
        /// </summary>
        private partial class HistoricalHeader : CompositeDrawable
        {
            private readonly string text;
            private readonly int? count;

            public HistoricalHeader(string text, int? count)
            {
                this.text = text;
                this.count = count;

                AutoSizeAxes = Axes.Both;
                Padding = new MarginPadding { Vertical = 10 };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                        },
                    },
                };

                if (count != null)
                {
                    flow.Add(new CounterPill
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Current = { Value = count.Value },
                    });
                }

                InternalChildren = new Drawable[]
                {
                    new CircularContainer
                    {
                        RelativeSizeAxes = Axes.Y,
                        Height = 0.75f,
                        Width = 3,
                        Masking = true,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 10 },
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Highlight1,
                        },
                    },
                    flow,
                };
            }
        }

        /// <summary>
        /// "Months" / "Weeks" switch for the play history chart.
        /// </summary>
        private partial class PeriodButton : OsuClickableContainer
        {
            private readonly string text;
            private readonly bool weeks;
            private readonly Bindable<bool> byWeek;

            private Box background = null!;
            private OsuSpriteText label = null!;

            [Resolved]
            private OverlayColourProvider colours { get; set; } = null!;

            public PeriodButton(string text, bool weeks, Bindable<bool> byWeek)
            {
                this.text = text;
                this.weeks = weeks;
                this.byWeek = byWeek;

                AutoSizeAxes = Axes.X;
                Height = 26;
                Masking = true;
                CornerRadius = 13;
                Action = () => byWeek.Value = weeks;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                        Margin = new MarginPadding { Horizontal = 14 },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                byWeek.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                bool selected = byWeek.Value == weeks;
                background.FadeColour(selected ? colours.Highlight1 : IsHovered ? colours.Background2 : colours.Background3, 150);
                label.FadeColour(selected ? Color4.White : colours.Content2, 150);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// One most played beatmap: background, "title [difficulty] by artist", "mapped by …", and ▶ play count. Click: show it in song select.
        /// </summary>
        private partial class MostPlayedRow : OsuClickableContainer
        {
            private readonly BeatmapInfo beatmap;
            private readonly long plays;

            private Box hover = null!;

            public MostPlayedRow(BeatmapInfo beatmap, long plays)
            {
                this.beatmap = beatmap;
                this.plays = plays;

                RelativeSizeAxes = Axes.X;
                Height = 60;
                Masking = true;
                CornerRadius = 8;
                TooltipText = @"Show in song select";
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours, OsuColour osuColour)
            {
                var metadata = beatmap.Metadata;

                Children = new Drawable[]
                {
                    // Website item colours: lighter than the section behind it.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background2,
                    },
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.06f),
                        Alpha = 0,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 80),
                            new Dimension(GridSizeMode.Absolute, 12),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    CornerRadius = 8,
                                    Child = new UpdateableBeatmapBackgroundSprite
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Beatmap = { Value = beatmap },
                                    },
                                },
                                Empty(),
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 17, weight: FontWeight.Bold))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                        }.With(f =>
                                        {
                                            // Like the website: just the song title here (the artist only follows after "by").
                                            f.AddText(new RomanisableString(metadata.TitleUnicode, metadata.Title));
                                            f.AddText($@" [{beatmap.DifficultyName}] ");
                                            f.AddText($@"by {metadata.Artist}", t => t.Font = OsuFont.GetFont(size: 17, weight: FontWeight.Regular));
                                        }),
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Text = @"mapped by ",
                                                    Font = OsuFont.GetFont(size: 12),
                                                    Colour = colours.Foreground1,
                                                },
                                                new MapperLink(metadata.Author),
                                            },
                                        },
                                    },
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5, 0),
                                    Margin = new MarginPadding { Horizontal = 15 },
                                    Colour = osuColour.Yellow,
                                    Children = new Drawable[]
                                    {
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Icon = FontAwesome.Solid.Play,
                                            Size = new Vector2(14),
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = plays.ToString(@"N0"),
                                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Regular),
                                            // Text sits low in its box (room below the baseline): lift the digits to the arrow's middle.
                                            Y = -4,
                                        },
                                    },
                                },
                            },
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.FadeIn(100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(200);
                base.OnHoverLost(e);
            }
        }
    
        /// <summary>
        /// The mapper's name; clicking it opens their profile (needs the online account for lookups when a local profile is active).
        /// </summary>
        private partial class MapperLink : OsuHoverContainer
        {
            private readonly osu.Game.Models.RealmUser author;

            [Resolved]
            private OsuGame? game { get; set; }

            public MapperLink(osu.Game.Models.RealmUser author)
            {
                this.author = author;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                IdleColour = colours.Light2;
                HoverColour = colours.Light1;

                Child = new OsuSpriteText
                {
                    Text = author.Username,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                };

                // Every mapper is clickable: by their online ID, or by name for beatmaps that don't store it.
                if (!string.IsNullOrEmpty(author.Username))
                {
                    TooltipText = @"View profile";
                    Action = () => game?.ShowUser(new APIUser { Id = author.OnlineID > 1 ? author.OnlineID : 1, Username = author.Username }); // ID 1: looked up by name
                }
            }
        }
}
}
