// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// An interface for mods that make general adjustments to difficulty.
    /// </summary>
    public interface IApplicableToDifficulty : IApplicableMod
    {
        int DifficultyAdjustmentOrder => 0;

        static IEnumerable<IApplicableToDifficulty> InApplicationOrder(IEnumerable<Mod> mods)
            => mods.OfType<IApplicableToDifficulty>().OrderBy(mod => mod.DifficultyAdjustmentOrder);

        /// <summary>
        /// Called post beatmap conversion. Can be used to apply changes to difficulty attributes.
        /// </summary>
        /// <param name="difficulty">The difficulty to mutate.</param>
        void ApplyToDifficulty(BeatmapDifficulty difficulty);
    }
}
