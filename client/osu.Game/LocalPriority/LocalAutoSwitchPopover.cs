// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// "Auto-switch rules" of a profile (right-click its chip on the profile page): which beatmaps make it active by itself.
    /// </summary>
    public partial class LocalAutoSwitchPopover : OsuPopover
    {
        private readonly LocalProfile profile;
        private readonly LocalProfileManager manager;

        private OsuCheckbox enabled = null!;
        private OsuTextBox tags = null!;
        private OsuTextBox stars = null!;
        private OsuTextBox bpm = null!;
        private OsuTextBox length = null!;
        private OsuTextBox mapper = null!;
        private OsuTextBox name = null!;
        private OsuSpriteText error = null!;
        private RoundedButton modsButton = null!;

        /// <summary>
        /// The mods condition as last picked (kept until "Save").
        /// </summary>
        private LocalAutoSwitchRules modsCondition = null!;

        [Resolved]
        private IOverlayManager? overlays { get; set; }

        [Resolved]
        private LocalProfileOverlay? profileOverlay { get; set; }

        public LocalAutoSwitchPopover(LocalProfile profile, LocalProfileManager manager)
        {
            this.profile = profile;
            this.manager = manager;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            var rules = profile.AutoSwitch ?? new LocalAutoSwitchRules();
            modsCondition = rules.Clone();

            Child = new FillFlowContainer
            {
                Width = 360,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = $@"Auto-switch to ""{profile.Name}""",
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                    },
                    new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Alpha = 0.75f,
                        Text = @"Selecting a beatmap (and mods) in song select that matches every filled-in condition makes this profile active. "
                               + @"On beatmaps no profile wants, the profile from before comes back. "
                               + @"Separate several values with commas, any of them counts (""180, 190"" = 180 or 190 BPM). "
                               + @"Numbers: ""6"", ""=6"", "">5"", "">=5"", ""<7"", ""<=7"", ""5-7"", or both sides like "">5 <7"". Decimals use a dot.",
                    },
                    enabled = new OsuCheckbox
                    {
                        RelativeSizeAxes = Axes.X,
                        LabelText = @"Switch to this profile automatically",
                        Current = { Value = profile.AutoSwitch?.Enabled ?? true },
                    },
                    field(@"Tags (any of them; local, voted or mapper tags)", tags = box(string.Join(@", ", rules.Tags), @"skillset/jumps, jumps/cross-screen")),
                    field(@"Star rating", stars = box(rules.Stars, @"e.g. 5-7 or >6.5")),
                    field(@"BPM", bpm = box(rules.Bpm, @"e.g. 180, 190 or >=200")),
                    field(@"Length in seconds", length = box(rules.Length, @"e.g. <90 or 60-240")),
                    field(@"Mapper (part of the name; any of them)", mapper = box(rules.Mapper, @"e.g. Sotarks, Monstrata")),
                    field(@"Title, artist or difficulty (part of it; any of them)", name = box(rules.Name, string.Empty)),
                    field(@"Selected mods", modsButton = new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Action = pickMods,
                    }),
                    error = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        Colour = colours.Red1,
                        Alpha = 0,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            new RoundedButton
                            {
                                Width = 170,
                                Text = @"Save",
                                Action = save,
                            },
                            new RoundedButton
                            {
                                Width = 170,
                                Text = @"Remove rules",
                                BackgroundColour = colours.Red3,
                                Action = () =>
                                {
                                    manager.SetAutoSwitch(profile.Id, null);
                                    this.HidePopover();
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateModsButton();
        }

        private void updateModsButton() => modsButton.Text = modsCondition.Mods.Count == 0 ? @"Any mods (click to pick mods)" : modsCondition.DescribeMods();

        /// <summary>
        /// Opens the mod rules picker for this rule's mods. The profile page makes room for it, so what is filled in here is saved first
        /// (when it is valid), and the page comes back with this editor open afterwards.
        /// </summary>
        private void pickMods()
        {
            if (overlays == null)
                return;

            var current = tryBuildRules(out string? problem);

            if (current == null)
            {
                showError(problem!);
                return;
            }

            if (current.HasAnyCondition)
                manager.SetAutoSwitch(profile.Id, current);

            Guid id = profile.Id;

            LocalProfileModRulesOverlay.PickAutoSwitchMods(overlays, current, $@"auto-switch mods for {profile.Name}",
                picked => manager.SetAutoSwitch(id, picked.HasAnyCondition ? picked : null),
                () => profileOverlay?.ShowAutoSwitchRules(id),
                () =>
                {
                    this.HidePopover();
                    profileOverlay?.Hide();
                });
        }

        private static OsuTextBox box(string text, string placeholder) => new OsuTextBox
        {
            RelativeSizeAxes = Axes.X,
            Text = text,
            PlaceholderText = placeholder,
            CommitOnFocusLost = true,
        };

        private static Drawable field(string label, Drawable control) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 2),
            Children = new[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                },
                control,
            },
        };

        /// <summary>
        /// The rule as filled in, or null (with the reason) when a number field can't be read.
        /// </summary>
        private LocalAutoSwitchRules? tryBuildRules(out string? problem)
        {
            problem = null;

            var rules = new LocalAutoSwitchRules
            {
                Enabled = enabled.Current.Value,
                Tags = tags.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(LocalBeatmapTags.Normalise).Where(t => t.Length > 0).Distinct().ToList(),
                Mapper = string.Join(@", ", LocalAutoSwitchCondition.Split(mapper.Text)),
                Name = string.Join(@", ", LocalAutoSwitchCondition.Split(name.Text)),
                Mods = modsCondition.Clone().Mods,
                ModMatch = modsCondition.ModMatch,
                ExactModSettings = modsCondition.ExactModSettings,
            };

            if ((problem = number(stars, @"Star rating", @"5-7, >6.5 or 4, 6", out string starsCondition)) != null)
                return null;

            if ((problem = number(bpm, @"BPM", @"180, 190, >=200 or 180-220", out string bpmCondition)) != null)
                return null;

            if ((problem = number(length, @"Length", @"<90, 60-240 or >=120 (seconds)", out string lengthCondition)) != null)
                return null;

            rules.Stars = starsCondition;
            rules.Bpm = bpmCondition;
            rules.Length = lengthCondition;

            return rules;
        }

        /// <summary>
        /// Reads a number field into <paramref name="condition"/>; returns what's wrong with it, or null when it's fine.
        /// </summary>
        private static string? number(OsuTextBox box, string label, string examples, out string condition) =>
            LocalAutoSwitchCondition.TryNormaliseNumber(box.Text, out condition, out string? bad)
                ? null
                : $@"{label}: can't read ""{bad}"". Use e.g. {examples}.";

        private void save()
        {
            var rules = tryBuildRules(out string? problem);

            if (rules == null)
            {
                showError(problem!);
                return;
            }

            if (!rules.HasAnyCondition)
            {
                showError(@"Fill in at least one condition (or use ""Remove rules"").");
                return;
            }

            manager.SetAutoSwitch(profile.Id, rules);
            this.HidePopover();
        }

        private void showError(string text)
        {
            error.Text = text;
            error.FadeIn(100);
        }
    }
}
