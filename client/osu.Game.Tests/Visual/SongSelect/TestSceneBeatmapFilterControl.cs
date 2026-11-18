// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Select.Filter;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapFilterControl : SongSelectComponentsTestScene
    {
        private FilterControl filterControl = null!;

        protected override Anchor ComponentAnchor => Anchor.TopRight;
        protected override float InitialRelativeWidth => 0.7f;

        [SetUp]
        public void SetUp() => Schedule(createFilterControl);

        [Test]
        public void TestSearch()
        {
            AddStep("search for text", () => filterControl.Search("test search"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestVisibleSearchHistory(bool legacy)
        {
            string favourite = $"bpm>=180 {Guid.NewGuid():N}";
            string recent = $"stars>=5 {Guid.NewGuid():N}";

            LegacySearchBox? legacySearch = null;

            SearchHistoryControls controls() => legacy
                ? legacySearch!.ChildrenOfType<SearchHistoryControls>().Single()
                : filterControl.ChildrenOfType<SearchHistoryControls>().Single();

            Drawable searchBar() => legacy ? legacySearch! : filterControl.ChildrenOfType<FilterControl.SongSelectSearchTextBox>().Single();
            PopoverContainer popoverContainer() => searchBar().FindClosestParent<PopoverContainer>()!;
            Popover? popover() => popoverContainer().CurrentPopover;
            IconButton star() => controls().ChildrenOfType<IconButton>().Single();
            FilterControl.SongSelectSearchTextBox searchHistory() => filterControl.ChildrenOfType<FilterControl.SongSelectSearchTextBox>().Single();

            void openSearches()
            {
                InputManager.MoveMouseTo(searchBar().ChildrenOfType<TextBox>().Single());
                InputManager.Click(MouseButton.Left);
            }

            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            if (legacy)
            {
                AddStep("show classic search field", () =>
                {
                    filterControl.Hide();
                    ((Container)Child).Add(legacySearch = new LegacySearchBox
                    {
                        Current = { BindTarget = filterControl.SearchQuery },
                        FilterControl = filterControl,
                    });
                });
                AddUntilStep("wait for classic search field", () => legacySearch!.IsLoaded);
            }

            AddStep("search for favourite query", () => filterControl.Search(favourite));
            AddStep("click favourite star", () =>
            {
                InputManager.MoveMouseTo(star());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("search is favourited", () => star().TooltipText.ToString(), () => Is.EqualTo("Remove search from favourites"));

            AddStep("search for another query", () => filterControl.Search(recent));
            AddUntilStep("query saved to history", () => searchHistory().Recent.Contains(recent));
            AddStep("click the search field", openSearches);
            AddUntilStep("favourite and recent search are visible", () =>
                popover()?.ChildrenOfType<OsuButton>().Select(button => button.Text.ToString()).ToArray() is { } queries
                && queries.Contains(favourite) && queries.Contains(recent));
            AddAssert("search history is anchored to the search bar", () => ReferenceEquals(popoverContainer().CurrentTarget, searchBar()));
            AddAssert("search history matches the bar width", () => popover()!.ChildrenOfType<OsuScrollContainer>().Single().DrawWidth, () => Is.EqualTo(searchBar().DrawWidth).Within(1));
            AddUntilStep("search history lines up with the lower edge of the bar", () =>
            {
                var panel = popover()!.ChildrenOfType<OsuScrollContainer>().Single().ScreenSpaceDrawQuad;
                var bar = searchBar().ScreenSpaceDrawQuad;
                float panelTopCentre = (panel.TopLeft.X + panel.TopRight.X) / 2;
                float barBottomCentre = (bar.BottomLeft.X + bar.BottomRight.X) / 2;
                return Math.Abs(panelTopCentre - barBottomCentre) <= 1;
            });
            AddStep("select favourite", () =>
            {
                InputManager.MoveMouseTo(popover()!.ChildrenOfType<OsuButton>().Single(button => button.Text.ToString() == favourite));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("favourite query restored", () => filterControl.SearchQuery.Value, () => Is.EqualTo(favourite));
            AddAssert("searches closed", () => popoverContainer().CurrentTarget == null);
            AddStep("reopen searches", openSearches);
            AddStep("clear recent searches from the header", () =>
            {
                var clear = popover()!.ChildrenOfType<IconButton>().Single(button => button.TooltipText.ToString() == "Clear history");
                InputManager.MoveMouseTo(clear);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("recent searches cleared", () => searchHistory().Recent.Count == 0);
            AddAssert("favourites kept", () => searchHistory().Favourites.Contains(favourite));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestCompleteFilterWithKeyboard(bool legacy, bool altFirst)
        {
            LegacySearchBox? legacySearch = null;
            Drawable searchBar() => legacy ? legacySearch! : filterControl.ChildrenOfType<FilterControl.SongSelectSearchTextBox>().Single();
            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            if (legacy)
            {
                AddStep("use classic search", () =>
                {
                    filterControl.Hide();
                    ((Container)Child).Add(legacySearch = new LegacySearchBox
                    {
                        Current = { BindTarget = filterControl.SearchQuery },
                        FilterControl = filterControl,
                    });
                });
                AddUntilStep("wait for classic search", () => legacySearch!.IsLoaded);
            }

            AddStep("start filter name", () => filterControl.Search("len"));
            AddStep("request completion", () =>
            {
                InputManager.MoveMouseTo(searchBar().ChildrenOfType<TextBox>().Single());
                InputManager.Click(MouseButton.Left);
                InputManager.Key(Key.End);
                InputManager.PressKey(altFirst ? Key.AltLeft : Key.ControlLeft);
                InputManager.PressKey(altFirst ? Key.ControlLeft : Key.AltLeft);
                InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("suggestion shown", () => searchBar().FindClosestParent<PopoverContainer>()?.CurrentTarget is SearchHistoryControls);
            AddStep("accept with Tab", () => InputManager.Key(Key.Tab));
            AddAssert("filter inserted", () => filterControl.SearchQuery.Value, () => Is.EqualTo("length="));
        }

        [Test]
        public void TestRestoreSearchInOneFilterUpdate()
        {
            int updates = 0;
            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            AddStep("restore a saved search", () =>
            {
                var state = filterControl.CaptureSearch() with
                {
                    Query = "length<2m",
                    MinimumStars = 3,
                    MaximumStars = 7,
                    MinimumPP = 80,
                    MaximumPP = 250,
                    Sort = SortMode.Length,
                    Group = GroupMode.Difficulty,
                    ReverseSort = true,
                    ReverseGroup = true,
                };
                filterControl.CriteriaChanged += _ => updates++;
                filterControl.RestoreSearch(state);
                Assert.That(filterControl.CaptureSearch(), Is.EqualTo(state));
            });
            AddAssert("one criteria update", () => updates, () => Is.EqualTo(1));
        }

        [Test]
        public void TestSettingChangeTrackedAfterModReplacedByEqualInstance()
        {
            var first = new OsuModDoubleTime();
            var second = new OsuModDoubleTime();
            int filterCount = 0;

            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            AddStep("filter by star rating", () => filterControl.Search("stars>1"));
            AddStep("select double time", () => SelectedMods.Value = new Mod[] { first });
            AddStep("hook filter event", () => filterControl.CriteriaChanged += _ => filterCount++);

            // Equal by value, so this shouldn't re-filter, but settings of the new instance should be tracked from here on.
            AddStep("replace with equal instance", () => SelectedMods.Value = new Mod[] { second });
            AddAssert("did not filter", () => filterCount, () => Is.Zero);

            AddStep("change rate", () => second.SpeedChange.Value = 1.8);
            AddUntilStep("filtered once", () => filterCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestSettingChangeTrackedForModSelectedBeforeLoad()
        {
            var doubleTime = new OsuModDoubleTime();
            int filterCount = 0;

            AddStep("select double time", () => SelectedMods.Value = new Mod[] { doubleTime });
            AddStep("recreate filter control", createFilterControl);
            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            AddStep("filter by star rating", () => filterControl.Search("stars>1"));
            AddStep("hook filter event", () => filterControl.CriteriaChanged += _ => filterCount++);

            AddStep("change rate", () => doubleTime.SpeedChange.Value = 1.8);
            AddUntilStep("filtered once", () => filterCount, () => Is.EqualTo(1));
        }

        [TestCaseSource(typeof(TestSceneBeatmapDifficultyCache), nameof(TestSceneBeatmapDifficultyCache.DIFFICULTY_MODS_WITHOUT_DIFFICULTY_INTERFACES))]
        public void TestDifficultyModToggleRefilters(Mod sourceMod)
        {
            var mod = sourceMod.DeepClone();
            int filterCount = 0;

            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            AddStep("filter by star rating", () => filterControl.Search("stars>1"));
            AddStep("hook filter event", () => filterControl.CriteriaChanged += _ => filterCount++);

            AddStep($"select {mod.Acronym}", () => SelectedMods.Value = new[] { mod });
            AddUntilStep("filtered once", () => filterCount, () => Is.EqualTo(1));

            AddStep($"deselect {mod.Acronym}", () => SelectedMods.Value = Array.Empty<Mod>());
            AddUntilStep("filtered twice", () => filterCount, () => Is.EqualTo(2));
        }

        [Test]
        public void TestMagnetisedStrengthChangeRefilters()
        {
            var magnetised = new OsuModMagnetised();
            int filterCount = 0;

            AddUntilStep("wait for load", () => filterControl.IsLoaded);
            AddStep("filter by star rating", () => filterControl.Search("stars>1"));
            AddStep("select magnetised", () => SelectedMods.Value = new Mod[] { magnetised });
            AddStep("hook filter event", () => filterControl.CriteriaChanged += _ => filterCount++);

            AddStep("change attraction strength", () => magnetised.AttractionStrength.Value = 0.8f);
            AddUntilStep("filtered once", () => filterCount, () => Is.EqualTo(1));
        }

        private void createFilterControl()
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = filterControl = new FilterControl
                {
                    State = { Value = Visibility.Visible },
                    RelativeSizeAxes = Axes.X,
                },
            };
        }
    }
}
