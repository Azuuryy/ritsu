// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Tests.Resources;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.LocalPriority;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Framework.Allocation;
using osu.Game.Tests.Beatmaps.IO;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneDeathProfiles : OsuGameTestScene
    {
        private LocalProfileManager manager => Game.Dependencies.Get<LocalProfileManager>();
        private LocalProfileOverlay overlay => Game.ChildrenOfType<LocalProfileOverlay>().Single();

        private LocalProfile profile = null!;

        private void createProfile(LocalDeathMode mode)
        {
            AddStep($"create {mode} profile", () =>
            {
                profile = manager.CreateDefaultProfile(mode);
                manager.Switch(profile.Id);
            });
        }

        private void importAndWaitForSongSelect()
        {
            // Ritsu offline keeps imported archives, so BeatmapImportHelper's "temporary file deleted" check can't pass here.
            AddStep("import beatmap", () => Game.BeatmapManager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).WaitSafely());
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("beatmap updated", () => Game.Beatmap.Value.BeatmapSetInfo.OnlineID == 241526);
            // Ritsu: Torii's first-run briefing would catch the keys pressed below.
            AddStep("close Torii briefing", () => { foreach (var o in Game.ChildrenOfType<osu.Game.Overlays.ToriiBriefing.ToriiBriefingOverlay>()) o.Hide(); });
        }

        // The page is only inside the grey-fading buffer during a last look (0002 optimisation pass); otherwise it is drawn in full colour.
        private float pageGrayscale() => overlay.ChildrenOfType<BufferedContainer>().FirstOrDefault(b => b.ChildrenOfType<osu.Framework.Graphics.Cursor.PopoverContainer>().Any())?.GrayscaleStrength ?? 0;

        private OsuSpriteText countdownSprite() => overlay.ChildrenOfType<OsuSpriteText>()
                                                         .First(t => System.Text.RegularExpressions.Regex.IsMatch(t.Text.ToString(), @"^\d\d:\d\d$"));

        private bool nameGlowGone() => !overlay.ChildrenOfType<BufferedContainer>().Any(b => b.Children.OfType<OsuSpriteText>().Any());

        private float countdownScale() => overlay.ChildrenOfType<OsuSpriteText>()
                                                  .First(t => System.Text.RegularExpressions.Regex.IsMatch(t.Text.ToString(), @"^\d\d:\d\d$")).Scale.X;

        private string? countdownText() => overlay.ChildrenOfType<OsuSpriteText>()
                                                  .FirstOrDefault(t => t.Alpha > 0 && System.Text.RegularExpressions.Regex.IsMatch(t.Text.ToString(), @"^\d\d:\d\d$"))?.Text.ToString();

        [Test]
        public void TestDeathProfileNumbering()
        {
            LocalProfile? second = null;

            AddStep("reset counts", () => manager.ResetDeathProfileCounts());
            createProfile(LocalDeathMode.SuddenDeath);
            AddAssert("SD Profile 1", () => profile.Name, () => Is.EqualTo("SD Profile 1"));
            AddStep("delete it", () => manager.Delete(profile.Id, deleteScores: true));
            createProfile(LocalDeathMode.SuddenDeath);
            AddAssert("deleted ones still count: SD Profile 2", () => profile.Name, () => Is.EqualTo("SD Profile 2"));
            createProfile(LocalDeathMode.PerfectDeath);
            AddAssert("PF counted separately: PF Profile 1", () => profile.Name, () => Is.EqualTo("PF Profile 1"));
            AddAssert("counts 2 SD, 1 PF", () => manager.GetDeathProfileCounts(), () => Is.EqualTo((2, 1)));

            AddStep("reset counts", () => manager.ResetDeathProfileCounts());
            AddAssert("counts zeroed", () => manager.GetDeathProfileCounts(), () => Is.EqualTo((0, 0)));
            AddStep("create SD and PF", () =>
            {
                second = manager.CreateDefaultProfile(LocalDeathMode.SuddenDeath);
                profile = manager.CreateDefaultProfile(LocalDeathMode.PerfectDeath);
            });
            AddAssert("SD Profile 2 still exists, so SD Profile 1", () => second!.Name, () => Is.EqualTo("SD Profile 1"));
            AddAssert("PF Profile 1 taken, so PF Profile 2", () => profile.Name, () => Is.EqualTo("PF Profile 2"));
            AddAssert("counts follow the numbers", () => manager.GetDeathProfileCounts(), () => Is.EqualTo((1, 2)));
        }

        [Test]
        public void TestSuddenDeathModIsForced()
        {
            importAndWaitForSongSelect();
            AddStep("select HD + DT", () => Game.SelectedMods.Value = new Mod[] { new OsuModHidden(), new OsuModDoubleTime() });
            createProfile(LocalDeathMode.SuddenDeath);

            AddAssert("named SD Profile 1", () => profile.Name, () => Is.EqualTo("SD Profile 1"));
            AddUntilStep("SD selected", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Count(), () => Is.EqualTo(1));
            AddAssert("SD doesn't restart", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Single().Restart.Value, () => Is.False);

            AddStep("clear mods", () => Game.SelectedMods.Value = Array.Empty<Mod>());
            AddUntilStep("SD back", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Count(), () => Is.EqualTo(1));

            AddStep("select NF + HD", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail(), new OsuModHidden() });
            AddUntilStep("SD + HD, no NF", () => Game.SelectedMods.Value.Select(m => m.Acronym).OrderBy(a => a).ToArray(), () => Is.EqualTo(new[] { "HD", "SD" }));

            AddStep("select SD with restart on", () =>
            {
                var sd = new OsuModSuddenDeath();
                sd.Restart.Value = true;
                Game.SelectedMods.Value = new Mod[] { sd };
            });
            AddUntilStep("restart turned off", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Single().Restart.Value, () => Is.False);

            AddAssert("NF not allowed", () => manager.IsModAllowed(new OsuModNoFail()), () => Is.False);
            AddAssert("PF not allowed", () => manager.IsModAllowed(new OsuModPerfect()), () => Is.False);
            AddAssert("HD allowed", () => manager.IsModAllowed(new OsuModHidden()), () => Is.True);
            AddAssert("SD locked", () => manager.IsModLocked(new OsuModSuddenDeath()), () => Is.True);
            AddAssert("only restart setting hidden", () =>
            {
                var mod = new OsuModSuddenDeath();
                return mod.CreateSettingsControls().Count(c => !manager.IsModSettingHidden(mod, c));
            }, () => Is.EqualTo(1));

            AddStep("open mod select", () => InputManager.Key(Key.F1));
            AddUntilStep("mod select open", () => Game.ChildrenOfType<osu.Game.Overlays.Mods.ModSelectOverlay>().Any(o => o.State.Value == Visibility.Visible));
            AddStep("click SD panel", () =>
            {
                var panel = Game.ChildrenOfType<osu.Game.Overlays.Mods.ModPanel>().Single(p => p.Mod is OsuModSuddenDeath);
                InputManager.MoveMouseTo(panel);
                InputManager.Click(MouseButton.Left);
            });
            AddWaitStep("wait", 3);
            AddAssert("SD still selected", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Count(), () => Is.EqualTo(1));

            AddStep("switch to a Perfect Death profile", () => manager.Switch(manager.CreateDefaultProfile(LocalDeathMode.PerfectDeath).Id));
            AddUntilStep("PF instead", () => Game.SelectedMods.Value.Select(m => m.Acronym).ToArray(), () => Does.Contain("PF").And.Not.Contain("SD"));
            AddStep("switch to a normal profile", () => manager.Switch(manager.CreateDefaultProfile().Id));
            AddUntilStep("mods from before are back", () => Game.SelectedMods.Value.Select(m => m.Acronym).OrderBy(a => a).ToArray(), () => Is.EqualTo(new[] { "DT", "HD" }));
            AddAssert("SD unlocked", () => manager.IsModLocked(new OsuModSuddenDeath()), () => Is.False);
            AddStep("clear mods", () => Game.SelectedMods.Value = Array.Empty<Mod>());
            AddWaitStep("wait", 3);
            AddAssert("no SD forced", () => Game.SelectedMods.Value, () => Is.Empty);
        }

        [Test]
        public void TestPerfectDeathModIsForced()
        {
            importAndWaitForSongSelect();
            AddStep("select SD", () => Game.SelectedMods.Value = new Mod[] { new OsuModSuddenDeath() });
            createProfile(LocalDeathMode.PerfectDeath);

            AddUntilStep("PF only", () => Game.SelectedMods.Value.Select(m => m.Acronym).ToArray(), () => Is.EqualTo(new[] { "PF" }));
            AddAssert("PF doesn't restart (defaults to on)", () => Game.SelectedMods.Value.OfType<OsuModPerfect>().Single().Restart.Value, () => Is.False);
            AddAssert("PF settings hidden", () =>
            {
                var mod = new OsuModPerfect();
                return mod.CreateSettingsControls().Count(c => !manager.IsModSettingHidden(mod, c));
            }, () => Is.EqualTo(0));
        }

        [Test]
        public void TestFailDeletesProfile()
        {
            importAndWaitForSongSelect();
            AddStep("select HR", () => Game.SelectedMods.Value = new Mod[] { new OsuModHardRock() });
            createProfile(LocalDeathMode.SuddenDeath);
            AddStep("play as the profile", () => ((DummyAPIAccess)API).LocalUser.Value = profile.ToUser());
            AddUntilStep("SD selected", () => Game.SelectedMods.Value.OfType<OsuModSuddenDeath>().Count(), () => Is.EqualTo(1));

            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("in gameplay", () => Game.ScreenStack.CurrentScreen is Player { IsLoaded: true });
            AddUntilStep("failed (first object missed)", () => ((Player)Game.ScreenStack.CurrentScreen).GameplayState.HasFailed);
            AddAssert("can't retry", () => ((Player)Game.ScreenStack.CurrentScreen).Configuration.AllowRestart, () => Is.False);
            AddAssert("marked dead", () => manager.Store.GetProfile(profile.Id)?.Died, () => Is.True);
            AddAssert("profile page closed during the fail screen", () => overlay.State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddUntilStep("fail screen buttons shown", () => Game.ChildrenOfType<FailOverlay>().Single().State.Value == Visibility.Visible);
            AddAssert("profile page opened with them", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddAssert("still in gameplay below", () => Game.ScreenStack.CurrentScreen is Player);
            AddWaitStep("wait 2 seconds", 10);
            AddAssert("no pulse before the last 3 seconds", () => countdownScale(), () => Is.EqualTo(1));
            AddUntilStep("countdown shown", () => countdownText(), () => Does.Match(@"^(30:00|2\d:\d\d)$"));
            AddUntilStep("scores shown", () => overlay.ChildrenOfType<osu.Game.Overlays.Profile.ProfileSection>().Any(s => s.Identifier == "scores"));
            AddUntilStep("edge glows red", () => overlay.EdgeEffect.Colour.SRGB.R > 0.9f && overlay.EdgeEffect.Colour.SRGB.G < 0.5f && overlay.EdgeEffect.Colour.Linear.A > 0.45f);
            AddAssert("no glow on the name", nameGlowGone);
            AddAssert("countdown drawn above the fade (stays red)", () => countdownSprite().HasProxy);
            float grayscaleBefore = 0;
            AddStep("note how grey the page is", () => grayscaleBefore = pageGrayscale());
            AddAssert("page started fading to grey", () => grayscaleBefore, () => Is.GreaterThan(0).And.LessThan(0.2f));
            AddWaitStep("wait 2 seconds", 10);
            AddAssert("still fading, slowly", () => pageGrayscale(), () => Is.GreaterThan(grayscaleBefore).And.LessThan(0.4f));
            AddStep("close profile page", () => overlay.Hide());
            AddUntilStep("back at song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddUntilStep("profile deleted", () => manager.Store.GetProfile(profile.Id), () => Is.Null);
            AddUntilStep("HR back, no SD", () => Game.SelectedMods.Value.Select(m => m.Acronym).ToArray(), () => Is.EqualTo(new[] { "HR" }));
        }

        [Test]
        public void TestDeathTimesOut()
        {
            importAndWaitForSongSelect();
            createProfile(LocalDeathMode.PerfectDeath);
            AddStep("die", () => manager.BeginProfileDeath(profile.UserId));
            AddUntilStep("profile page opens", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("edge glows golden", () => overlay.EdgeEffect.Colour.SRGB.R > 0.9f && overlay.EdgeEffect.Colour.SRGB.G > 0.6f && overlay.EdgeEffect.Colour.SRGB.B < 0.5f && overlay.EdgeEffect.Colour.Linear.A > 0.45f);
            AddWaitStep("wait 20 seconds", 100);
            AddUntilStep("down to 4 seconds", () => countdownText(), () => Does.Match(@"^0[0-4]:\d\d$"));
            AddAssert("no ticking before the last 3 seconds", () => countdownSounds(), () => Is.EqualTo(0));
            AddUntilStep("pulses in the last 3 seconds", () => countdownScale() > 1.01f);
            AddAssert("the final sound plays", () => countdownSounds(), () => Is.GreaterThan(0));
            AddAssert("pulse is 6% at most", () => countdownScale(), () => Is.LessThanOrEqualTo(1.0601f));
            AddAssert("mostly grey near the end", () => pageGrayscale(), () => Is.GreaterThan(0.85f));
            AddAssert("edge glow mostly grey too", () => overlay.EdgeEffect.Colour.SRGB.R - overlay.EdgeEffect.Colour.SRGB.B, () => Is.LessThan(0.15f));
            AddAssert("edge glow still there", () => overlay.EdgeEffect.Colour.Linear.A, () => Is.GreaterThan(0.45f));
            AddWaitStep("wait for the rest", 70);
            AddUntilStep("closed after 30 seconds", () => overlay.State.Value == Visibility.Hidden);
            AddUntilStep("profile deleted", () => manager.Store.GetProfile(profile.Id), () => Is.Null);
            AddAssert("at song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);

            AddStep("open a normal profile", () =>
            {
                manager.Switch(manager.CreateDefaultProfile().Id);
                overlay.Show();
            });
            AddUntilStep("statistics loaded", () => overlay.ChildrenOfType<osu.Game.Overlays.Profile.ProfileSection>().Any());
            AddAssert("back in full colour", () => pageGrayscale(), () => Is.EqualTo(0));
        }

        // Countdown sounds started so far (the top section keeps them so they can fade out when the page closes).
        private int countdownSounds()
        {
            var top = overlay.ChildrenOfType<osu.Framework.Graphics.Containers.CompositeDrawable>().First(d => d.GetType().Name == "TopSection");
            var list = (System.Collections.ICollection)top.GetType().GetField("playingSounds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(top)!;
            return list.Count;
        }

        [Test]
        public void TestLongerTimer()
        {
            importAndWaitForSongSelect();
            AddStep("extend the timer to a minute", () => manager.LongerLastLook.Value = true);
            AddAssert("saved", () => manager.Store.Options.LongerLastLook, () => Is.True);
            createProfile(LocalDeathMode.SuddenDeath);
            AddStep("die", () => manager.BeginProfileDeath(profile.UserId));
            AddUntilStep("profile page opens", () => overlay.State.Value == Visibility.Visible);
            AddAssert("a minute long", () => manager.LastLookEndTime - manager.LastLookStartTime, () => Is.EqualTo(60_000).Within(0.01));
            AddUntilStep("countdown from a minute", () => countdownText(), () => Does.Match(@"^(60:00|5\d:\d\d)$"));
            AddWaitStep("wait 35 seconds", 175);
            AddAssert("still open after 30 seconds", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddAssert("countdown below 30 seconds", () => countdownText(), () => Does.Match(@"^[0-2]\d:\d\d$"));
            AddUntilStep("closed after a minute", () => overlay.State.Value == Visibility.Hidden);
            AddUntilStep("profile deleted", () => manager.Store.GetProfile(profile.Id), () => Is.Null);
            AddStep("back to 30 seconds", () => manager.LongerLastLook.Value = false);
            AddAssert("saved off", () => manager.Store.Options.LongerLastLook, () => Is.False);
        }

        [Test]
        public void TestEdgeGlowBuildsUpSmoothly()
        {
            var samples = new System.Collections.Generic.List<(EdgeEffectType Type, float Radius, float Alpha, osuTK.Graphics.Color4 Srgb)>();

            AddStep("create Perfect Death profile", () => manager.Switch(manager.CreateDefaultProfile(LocalDeathMode.PerfectDeath).Id));
            AddUntilStep("profile page loaded", () => Game.ChildrenOfType<LocalProfileOverlay>().SingleOrDefault()?.IsLoaded == true);
            AddStep("record the edge every frame", () => overlay.OnUpdate += o =>
            {
                var e = ((LocalProfileOverlay)o).EdgeEffect;
                samples.Add((e.Type, e.Radius, e.Colour.Linear.A, e.Colour.SRGB));
            });
            AddStep("open profile page", () => overlay.Show());
            AddUntilStep("glow finished", () => samples.Count > 0 && samples[^1].Alpha > 0.49f && samples[^1].Srgb.R > 0.9f);
            AddWaitStep("settle", 10);
            AddAssert("shadow turns into the glow without a cut", () =>
            {
                // From opening (the overlay's own shadow fading in) to the finished glow: one kind of effect, no jumps.
                int start = samples.FindIndex(x => x.Alpha > 0);
                string trace = string.Join(" ", samples.Skip(Math.Max(0, start - 2)).Select(x => $"{x.Type.ToString()[0]}{x.Radius:0}:{x.Alpha:0.000}/{x.Srgb.R:0.00},{x.Srgb.G:0.00},{x.Srgb.B:0.00}"));
                TestContext.WriteLine(trace);

                for (int i = start + 1; i < samples.Count; i++)
                {
                    var a = samples[i - 1];
                    var b = samples[i];

                    if (b.Type != a.Type || Math.Abs(b.Radius - a.Radius) > 2 || Math.Abs(b.Alpha - a.Alpha) > 0.05f
                        || Math.Abs(b.Srgb.R - a.Srgb.R) > 0.05f || Math.Abs(b.Srgb.G - a.Srgb.G) > 0.05f || Math.Abs(b.Srgb.B - a.Srgb.B) > 0.05f)
                        return $"jump at frame {i}: {trace}";
                }

                return "smooth";
            }, () => Is.EqualTo("smooth"));
        }

        [Test]
        public void TestNormalProfileHasNoGlow()
        {
            AddStep("create normal profile", () => manager.Switch(manager.CreateDefaultProfile().Id));
            AddUntilStep("profile page loaded", () => Game.ChildrenOfType<LocalProfileOverlay>().SingleOrDefault()?.IsLoaded == true);
            AddStep("open profile page", () => overlay.Show());
            AddAssert("shadow still fading in", () => overlay.EdgeEffect.Colour.Linear.A, () => Is.LessThan(0.15f));
            AddUntilStep("statistics loaded", () => overlay.ChildrenOfType<osu.Game.Overlays.Profile.ProfileSection>().Any());
            AddWaitStep("wait", 20);
            AddAssert("plain black shadow", () => overlay.EdgeEffect.Colour.SRGB.R, () => Is.LessThan(0.05f));
            AddAssert("shadow faded in", () => overlay.EdgeEffect.Colour.Linear.A, () => Is.EqualTo(0.2f).Within(0.01f));
            AddAssert("no name glow", nameGlowGone);
            AddAssert("full colour", () => pageGrayscale(), () => Is.EqualTo(0));
            AddAssert("no countdown", () => countdownText(), () => Is.Null);
        }
    }
}
