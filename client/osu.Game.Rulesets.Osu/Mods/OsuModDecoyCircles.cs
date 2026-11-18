// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Graphics;
using osu.Framework.Graphics.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModDecoyCircles : OsuModBeatmapVariant, IApplicableToBeatmap
    {
        public override string Name => "Decoy Circles";
        public override ModType Type => ModType.DifficultyIncrease;
        public override IconUsage? Icon => OsuIcon.ModDecoyCircles;
        public override string Acronym => "DCY";
        public override LocalisableString Description => "Avoid the fake circles.";
        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(OsuModRelax), typeof(OsuModClassic) }).ToArray();

        [SettingSource("Minimum interval (beats)", "Minimum spacing at the last decoy's tempo. A decoy still needs an isolated circle and enough room.")]
        public BindableInt IntervalBeats { get; } = new BindableInt(8)
        {
            MinValue = 2,
            MaxValue = 32,
        };

        [SettingSource("Minimum note spacing (beats)", "Lower this to allow decoys in denser patterns, when there is enough room.")]
        public BindableDouble MinimumNoteSpacing { get; } = new BindableDouble(0.5)
        {
            MinValue = 0.25,
            MaxValue = 1,
            Precision = 0.25,
        };

        [SettingSource("Decoy appearance", "Use the skin's colours or grayscale.")]
        public Bindable<DecoyCircleStyle> Style { get; } = new Bindable<DecoyCircleStyle>(DecoyCircleStyle.RealFake);

        [SettingSource("Decoy number", "Use -1 for the cross, or choose a number from 0 to 99.")]
        public BindableInt Number { get; } = new BindableInt(-1)
        {
            MinValue = -1,
            MaxValue = 99,
        };

        protected override void ApplyVariant(OsuBeatmap beatmap)
        {
        }

        void IApplicableToBeatmap.ApplyToBeatmap(IBeatmap beatmap)
        {
            if (beatmap is not OsuBeatmap osuBeatmap)
                return;

            var decoys = new List<OsuHitObject>();
            double nextEligibleTime = double.NegativeInfinity;
            for (int i = 0; i < osuBeatmap.HitObjects.Count; i++)
            {
                var anchor = osuBeatmap.HitObjects[i];
                if (anchor.StartTime < nextEligibleTime || !IsIsolatedCircle(osuBeatmap, i, MinimumNoteSpacing.Value)
                    || FindAdjacentPosition(osuBeatmap, i) is not Vector2 position)
                    continue;

                var decoy = new DecoyCircle
                {
                    StartTime = anchor.StartTime,
                    Position = position,
                    Style = Style.Value,
                    Number = Number.Value,
                    ComboIndex = anchor.ComboIndex,
                    ComboIndexWithOffsets = anchor.ComboIndexWithOffsets,
                };
                decoy.ApplyDefaults(osuBeatmap.ControlPointInfo, osuBeatmap.Difficulty);
                decoys.Add(decoy);
                nextEligibleTime = anchor.StartTime + osuBeatmap.ControlPointInfo.TimingPointAt(anchor.StartTime).BeatLength * IntervalBeats.Value;
            }

            if (decoys.Count > 0)
                osuBeatmap.HitObjects = osuBeatmap.HitObjects.Concat(decoys).OrderBy(o => o.StartTime).ToList();
        }
    }
}
