// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModOneEye : Mod, IApplicableToDifficulty, IRitsuExclusiveMod
    {
        public override string Name => "One Eye";
        public override string Acronym => "HFR";
        public override IconUsage? Icon => OsuIcon.ModOneEye;
        public override ModType Type => ModType.DifficultyIncrease;
        public override LocalisableString Description => "Halve approach rate.";
        public override bool AlwaysValidForSubmission => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;

        int IApplicableToDifficulty.DifficultyAdjustmentOrder => int.MaxValue;

        public void ApplyToDifficulty(BeatmapDifficulty difficulty) => difficulty.ApproachRate *= 0.5f;
    }
}
