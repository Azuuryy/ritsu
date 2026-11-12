// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;
using osuTK;
using osuTK.Input;

namespace osu.Game.Offline
{
    internal sealed record RitsuPaletteEntry(string Title, string Alias, Action Invoke, bool KeepOpen = false);

    internal partial class RitsuCommandPalette : PopupDialog
    {
        private readonly Func<RitsuPaletteEntry[]> getEntries;
        private readonly PaletteSearch search;
        private readonly FillFlowContainer rows;
        private readonly OsuScrollContainer scroll;
        private RitsuPaletteEntry[] entries = Array.Empty<RitsuPaletteEntry>();
        private RitsuPaletteEntry[] visibleEntries = Array.Empty<RitsuPaletteEntry>();
        private int selection;

        public RitsuCommandPalette(Func<RitsuPaletteEntry[]> getEntries)
        {
            this.getEntries = getEntries;
            HeaderText = "Ritsu command palette";
            Icon = FontAwesome.Solid.Terminal;

            MainContent.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = 35, Vertical = 10 },
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    search = new PaletteSearch(handleSearchKey)
                    {
                        RelativeSizeAxes = Axes.X,
                        PlaceholderText = "Search actions or enter /recent",
                        ReleaseFocusOnCommit = false,
                    },
                    scroll = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 270,
                        Child = rows = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 3),
                        },
                    },
                },
            });
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "Run", Action = runSelection, HideDialogOnAction = false },
                new PopupDialogCancelButton { Text = "Close" },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            entries = getEntries();
            search.Current.BindValueChanged(_ =>
            {
                selection = 0;
                refresh();
            }, true);
        }

        protected override void PopIn()
        {
            base.PopIn();
            search.Delay(ENTER_DURATION).Schedule(() => GetContainingFocusManager()?.ChangeFocus(search));
        }

        private void refresh()
        {
            string query = search.Text.Trim();
            visibleEntries = entries.Where(e => e.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                                                || e.Alias.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            selection = Math.Clamp(selection, 0, Math.Max(0, visibleEntries.Length - 1));
            rows.Clear();
            if (visibleEntries.Length == 0)
            {
                rows.Add(new OsuSpriteText { Text = "No matching actions" });
                return;
            }

            foreach (var (entry, index) in visibleEntries.Select((entry, index) => (entry, index)))
            {
                rows.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 28,
                    Text = string.IsNullOrEmpty(entry.Alias) ? entry.Title : $"{entry.Title}   {entry.Alias}",
                    Alpha = index == selection ? 1 : 0.65f,
                    Action = () => invoke(entry),
                });
            }
        }

        private bool handleSearchKey(Key key)
        {
            if (key is Key.Up or Key.Down && visibleEntries.Length > 0)
            {
                int direction = key == Key.Up ? -1 : 1;
                int previous = selection;
                selection = Math.Clamp(selection + direction, 0, visibleEntries.Length - 1);
                rows[previous].Alpha = 0.65f;
                rows[selection].Alpha = 1;
                scroll.ScrollIntoView(rows[selection]);
                return true;
            }

            if (key is Key.Enter or Key.KeypadEnter)
            {
                runSelection();
                return true;
            }

            return false;
        }

        private void runSelection()
        {
            if (visibleEntries.Length > 0)
                invoke(visibleEntries[selection]);
        }

        private void invoke(RitsuPaletteEntry entry)
        {
            if (!entry.KeepOpen)
            {
                Hide();
                entry.Invoke();
                return;
            }

            entry.Invoke();
            entries = getEntries();
            refresh();
            GetContainingFocusManager()?.ChangeFocus(search);
        }

        private partial class PaletteSearch : OsuTextBox
        {
            private readonly Func<Key, bool> handleKey;

            public override bool RequestsFocus => true;

            public PaletteSearch(Func<Key, bool> handleKey)
            {
                this.handleKey = handleKey;
            }

            protected override bool OnKeyDown(KeyDownEvent e) => handleKey(e.Key) || base.OnKeyDown(e);
        }
    }
}
