// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Mods
{
    public partial class ModPanel : ModSelectPanel, IFilterable
    {
        public Mod Mod => modState.Mod;
        public override BindableBool Active => modState.Active;

        protected override float IdleSwitchWidth => 54;
        protected override float ExpandedSwitchWidth => 70;

        private readonly ModState modState;

        // rojo de la gate torii (mismo vermillion que usan los auras Founder).
        private static readonly Color4 torii_red = new Color4(255, 80, 60, 255);
        private static readonly Color4 torii_badge_active = new Color4(70, 20, 35, 255);
        private static readonly Color4 modosu_gold = new Color4(217, 175, 79, 255);
        private static readonly Color4 modosu_badge_active = new Color4(72, 50, 20, 255);
        private static readonly Color4 ritsu_blue = new Color4(158, 203, 248, 255);
        private static readonly Color4 ritsu_lavender = new Color4(209, 198, 245, 255);
        private static readonly Color4 ritsu_badge_active = new Color4(35, 48, 85, 255);

        /// <summary>
        /// Style of the "exclusive" badge associated with this panel's mod, if any.
        /// Single source of truth for badge, border and edge glow of all exclusive mods.
        /// </summary>
        private ExclusiveStyle? exclusiveStyle;

        private sealed record ExclusiveStyle(
            LocalisableString Text,
            Drawable? Icon,
            bool TintIcon,
            Color4 IdleColour,
            Color4 ActiveColour,
            Color4 BorderStart,
            Color4 BorderEnd,
            Color4 EdgeColour);

        protected override ColourInfo BorderColourFor(Colour4 background, Colour4 foreground)
        {
            if (exclusiveStyle != null)
                return ColourInfo.GradientVertical(exclusiveStyle.BorderStart, exclusiveStyle.BorderEnd);

            return base.BorderColourFor(background, foreground);
        }

        public ModPanel(ModState modState)
        {
            this.modState = modState;

            Title = Mod.Name;
            Description = Mod.Description;

            SwitchContainer.Child = new ModSwitchSmall(Mod)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Active = { BindTarget = Active },
                Shear = -OsuGame.SHEAR,
                Scale = new Vector2(HEIGHT / ModSwitchSmall.DEFAULT_SIZE)
            };
        }

        public ModPanel(Mod mod)
            : this(new ModState(mod))
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, TextureStore textures)
        {
            AccentColour = colours.ForModType(Mod.Type);

            exclusiveStyle = Mod switch
            {
                IToriiExclusiveMod => new ExclusiveStyle(
                    "from torii",
                    new SpriteIcon { Icon = FontAwesome.Solid.ToriiGate, Size = new Vector2(12) },
                    TintIcon: true,
                    torii_red, torii_badge_active,
                    torii_red, torii_red, torii_red),

                IModosuMod => new ExclusiveStyle(
                    "from modosu",
                    new Container
                    {
                        Size = new Vector2(9),
                        Children = new Drawable[]
                        {
                            new Circle
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Colour = Color4.Black,
                                Text = "M",
                                Font = OsuFont.Default.With(size: 7, weight: FontWeight.Bold)
                            }
                        }
                    },
                    TintIcon: false,
                    modosu_gold, modosu_badge_active,
                    modosu_gold, modosu_gold, modosu_gold),

                IRitsuExclusiveMod => new ExclusiveStyle(
                    "from ritsu",
                    new Sprite { Texture = textures.Get("Ritsu/logo"), Size = new Vector2(16) },
                    TintIcon: false,
                    ritsu_blue, ritsu_badge_active,
                    ritsu_blue, ritsu_lavender, ritsu_blue),

                _ => null,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            modState.ValidForSelection.BindValueChanged(_ => updateFilterState());
            modState.MatchingTextFilter.BindValueChanged(_ => updateFilterState(), true);

            modState.Preselected.BindValueChanged(b => updateEdgeEffect(b.NewValue), true);

            if (exclusiveStyle != null)
                createExclusiveBadge(exclusiveStyle);
        }

        private void createExclusiveBadge(ExclusiveStyle style)
        {
            // el marco mas grueso va con el badge, no con un mod en particular.
            Content.BorderThickness = 2;
            // Borders are rendered by the mask shader on its children. The text background
            // has its own mask, so repaint the border on a transparent box in front of it.
            Content.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Transparent, Depth = float.MinValue });

            if (style.Icon != null)
            {
                // mismo anchor X que el texto, sino el flow crashea.
                style.Icon.Anchor = Anchor.CentreLeft;
                style.Icon.Origin = Anchor.CentreLeft;

                if (style.TintIcon)
                    style.Icon.Colour = style.IdleColour;
            }

            var text = new OsuSpriteText
            {
                Text = style.Text,
                Font = OsuFont.Default.With(size: 9, weight: FontWeight.SemiBold),
                Colour = style.IdleColour,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            var flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(3, 0),
            };

            if (style.Icon != null)
                flow.Add(style.Icon);
            flow.Add(text);

            MainContentContainer.Add(new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding { Right = 6, Top = 5 },
                Shear = -OsuGame.SHEAR,
                Child = flow,
            });

            Active.BindValueChanged(_ =>
            {
                Color4 colour = Active.Value ? style.ActiveColour : style.IdleColour;

                if (style.Icon != null && style.TintIcon)
                    style.Icon.FadeColour(colour, TRANSITION_DURATION, Easing.OutQuint);

                text.FadeColour(colour, TRANSITION_DURATION, Easing.OutQuint);
            }, true);
        }

        private void updateEdgeEffect(bool preselected)
        {
            if (preselected)
            {
                Content.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = AccentColour,
                    Hollow = true,
                    Radius = 2,
                };

                return;
            }

            if (exclusiveStyle != null)
            {
                Content.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = exclusiveStyle.EdgeColour,
                    Hollow = true,
                    Radius = 1,
                };

                return;
            }

            Content.EdgeEffect = default;
        }

        protected override void Select()
        {
            modState.PendingConfiguration = Mod.RequiresConfiguration;
            Active.Value = true;
        }

        protected override void Deselect()
        {
            modState.PendingConfiguration = false;
            Active.Value = false;
        }

        #region Filtering support

        /// <seealso cref="ModState.Visible"/>
        public bool Visible => modState.Visible;

        public override IEnumerable<LocalisableString> FilterTerms => new LocalisableString[]
        {
            Mod.Name,
            Mod.Name.Replace(" ", string.Empty),
            Mod.Acronym,
        };

        public override bool MatchingFilter
        {
            get => modState.MatchingTextFilter.Value;
            set => modState.MatchingTextFilter.Value = value;
        }

        private void updateFilterState()
        {
            this.FadeTo(Visible ? 1 : 0);
        }

        #endregion
    }
}
