// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics.Sprites;
using osu.Game.Offline;
using osu.Game.Tests.Resources;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneRitsuLeaderboardScope : OsuGameTestScene
    {
        [Test]
        public void TestLocalStatusChangesWithProfile()
        {
            BeatmapInfo selected = null!;
            AddStep("import ranked beatmap", () =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(2);
                set.Status = BeatmapOnlineStatus.Ranked;
                foreach (var map in set.Beatmaps)
                    map.Status = BeatmapOnlineStatus.Ranked;
                selected = Game.BeatmapManager.Import(set)!.Value.Beatmaps.First();
            });
            AddStep("enter song select", () => Game.ChildrenOfType<ButtonSystem>().Single().OnSolo?.Invoke());
            AddUntilStep("song select is current", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented);
            AddStep("select beatmap", () => Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(selected, true));
            string displayedStatus() => Game.ChildrenOfType<BeatmapTitleWedge>().Single().ChildrenOfType<BeatmapSetOnlineStatusPill>()
                .Single().ChildrenOfType<OsuSpriteText>().Single().Text.ToString();
            AddUntilStep("ranked status shown", () => displayedStatus() == "RANKED");
            AddStep("exclude difficulty", () =>
            {
                var profiles = Game.Dependencies.Get<RitsuProfileManager>();
                profiles.SetBeatmapScoresIncludedInPP(profiles.ActiveProfileId, selected.ID, false, selected.BeatmapSet!.ID);
            });
            AddUntilStep("unranked status shown", () => displayedStatus() == "UNRANKED");
            AddStep("switch to another profile", () => Game.Dependencies.Get<RitsuProfileManager>().AddProfile("Other"));
            AddUntilStep("ranked status restored for other profile", () => displayedStatus() == "RANKED");
            AddAssert("shared status unchanged", () => selected.Status == BeatmapOnlineStatus.Ranked);
        }

        [Test]
        public void TestScopeFollowsBothLayouts()
        {
            var config = () => Game.Dependencies.Get<OsuConfigManager>();
            var songSelect = () => (SoloSongSelect)Game.ScreenStack.CurrentScreen;
            AddStep("start with all profiles", () => config().SetValue(OsuSetting.RitsuLeaderboardScope, BeatmapLeaderboardScope.LocalAll));
            AddStep("enter song select", () => Game.ChildrenOfType<ButtonSystem>().Single().OnSolo?.Invoke());
            AddUntilStep("song select is current", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented);
            AddUntilStep("wait for modern leaderboard", () => songSelect().ChildrenOfType<BeatmapDetailsArea.Header>().Any());
            AddAssert("modern restores scope", () => songSelect().ChildrenOfType<BeatmapDetailsArea.Header>().Single().Scope.Value == BeatmapLeaderboardScope.LocalAll);
            AddStep("enable classic layout", () => config().SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            AddUntilStep("classic restores scope", () => songSelect().ChildrenOfType<LegacyLeaderboard>().Any(leaderboard => leaderboard.IsLoaded && leaderboard.Scope.Value == BeatmapLeaderboardScope.LocalAll));
            AddStep("select current profile in classic", () => songSelect().ChildrenOfType<LegacyLeaderboard>().Single().Scope.Value = BeatmapLeaderboardScope.Local);
            AddAssert("choice stored", () => config().Get<BeatmapLeaderboardScope>(OsuSetting.RitsuLeaderboardScope) == BeatmapLeaderboardScope.Local);
            AddStep("enable modern layout", () => config().SetValue(OsuSetting.ToriiLegacyFooterUseSkin, false));
            AddUntilStep("modern follows classic", () => songSelect().ChildrenOfType<BeatmapDetailsArea.Header>().Single().Scope.Value == BeatmapLeaderboardScope.Local);
            AddStep("select all profiles in modern", () =>
                ((Bindable<BeatmapLeaderboardScope>)songSelect().ChildrenOfType<BeatmapDetailsArea.Header>().Single().Scope).Value = BeatmapLeaderboardScope.LocalAll);
            AddAssert("choice stored again", () => config().Get<BeatmapLeaderboardScope>(OsuSetting.RitsuLeaderboardScope) == BeatmapLeaderboardScope.LocalAll);
            AddStep("re-enable classic layout", () => config().SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            AddUntilStep("classic follows modern", () => songSelect().ChildrenOfType<LegacyLeaderboard>().Any(leaderboard => leaderboard.IsLoaded && leaderboard.Scope.Value == BeatmapLeaderboardScope.LocalAll));
            AddStep("restore defaults", () =>
            {
                config().SetValue(OsuSetting.RitsuLeaderboardScope, BeatmapLeaderboardScope.Local);
                config().SetValue(OsuSetting.ToriiLegacyFooterUseSkin, false);
            });
        }
    }
}
