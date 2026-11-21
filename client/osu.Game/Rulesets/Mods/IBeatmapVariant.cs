// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mods
{
    public interface IBeatmapVariant : IAffectsDifficulty
    {
        BindableInt TransformationVersion { get; }
        int DefaultTransformationVersion => 1;
        int LatestTransformationVersion => 1;
    }

    public interface IRetrySeededMod : IHasSeed
    {
    }

    public sealed class UnsupportedBeatmapVariantException : InvalidOperationException
    {
        public UnsupportedBeatmapVariantException(Mod mod, int version)
            : base($"{mod.Name} variant version {version} is not supported by this client.")
        {
        }
    }
}
