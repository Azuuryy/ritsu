// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.LocalPriority;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneProfileSwitchScroll : OsuGameTestScene
    {
        private LocalProfileManager manager => Game.Dependencies.Get<LocalProfileManager>();
        private LocalProfileOverlay overlay => Game.ChildrenOfType<LocalProfileOverlay>().Single();
        private OverlayScrollContainer scroll => overlay.ChildrenOfType<OverlayScrollContainer>().First();

        [TestCase(true)]
        [TestCase(false)]
        public void TestSwitchKeepsScroll(bool atBottom)
        {
            LocalProfile a = null!, b = null!;
            double before = 0, maxDeviation = 0;

            AddStep("create profiles", () =>
            {
                a = manager.CreateDefaultProfile();
                b = manager.CreateDefaultProfile();
                manager.Switch(a.Id);
            });
            AddUntilStep("page ready", () => Game.ChildrenOfType<LocalProfileOverlay>().SingleOrDefault()?.IsLoaded == true);
            AddStep("open page", () => overlay.Show());
            AddUntilStep("loaded", () => scroll.ScrollableExtent > 50);
            AddWaitStep("settle", 20);

            for (int round = 0; round < 4; round++)
            {
                int r = round;
                AddStep($"scroll ({r})", () =>
                {
                    scroll.ScrollTo(atBottom ? scroll.ScrollableExtent : scroll.ScrollableExtent / 2, false);
                });
                AddWaitStep("settle", 5);
                AddStep($"switch ({r})", () =>
                {
                    before = scroll.Current;
                    maxDeviation = 0;
                    manager.Switch(r % 2 == 0 ? b.Id : a.Id);
                });
                for (int i = 0; i < 60; i++)
                    AddStep("measure", () => maxDeviation = Math.Max(maxDeviation, Math.Abs(scroll.Current - before)));
                AddAssert($"scroll unchanged ({r})", () => maxDeviation, () => Is.LessThan(0.5));
            }
        }
    }
}
