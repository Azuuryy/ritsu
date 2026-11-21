// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Commands;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    public partial class TestSceneRitsuSimulation : ScreenTestScene
    {
        [Test]
        public void TestEditablePerfectScoreFollowsSelectedRuleset()
        {
            RitsuSimulationScreen screen = null!;
            string perfectAccuracy = 1d.ToString("P2");

            AddStep("open simulation in osu!", () =>
            {
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                LoadScreen(screen = new RitsuSimulationScreen(CreateWorkingBeatmap(Ruleset.Value)));
            });
            AddUntilStep("start with a perfect score", () => field("Target accuracy")?.PlaceholderText.ToString() == perfectAccuracy && field("100") != null);

            AddUntilStep("targets share a row above the judgements", () =>
                Math.Abs(field("Target accuracy")!.ScreenSpaceDrawQuad.Centre.Y - field("Maximum combo")!.ScreenSpaceDrawQuad.Centre.Y) < 1
                && Math.Abs(field("100")!.ScreenSpaceDrawQuad.Centre.Y - field("Miss")!.ScreenSpaceDrawQuad.Centre.Y) < 1
                && field("Target accuracy")!.ScreenSpaceDrawQuad.Centre.Y < field("Miss")!.ScreenSpaceDrawQuad.Centre.Y);

            AddStep("enter a miss", () => field("Miss")!.Text = "1");
            AddUntilStep("accuracy updates from the edited judgement", () =>
                field("Target accuracy") is { } accuracy && accuracy.PlaceholderText.ToString() != perfectAccuracy);

            AddStep("reset to perfect with F2", () => InputManager.Key(osuTK.Input.Key.F2));
            AddUntilStep("restore the perfect score", () => field("Target accuracy")?.PlaceholderText.ToString() == perfectAccuracy);
            AddStep("open mods with F1", () => InputManager.Key(osuTK.Input.Key.F1));
            AddUntilStep("mod picker shown", () => screen.ChildrenOfType<osu.Game.Overlays.Mods.UserModSelectOverlay>().Single().State.Value == osu.Framework.Graphics.Containers.Visibility.Visible);
            AddStep("close mods with F1", () => InputManager.Key(osuTK.Input.Key.F1));
            AddUntilStep("mod picker hidden", () => screen.ChildrenOfType<osu.Game.Overlays.Mods.UserModSelectOverlay>().Single().State.Value == osu.Framework.Graphics.Containers.Visibility.Hidden);

            AddStep("change the selected ruleset to taiko", () => Ruleset.Value = new TaikoRuleset().RulesetInfo);
            AddUntilStep("judgements follow the selected ruleset", () => field("Ok") != null && field("100") == null);

            OsuTextBox? field(string name) => screen.ChildrenOfType<OsuTextBox>().SingleOrDefault(input => input.Name == name);
        }
    }
}
