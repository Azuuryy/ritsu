// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Offline
{
    internal partial class RitsuInputDialog : PopupDialog
    {
        private readonly OsuTextBox input;
        private readonly Func<string, string?> save;

        public RitsuInputDialog(string title, string initialValue, Func<string, string?> save, string description = "")
        {
            this.save = save;
            HeaderText = title;
            BodyText = description;
            Icon = FontAwesome.Solid.Edit;

            MainContent.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 40, Vertical = 10 },
                Child = input = new OsuTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    Text = initialValue,
                    ReleaseFocusOnCommit = false,
                },
            });

            input.OnCommit += (_, _) => confirm();
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "Save", Action = confirm, HideDialogOnAction = false },
                new PopupDialogCancelButton { Text = "Cancel", Action = Hide },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Schedule(() =>
            {
                GetContainingFocusManager()?.ChangeFocus(input);
                input.SelectAll();
            });
        }

        private void confirm()
        {
            string? error = save(input.Text.Trim());
            if (error == null)
                Hide();
            else
                BodyText = error;
        }
    }
}
