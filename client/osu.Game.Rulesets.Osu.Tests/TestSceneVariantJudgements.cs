// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneVariantJudgements : ModTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        [TestCase(false, false, DecoyCircleStyle.Grayscale, -1)]
        [TestCase(true, false, DecoyCircleStyle.RealFake, 0)]
        [TestCase(false, true, DecoyCircleStyle.RealFake, 7)]
        [TestCase(true, true, DecoyCircleStyle.Grayscale, -1)]
        [TestCase(false, false, DecoyCircleStyle.RealFake, 99)]
        [TestCase(false, true, DecoyCircleStyle.Grayscale, 99)]
        public void DecoyOnlyPenalisesAHit(bool clickDecoy, bool legacySkin, DecoyCircleStyle style, int number)
        {
            AddStep("select skin", () =>
            {
                if (legacySkin)
                    skins.CurrentSkinInfo.Value = DefaultLegacySkin.CreateInfo().ToLiveUnmanaged();
                else
                    skins.CurrentSkinInfo.SetDefault();
            });

            OsuBeatmap createBeatmap() => beatmap(circle(1500, 200), circle(3000, 200));
            var mod = new OsuModDecoyCircles { Style = { Value = style }, Number = { Value = number } };
            var layout = createBeatmap();
            ((IApplicableToBeatmap)mod).ApplyToBeatmap(layout);
            Vector2 decoy = layout.HitObjects.OfType<DecoyCircle>().Single().Position;
            var cursor = new Vector2(200, 192);
            var frames = new List<ReplayFrame>
            {
                new OsuReplayFrame(1400, cursor),
                new OsuReplayFrame(1500, cursor, OsuAction.LeftButton),
                new OsuReplayFrame(1515, cursor),
            };
            if (clickDecoy)
            {
                frames.Add(new OsuReplayFrame(1530, decoy));
                frames.Add(new OsuReplayFrame(1540, decoy, OsuAction.RightButton));
                frames.Add(new OsuReplayFrame(1555, decoy));
            }

            frames.Add(new OsuReplayFrame(2900, cursor));
            frames.Add(new OsuReplayFrame(3000, cursor, OsuAction.LeftButton));
            frames.Add(new OsuReplayFrame(3015, cursor));
            frames.Add(new OsuReplayFrame(4000, cursor));

            bool? displayedDecoyJudgement = null;
            string? displayedNumber = null;
            bool? renderedCorrectly = null;
            CreateModTest(new ModTestData
            {
                Autoplay = !clickDecoy,
                Mod = mod,
                ReplayFrames = clickDecoy ? frames : null,
                CreateBeatmap = createBeatmap,
                PassCondition = () => Player.Results.Count >= 3
                    && Player.Results.Count(r => r.HitObject is DecoyCircle && r.Type == (clickDecoy ? HitResult.Miss : HitResult.IgnoreMiss)) == 1
                    && Player.Results.Count(r => r.HitObject is not DecoyCircle && r.Type == HitResult.Great) == 2
                    && Player.ScoreProcessor.Combo.Value == (clickDecoy ? 1 : 2)
                    && displayedDecoyJudgement == clickDecoy
                    && displayedNumber == (number < 0 ? string.Empty : number.ToString())
                    && renderedCorrectly == true,
            });

            AddStep("track whether the decoy displays a judgement", () =>
            {
                Player.DrawableRuleset.Playfield.NewResult += (drawable, result) =>
                {
                    if (result.HitObject is DecoyCircle)
                    {
                        displayedDecoyJudgement = drawable.DisplayResult;
                        var decoyDrawable = (DrawableDecoyCircle)drawable;
                        displayedNumber = decoyDrawable.NumberText.Value;
                        string expected = number < 0 ? string.Empty : number.ToString();
                        bool renderedNumber = decoyDrawable.ChildrenOfType<SkinnableSpriteText>().Any(text => text.Text.ToString() == expected && (number < 0 || text.IsPresent))
                                              || decoyDrawable.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == expected && (number < 0 || text.IsPresent));
                        var bars = decoyDrawable.ChildrenOfType<Box>().Where(box => box.Size == new Vector2(42, 7) && (box.Rotation == 45 || box.Rotation == -45)).ToArray();
                        bool correctSkin = legacySkin ? decoyDrawable.ChildrenOfType<LegacyMainCirclePiece>().Any() : decoyDrawable.ChildrenOfType<ArgonMainCirclePiece>().Any();
                        renderedCorrectly = correctSkin && renderedNumber && bars.Length == 2 && bars.All(box => box.Parent.Alpha == (number < 0 ? 1 : 0));
                    }
                };
            });
        }

        [TestCase(300, 0.5)]
        [TestCase(400, 0.5)]
        [TestCase(300, 0.25)]
        [TestCase(400, 0.25)]
        public void DecoysDoNotBlockPatternsAtHighTempo(int bpm, double noteSpacing)
        {
            double beatLength = 60000.0 / bpm;
            OsuBeatmap createBeatmap()
            {
                var map = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
                map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });
                map.HitObjects.AddRange(Enumerable.Range(0, 6).Select(i => circle(1500 + i * beatLength * noteSpacing, i % 2 == 0 ? 120 : 380)));
                return map;
            }

            var mod = new OsuModDecoyCircles { IntervalBeats = { Value = 2 }, MinimumNoteSpacing = { Value = noteSpacing } };
            var layout = createBeatmap();
            ((IApplicableToBeatmap)mod).ApplyToBeatmap(layout);
            int decoyCount = layout.HitObjects.OfType<DecoyCircle>().Count();
            Assert.That(decoyCount, Is.GreaterThan(0));
            CreateModTest(new ModTestData
            {
                Autoplay = true,
                Mod = mod,
                CreateBeatmap = createBeatmap,
                PassCondition = () => Player.Results.Count(r => r.HitObject is not DecoyCircle && r.Type == HitResult.Great) == 6
                    && Player.Results.Count(r => r.HitObject is DecoyCircle && r.Type == HitResult.IgnoreMiss) == decoyCount
                    && Player.ScoreProcessor.Combo.Value == 6,
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FlickCanBeRecoveredUntilTheWindowCloses(bool recover)
        {
            var left = new Vector2(120, 192);
            var note = new Vector2(320, 192);
            var frames = new List<ReplayFrame>
            {
                new OsuReplayFrame(1400, left),
                new OsuReplayFrame(1500, left, OsuAction.LeftButton),
                new OsuReplayFrame(1515, left),
                new OsuReplayFrame(1930, new Vector2(380, 192)),
                new OsuReplayFrame(1970, note, OsuAction.LeftButton),
                new OsuReplayFrame(1975, note),
            };
            if (recover)
            {
                frames.Add(new OsuReplayFrame(1999, new Vector2(265, 192)));
                frames.Add(new OsuReplayFrame(2000, new Vector2(265, 192)));
                frames.Add(new OsuReplayFrame(2040, note, OsuAction.RightButton));
                frames.Add(new OsuReplayFrame(2045, note));
            }

            frames.Add(new OsuReplayFrame(2900, note));
            frames.Add(new OsuReplayFrame(3000, note, OsuAction.LeftButton));
            frames.Add(new OsuReplayFrame(3015, note));
            frames.Add(new OsuReplayFrame(4000, note));

            CreateModTest(new ModTestData
            {
                Autoplay = false,
                Mod = new OsuModDirectionalFlicks(),
                ReplayFrames = frames,
                CreateBeatmap = () => beatmap(circle(1500, 120), circle(2000, 320), circle(3000, 320)),
                PassCondition = () => Player.Results.Any(r => r.HitObject is HitCircle { FlickDirection: not null }
                    && (recover ? r.IsHit && r.TimeOffset > 0 : r.Type == HitResult.Miss && r.TimeOffset > 40)),
            });
        }

        private static HitCircle circle(double time, float x) => new HitCircle { StartTime = time, Position = new Vector2(x, 192) };

        public override void TearDownSteps()
        {
            base.TearDownSteps();
            AddStep("restore default skin", () => skins.CurrentSkinInfo.SetDefault());
        }

        private static OsuBeatmap beatmap(params OsuHitObject[] objects)
        {
            var map = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            map.HitObjects.AddRange(objects);
            return map;
        }
    }
}
