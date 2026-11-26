// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneEditorScreenModes : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        [Test]
        public void TestModPreviewUsesUnsavedMapAndKeepsEditorObjects()
        {
            int originalCount = 0;
            double originalSize = 0;
            AddStep("add an unsaved note and enable Hard Rock and Double Time", () =>
            {
                originalSize = EditorBeatmap.Difficulty.CircleSize;
                EditorBeatmap.Add(new HitCircle { StartTime = 1500 });
                originalCount = EditorBeatmap.HitObjects.Count;
                Editor.Mods.Value = new Mod[] { new OsuModHardRock(), new OsuModDoubleTime() };
                Editor.PreviewMods.Value = true;
            });
            AddUntilStep("modded preview shows the current objects",
                () => Editor.ChildrenOfType<DrawableOsuRuleset>().Any(r => r.Mods.OfType<OsuModHardRock>().Any()
                    && r.Beatmap.HitObjects.Count == originalCount && r.Beatmap.Difficulty.CircleSize > originalSize));
            AddAssert("editable difficulty is unchanged", () => EditorBeatmap.Difficulty.CircleSize, () => Is.EqualTo(originalSize));
            AddAssert("track follows Double Time", () => Beatmap.Value.Track.AggregateTempo.Value, () => Is.EqualTo(1.5));
            AddStep("set Double Time to 1.25x", () => Editor.Mods.Value.OfType<OsuModDoubleTime>().Single().SpeedChange.Value = 1.25);
            AddUntilStep("track follows the updated speed", () => Beatmap.Value.Track.AggregateTempo.Value == 1.25);
            AddStep("disable preview", () => Editor.PreviewMods.Value = false);
            AddUntilStep("only the editable composer remains", () => !Editor.ChildrenOfType<DrawableOsuRuleset>().Any(r => r.Mods.OfType<OsuModHardRock>().Any()));
            AddAssert("track returns to normal speed", () => Beatmap.Value.Track.AggregateTempo.Value, () => Is.EqualTo(1));
        }

        [Test]
        public void TestSwitchScreensInstantaneously()
        {
            AddStep("switch between all screens at once", () =>
            {
                foreach (var screen in Enum.GetValues(typeof(EditorScreenMode)).Cast<EditorScreenMode>())
                    Editor.Mode.Value = screen;
            });
        }
    }
}
