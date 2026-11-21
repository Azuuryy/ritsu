// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// The game's mod select (same rules: conflicting mods are greyed out), used to choose the mods of a profile's mod rules.
    /// In this screen "Customise" sits on the left and also holds the profile rules; the create / save button is to its right.
    /// </summary>
    public partial class LocalProfileModRulesOverlay : UserModSelectOverlay
    {
        /// <summary>
        /// Mod select's side padding around the search box and "Customise".
        /// </summary>
        private const float side_padding = 100;

        /// <summary>
        /// Opens the mod rules picker on top of everything (with the game's footer, like song select's mod select).
        /// <paramref name="hideOthers"/> closes the screens it was opened from, so the picker has the whole screen.
        /// </summary>
        internal static void Open(IOverlayManager? overlays, LocalModRules? initial, string title, string confirmText, Action<LocalModRules> confirmed, Action? hideOthers)
        {
            if (overlays == null)
                return;

            hideOthers?.Invoke();

            var overlay = new LocalProfileModRulesOverlay(initial, null, title, confirmText) { Confirmed = confirmed };
            overlay.registration = overlays.RegisterBlockingOverlay(overlay);
            overlay.Show();
        }

        /// <summary>
        /// The same picker for an auto-switch rule's mods: "Customise" then holds how the selected mods have to match instead of the profile rules.
        /// Picking no mods removes the mod condition. <paramref name="closed"/> runs once the picker goes away (confirmed or not).
        /// </summary>
        internal static void PickAutoSwitchMods(IOverlayManager? overlays, LocalAutoSwitchRules initial, string title, Action<LocalAutoSwitchRules> confirmed,
                                                Action? closed, Action? hideOthers)
        {
            if (overlays == null)
                return;

            hideOthers?.Invoke();

            var overlay = new LocalProfileModRulesOverlay(null, initial, title, @"Save mods")
            {
                SwitchConfirmed = confirmed,
                Closed = closed,
            };
            overlay.registration = overlays.RegisterBlockingOverlay(overlay);
            overlay.Show();
        }

        /// <summary>
        /// "New profile with mod rules": pick mods and rules, then the profile is created, switched to, and (if set) offered same-name plays.
        /// </summary>
        internal static void CreateProfileWithRules(IOverlayManager? overlays, LocalProfileManager manager, IDialogOverlay? dialogs, Action? hideOthers = null) =>
            Open(overlays, null, @"new profile with mod rules", @"Create profile", rules =>
            {
                var profile = manager.CreateDefaultProfile();

                if (rules.Mods.Count > 0)
                    manager.SetModRules(profile.Id, rules);

                manager.Switch(profile.Id);
                LocalProfileSyncDialog.Offer(manager, dialogs, profile.Id);
            }, hideOthers);

        /// <summary>
        /// Change (or, by picking no mods, remove) an existing profile's mod rules.
        /// </summary>
        internal static void EditRules(IOverlayManager? overlays, LocalProfileManager manager, LocalProfile profile, Action? hideOthers = null)
        {
            Guid id = profile.Id;

            Open(overlays, profile.ModRules, $@"mod rules for {profile.Name}", @"Save rules",
                rules => manager.SetModRules(id, rules.Mods.Count > 0 ? rules : null), hideOthers);
        }

        public Action<LocalModRules>? Confirmed;

        /// <summary>
        /// Auto-switch mode: the rule passed in, with the picked mods and match options.
        /// </summary>
        public Action<LocalAutoSwitchRules>? SwitchConfirmed;

        /// <summary>
        /// Runs once when the picker closes, confirmed or not.
        /// </summary>
        public Action? Closed;

        private readonly LocalModRules? initial;

        /// <summary>
        /// Set when picking the mods of an auto-switch rule instead of a profile's mod rules.
        /// </summary>
        private readonly LocalAutoSwitchRules? switchInitial;

        private bool switchMode => switchInitial != null;

        private readonly BindableBool switchAll = new BindableBool();
        private readonly BindableBool switchAny = new BindableBool();
        private readonly BindableBool switchExact = new BindableBool();
        private readonly BindableBool switchExactSettings = new BindableBool();
        private readonly string title;
        private readonly string confirmText;

        private readonly BindableBool blockSelected = new BindableBool();
        private readonly BindableBool onlyAllowSelected = new BindableBool();
        private readonly BindableBool onlyAllowSelectedSettings = new BindableBool();
        private readonly BindableBool applyOnSwitch = new BindableBool();

        private IDisposable? registration;
        private bool confirmed;
        private bool wasShown;
        private OsuSpriteText summary = null!;

        [Resolved]
        private IBindable<RulesetInfo> gameRuleset { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> gameBeatmap { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        /// <summary>
        /// Opened over song select: the chosen mods apply to the game while choosing (speed, music, star rating), like song select's mod select.
        /// From anywhere else (main menu): nothing changes and no star rating / beatmap attributes are shown, like mod select there.
        /// </summary>
        private bool overSongSelect;

        private LocalProfileManager.ModPreview? preview;

        private ModSettingChangeTracker? previewSettingsTracker;

        private void pushPreview() => preview?.Set(SelectedMods.Value);

        protected override void Dispose(bool isDisposing)
        {
            previewSettingsTracker?.Dispose();
            preview?.Dispose();
            base.Dispose(isDisposing);
        }

        protected override bool AlwaysEnableCustomisation => true;

        public override VisibilityContainer CreateFooterContent() => new RulesFooterContent(this, overSongSelect)
        {
            Beatmap = { BindTarget = Beatmap },
            ActiveMods = { BindTarget = ActiveMods },
            Ruleset = { BindTarget = Ruleset },
        };

        private LocalProfileModRulesOverlay(LocalModRules? initial, LocalAutoSwitchRules? switchInitial, string title, string confirmText)
            : base(OverlayColourScheme.Pink)
        {
            this.initial = initial;
            this.switchInitial = switchInitial;

            switchAll.Value = switchInitial?.ModMatch == LocalAutoSwitchModMatch.All;
            switchAny.Value = switchInitial?.ModMatch == LocalAutoSwitchModMatch.Any;
            switchExact.Value = switchInitial?.ModMatch == LocalAutoSwitchModMatch.Exact;
            switchExactSettings.Value = switchInitial?.ExactModSettings ?? false;
            this.title = title;
            this.confirmText = confirmText;

            blockSelected.Value = initial?.BlockSelected ?? false;
            onlyAllowSelected.Value = initial?.OnlyAllowSelected ?? false;
            onlyAllowSelectedSettings.Value = initial?.OnlyAllowSelectedSettings ?? false;
            applyOnSwitch.Value = initial?.ApplyOnSwitch ?? true;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            // The mod panels grey out mods that conflict with "the selected mods": here that is this picker's selection, not the game's.
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<Bindable<IReadOnlyList<Mod>>>(SelectedMods);
            return dependencies;
        }

        protected override ModColumn CreateModColumn(ModType modType) => new RulesModColumn(modType);

        [BackgroundDependencyLoader]
        private void load()
        {
            // The create / save button's right edge lines up with the right edge of the header bar above;
            // "Customise" (with the profile rules as its first section) sits just left of the button.
            CustomisationPanel.Anchor = Anchor.TopRight;
            CustomisationPanel.Origin = Anchor.TopRight;
            CustomisationPanel.X = -(confirm_button_width + button_gap - (side_padding - header_side_padding));
            CustomisationPanel.ExtraSection = switchMode ? createSwitchSection(ColourProvider) : createRulesSection(ColourProvider);

            MainAreaContent.Add(confirmButton = new ShearedButton
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                X = -header_side_padding,
                Width = confirm_button_width,
                Height = 42,
                Text = confirmText,
                TextSize = 20,
                Action = confirm,
                // Comes in and goes out together with the search box and "Customise".
                Alpha = 0,
                Y = -slide_distance,
            });
        }

        /// <summary>
        /// The header bar's distance from the screen edges (its right edge is what the button lines up with).
        /// </summary>
        private const float header_side_padding = 70;

        private const float confirm_button_width = 220;

        /// <summary>
        /// Space between "Customise" and the button.
        /// </summary>
        private const float button_gap = 20;

        /// <summary>
        /// Same slide as mod select uses for the search box and "Customise".
        /// </summary>
        private const float slide_distance = 700;

        private ShearedButton confirmButton = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Header.Title = title;
            Header.Description = switchMode
                ? @"Pick the mods that switch to this profile, then choose how they have to match under ""Customise"". No mods: no mod condition."
                : @"Pick the mods, then choose what the profile does with them under ""Customise"".";

            Ruleset.Value = gameRuleset.Value;
            Beatmap.Value = gameBeatmap.Value;

            var ruleset = gameRuleset.Value?.CreateInstance();
            var initialMods = switchInitial?.Mods ?? initial?.Mods;

            if (initialMods != null && ruleset != null)
                SelectedMods.Value = initialMods.Select(m => m.ToMod(ruleset)).Where(m => m is not UnknownMod).ToArray();

            // Auto-switch: one way of matching at a time (unticking all of them means "all of these mods").
            void exclusive(BindableBool changed, params BindableBool[] others) => changed.BindValueChanged(v =>
            {
                if (v.NewValue)
                    foreach (var o in others) o.Value = false;
                updateSummary();
            });

            exclusive(switchAll, switchAny, switchExact);
            exclusive(switchAny, switchAll, switchExact);
            exclusive(switchExact, switchAll, switchAny);
            switchExactSettings.BindValueChanged(_ => updateSummary());

            // Only one way of limiting mods at a time; blocking and selecting the same mods don't go together.
            blockSelected.BindValueChanged(v =>
            {
                if (!v.NewValue) return;

                onlyAllowSelected.Value = onlyAllowSelectedSettings.Value = applyOnSwitch.Value = false;
            });
            onlyAllowSelected.BindValueChanged(v =>
            {
                if (!v.NewValue) return;

                blockSelected.Value = onlyAllowSelectedSettings.Value = false;
            });
            onlyAllowSelectedSettings.BindValueChanged(v =>
            {
                if (!v.NewValue) return;

                blockSelected.Value = onlyAllowSelected.Value = false;
            });
            applyOnSwitch.BindValueChanged(v =>
            {
                if (v.NewValue) blockSelected.Value = false;
            });

            blockSelected.BindValueChanged(_ => updateSummary());
            onlyAllowSelected.BindValueChanged(_ => updateSummary());
            onlyAllowSelectedSettings.BindValueChanged(_ => updateSummary());
            applyOnSwitch.BindValueChanged(_ => updateSummary());
            SelectedMods.BindValueChanged(_ => updateSummary(), true);
            // Over song select the picked mods are applied to the game. Song select's own mod select copies them into its own mod instances,
            // so changed settings (e.g. Double Time's speed, Adjust pitch, Difficulty Adjust values) are sent again on every change:
            // the music speed / pitch and star rating then follow exactly like with song select's mod select.
            SelectedMods.BindValueChanged(v =>
            {
                previewSettingsTracker?.Dispose();
                previewSettingsTracker = new ModSettingChangeTracker(SelectedMods.Value);
                previewSettingsTracker.SettingChanged += _ => Scheduler.AddOnce(pushPreview);

                pushPreview();
            }, true);
        }

        private Drawable createRulesSection(OverlayColourProvider colours) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            Padding = new MarginPadding { Horizontal = 20 },
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = @"Profile rules",
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                },
                summary = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 13),
                    Colour = colours.Content2,
                },
                rule(@"Block these mods", blockSelected),
                rule(@"Only allow these mods", onlyAllowSelected),
                rule(@"Only allow these mods, with these exact settings", onlyAllowSelectedSettings),
                new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = colours.Content2,
                    Text = @"e.g. Difficulty Adjust set to CS 9 can then only be used at CS 9.",
                },
                rule(@"Select these mods whenever switching to this profile", applyOnSwitch),
            },
        };

        private Drawable createSwitchSection(OverlayColourProvider colours) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            Padding = new MarginPadding { Horizontal = 20 },
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = @"Auto-switch rule",
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                },
                summary = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 13),
                    Colour = colours.Content2,
                },
                rule(@"Switch when all of these mods are selected (other mods allowed)", switchAll),
                rule(@"Switch when any of these mods is selected", switchAny),
                rule(@"Switch only with exactly this mod combination", switchExact),
                rule(@"Only switch if the settings are exact", switchExactSettings),
                new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = colours.Content2,
                    Text = @"e.g. Double Time set to 1.3x then only switches at 1.3x. The rule's other conditions (tags, star rating...) still have to match too.",
                },
            },
        };

        private LocalAutoSwitchRules currentSwitchRules()
        {
            var rules = switchInitial!.Clone();

            rules.Mods = SelectedMods.Value.Where(m => m.Type != ModType.System && m is not ModAutoplay).Select(m => new APIMod(m)).ToList();
            rules.ModMatch = switchAny.Value ? LocalAutoSwitchModMatch.Any
                : switchExact.Value ? LocalAutoSwitchModMatch.Exact
                : LocalAutoSwitchModMatch.All;
            rules.ExactModSettings = switchExactSettings.Value;

            return rules;
        }

        private static Drawable rule(string text, BindableBool current) => new OsuCheckbox
        {
            RelativeSizeAxes = Axes.X,
            LabelText = text,
            Current = current,
        };

        private LocalModRules currentRules() => new LocalModRules
        {
            Mods = SelectedMods.Value.Where(m => m.Type != ModType.System).Select(m => new APIMod(m)).ToList(),
            BlockSelected = blockSelected.Value,
            OnlyAllowSelected = onlyAllowSelected.Value,
            OnlyAllowSelectedSettings = onlyAllowSelectedSettings.Value,
            ApplyOnSwitch = applyOnSwitch.Value,
        };

        private void updateSummary()
        {
            if (switchMode)
            {
                var switchRules = currentSwitchRules();
                summary.Text = switchRules.Mods.Count == 0 ? @"No mods picked: the rule won't check mods." : $@"Switches on {switchRules.DescribeMods()}";
                return;
            }

            var rules = currentRules();

            summary.Text = rules.Mods.Count == 0
                ? @"No mods picked yet."
                : !rules.HasAnyRule
                    ? @"No rule ticked yet."
                    : rules.Describe();
        }

        private void confirm()
        {
            if (confirmed)
                return;

            confirmed = true;

            if (switchMode)
                SwitchConfirmed?.Invoke(currentSwitchRules());
            else
                Confirmed?.Invoke(currentRules());

            Hide();
        }

        protected override void PopIn()
        {
            // Decided before the footer (created by the base call) needs it.
            overSongSelect = game?.ScreenStack?.CurrentScreen is SoloSongSelect;

            base.PopIn();
            wasShown = true;

            if (overSongSelect && preview == null)
            {
                preview = manager.BeginModPreview();
                preview?.Set(SelectedMods.Value);
            }

            confirmButton.FadeIn(400, Easing.OutQuint).MoveToY(0, 400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            confirmButton.FadeOut(250, Easing.OutQuint).MoveToY(-slide_distance, 250, Easing.OutQuint);

            // Song select gets its own mods back.
            preview?.Dispose();
            preview = null;

            // Remove the picker once it has faded out after being used; it is created anew each time.
            if (wasShown)
            {
                Scheduler.AddDelayed(() => registration?.Dispose(), 600);

                var closed = Closed;
                Closed = null;
                closed?.Invoke();
            }
        }

        /// <summary>
        /// Mod columns like song select's, but conflicting mods are greyed out and can't be clicked,
        /// and the active profile's own rules don't apply while choosing rules.
        /// </summary>
        private partial class RulesModColumn : ModColumn
        {
            public RulesModColumn(ModType modType)
                : base(modType, false)
            {
            }

            protected override ModPanel CreateModPanel(ModState modState) => new RulesModPanel(modState);
        }

        /// <summary>
        /// The footer's ranking / star rating / beatmap attributes: shown over song select only.
        /// </summary>
        private partial class RulesFooterContent : ModSelectFooterContent
        {
            private readonly bool showModEffects;

            public RulesFooterContent(ModSelectOverlay overlay, bool showModEffects)
                : base(overlay)
            {
                this.showModEffects = showModEffects;
            }

            protected override bool ShowModEffects => showModEffects;
        }

        private partial class RulesModPanel : IncompatibilityDisplayingModPanel
        {
            public RulesModPanel(ModState modState)
                : base(modState)
            {
                BlockConflictingSelection = true;
                IgnoreLocalProfileRules = true;
            }
        }
    }
}
