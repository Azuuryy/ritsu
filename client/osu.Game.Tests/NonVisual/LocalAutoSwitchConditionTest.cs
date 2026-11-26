// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.LocalPriority;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LocalAutoSwitchConditionTest
    {
        [TestCase("180, 190", 180, true)]
        [TestCase("180, 190", 190, true)]
        [TestCase("180, 190", 185, false)]
        [TestCase(">5", 5, false)]
        [TestCase(">=5", 5, true)]
        [TestCase("<7", 7, false)]
        [TestCase("<=7", 7, true)]
        [TestCase("=6", 6, true)]
        [TestCase("6", 6.01, false)]
        [TestCase("5-7", 6, true)]
        [TestCase("5 - 7", 8, false)]
        [TestCase("5+", 5, true)]
        [TestCase("-7", 7.5, false)]
        [TestCase(">5 <7", 6, true)]
        [TestCase(">5 <7", 7, false)]
        [TestCase("<100, >200", 250, true)]
        [TestCase("<100, >200", 150, false)]
        [TestCase("5.5", 5.5, true)]
        [TestCase("≥ 4.25", 4.25, true)]
        [TestCase("", 1, true)]
        [TestCase("abc", 1, false)]
        public void TestNumber(string condition, double value, bool expected) =>
            Assert.That(LocalAutoSwitchCondition.MatchesNumber(condition, value), Is.EqualTo(expected));

        [TestCase(">= 5 ,180", true, ">= 5, 180")]
        [TestCase("x>5", false, "")]
        [TestCase("5,5", true, "5, 5")]
        [TestCase("5+ <3", true, "5+ <3")]
        [TestCase(">+5", false, "")]
        public void TestNormalise(string text, bool ok, string normalised)
        {
            Assert.That(LocalAutoSwitchCondition.TryNormaliseNumber(text, out string result, out _), Is.EqualTo(ok));
            Assert.That(result, Is.EqualTo(normalised));
        }

        [Test]
        public void TestRulesMatchBeatmap()
        {
            var beatmap = new BeatmapInfo { StarRating = 6.234, BPM = 179.97, Length = 95_900, DifficultyName = "Insane" };
            beatmap.Metadata.Author.Username = "Sotarks";
            beatmap.Metadata.Title = "Blue Zenith";

            bool matches(LocalAutoSwitchRules r) => r.Matches(beatmap, new List<Mod>(), null);

            Assert.That(matches(new LocalAutoSwitchRules { Bpm = "180, 190" }), Is.True, "BPM is shown as 180");
            Assert.That(matches(new LocalAutoSwitchRules { Bpm = "170, 190" }), Is.False);
            Assert.That(matches(new LocalAutoSwitchRules { Stars = ">6.2 <6.3" }), Is.True);
            Assert.That(matches(new LocalAutoSwitchRules { Stars = "6.23" }), Is.True, "stars to two decimals");
            Assert.That(matches(new LocalAutoSwitchRules { Length = "95" }), Is.True, "whole seconds");
            Assert.That(matches(new LocalAutoSwitchRules { Length = "<95" }), Is.False);
            Assert.That(matches(new LocalAutoSwitchRules { Mapper = "Monstrata, sotarks" }), Is.True);
            Assert.That(matches(new LocalAutoSwitchRules { Mapper = "Monstrata, Mismagius" }), Is.False);
            Assert.That(matches(new LocalAutoSwitchRules { Name = "freedom dive, zenith" }), Is.True);
            Assert.That(matches(new LocalAutoSwitchRules { Name = "zenith", Bpm = ">200" }), Is.False, "every field has to match");
        }

        [Test]
        public void TestOldSavedRulesAreUpgraded()
        {
            var rules = JsonConvert.DeserializeObject<LocalAutoSwitchRules>(
                @"{""MinStars"":5.0,""MaxStars"":7.0,""MinBpm"":180.0,""MaxBpm"":null,""MinLength"":null,""MaxLength"":120.0,""Mapper"":""x""}")!;

            Assert.That(rules.Stars, Is.EqualTo("5-7"));
            Assert.That(rules.Bpm, Is.EqualTo(">=180"));
            Assert.That(rules.Length, Is.EqualTo("<=120"));

            string saved = JsonConvert.SerializeObject(rules);
            Assert.That(saved, Does.Not.Contain("MinStars"));
            Assert.That(JsonConvert.DeserializeObject<LocalAutoSwitchRules>(saved)!.Stars, Is.EqualTo("5-7"));
            Assert.That(rules.Clone().Bpm, Is.EqualTo(">=180"));
        }
    }
}
