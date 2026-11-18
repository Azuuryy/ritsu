// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Scoring.Legacy
{
    public class RitsuLegacyScoreDecoder : DatabasedLegacyScoreDecoder
    {
        public RitsuLegacyScoreDecoder(IRulesetStore rulesets, BeatmapManager beatmaps)
            : base(rulesets, beatmaps)
        {
        }

        protected override Ruleset GetRuleset(int rulesetId) => base.GetRuleset(rulesetId)
            ?? throw new InvalidDataException($"Unsupported replay ruleset: {rulesetId}.");

        protected override Mod ConvertConfiguredMod(IConfiguredMod configuredMod, Ruleset ruleset) => ValidateMod(configuredMod, ruleset);

        internal static string MapLegacyAcronym(string acronym, Ruleset ruleset)
            => ruleset.ShortName == "osu" && string.Equals(acronym, "FSA", StringComparison.OrdinalIgnoreCase) ? "SOT" : acronym;

        internal static Mod ValidateMod(IConfiguredMod configuredMod, Ruleset ruleset)
        {
            var mod = ruleset.CreateModFromAcronym(MapLegacyAcronym(configuredMod.Acronym, ruleset))
                      ?? throw new InvalidDataException($"Unsupported mod: {configuredMod.Acronym}.");
            var properties = mod.GetSettingsSourceProperties().ToDictionary(entry => entry.Item2.Name.ToSnakeCase(), entry => entry.Item2, StringComparer.Ordinal);

            foreach (var setting in configuredMod.Settings)
            {
                if (!properties.TryGetValue(setting.Key, out var property))
                    throw new InvalidDataException($"Unsupported setting '{setting.Key}' for mod {configuredMod.Acronym}.");

                try
                {
                    var bindable = (IBindable)property.GetValue(mod)!;
                    mod.CopyAdjustedSetting(bindable, setting.Value);
                    object actual = bindable.GetUnderlyingSettingValue();
                    object? expected = JToken.FromObject(setting.Value).ToObject(actual.GetType());
                    if (!EqualityComparer<object>.Default.Equals(actual, expected))
                        throw new InvalidDataException($"Setting '{setting.Key}' for mod {configuredMod.Acronym} cannot be preserved.");
                }
                catch (InvalidDataException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Invalid setting '{setting.Key}' for mod {configuredMod.Acronym}.", ex);
                }
            }

            return mod;
        }
    }
}
