// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Library
{
    internal partial class RitsuQueuePanel : OverlayContainer
    {
        private readonly OsuGame game;
        private readonly SongSelect songSelect;
        private readonly FillFlowContainer rows;
        private readonly osu.Framework.Bindables.Bindable<WorkingBeatmap> selection = new osu.Framework.Bindables.Bindable<WorkingBeatmap>();

        public RitsuQueuePanel(OsuGame game, SongSelect songSelect)
        {
            this.game = game;
            this.songSelect = songSelect;
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.35f, 0.86f);
            Y = 60;
            Masking = true;
            CornerRadius = 8;
            Depth = -100;
            AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(25, 24, 34, 255) });
            AddInternal(new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(12),
                Child = rows = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 7),
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            game.RitsuQueue.Changed += Refresh;
            selection.BindTo(songSelect.BeatmapForQueue);
            selection.BindValueChanged(_ => Refresh(), true);
        }

        protected override void PopIn()
        {
            Refresh();
            this.FadeIn(120);
        }

        protected override void PopOut() => this.FadeOut(120);

        public void Refresh()
        {
            if (!IsLoaded || State.Value != Visibility.Visible)
                return;

            rows.Clear();
            rows.Add(button("Close queue", Hide));
            label("Current selection");
            var current = songSelect.BeatmapForQueue.Value;
            if (current.BeatmapInfo.BeatmapSet != null)
                rows.Add(mapRow(current, null));

            if (game.RitsuQueue.CanAdd(current.BeatmapInfo))
                rows.Add(button("Add current difficulty", () => game.QueueRitsuBeatmap(current.BeatmapInfo)));

            label("Up next");
            var items = game.RitsuQueue.Snapshot();
            if (items.Length == 0)
            {
                label("The queue is empty.");
                return;
            }

            rows.Add(button("Select next difficulty", () => game.SelectNextRitsuQueueItem(songSelect)));
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var map = game.ResolveRitsuQueueBeatmap(item.Hash);
                if (map != null)
                    rows.Add(mapRow(game.GetRitsuQueueWorkingBeatmap(map), () => game.SelectRitsuQueueItem(songSelect, item.Hash)));
                else
                    label(item.Title + " · unavailable");

                var actions = new FillFlowContainer { RelativeSizeAxes = Axes.X, Height = 24, Direction = FillDirection.Horizontal };
                if (i > 0)
                    actions.Add(button("Up", () => game.RitsuQueue.Move(item.Hash, -1), 0.25f));
                if (i + 1 < items.Length)
                    actions.Add(button("Down", () => game.RitsuQueue.Move(item.Hash, 1), 0.25f));
                actions.Add(button("Remove", () => game.RitsuQueue.Remove(item.Hash), 0.35f));
                rows.Add(actions);
            }
        }

        private void label(string text) => rows.Add(new OsuSpriteText { Text = text });

        private static Drawable mapRow(WorkingBeatmap map, Action? select)
        {
            var info = map.BeatmapInfo;
            var row = new Container { RelativeSizeAxes = Axes.X, Height = 46, Masking = true };
            row.Add(new Container
            {
                Size = new Vector2(46),
                Masking = true,
                Child = new BeatmapBackgroundSprite(map) { RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill },
            });
            var title = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Left = 54 },
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = info.Metadata.Title },
                    new OsuSpriteText { Text = $"{info.Metadata.Artist} · {info.DifficultyName}" },
                },
            };
            if (select != null)
                row.Add(new RoundedButton { RelativeSizeAxes = Axes.Both, Action = select });
            row.Add(title);
            return row;
        }

        private static RoundedButton button(string text, Action action, float width = 1) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Width = width,
            Height = 24,
            Text = text,
            Action = action,
        };

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                game.RitsuQueue.Changed -= Refresh;
                selection.UnbindAll();
            }
            base.Dispose(isDisposing);
        }
    }
}
