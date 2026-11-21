// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Select
{
    public partial class FooterButtonCollections : ScreenFooterButton
    {
        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Text = CommonStrings.Collections;
            Icon = FontAwesome.Solid.Book;
            AccentColour = colours.DarkOrange1;
        }
    }
}
