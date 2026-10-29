// Adapted from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
// See LICENCE-Modosu.txt in the distribution.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Offline;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModMissChallenge : ModFailCondition, IApplicableToPlayer, IModosuMod
    {
        public override string Name => "Miss Challenge";
        public override string Acronym => "MC";
        public override LocalisableString Description => "Fail if the miss limit is exceeded.";
        public override ModType Type => ModType.DifficultyIncrease;
        public override bool Ranked => false;
        public override bool AlwaysValidForSubmission => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;
        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(ModPerfect) }).ToArray();

        [SettingSource("Use best local score", "Use the active profile's best miss count for this map and matching mods.")]
        public BindableBool UseBestLocalScore { get; } = new BindableBool();

        [SettingSource("Max misses", "Fail once the miss count exceeds this limit.", SettingControlType = typeof(MissLimitSlider))]
        public BindableInt MaxMisses { get; } = new BindableInt(10) { MinValue = 1, MaxValue = 100, Default = 10 };

        private int missCount;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (UseBestLocalScore.Value) yield return ("Use best local score", "On");
                if (!MaxMisses.IsDefault) yield return ("Max misses", MaxMisses.Value.ToString());
                if (!Restart.IsDefault) yield return ("Restart on fail", "On");
            }
        }

        public void ApplyToPlayer(Player player)
        {
            if (!UseBestLocalScore.Value || player.Dependencies.Get(typeof(RitsuProfileManager)) is not RitsuProfileManager profiles)
                return;

            static string modKey(Mod mod)
            {
                if (mod is ModRateAdjust rate)
                    return $"{mod.GetType().Name}:{rate.SpeedChange.Value:F2}";
                if (mod is OsuModDifficultyAdjust difficulty)
                {
                    var parts = new List<string> { mod.GetType().Name };
                    if (!difficulty.CircleSize.IsDefault) parts.Add($"CS:{difficulty.CircleSize.Value:F1}");
                    if (!difficulty.ApproachRate.IsDefault) parts.Add($"AR:{difficulty.ApproachRate.Value:F1}");
                    return string.Join(",", parts);
                }
                return mod.GetType().Name;
            }

            static string[] keys(IEnumerable<Mod> mods) => mods.Where(m => m is not OsuModMissChallenge && m is not ModNoFail).Select(modKey).OrderBy(k => k).ToArray();

            var activeMods = keys(player.Mods.Value);
            var scores = profiles.GetScores(profiles.ActiveProfileId, player.Ruleset.Value.ShortName)
                                 .Where(s => s.BeatmapHash == player.Beatmap.Value.BeatmapInfo.Hash && keys(s.Mods).SequenceEqual(activeMods))
                                 .ToArray();
            if (scores.Length > 0)
            {
                int best = scores.Min(s => s.Statistics.GetValueOrDefault(HitResult.Miss, int.MaxValue));
                MaxMisses.Value = Math.Clamp(best, MaxMisses.MinValue, MaxMisses.MaxValue);
            }
        }

        protected override bool FailCondition(HealthProcessor healthProcessor, JudgementResult result)
            => result.Type == HitResult.Miss && ++missCount > MaxMisses.Value;

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            missCount = 0;
        }
    }

    public partial class MissLimitSlider : SettingsSlider<int>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (SettingSourceObject is OsuModMissChallenge mod)
                mod.UseBestLocalScore.BindValueChanged(e => { if (e.NewValue) Hide(); else Show(); }, true);
        }
    }
}
