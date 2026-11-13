// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Offline;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// torii: el campo de busqueda estilo osu!stable para la barra de arriba del song-select legacy.
    /// un label "Search:" verde-amarillo seguido de un text box inline sobre un fondo oscuro suave (asi
    /// se sigue leyendo sobre el fondo del beatmap, como el menu-button-background tenue de stable). va
    /// two-way con el filter query compartido asi escribir filtra el carousel igual que la busqueda
    /// moderna (que esta oculta).
    /// </summary>
    public partial class LegacySearchBox : CompositeDrawable, IHasPopover
    {
        public Bindable<string> Current { get; } = new Bindable<string>();

        internal FilterControl FilterControl { get; init; } = null!;

        public Popover GetPopover() => FilterControl.CreateSearchHistoryPopover(this);

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;
            var historyControls = FilterControl.CreateSearchHistoryControls(this);

            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0f, 0f, 0f, 0.4f),
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.X,
                        Height = 26,
                        Padding = new MarginPadding { Horizontal = 8 },
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = @"Search:",
                                Font = LegacyFonts.Get(20, FontWeight.Bold),
                                Colour = Color4.GreenYellow,
                                Shadow = true,
                            },
                            new SearchTextBox
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 24,
                                Current = { BindTarget = Current },
                                SearchClicked = historyControls.ShowHistory,
                                CompletionControls = historyControls,
                            },
                            historyControls,
                        },
                    },
                },
            };
        }

        private partial class SearchTextBox : FocusedTextBox
        {
            public override bool HandleLeftRightArrows => false;

            public SearchHistoryControls CompletionControls { get; init; } = null!;

            public Action? SearchClicked { get; init; }

            protected override bool OnClick(ClickEvent e)
            {
                if (e.Button == MouseButton.Left)
                    SearchClicked?.Invoke();

                return base.OnClick(e);
            }

            public SearchTextBox()
            {
                PlaceholderText = @"Type to search!";
                // como en lazer: agarra y mantiene el foco asi se puede tipear directo sin clickear.
                // solo esta presente en modo legacy, asi que no roba foco en el resto.
                HoldFocus = true;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Transparent;
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                if (CompletionControls.HandleSuggestionKey(e))
                    return true;

                if (this.FindClosestParent<SongSelect>()?.TryOpenRitsuQueue(e) == true)
                    return true;

                if (RitsuOffline.Enabled && HasFocus && !e.Repeat && ((e.Key == Key.AltLeft && e.ControlPressed)
                        || (e.Key is Key.ControlLeft or Key.ControlRight && e.CurrentState.Keyboard.Keys.IsPressed(Key.AltLeft))))
                {
                    MoveCursorBy(0);
                    ExpandSelectionBy(Text.Length);
                    int caret = Text.Length - SelectedText.Length;
                    MoveCursorBy(caret - Text.Length);
                    CompletionControls.Complete(Text, caret, completion =>
                    {
                        Text = completion.Query;
                        MoveCursorBy(completion.Caret - Text.Length);
                    });
                    return true;
                }

                // NO consumimos Enter: FocusedTextBox vendria a commitearlo y se lo comeria, y entonces
                // GlobalAction.Select (empezar el mapa) nunca dispara. lo dejamos pasar, igual que
                // FocusedTextBox ya hace con Escape para GlobalAction.Back. la busqueda filtra live por el
                // binding two-way, asi que no necesitamos el commit.
                if (e.Key == Key.Enter || e.Key == Key.KeypadEnter)
                    return false;

                return base.OnKeyDown(e);
            }
        }
    }
}
