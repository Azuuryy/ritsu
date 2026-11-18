// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Modosu.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModRandomV2
    {
        public int LatestTransformationVersion => 2;

        public int DefaultTransformationVersion => CustomAngle.Value && (SeparatePatterns.Value || MirrorResponses.Value || ProgressiveSpacing.Value) ? 2 : 1;

        [SettingSource("Separate pattern selections", "Choose different patterns for jumps, streams and bursts.", SettingControlType = typeof(AdvancedPatternToggle))]
        public BindableBool SeparatePatterns { get; } = new BindableBool();

        [SettingSource("Jump patterns", SettingControlType = typeof(CategoryPatternSetting))]
        public BindableInt JumpPatterns { get; } = new BindableInt();

        [SettingSource("Stream patterns", SettingControlType = typeof(CategoryPatternSetting))]
        public BindableInt StreamPatterns { get; } = new BindableInt();

        [SettingSource("Burst patterns", SettingControlType = typeof(CategoryPatternSetting))]
        public BindableInt BurstPatterns { get; } = new BindableInt();

        [SettingSource("Mirrored responses", "Mirror the last successful pattern in the next phrase of the same type.", SettingControlType = typeof(AdvancedPatternToggle))]
        public BindableBool MirrorResponses { get; } = new BindableBool();

        [SettingSource("Progressive spacing", "Increase spacing for repeated motifs by 4% per repetition, up to 12%.", SettingControlType = typeof(AdvancedPatternToggle))]
        public BindableBool ProgressiveSpacing { get; } = new BindableBool();

        private void applyAdvancedPatterns(OsuBeatmap beatmap, IReadOnlyList<ModosuHitObjectGenerationUtils.ObjectPositionInfo> positionInfos)
        {
            var jumps = new PatternCycle(SeparatePatterns.Value ? JumpPatterns.Value : PatternMask.Value);
            var streams = new PatternCycle(SeparatePatterns.Value ? StreamPatterns.Value : PatternMask.Value);
            var bursts = new PatternCycle(SeparatePatterns.Value ? BurstPatterns.Value : PatternMask.Value);
            var objects = beatmap.HitObjects;
            int breakIndex = 0;

            for (int start = 0; start < objects.Count;)
            {
                double time = objects[start].StartTime;
                bool afterBreak = breakIndex < beatmap.Breaks.Count && beatmap.Breaks[breakIndex].EndTime <= time;
                while (breakIndex < beatmap.Breaks.Count && beatmap.Breaks[breakIndex].EndTime <= time)
                    breakIndex++;

                if (start > 0 && (afterBreak || time - objects[start - 1].GetEndTime() >= beatmap.ControlPointInfo.TimingPointAt(time).BeatLength * 4))
                {
                    jumps.ResetProgression();
                    streams.ResetProgression();
                    bursts.ResetProgression();
                }

                if (objects[start] is not HitCircle)
                {
                    start++;
                    continue;
                }

                int end = start + 1;
                PatternCycle cycle;
                if (startsRapidRun(beatmap, start))
                {
                    while (end < objects.Count && isPatternRapidPair(beatmap, end - 1, end))
                        end++;
                    cycle = end - start >= 6 ? streams : bursts;
                }
                else
                {
                    while (end < objects.Count && objects[end] is HitCircle && !isPatternBoundary(beatmap, start, end) && !startsRapidRun(beatmap, end))
                        end++;
                    cycle = jumps;
                }

                if (cycle.Patterns.Length > 0 && end - start >= (cycle == jumps ? 4 : 3))
                {
                    var response = cycle.Response;
                    cycle.Response = null;
                    var pattern = response?.Pattern ?? cycle.Take(random);
                    int repeats = cycle.Repetitions.GetValueOrDefault(pattern);
                    float spacing = ProgressiveSpacing.Value ? 1 + 0.04f * Math.Min(3, repeats) : 1;
                    if (fitPattern(beatmap, positionInfos, start, end - 1, pattern, response?.Side, spacing) is int side)
                    {
                        cycle.Repetitions[pattern] = Math.Min(3, repeats + 1);
                        if (MirrorResponses.Value && response == null)
                            cycle.Response = (pattern, -side);
                    }
                }

                start = end;
            }
        }

        private static bool startsRapidRun(OsuBeatmap beatmap, int start) => start + 2 < beatmap.HitObjects.Count
                                                                            && isPatternRapidPair(beatmap, start, start + 1)
                                                                            && isPatternRapidPair(beatmap, start + 1, start + 2);

        private static bool isPatternRapidPair(OsuBeatmap beatmap, int left, int right)
        {
            if (beatmap.HitObjects[left] is not HitCircle previous || beatmap.HitObjects[right] is not HitCircle current)
                return false;

            double gap = current.StartTime - previous.StartTime;
            return gap > 0 && gap <= beatmap.ControlPointInfo.TimingPointAt(current.StartTime).BeatLength / 3 + 1;
        }

        private sealed class PatternCycle
        {
            public readonly Pattern[] Patterns;
            public readonly Dictionary<Pattern, int> Repetitions = new Dictionary<Pattern, int>();
            public (Pattern Pattern, int Side)? Response;
            private int next;
            private Pattern? last;

            public PatternCycle(int mask)
            {
                Patterns = patterns.Where(pattern => (pattern.Mask & mask) != 0).ToArray();
                next = Patterns.Length;
            }

            public Pattern Take(Random rng)
            {
                if (next == Patterns.Length)
                {
                    rng.Shuffle(Patterns.AsSpan());
                    if (Patterns.Length > 1 && Patterns[0] == last)
                        (Patterns[0], Patterns[1]) = (Patterns[1], Patterns[0]);
                    next = 0;
                }

                return last = Patterns[next++];
            }

            public void ResetProgression()
            {
                Response = null;
                Repetitions.Clear();
            }
        }

        public partial class AdvancedPatternToggle : SettingsCheckbox
        {
            private readonly BindableBool customAngle = new BindableBool();

            protected override void LoadComplete()
            {
                base.LoadComplete();
                if (SettingSourceObject is OsuModRandomV2 mod)
                {
                    customAngle.BindTo(mod.CustomAngle);
                    customAngle.BindValueChanged(value =>
                    {
                        if (value.NewValue)
                            Show();
                        else
                            Hide();
                    }, true);
                }
            }
        }
    }
}
