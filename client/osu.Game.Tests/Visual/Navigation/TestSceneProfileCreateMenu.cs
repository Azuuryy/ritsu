// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.LocalPriority;
using osu.Game.Online.API;
using osu.Game.Overlays.Toolbar;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneProfileCreateMenu : OsuGameTestScene
    {
        private LocalProfileManager manager => Game.Dependencies.Get<LocalProfileManager>();
        private LocalProfileOverlay overlay => Game.ChildrenOfType<LocalProfileOverlay>().Single();

        private SpriteIcon? moreButton() => overlay.ChildrenOfType<SpriteIcon>().FirstOrDefault(i => i.Icon.Equals(FontAwesome.Solid.EllipsisV));

        private OsuSpriteText? row(string text) => Game.ChildrenOfType<OsuSpriteText>().FirstOrDefault(t => t.Text.ToString() == text && t.IsPresent);

        [Test]
        public void TestMoreMenuCreatesDeathProfile()
        {
            AddStep("create profile", () =>
            {
                var profile = manager.CreateDefaultProfile();
                manager.Switch(profile.Id);
                ((DummyAPIAccess)API).LocalUser.Value = profile.ToUser();
            });
            AddUntilStep("page ready", () => Game.ChildrenOfType<LocalProfileOverlay>().SingleOrDefault()?.IsLoaded == true);

            AddAssert("local profile active", () => manager.IsLocalProfileActive);
            AddStep("show toolbar", () => Game.Toolbar.Show());
            AddUntilStep("toolbar title", () => Game.ChildrenOfType<ToolbarUserButton>().Single().TooltipMain.ToString(), () => Is.EqualTo("Local Profile"));
            AddUntilStep("toolbar subtext", () => Game.ChildrenOfType<ToolbarUserButton>().Single().TooltipSub.ToString(), () => Is.EqualTo("play locally, free of charge!"));

            AddStep("open page", () => overlay.Show());
            AddUntilStep("⋮ shown", () => moreButton()?.IsPresent == true);
            AddWaitStep("let the page slide in", 10);

            AddStep("left-click ⋮", () =>
            {
                InputManager.MoveMouseTo(moreButton()!);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("SD row shown", () => row("New 'Sudden Death' profile") != null);
            AddAssert("PF row shown", () => row("New 'Perfect Death' profile") != null);

            AddStep("left-click ⋮ again", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("menu closed", () => row("New 'Perfect Death' profile") == null);
            AddWaitStep("wait", 5);
            AddAssert("stays closed", () => row("New 'Perfect Death' profile") == null);

            AddStep("left-click ⋮ once more", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("menu open again", () => row("New 'Perfect Death' profile") != null);

            AddStep("click PF", () =>
            {
                InputManager.MoveMouseTo(row("New 'Perfect Death' profile")!);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("PF profile active", () => manager.ActiveProfile.Value?.DeathMode, () => Is.EqualTo(LocalDeathMode.PerfectDeath));
        }
    }
}
