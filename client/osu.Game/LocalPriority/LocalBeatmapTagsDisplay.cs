// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// "Local tags" in song select's beatmap details: tags given to this difficulty locally.
    /// Click a tag to search for it, × to remove it, "+ add" to tag the beatmap (several at once with commas).
    /// </summary>
    public partial class LocalBeatmapTagsDisplay : FillFlowContainer
    {
        private readonly IBindable<WorkingBeatmap> beatmap;
        private readonly Action<string> search;

        private FillFlowContainer tagsFlow = null!;
        private LocalBeatmapTags? tags;

        public LocalBeatmapTagsDisplay(IBindable<WorkingBeatmap> beatmap, Action<string> search)
        {
            this.beatmap = beatmap.GetBoundCopy();
            this.search = search;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);
            Padding = new MarginPadding { Right = 10 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            tags = LocalBeatmapTags.Current;

            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = @"Local tags",
                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                },
                tagsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(5),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindValueChanged(_ => Scheduler.AddOnce(refresh), true);

            if (tags != null)
                tags.Changed += onTagsChanged;
        }

        private void onTagsChanged() => Scheduler.AddOnce(refresh);

        /// <summary>
        /// The tags currently shown (the store hands out the same array until a beatmap's tags change, and one shared empty array).
        /// </summary>
        private IReadOnlyList<string>? shownTags;

        private AddTagButton? addButton;

        /// <summary>
        /// Song select changes beatmaps constantly while scrolling: the drawables are only rebuilt when the shown tags actually change,
        /// and the "+ add" button is created once (it always adds to the beatmap selected at that moment).
        /// </summary>
        private void refresh()
        {
            var info = beatmap.Value?.BeatmapInfo;
            var current = tags == null || info == null || string.IsNullOrEmpty(info.MD5Hash) ? null : tags.Get(info);

            if (current != null && addButton == null)
            {
                tagsFlow.Add(addButton = new AddTagButton(text =>
                {
                    var target = beatmap.Value?.BeatmapInfo;

                    if (target == null || tags == null)
                        return;

                    foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        tags.Add(target, part);
                }));

                tagsFlow.SetLayoutPosition(addButton, float.MaxValue);
            }

            addButton?.FadeTo(current != null ? 1 : 0);

            if (ReferenceEquals(current, shownTags))
                return;

            shownTags = current;

            foreach (var chip in tagsFlow.OfType<TagChip>().ToArray())
                tagsFlow.Remove(chip, true);

            if (current == null || info == null || tags == null)
                return;

            foreach (string tag in current)
            {
                string t = tag;
                tagsFlow.Add(new TagChip(t, () => search($@"tag=""{t}""!"), () => tags.Remove(info, t)));
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (tags != null)
                tags.Changed -= onTagsChanged;

            base.Dispose(isDisposing);
        }

        private partial class TagChip : CompositeDrawable
        {
            private readonly string tag;
            private readonly Action onSearch;
            private readonly Action onRemove;

            public TagChip(string tag, Action onSearch, Action onRemove)
            {
                this.tag = tag;
                this.onSearch = onSearch;
                this.onRemove = onRemove;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 4;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Lime1.Darken(0.6f),
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Children = new Drawable[]
                        {
                            new OsuHoverContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Action = onSearch,
                                TooltipText = @"Search for this tag",
                                Child = new OsuSpriteText
                                {
                                    Text = tag,
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Margin = new MarginPadding { Left = 6, Right = 4, Vertical = 2 },
                                },
                            },
                            new OsuHoverContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Action = onRemove,
                                TooltipText = @"Remove this local tag",
                                Child = new SpriteIcon
                                {
                                    Icon = FontAwesome.Solid.Times,
                                    Size = new Vector2(8),
                                    Margin = new MarginPadding { Right = 6, Left = 2, Vertical = 5 },
                                },
                            },
                        },
                    },
                };
            }
        }

        private partial class AddTagButton : OsuClickableContainer, IHasPopover
        {
            private readonly Action<string> add;

            public AddTagButton(Action<string> add)
            {
                this.add = add;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 4;
                TooltipText = @"Tag this beatmap locally (searchable, and usable for profile auto-switching)";
                Action = this.ShowPopover;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.15f),
                    },
                    new OsuSpriteText
                    {
                        Text = @"+ add",
                        Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                        Margin = new MarginPadding { Horizontal = 6, Vertical = 2 },
                    },
                };
            }

            public Popover GetPopover() => new AddTagPopover(add);
        }

        private partial class AddTagPopover : OsuPopover
        {
            private readonly Action<string> add;
            private OsuTextBox textBox = null!;

            public AddTagPopover(Action<string> add)
            {
                this.add = add;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                string[] known = LocalBeatmapTags.Current?.AllTags().Take(12).ToArray() ?? Array.Empty<string>();

                Child = new FillFlowContainer
                {
                    Width = 280,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        textBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            PlaceholderText = @"e.g. jumps/cross-screen, skillset/aim",
                            CommitOnFocusLost = false,
                        },
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0.7f,
                            Text = known.Length > 0
                                ? $@"Enter adds (commas for several). Used before: {string.Join(@", ", known)}"
                                : @"Enter adds (commas for several).",
                        },
                    },
                };

                textBox.OnCommit += (_, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(textBox.Text))
                        add(textBox.Text);

                    this.HidePopover();
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(textBox));
            }
        }
    }
}
