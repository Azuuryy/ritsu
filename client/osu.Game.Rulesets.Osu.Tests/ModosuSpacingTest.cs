// Adapted functionality from Modosu and MOsu; see LICENCE-Modosu.txt.
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public class ModosuSpacingTest
    {
        [Test]
        public void SpacingReapplicationDoesNotAccumulate()
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 0, Position = new Vector2(200, 150) });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 500, Position = new Vector2(250, 150) });
            var mod = new OsuModSpacingAdjust();
            mod.ObjectSpacing.Value = 2;
            mod.ApplyToBeatmap(beatmap);
            var first = beatmap.HitObjects.Select(o => o.Position).ToArray();
            mod.ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Select(o => o.Position).ToArray(), Is.EqualTo(first));
        }

        [Test]
        public void FunSpacingAcceptsEmptyBeatmap()
        {
            Assert.DoesNotThrow(() => new OsuModSpacingOvertime().ApplyToBeatmap(new OsuBeatmap()));
        }

        [Test]
        public void ConversionModsAreRegistered()
        {
            var mods = new OsuRuleset().GetModsFor(ModType.Conversion).ToArray();
            Assert.That(mods.Any(m => m is OsuModCircleGeneration), Is.True);
            Assert.That(mods.Any(m => m is OsuModNoSliding), Is.True);
            Assert.That(mods.Any(m => m is OsuModRandomV2), Is.True);
        }

        [Test]
        public void RandomV2IsRepeatableForSameSeed()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 8; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 + i * 250, Position = new Vector2(150 + i * 20, 150 + i % 2 * 30) });
            var mod = new OsuModRandomV2();
            mod.Seed.Value = 123;
            mod.ApplyToBeatmap(beatmap);
            var first = beatmap.HitObjects.Select(o => o.Position).ToArray();
            mod.ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Select(o => o.Position).ToArray(), Is.EqualTo(first));
        }

        [Test]
        public void NoSlidingConvertsSliderIntoCircles()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            var slider = new Slider
            {
                StartTime = 1000,
                Position = new Vector2(200, 150),
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(200, 0)) })
            };
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            beatmap.HitObjects.Add(slider);
            new OsuModNoSliding().ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Count, Is.GreaterThan(0));
            Assert.That(beatmap.HitObjects.All(o => o is HitCircle), Is.True);
        }

        [Test]
        public void CircleGenerationCreatesPracticeObjects()
        {
            var beatmap = new OsuBeatmap { BeatmapInfo = new BeatmapInfo { Difficulty = new BeatmapDifficulty() } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1000, Position = new Vector2(200, 150) });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 3000, Position = new Vector2(250, 150) });
            new OsuModCircleGeneration().ApplyToBeatmap(beatmap);
            Assert.That(beatmap.HitObjects.Count, Is.GreaterThan(2));
            Assert.That(beatmap.HitObjects.All(o => o is HitCircle), Is.True);
        }
    }
}
