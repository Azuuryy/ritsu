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

namespace osu.Game.Commands
{
    internal partial class RitsuCommandPalette : PopupDialog
    {
        private readonly RitsuCommandRegistry registry;
        private readonly string? prefix;
        private readonly OsuSpriteText status;
        private readonly PaletteSearch search;
        private readonly FillFlowContainer rows;
        private readonly OsuScrollContainer scroll;
        private RitsuPaletteEntry[] entries = Array.Empty<RitsuPaletteEntry>();
        private RitsuPaletteEntry[] visibleEntries = Array.Empty<RitsuPaletteEntry>();
        private int selection;

        public RitsuCommandPalette(RitsuCommandRegistry registry, string? prefix = null)
        {
            this.registry = registry;
            this.prefix = prefix;
            HeaderText = prefix == null ? "Ritsu command palette" : "Play queue";
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
                    status = new OsuSpriteText(),
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
            entries = registry.Entries(prefix);
            search.Current.BindValueChanged(_ =>
            {
                selection = 0;
                status.Text = string.Empty;
                refresh();
            }, true);
        }

        protected override void PopIn()
        {
            base.PopIn();
            search.Delay(ENTER_DURATION).Schedule(() => GetContainingFocusManager()?.ChangeFocus(search));
        }

        private RitsuCommand? matchCommand(string query)
        {
            var command = registry.Match(query);
            return prefix == null || command?.Alias.StartsWith(prefix, StringComparison.Ordinal) == true ? command : null;
        }

        private void refresh()
        {
            string query = search.Text.Trim();
            var match = matchCommand(query);
            visibleEntries = match != null
                ? entries.Where(e => e.Command == match).ToArray()
                : entries.Where(e => e.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                                     || e.Alias.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            selection = Math.Clamp(selection, 0, Math.Max(0, visibleEntries.Length - 1));
            rows.Clear();
            if (visibleEntries.Length == 0)
            {
                rows.Add(new OsuSpriteText { Text = prefix != null && query.Length == 0 ? "The queue is empty." : "No matching actions" });
                return;
            }

            foreach (var (entry, index) in visibleEntries.Select((entry, index) => (entry, index)))
            {
                rows.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 28,
                    Text = $"{entry.Title}   {entry.Alias} {entry.Command.Options}".TrimEnd(),
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
            string query = search.Text.Trim();
            var command = matchCommand(query);
            if (command != null)
            {
                string arguments = query[command.Alias.Length..].Trim();
                if (command.Choices == null)
                {
                    invoke(command, arguments);
                    return;
                }

                if (arguments.Length > 0)
                {
                    status.Text = $"Choose an entry for {command.Alias}.";
                    return;
                }

                if (visibleEntries.Length == 0)
                {
                    status.Text = "No choices are available.";
                    return;
                }
            }
            else if (query.StartsWith('/') && visibleEntries.Length == 0)
            {
                status.Text = "Unknown command.";
                return;
            }

            if (visibleEntries.Length > 0)
                invoke(visibleEntries[selection]);
        }

        private void invoke(RitsuPaletteEntry entry)
        {
            string query = search.Text.Trim();
            string input = entry.Choice?.Id ?? (matchCommand(query) == entry.Command ? query[entry.Alias.Length..].Trim() : string.Empty);
            invoke(entry.Command, input);
        }

        private void invoke(RitsuCommand command, string arguments)
        {
            var invocation = RitsuCommandRegistry.Bind(command, arguments);
            if (invocation.Run == null)
            {
                status.Text = invocation.Error ?? "This action is unavailable.";
                return;
            }

            if (!invocation.KeepOpen)
            {
                Hide();
                invocation.Run();
                return;
            }

            invocation.Run();
            entries = registry.Entries(prefix);
            status.Text = string.Empty;
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
