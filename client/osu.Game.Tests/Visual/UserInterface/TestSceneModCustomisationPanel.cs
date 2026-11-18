// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneModCustomisationPanel : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private ModCustomisationPanel panel = null!;
        private ModCustomisationHeader header = null!;
        private Container content = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            SelectedMods.Value = Array.Empty<Mod>();
            InputManager.MoveMouseTo(Vector2.One);

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(20f),
                Child = panel = new ModCustomisationPanel
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Width = 400f,
                    State = { Value = Visibility.Visible },
                    SelectedMods = { BindTarget = SelectedMods },
                }
            };

            header = panel.Children.OfType<ModCustomisationHeader>().First();
            content = panel.Children.OfType<Container>().First();
        });

        [Test]
        public void TestDisplay()
        {
            AddStep("set DT", () =>
            {
                SelectedMods.Value = new[] { new OsuModDoubleTime() };
                panel.Enabled.Value = true;
                panel.ExpandedState.Value = ModCustomisationPanel.ModCustomisationPanelState.ExpandedByMod;
            });
            AddStep("set DA", () =>
            {
                SelectedMods.Value = new Mod[] { new OsuModDifficultyAdjust() };
                panel.Enabled.Value = true;
                panel.ExpandedState.Value = ModCustomisationPanel.ModCustomisationPanelState.ExpandedByMod;
            });
            AddStep("set FL+WU+DA+AD", () =>
            {
                SelectedMods.Value = new Mod[] { new OsuModFlashlight(), new ModWindUp(), new OsuModDifficultyAdjust(), new OsuModApproachDifferent() };
                panel.Enabled.Value = true;
                panel.ExpandedState.Value = ModCustomisationPanel.ModCustomisationPanelState.ExpandedByMod;
            });
            AddStep("set empty", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                panel.Enabled.Value = false;
                panel.ExpandedState.Value = ModCustomisationPanel.ModCustomisationPanelState.Collapsed;
            });
        }

        [Test]
        public void TestRandomV2PatternSelection()
        {
            var mod = new OsuModRandomV2 { Angle = { Value = OsuModRandomV2.AngleEnum.Ninety } };
            OsuModRandomV2.PatternSetting patterns = null!;
            OsuModRandomV2.CategoryPatternSetting streams = null!;
            OsuModRandomV2.AngleEnumSetting angle = null!;
            AddStep("add the trajectory settings", () =>
            {
                var controls = mod.CreateSettingsControls().Where(c => c is OsuModRandomV2.PatternSetting or OsuModRandomV2.AngleEnumSetting).ToArray();
                patterns = controls.OfType<OsuModRandomV2.PatternSetting>().Single(control => control is not OsuModRandomV2.CategoryPatternSetting);
                streams = controls.OfType<OsuModRandomV2.CategoryPatternSetting>().Single(control => control.LabelText.ToString() == "Stream patterns");
                angle = controls.OfType<OsuModRandomV2.AngleEnumSetting>().Single();
                Child = new FillFlowContainer
                {
                    Width = 400,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = controls,
                };
            });
            AddUntilStep("trajectory settings hidden", () => patterns.IsLoaded && angle.IsLoaded && !patterns.IsPresent && !angle.IsPresent);
            AddStep("enable custom angle", () => mod.CustomAngle.Value = true);
            AddUntilStep("patterns and base angle visible", () => patterns.IsPresent && angle.IsPresent);
            AddStep("check octagon and ladder", () =>
            {
                var checkboxes = patterns.ChildrenOfType<OsuCheckbox>().ToArray();
                InputManager.MoveMouseTo(checkboxes[^1]);
                InputManager.Click(MouseButton.Left);
                InputManager.MoveMouseTo(checkboxes[0]);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("both patterns saved", () => mod.PatternMask.Value, () => Is.EqualTo(1 | (1 << 14)));
            AddUntilStep("base angle hidden when patterns are selected", () => !angle.IsPresent);
            AddStep("clear the pattern selection", () => patterns.ApplyDefault());
            AddAssert("all patterns unchecked", () => patterns.ChildrenOfType<OsuCheckbox>().All(checkbox => !checkbox.Current.Value));
            AddUntilStep("base angle visible again", () => angle.IsPresent);
            AddAssert("original angle restored", () => angle.Current.Value, () => Is.EqualTo(OsuModRandomV2.AngleEnum.Ninety));
            AddStep("enable separate selections", () => mod.SeparatePatterns.Value = true);
            AddUntilStep("stream settings replace the global selection", () => streams.IsPresent && !patterns.IsPresent && angle.IsPresent);
            AddStep("set a stream pattern", () => mod.StreamPatterns.Value = 1 << 11);
            AddUntilStep("base angle hides for a category selection", () => !angle.IsPresent);
        }

        [Test]
        public void TestHoverDoesNotExpandWhenNoCustomisableMods()
        {
            AddStep("hover header", () => InputManager.MoveMouseTo(header));

            checkExpanded(false);

            AddStep("hover content", () => InputManager.MoveMouseTo(content));

            checkExpanded(false);

            AddStep("left from content", () => InputManager.MoveMouseTo(Vector2.One));
        }

        [Test]
        public void TestHoverExpandsWithCustomisableMods()
        {
            AddStep("add customisable mod", () =>
            {
                SelectedMods.Value = new[] { new OsuModDoubleTime() };
                panel.Enabled.Value = true;
            });

            AddStep("hover header", () => InputManager.MoveMouseTo(header));
            checkExpanded(true);

            AddStep("move to content", () => InputManager.MoveMouseTo(content));
            checkExpanded(true);

            AddStep("move away", () => InputManager.MoveMouseTo(Vector2.One));
            checkExpanded(false);

            AddStep("hover header", () => InputManager.MoveMouseTo(header));
            checkExpanded(true);

            AddStep("move away", () => InputManager.MoveMouseTo(Vector2.One));
            checkExpanded(false);
        }

        [Test]
        public void TestHoverExpandsAndCollapsesWhenHeaderTouched()
        {
            AddStep("add customisable mod", () =>
            {
                SelectedMods.Value = new[] { new OsuModDoubleTime() };
                panel.Enabled.Value = true;
            });

            AddStep("touch header", () =>
            {
                var touch = new Touch(TouchSource.Touch1, header.ScreenSpaceDrawQuad.Centre);
                InputManager.BeginTouch(touch);
                Schedule(() => InputManager.EndTouch(touch));
            });
            checkExpanded(true);

            AddStep("touch away from header", () =>
            {
                var touch = new Touch(TouchSource.Touch1, header.ScreenSpaceDrawQuad.TopLeft - new Vector2(10));
                InputManager.BeginTouch(touch);
                Schedule(() => InputManager.EndTouch(touch));
            });
            checkExpanded(false);
        }

        [Test]
        public void TestDraggingKeepsPanelExpanded()
        {
            AddStep("add customisable mod", () =>
            {
                SelectedMods.Value = new[] { new OsuModDoubleTime() };
                panel.Enabled.Value = true;
            });

            AddStep("hover header", () => InputManager.MoveMouseTo(header));
            checkExpanded(true);

            AddStep("hover slider bar nub", () => InputManager.MoveMouseTo(panel.ChildrenOfType<OsuSliderBar<double>>().First().ChildrenOfType<Nub>().Single()));
            AddStep("hold", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag outside", () => InputManager.MoveMouseTo(Vector2.Zero));
            checkExpanded(true);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
            checkExpanded(false);
        }

        private void checkExpanded(bool expanded)
        {
            AddUntilStep(expanded ? "is expanded" : "not expanded", () => panel.ExpandedState.Value,
                () => expanded ? Is.Not.EqualTo(ModCustomisationPanel.ModCustomisationPanelState.Collapsed) : Is.EqualTo(ModCustomisationPanel.ModCustomisationPanelState.Collapsed));
        }
    }
}
