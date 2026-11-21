// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Commands;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Offline;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.ToriiBriefing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneSongSelectNavigation : OsuGameTestScene
    {
        [Test]
        public void TestCollectionContentsExpandInlineAndUndoRemoval()
        {
            ManageCollectionsDialog manager = null!;
            Guid collectionId = Guid.Empty;
            const string hash = "missing-from-test-library";

            AddUntilStep("wait for collection manager", () => Game.Dependencies.Get<ManageCollectionsDialog>()?.IsLoaded == true);
            AddStep("create and open a collection", () =>
            {
                manager = Game.Dependencies.Get<ManageCollectionsDialog>();
                collectionId = Game.Realm.Write(realm => realm.Add(new BeatmapCollection("Inline collection", new[] { hash })).ID);
                manager.Show();
            });
            AddUntilStep("show collection chevron", () => chevron() != null);
            AddStep("expand inline", () => chevron()!.TriggerClick());
            AddUntilStep("contents are under the original collection row", () =>
                manager.ExpandedCollection.Value == collectionId && manager.ChildrenOfType<RitsuCollectionEditor>().SingleOrDefault()?.IsLoaded == true);
            AddStep("remove and undo a collection difficulty", () =>
            {
                manager.RemoveMembers(collectionId, null, new[] { hash });
                Assert.That(Game.Realm.Run(realm => realm.Find<BeatmapCollection>(collectionId)!.BeatmapMD5Hashes.Count), Is.Zero);
                manager.UndoRemoval();
            });
            AddUntilStep("collection stays expanded and contains its difficulty", () => manager.ExpandedCollection.Value == collectionId
                && Game.Realm.Run(realm => realm.Find<BeatmapCollection>(collectionId)!.BeatmapMD5Hashes.Contains(hash)));
            AddStep("Escape collapses the row", () => InputManager.Key(Key.Escape));
            AddAssert("manager remains open", () => manager.ExpandedCollection.Value == null &&
                manager.State.Value == osu.Framework.Graphics.Containers.Visibility.Visible);

            IconButton? chevron() => manager.ChildrenOfType<IconButton>().FirstOrDefault(button =>
                button.IsPresent && button.TooltipText.ToString() == "Show collection contents");
        }

        [Test]
        public void TestCollectionsEntryPointsShareTheSelectedBeatmap()
        {
            BeatmapInfo beatmap = null!;
            AddStep("import a beatmap", () =>
            {
                var manager = Game.Dependencies.Get<BeatmapManager>();
                var imported = manager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely();
                beatmap = manager.GetAllUsableBeatmapSets().Single(s => s.ID == imported!.ID).Beatmaps.First();
                Game.PresentBeatmap(beatmap.BeatmapSet!, candidate => candidate.ID == beatmap.ID);
            });
            AddUntilStep("wait for beatmap and footer", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented
                && Game.Beatmap.Value.BeatmapInfo.MD5Hash == beatmap.MD5Hash && Game.ChildrenOfType<FooterButtonCollections>().Any(b => b.IsPresent));

            AddStep("dismiss the welcome overlay", () => Game.ChildrenOfType<ToriiBriefingOverlay>().FirstOrDefault()?.Hide());

            openCollections("Collections footer", () => Game.ChildrenOfType<FooterButtonCollections>().Single(b => b.IsPresent).TriggerClick());
            openCollections("Manage collections dropdown item", () =>
            {
                var dropdown = ((SoloSongSelect)Game.ScreenStack.CurrentScreen).ChildrenOfType<CollectionDropdown>().First(d => d.IsPresent);
                dropdown.Current.Value = dropdown.ItemSource.OfType<ManageCollectionsFilterMenuItem>().Single();
                Assert.That(dropdown.Current.Value, Is.Not.InstanceOf<ManageCollectionsFilterMenuItem>());
            });
            openCollections("beatmap context menu", () => ((SoloSongSelect)Game.ScreenStack.CurrentScreen).GetForwardActions(beatmap)
                .Single(item => item.Text.Value.Equals(CommonStrings.Collections)).Action.Value!.Invoke());

            AddStep("open Options", () => Game.ChildrenOfType<FooterButtonOptions>().Single(b => b.IsPresent).TriggerClick());
            AddUntilStep("wait for the only Collections option", () => collectionOptions().Count() == 1);
            openCollections("Collections option", () => collectionOptions().Single().TriggerClick());

            System.Collections.Generic.IEnumerable<OsuButton> collectionOptions() => Game.ChildrenOfType<FooterButtonOptions.Popover>()
                .Where(p => p.IsPresent).SelectMany(p => p.ChildrenOfType<OsuButton>()).Where(b => b.Text.Equals(CommonStrings.Collections));

            void openCollections(string entryPoint, Action action)
            {
                AddStep("open from " + entryPoint, action);
                AddUntilStep("the same manager targets the beatmap", () => Game.ChildrenOfType<ManageCollectionsDialog>().Single() is { } manager
                    && manager.State.Value == osu.Framework.Graphics.Containers.Visibility.Visible && manager.Target.Value?.DifficultyHash == beatmap.MD5Hash);
                AddStep("close collections", () => InputManager.Key(Key.Escape));
                AddUntilStep("manager closed", () => Game.ChildrenOfType<ManageCollectionsDialog>().Single().State.Value
                    == osu.Framework.Graphics.Containers.Visibility.Hidden);
            }
        }

        [Test]
        public void TestRitsuPaletteQueueAndSessionPreset()
        {
            string mapHash = string.Empty;
            string presetName = $"Practice {Guid.NewGuid():N}";
            var select = () => (SoloSongSelect)Game.ScreenStack.CurrentScreen;
            var dialog = () => Game.Dependencies.Get<IDialogOverlay>().CurrentDialog;
            AddUntilStep("wait for dialog overlay", () => Game.Dependencies.Get<IDialogOverlay>() is { } overlay && ((osu.Framework.Graphics.Drawable)overlay).IsLoaded);
            AddStep("dismiss the welcome overlay", () => Game.ChildrenOfType<ToriiBriefingOverlay>().FirstOrDefault()?.Hide());
            AddStep("queue an imported difficulty", () =>
            {
                var manager = Game.Dependencies.Get<BeatmapManager>();
                var imported = manager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely();
                var beatmap = manager.GetAllUsableBeatmapSets().Single(s => s.ID == imported.ID).Beatmaps[0];
                mapHash = beatmap.MD5Hash;
                Game.QueueRitsuBeatmap(beatmap);
            });

            openPalette();
            runCommand("/queue next");
            AddUntilStep("next difficulty selected in Song Select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect && Game.Beatmap.Value.BeatmapInfo.MD5Hash == mapHash);
            AddUntilStep("wait for queue command to close", () => dialog() == null);
            AddStep("press Tab in Song Select", () => InputManager.Key(Key.Tab));
            AddUntilStep("queue view opens", () => select().ChildrenOfType<RitsuQueuePanel>().Any(panel => panel.State.Value == osu.Framework.Graphics.Containers.Visibility.Visible));
            AddStep("close queue", () => InputManager.Key(Key.Escape));

            openPalette();
            AddStep("select osu!standard for simulation", () => Game.Ruleset.Value = new OsuRuleset().RulesetInfo);
            runCommand("/simulate");
            AddUntilStep("simulator calculated perfect PP", () => Game.ScreenStack.CurrentScreen is RitsuSimulationScreen simulation
                && simulation.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().Any(text => text.Text.ToString().Equals("PERFECT PP", StringComparison.Ordinal)));
            AddStep("F4 again does not stack a second simulation", () => InputManager.Key(osuTK.Input.Key.F4));
            AddStep("close simulation", () => InputManager.Key(osuTK.Input.Key.Escape));
            AddUntilStep("wait for Song Select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddStep("Alt+F4 does not open the simulation", () =>
            {
                InputManager.PressKey(osuTK.Input.Key.AltLeft);
                InputManager.Key(osuTK.Input.Key.F4);
                InputManager.ReleaseKey(osuTK.Input.Key.AltLeft);
            });
            AddAssert("Song Select is still current", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddStep("press F4 alone", () => InputManager.Key(osuTK.Input.Key.F4));
            AddUntilStep("simulation opens from the F4 shortcut", () => Game.ScreenStack.CurrentScreen is RitsuSimulationScreen);
            AddStep("leave simulation", () => InputManager.Key(osuTK.Input.Key.Escape));
            AddUntilStep("back in Song Select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddStep("set initial search", () => select().RitsuFilters.Search("length<2m"));

            openPalette();
            runCommand("/session save");
            AddUntilStep("preset name dialog loaded", () => dialog() is RitsuInputDialog { IsLoaded: true });
            AddStep("name and save session", () =>
            {
                var input = dialog().ChildrenOfType<OsuTextBox>().Single();
                InputManager.MoveMouseTo(input);
                InputManager.Click(MouseButton.Left);
                input.Text = presetName;
                InputManager.Key(Key.Enter);
            });
            AddUntilStep("session saved", () => Game.Dependencies.Get<RitsuLibrary>().Presets.Any(p => p.Name == presetName));
            AddStep("change search", () => select().RitsuFilters.Search("stars>7"));

            openPalette();
            runCommand(presetName);
            AddUntilStep("starting search restored", () => select().RitsuFilters.SearchQuery.Value == "length<2m");

            void openPalette()
            {
                AddUntilStep("wait for any previous dialog to close", () => dialog() == null);
                AddStep("press Ctrl+Shift+P", () =>
                {
                    InputManager.PressKey(Key.ControlLeft);
                    InputManager.PressKey(Key.ShiftLeft);
                    InputManager.Key(Key.P);
                    InputManager.ReleaseKey(Key.ShiftLeft);
                    InputManager.ReleaseKey(Key.ControlLeft);
                });
                AddUntilStep("palette open", () => dialog() is RitsuCommandPalette { IsLoaded: true });
            }

            void runCommand(string query)
            {
                AddStep($"run {query}", () =>
                {
                    var search = dialog().ChildrenOfType<OsuTextBox>().Single();
                    InputManager.MoveMouseTo(search);
                    InputManager.Click(MouseButton.Left);
                    search.Text = query;
                    InputManager.Key(Key.Enter);
                });
            }
        }

        [Test]
        public void TestRitsuTargetBpmUsesSelectedTimingSection()
        {
            var map = new Beatmap();
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            map.ControlPointInfo.Add(1000, new TimingControlPoint { BeatLength = 60000.0 / 180 });
            Mod[]? applied = null;
            RitsuTargetBpmDialog dialog = null!;

            AddUntilStep("wait for dialog overlay", () => Game.Dependencies.Get<IDialogOverlay>() is { } overlay && ((osu.Framework.Graphics.Drawable)overlay).IsLoaded);
            AddStep("open target BPM for a variable-tempo map", () =>
            {
                Game.ChildrenOfType<ToriiBriefingOverlay>().FirstOrDefault()?.Hide();
                Game.Dependencies.Get<IDialogOverlay>().Push(dialog = new RitsuTargetBpmDialog(map, new OsuRuleset(), Array.Empty<Mod>(), mods => applied = mods));
            });
            AddUntilStep("wait for target BPM dialog", () => dialog.IsLoaded);
            AddStep("choose the 180 BPM timing section", () =>
            {
                dialog.ChildrenOfType<Menu.DrawableMenuItem>().Single(item => item.Item.Text.Value.ToString().Contains("180 BPM", StringComparison.Ordinal)).Item.Action.Value?.Invoke();
            });
            AddStep("enter and apply 240 BPM", () =>
            {
                dialog.ChildrenOfType<OsuTextBox>().Single(box => box.PlaceholderText.ToString() == "Target BPM").Text = "240";
                dialog.Buttons.OfType<PopupDialogOkButton>().Single().TriggerClick();
            });
            AddAssert("rate uses selected timing section", () => applied?.OfType<ModRateAdjust>().Single().SpeedChange.Value, () => Is.EqualTo(1.33).Within(0.001));
        }

        [Test]
        public void TestRetryFromResults()
        {
            var getOriginalPlayer = playToResults();

            AddStep("attempt to retry", () => ((ResultsScreen)Game.ScreenStack.CurrentScreen).ChildrenOfType<HotkeyRetryOverlay>().First().Action());
            AddUntilStep("wait for player", () => Game.ScreenStack.CurrentScreen != getOriginalPlayer() && Game.ScreenStack.CurrentScreen is Player);
        }

        [Test]
        public void TestPushSongSelectAndClickBottomLeftCorner()
        {
            AddStep("push song select", () => Game.ScreenStack.Push(new SoloSongSelect()));

            // TODO: without this step, a critical bug will be hit, see inline comment in `OsuGame.handleBackButton`.
            AddUntilStep("Wait for song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.IsLoaded);

            AddStep("click in corner", () =>
            {
                InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.BottomLeft);
                InputManager.Click(MouseButton.Left);
            });

            ConfirmAtMainMenu();
        }

        [Test]
        public void TestPushSongSelectAndPressBackButtonImmediately()
        {
            AddStep("push song select", () => Game.ScreenStack.Push(new SoloSongSelect()));

            // TODO: without this step, a critical bug will be hit, see inline comment in `OsuGame.handleBackButton`.
            AddUntilStep("Wait for song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.IsLoaded);

            AddStep("press back button", () => Game.ChildrenOfType<ScreenBackButton>().First().Action!.Invoke());

            ConfirmAtMainMenu();
        }

        [Test]
        public void TestEditBeatmap()
        {
            PushAndConfirm(() => new SoloSongSelect());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game, virtualTrack: true).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("open menu", () => InputManager.Key(Key.F3));
            AddUntilStep("wait for footer focus", () => InputManager.FocusedDrawable is FooterButtonOptions.Popover);

            AddStep("trigger edit", () =>
            {
                // TODO: should be 5, not 4.
                InputManager.Key(Key.Number4);
            });

            waitForScreen<Editor>();

            pushEscape();
            waitForScreen<SoloSongSelect>();
        }

        [Test]
        public void TestPresentBeatmapFromMainMenuUsesPreviewPoint()
        {
            BeatmapSetInfo beatmapInfo = null!;

            AddStep("import beatmap", () =>
            {
                var task = BeatmapImportHelper.LoadOszIntoOsu(Game, virtualTrack: true);
                task.WaitSafely();
                beatmapInfo = task.GetResultSafely();
            });

            AddStep("present beatmap", () => Game.PresentBeatmap(beatmapInfo));

            AddUntilStep("wait for track playing", () => Game.MusicController.IsPlaying);

            AddAssert("ensure time is reset to preview point",
                () =>
                {
                    double timeFromPreviewPoint = Math.Abs(Game.MusicController.CurrentTrack.CurrentTime - beatmapInfo.Metadata.PreviewTime);
                    return timeFromPreviewPoint < 5000;
                });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestSongContinuesAfterExitPlayer(bool withUserPause)
        {
            Player? player = null;

            IWorkingBeatmap beatmap() => Game.Beatmap.Value;

            PushAndConfirm(() => new SoloSongSelect());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game, virtualTrack: true).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            if (withUserPause)
                AddStep("pause", () => Game.Dependencies.Get<MusicController>().Stop(true));

            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return (player = Game.ScreenStack.CurrentScreen as Player) != null;
            });

            AddUntilStep("wait for fail", () => player?.GameplayState.HasFailed, () => Is.True);

            AddUntilStep("wait for track stop", () => !Game.MusicController.IsPlaying);
            AddAssert("Ensure time before preview point", () => Game.MusicController.CurrentTrack.CurrentTime < beatmap().BeatmapInfo.Metadata.PreviewTime);

            pushEscape();

            AddUntilStep("wait for track playing", () => Game.MusicController.IsPlaying);
            AddAssert("Ensure time wasn't reset to preview point", () => Game.MusicController.CurrentTrack.CurrentTime < beatmap().BeatmapInfo.Metadata.PreviewTime);
        }

        [Test]
        public void TestRandomV2PreviewSeedReproducesPlayableBeatmap()
        {
            int? seed = null;
            BeatmapInfo difficulty = null!;
            osuTK.Vector2[] positions = Array.Empty<osuTK.Vector2>();
            PushAndConfirm(() => new PreviewSongSelect());
            AddStep("import and select a beatmap", () =>
            {
                Game.ChildrenOfType<ToriiBriefingOverlay>().FirstOrDefault()?.Hide();
                var manager = Game.Dependencies.Get<BeatmapManager>();
                var imported = manager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely();
                difficulty = manager.GetAllUsableBeatmapSets().Single(set => set.ID == imported.ID).Beatmaps.First(map => map.Ruleset.OnlineID == 0);
                Game.Beatmap.Value = manager.GetWorkingBeatmap(difficulty);
            });
            AddUntilStep("wait for selection", () => !Game.Beatmap.IsDefault);
            AddStep("open seeded mod preview", () =>
            {
                Game.SelectedMods.Value = new[] { new OsuModRandomV2() };
                ((SoloSongSelect)Game.ScreenStack.CurrentScreen).StartModPreview(new OsuModAutoplay());
            });
            waitForScreen<ReplayPlayer>();
            AddUntilStep("wait for seed control", () => this.ChildrenOfType<osu.Game.Rulesets.Osu.UI.RandomV2Settings>().Any());
            AddStep("keep the preview seed", () =>
            {
                var preview = (ReplayPlayer)Game.ScreenStack.CurrentScreen;
                difficulty = preview.Beatmap.Value.BeatmapInfo;
                seed = preview.GameplayState.Mods.OfType<OsuModRandomV2>().Single().Seed.Value;
                positions = preview.GameplayState.Beatmap.HitObjects.OfType<osu.Game.Rulesets.Osu.Objects.OsuHitObject>().Select(o => o.Position).ToArray();
                this.ChildrenOfType<osu.Game.Rulesets.Osu.UI.RandomV2Settings>().Single()
                    .ChildrenOfType<osu.Game.Overlays.Settings.SettingsButton>().Single().Action?.Invoke();
            });
            pushEscape();
            waitForScreen<SoloSongSelect>();
            AddAssert("seed retained on selection", () => Game.SelectedMods.Value.OfType<OsuModRandomV2>().Single().Seed.Value, () => Is.EqualTo(seed).And.Not.Null);
            AddStep("play the previewed difficulty", () => ((PreviewSongSelect)Game.ScreenStack.CurrentScreen).Play(difficulty));
            waitForScreen<SoloPlayer>();
            AddAssert("play uses preview positions", () => ((SoloPlayer)Game.ScreenStack.CurrentScreen).GameplayState.Beatmap.HitObjects
                .OfType<osu.Game.Rulesets.Osu.Objects.OsuHitObject>().Select(o => o.Position).ToArray(), () => Is.EqualTo(positions));
        }

        private partial class PreviewSongSelect : SoloSongSelect
        {
            public void Play(BeatmapInfo difficulty) => SelectAndRun(difficulty, OnStart);
        }

        [Test]
        public void TestAutoplayShortcutReturnsInitialModsOnExit()
        {
            PushAndConfirm(() => new SoloSongSelect());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game, virtualTrack: true).WaitSafely());

            AddUntilStep("wait for selection", () => !Game.Beatmap.IsDefault);

            AddStep("open mod select", () => InputManager.Key(Key.F1));
            AddStep("search magnetised", () => this.ChildrenOfType<ModSelectOverlay>().Single().SearchTerm = "MG");
            AddStep("select", () => InputManager.Key(Key.Enter));

            AddAssert("magnetised selected", () => Game.SelectedMods.Value.Single(), Is.TypeOf<OsuModMagnetised>);
            AddStep("configure mod", () => ((OsuModMagnetised)Game.SelectedMods.Value.Single()).AttractionStrength.Value = 1.0f);

            pushEscape();
            pushEscape();

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });

            AddAssert("only autoplay selected", () => Game.SelectedMods.Value.Single(), Is.TypeOf<OsuModAutoplay>);

            pushEscape();
            waitForScreen<SoloSongSelect>();

            AddAssert("magnetised selected", () => Game.SelectedMods.Value.Single(), Is.TypeOf<OsuModMagnetised>);
            AddAssert("mod configured", () => ((OsuModMagnetised)Game.SelectedMods.Value.Single()).AttractionStrength.Value, () => Is.EqualTo(1.0f));
        }

        [Test]
        public void TestLeaderboardCorrectInPlayer()
        {
            IWorkingBeatmap beatmap() => Game.Beatmap.Value;

            PushAndConfirm(() => new SoloSongSelect());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game, virtualTrack: true).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);
            AddStep("switch to next difficulty and immediately press enter", () =>
            {
                InputManager.Key(Key.Down);
                Schedule(() => InputManager.Key(Key.Enter));
            });

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });
            AddAssert("leaderboard matches gameplay beatmap", () => Game.ChildrenOfType<LeaderboardManager>().Single().CurrentCriteria?.Beatmap, () => Is.EqualTo(beatmap().BeatmapInfo));
        }

        [Test]
        public void TestEnterKeyProgressesToGameplayEvenIfCarouselFilteredOut()
        {
            PushAndConfirm(() => new SoloSongSelect());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game, virtualTrack: true).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("filter out active beatmap", () => this.ChildrenOfType<SearchTextBox>().First().Text = "abacadabadaeba");
            AddUntilStep("wait for filter", () => this.ChildrenOfType<BeatmapCarousel>().Single().IsFiltering, () => Is.False);

            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("player entered", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });
        }

        [Test]
        public void TestSelectionNotLostWithConvertedBeatmapsShown()
        {
            BeatmapSetInfo beatmapSet = null!;
            BeatmapInfo selectedBeatmap = null!;

            AddStep("import beatmap", () => beatmapSet = BeatmapImportHelper.LoadOszIntoOsu(Game).GetResultSafely());
            PushAndConfirm(() => new SoloSongSelect());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("change ruleset to taiko", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Number2);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddStep("show converts", () => Game.LocalConfig.SetValue(OsuSetting.ShowConvertedBeatmaps, true));
            AddStep("select osu! beatmap", () =>
            {
                selectedBeatmap = beatmapSet.Beatmaps.First(b => b.Ruleset.OnlineID == 0);
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(selectedBeatmap);
            });

            pushEscape();
            AddUntilStep("went back to main menu", () => Game.ScreenStack.CurrentScreen is MainMenu);
            PushAndConfirm(() => new SoloSongSelect());

            AddUntilStep("selected beatmap is still osu! ruleset", () => Game.Beatmap.Value.BeatmapInfo, () => Is.EqualTo(selectedBeatmap));
        }

        /// <summary>
        /// Note: This test was written to demonstrate the failure described at https://github.com/ppy/osu/issues/35023,
        /// but because the failure scenario there entailed a race condition, it was possible for the test to pass regardless
        /// unless <see cref="osu.Game.Screens.Select.SongSelect.SELECTION_DEBOUNCE"/> was increased.
        /// </summary>
        [Test]
        public void TestPresentFromResults()
        {
            BeatmapSetInfo beatmapToPresent = null!;
            BeatmapSetInfo beatmapToPlay = null!;
            AddStep("manually insert beatmap to be presented", () =>
            {
                Game.Realm.Write(r =>
                {
                    var beatmapSet = TestResources.CreateTestBeatmapSetInfo(3, [r.Find<RulesetInfo>("osu")]);
                    r.Add(beatmapSet);
                    beatmapToPresent = beatmapSet.Detach();
                });
            });
            AddStep("import beatmap", () => beatmapToPlay = BeatmapImportHelper.LoadQuickOszIntoOsu(Game).GetResultSafely());
            AddStep("set global beatmap", () => Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(beatmapToPlay.Beatmaps.First()));
            playToResults();
            AddStep("present beatmap from results", () => Game.PresentBeatmap(beatmapToPresent));
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddUntilStep("presented beatmap is current", () => Game.Beatmap.Value.BeatmapSetInfo.Equals(beatmapToPresent));
        }

        private Func<Player> playToResults()
        {
            var player = playToCompletion();
            AddUntilStep("wait for results", () => (Game.ScreenStack.CurrentScreen as ResultsScreen)?.IsLoaded == true);
            return player;
        }

        private Func<Player> playToCompletion()
        {
            Player? player = null;

            IWorkingBeatmap beatmap() => Game.Beatmap.Value;

            PushAndConfirm(() => new SoloSongSelect());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail(), new OsuModDoubleTime { SpeedChange = { Value = 2 } } });

            pushEnter();

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return (player = Game.ScreenStack.CurrentScreen as Player) != null;
            });

            AddUntilStep("wait for track playing", () => beatmap().Track.IsRunning);
            AddStep("seek to near end", () => player.ChildrenOfType<GameplayClockContainer>().First().Seek(beatmap().Beatmap.HitObjects[^1].StartTime - 1000));
            AddUntilStep("wait for complete", () => player?.GameplayState.HasPassed, () => Is.True);

            return () => player!;
        }

        private void waitForScreen<T>() where T : OsuScreen =>
            AddUntilStep($"Wait for {typeof(T).ReadableName()}", () => Game.ScreenStack.CurrentScreen is T screen && screen.IsLoaded);

        private void pushEnter() =>
            AddStep("Press enter", () => InputManager.Key(Key.Enter));

        private void pushEscape() =>
            AddStep("Press escape", () => InputManager.Key(Key.Escape));
    }
}
