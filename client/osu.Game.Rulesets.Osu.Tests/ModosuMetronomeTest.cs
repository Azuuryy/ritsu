using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Tests
{
    public class ModosuMetronomeTest
    {
        [TestCase(1, 1, 4)]
        [TestCase(2, 1, 2)]
        [TestCase(4, 1, 1)]
        [TestCase(8, 2, 1)]
        [TestCase(12, 3, 1)]
        public void RateUsesExpectedSubdivision(int setting, int divisor, int beatsPerTick)
        {
            var type = typeof(OsuModMetronome).GetNestedType("DividedMetronomeBeat", BindingFlags.NonPublic)!;
            using var beat = (BeatSyncedContainer)Activator.CreateInstance(type, 0d, setting)!;
            var tickInterval = (int)type.GetField("beatsPerTick", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(beat)!;
            Assert.That(beat.Divisor, Is.EqualTo(divisor));
            Assert.That(tickInterval, Is.EqualTo(beatsPerTick));
            Assert.That(beat.MinimumBeatLength, Is.Zero);
            Assert.That(60000d / 180 / beat.Divisor * tickInterval, Is.EqualTo(60000d / 180 / (setting / 4d)).Within(0.001));
        }

        [Test]
        public void DropdownOffersOnlyRequestedRates()
        {
            using var dropdown = new OsuModMetronome.MetronomeScaleSetting();
            Assert.That(dropdown.Items.ToArray(), Is.EqualTo(new[] { 1, 2, 4, 8, 12 }));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(12)]
        [TestCase(9)] // Previously saved values remain readable.
        public void SavedRateRoundTrips(int setting)
        {
            var original = new OsuModMetronome();
            original.TempoScale.Value = setting;
            var restored = new APIMod(original).ToMod(new OsuRuleset());
            Assert.That(restored, Is.TypeOf<OsuModMetronome>());
            Assert.That(((OsuModMetronome)restored).TempoScale.Value, Is.EqualTo(setting));
        }
    }
}
