// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring.Legacy;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class RitsuReplayModValidationTest
    {
        [Test]
        public void TestKnownModPreservesSettings()
        {
            var configured = new APIMod { Acronym = "SA", Settings = new Dictionary<string, object> { ["object_spacing"] = 1.5 } };
            var mod = (OsuModSpacingAdjust)RitsuLegacyScoreDecoder.ValidateMod(configured, new OsuRuleset());
            Assert.That(mod.ObjectSpacing.Value, Is.EqualTo(1.5f));
        }

        [Test]
        public void TestMosuFunSpacingAliasKeepsItsSettings()
        {
            var configured = new APIMod { Acronym = "FSA", Settings = new Dictionary<string, object> { ["start_spacing"] = 1.5, ["end_spacing"] = 0.5 } };
            var ruleset = new OsuRuleset();
            var imported = (OsuModSpacingOvertime)RitsuLegacyScoreDecoder.ValidateMod(configured, ruleset);
            Assert.That(imported.StartSpacing.Value, Is.EqualTo(1.5f));
            Assert.That(imported.EndSpacing.Value, Is.EqualTo(0.5f));
            Assert.That(configured.ToMod(ruleset), Is.InstanceOf<OsuModSpacingOvertime>());
        }

        [Test]
        public void TestUnknownModIsRejected()
        {
            var error = Assert.Throws<InvalidDataException>(() => RitsuLegacyScoreDecoder.ValidateMod(new APIMod { Acronym = "UNKNOWN" }, new OsuRuleset()));
            Assert.That(error!.Message, Does.Contain("UNKNOWN"));
        }

        [Test]
        public void TestUnknownSettingIsRejected()
        {
            var configured = new APIMod { Acronym = "SA", Settings = new Dictionary<string, object> { ["unknown_setting"] = 1 } };
            var error = Assert.Throws<InvalidDataException>(() => RitsuLegacyScoreDecoder.ValidateMod(configured, new OsuRuleset()));
            Assert.That(error!.Message, Does.Contain("unknown_setting"));
        }

        [Test]
        public void TestClampedSettingIsRejected()
        {
            var configured = new APIMod { Acronym = "SA", Settings = new Dictionary<string, object> { ["object_spacing"] = 99.0 } };
            var error = Assert.Throws<InvalidDataException>(() => RitsuLegacyScoreDecoder.ValidateMod(configured, new OsuRuleset()));
            Assert.That(error!.Message, Does.Contain("object_spacing"));
        }
    }
}
