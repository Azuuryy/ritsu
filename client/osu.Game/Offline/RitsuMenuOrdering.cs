// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osuTK;
using osuTK.Input;

namespace osu.Game.Offline
{
    internal static partial class RitsuMenuOrdering
    {
        public const string Sort = "sort";
        public const string Group = "group";
        public const string Scope = "scope";
        public const string Collections = "collections";

        public static void RecordUse(RitsuLibrary library, string menu, string key)
        {
            var order = library.MenuOrder(menu);
            var uses = new Dictionary<string, int>(order.Uses);
            uses[key] = uses.GetValueOrDefault(key) + 1;
            library.SetMenuOrder(menu, order with { Uses = uses });
        }

        public static Action Bind<T>(RitsuLibrary library, IDialogOverlay? dialogs, Dropdown<T> dropdown, Menu menu, string name, Func<T, string> label)
        {
            bool sorting = false;
            void refresh()
            {
                T[] current = dropdown.Items.ToArray();
                T[] sorted = library.MenuOrder(name).Apply(current, key, label);
                if (current.SequenceEqual(sorted))
                    return;

                sorting = true;
                try
                {
                    var selected = dropdown.Current.Value;
                    foreach (var item in current)
                        dropdown.RemoveDropdownItem(item);
                    foreach (var item in sorted)
                        dropdown.AddDropdownItem(item);
                    dropdown.Current.Value = selected;
                }
                finally
                {
                    sorting = false;
                }
            }

            menu.StateChanged += state =>
            {
                if (state == MenuState.Open)
                    refresh();
            };
            dropdown.Current.ValueChanged += e =>
            {
                if (!sorting && menu.State == MenuState.Open && e.NewValue != null)
                    RecordUse(library, name, key(e.NewValue));
            };
            refresh();
            return () => dialogs?.Push(new OrderDialog(name, library,
                dropdown.Items.Select(item => new Entry(key(item), label(item))).ToArray(), refresh));

            static string key(T item) => item?.ToString() ?? string.Empty;
        }

        internal sealed record Entry(string Key, string Label);

        internal partial class OrderDialog : PopupDialog
        {
            public OrderDialog(string menu, RitsuLibrary library, Entry[] entries, Action? afterSave = null)
            {
                var order = library.MenuOrder(menu);
                HeaderText = $"Arrange {menu}";
                BodyText = "Pin items with the star. In manual mode, drag to reorder or focus an item and press ↑ or ↓.";
                Icon = FontAwesome.Solid.Sort;

                var pins = order.Pins.ToHashSet();
                var list = new OrderList(pins)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 260,
                };
                list.Items.AddRange(order.Apply(entries, entry => entry.Key, entry => entry.Label));
                var mode = new OsuDropdown<RitsuMenuOrderMode>
                {
                    RelativeSizeAxes = Axes.X,
                    Items = Enum.GetValues<RitsuMenuOrderMode>(),
                    Current = { Value = order.Mode },
                };
                list.CanReorder = () => mode.Current.Value == RitsuMenuOrderMode.Manual;

                MainContent.Add(new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding { Horizontal = 40, Vertical = 8 },
                    Children = new Drawable[] { mode, list },
                });
                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = "Save",
                        Action = () =>
                        {
                            library.SetMenuOrder(menu, order with
                            {
                                Mode = mode.Current.Value,
                                Order = list.Items.Select(item => item.Key).ToArray(),
                                Pins = pins.ToArray(),
                            });
                            afterSave?.Invoke();
                        },
                    },
                    new PopupDialogCancelButton { Text = "Cancel" },
                };
            }
        }

        private partial class OrderList : OsuRearrangeableListContainer<Entry>
        {
            private readonly HashSet<string> pins;
            public Func<bool> CanReorder { get; set; } = () => true;

            public OrderList(HashSet<string> pins)
            {
                this.pins = pins;
            }

            protected override OsuRearrangeableListItem<Entry> CreateOsuDrawable(Entry item) => new OrderRow(item, this, pins);

            public void Move(Entry entry, int direction)
            {
                int current = Items.IndexOf(entry);
                int target = current + direction;
                if (CanReorder() && current >= 0 && target >= 0 && target < Items.Count)
                    Items.Move(current, target);
            }
        }

        private partial class OrderRow : OsuRearrangeableListItem<Entry>
        {
            private readonly OrderList list;
            private readonly HashSet<string> pins;

            public OrderRow(Entry entry, OrderList list, HashSet<string> pins)
                : base(entry)
            {
                this.list = list;
                this.pins = pins;
            }

            protected override bool IsDraggableAt(Vector2 screenSpacePos) => list.CanReorder() && base.IsDraggableAt(screenSpacePos);

            protected override Drawable CreateContent()
            {
                var button = new OrderButton(list, Model)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 26,
                };
                void updateLabel() => button.Text = $"{(pins.Contains(Model.Key) ? "★" : "☆")}  {Model.Label}";
                button.Action = () =>
                {
                    if (!pins.Remove(Model.Key))
                        pins.Add(Model.Key);
                    updateLabel();
                    GetContainingFocusManager()?.ChangeFocus(button);
                };
                updateLabel();
                return button;
            }
        }

        private partial class OrderButton : OsuButton
        {
            private readonly OrderList list;
            private readonly Entry entry;

            public override bool AcceptsFocus => true;

            public OrderButton(OrderList list, Entry entry)
            {
                this.list = list;
                this.entry = entry;
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                if (HasFocus && e.Key is Key.Up or Key.Down)
                {
                    list.Move(entry, e.Key == Key.Up ? -1 : 1);
                    return true;
                }

                if (HasFocus && e.Key is Key.Enter or Key.KeypadEnter or Key.Space)
                {
                    Action?.Invoke();
                    return true;
                }

                return base.OnKeyDown(e);
            }
        }
    }
}
