// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Modosu.Utils;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModRandomV2
    {
        private const float octagon_diagonal = 0.70710678f;

        private static readonly Pattern[] patterns =
        {
            new Pattern(1 << 0, "Ladder", new(0, 0), new(3, 0), new(3, 1), new(0, 1), new(0, 2), new(3, 2)),
            new Pattern(1 << 1, "Cross", new(0, 0), new(3, 2), new(3, 0), new(0, 2)),
            new Pattern(1 << 2, "Offset 1–2s", new(0, 0), new(3, 1), new(3.5f, 2), new(0, 2), new(0.5f, 1), new(3, 0)),
            new Pattern(1 << 3, "Double Arc", new(0, 0), new(1, -1), new(2, -1), new(3, 0), new(4, 1), new(5, 1), new(6, 0)),
            new Pattern(1 << 4, "Boomerang", new(0, 0), new(1, -1), new(2, -1.5f), new(3, -0.5f), new(2, 0.5f), new(1, 0.4f)),
            new Pattern(1 << 5, "Funnel", new(-2, 0), new(2, 0.5f), new(-1.3f, 1), new(1.3f, 1.5f), new(-0.6f, 2), new(0.6f, 2.5f)),
            new Pattern(1 << 6, "Clusters", new(0, 0), new(0.5f, 0.3f), new(1, 0), new(3, 1.5f), new(3.5f, 1.2f), new(4, 1.5f)),
            new Pattern(1 << 7, "Zigzag", new(0, 0), new(1, 1), new(2, 0), new(3, 1), new(4, 0), new(5, 1)),
            new Pattern(1 << 8, "Arc", new(-2, 0), new(-1.7f, -1), new(-1, -1.7f), new(0, -2), new(1, -1.7f), new(1.7f, -1), new(2, 0)),
            new Pattern(1 << 9, "Back and Forth", new(0, 0), new(2, 0.25f), new(0.5f, 0.5f), new(2.5f, 0.75f)),
            new Pattern(1 << 10, "Triangle", new(0, 0), new(1, -1.73f), new(2, 0)),
            new Pattern(1 << 11, "Square (90°)", new(0, 0), new(1, 0), new(1, 1), new(0, 1)),
            new Pattern(1 << 12, "Star", new(0, -2), new(1.18f, 1.62f), new(-1.9f, -0.62f), new(1.9f, -0.62f), new(-1.18f, 1.62f)),
            new Pattern(1 << 13, "Hub and Spoke", new(0, 0), new(1, 0), new(0, 0), new(0, 1), new(0, 0), new(-1, 0), new(0, 0), new(0, -1)),
            new Pattern(1 << 14, "Octagon (45°)", new(1, 0), new(octagon_diagonal, octagon_diagonal), new(0, 1), new(-octagon_diagonal, octagon_diagonal),
                new(-1, 0), new(-octagon_diagonal, -octagon_diagonal), new(0, -1), new(octagon_diagonal, -octagon_diagonal)),
        };

        private static readonly int known_pattern_mask = patterns.Aggregate(0, (mask, pattern) => mask | pattern.Mask);

        private void applyPatterns(OsuBeatmap beatmap, IReadOnlyList<ModosuHitObjectGenerationUtils.ObjectPositionInfo> positionInfos)
        {
            var selected = patterns.Where(pattern => (pattern.Mask & PatternMask.Value) != 0).ToArray();
            if (selected.Length == 0)
                return;

            var objects = beatmap.HitObjects;
            int nextPattern = selected.Length;
            Pattern? lastPattern = null;

            for (int start = 0; start < objects.Count;)
            {
                int end = start + 1;
                if (objects[start] is HitCircle)
                {
                    while (end < objects.Count && objects[end] is HitCircle && !isPatternBoundary(beatmap, start, end))
                        end++;

                    if (end - start >= 4)
                    {
                        if (nextPattern == selected.Length)
                        {
                            random.Shuffle(selected.AsSpan());
                            if (selected.Length > 1 && selected[0] == lastPattern)
                                (selected[0], selected[1]) = (selected[1], selected[0]);
                            nextPattern = 0;
                        }

                        lastPattern = selected[nextPattern++];
                        fitPattern(beatmap, positionInfos, start, end - 1, lastPattern);
                    }
                }

                start = end;
            }
        }

        private static bool isPatternBoundary(OsuBeatmap beatmap, int start, int next)
        {
            var current = beatmap.HitObjects[next];
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(current.StartTime).BeatLength;
            double gap = current.StartTime - beatmap.HitObjects[next - 1].StartTime;

            return gap >= beatLength * 1.25 || (next - start >= 8 && gap + 1 >= beatLength / 2 &&
                (current.NewCombo || ModosuHitObjectGenerationUtils.IsHitObjectOnBeat(beatmap, current, true)));
        }

        private int? fitPattern(OsuBeatmap beatmap, IReadOnlyList<ModosuHitObjectGenerationUtils.ObjectPositionInfo> positionInfos, int start, int end,
                                Pattern pattern, int? requiredSide = null, float spacingMultiplier = 1)
        {
            var objects = beatmap.HitObjects;
            Vector2 entry = objects[start].Position;
            Vector2 exit = objects[end].Position;
            Vector2 incoming = start > 0 && objects[start - 1] is not Spinner ? entry - objects[start - 1].EndPosition : Vector2.Zero;
            Vector2 outgoing = end + 1 < objects.Count && objects[end + 1] is not Spinner ? objects[end + 1].Position - exit : Vector2.Zero;
            float originalExitDistance = Vector2.Distance(objects[end - 1].Position, exit);
            bool fastExit = isRapidStep(beatmap, end);
            float maxExitDistance = originalExitDistance * (fastExit ? 1.08f : 1.20f) + 1;
            float minExitDistance = originalExitDistance * 0.65f;

            int count = end - start - 1;
            var steps = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int index = start + i + 1;
                Vector2 step = pattern.Steps[i % pattern.Steps.Length];
                float length = step.Length;
                bool rapid = isRapidStep(beatmap, index);
                float strength = i == 0 || i == count - 1 ? 0.4f : 1;
                float spacing = 1 + strength * (Math.Clamp(length / pattern.AverageStep, 0.65f, rapid ? 1 : 1.25f) - 1);
                float distance = positionInfos[index].DistanceFromPrevious * spacingMultiplier;
                if (rapid)
                    distance = Math.Min(distance, Vector2.Distance(objects[index - 1].Position, objects[index].Position) * 1.08f + 1);

                steps[i] = step * (spacing * distance / length);
            }

            var candidate = new Vector2[count];
            var best = new Vector2[count];
            float bestCost = float.PositiveInfinity;
            int bestSide = 1;
            Vector2 bounds = OsuPlayfield.BASE_SIZE + (ExtendPlayArea.Value ? new Vector2(40, 30) : Vector2.Zero);
            int angleOffset = random.Next(24);
            int mirrorOffset = random.Next(2);

            for (int scaleIndex = 0; scaleIndex < 3; scaleIndex++)
            {
                float scale = 1 - scaleIndex * 0.12f;
                for (int rotation = 0; rotation < 24; rotation++)
                {
                    float angle = (rotation + angleOffset) * (MathF.Tau / 24);
                    float sin = MathF.Sin(angle) * scale;
                    float cos = MathF.Cos(angle) * scale;

                    for (int mirror = 0; mirror < 2; mirror++)
                    {
                        int side = (mirror + mirrorOffset) % 2 == 0 ? 1 : -1;
                        if (requiredSide.HasValue && side != requiredSide)
                            continue;
                        Vector2 position = entry;
                        int i = 0;
                        for (; i < count; i++)
                        {
                            Vector2 step = steps[i];
                            step.Y *= side;
                            position += new Vector2(step.X * cos - step.Y * sin, step.X * sin + step.Y * cos);
                            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
                                ((!InfinitePlayArea.Value || ExtendPlayArea.Value) && (position.X < 0 || position.Y < 0 || position.X > bounds.X || position.Y > bounds.Y)))
                                break;

                            candidate[i] = position;
                        }

                        if (i != count)
                            continue;

                        Vector2 exitStep = exit - candidate[^1];
                        float exitDistance = exitStep.Length;
                        if (exitDistance < minExitDistance || exitDistance > maxExitDistance)
                            continue;

                        Vector2 lastStep = candidate[^1] - (count > 1 ? candidate[^2] : entry);
                        float cost = scaleIndex * 0.25f
                                     + MathF.Abs(exitDistance - originalExitDistance) / MathF.Max(8, originalExitDistance)
                                     + 0.35f * turnCost(incoming, candidate[0] - entry)
                                     + (fastExit ? 0.6f : 0.3f) * turnCost(lastStep, exitStep)
                                     + 0.35f * turnCost(exitStep, outgoing);

                        if (cost >= bestCost)
                            continue;

                        bestCost = cost;
                        bestSide = side;
                        candidate.CopyTo(best, 0);
                    }
                }
            }

            if (float.IsPositiveInfinity(bestCost))
                return null;

            for (int i = 0; i < count; i++)
                objects[start + i + 1].Position = best[i];

            return bestSide;
        }

        private static bool isRapidStep(OsuBeatmap beatmap, int index)
        {
            var current = beatmap.HitObjects[index];
            return current.StartTime - beatmap.HitObjects[index - 1].StartTime + 1 < beatmap.ControlPointInfo.TimingPointAt(current.StartTime).BeatLength / 2;
        }

        private static float turnCost(Vector2 before, Vector2 after)
        {
            float lengths = before.Length * after.Length;
            return lengths < 1 ? 0 : 1 - Vector2.Dot(before, after) / lengths;
        }

        private sealed class Pattern
        {
            public readonly int Mask;
            public readonly string Name;
            public readonly Vector2[] Steps;
            public readonly float AverageStep;

            public Pattern(int mask, string name, params Vector2[] points)
            {
                Mask = mask;
                Name = name;
                Steps = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                    Steps[i] = points[(i + 1) % points.Length] - points[i];
                AverageStep = Steps.Average(step => step.Length);
            }
        }

        public partial class PatternSetting : SettingsItem<int>
        {
            private readonly BindableBool customAngle = new BindableBool();
            private readonly BindableBool separatePatterns = new BindableBool();

            protected virtual bool Separate => false;

            protected override Drawable CreateControl() => new PatternCheckboxes();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (SettingSourceObject is OsuModRandomV2 mod)
                {
                    customAngle.BindTo(mod.CustomAngle);
                    separatePatterns.BindTo(mod.SeparatePatterns);
                    customAngle.BindValueChanged(_ => updateVisibility());
                    separatePatterns.BindValueChanged(_ => updateVisibility(), true);
                }
            }
            private void updateVisibility()
            {
                if (customAngle.Value && separatePatterns.Value == Separate)
                    Show();
                else
                    Hide();
            }
        }

        public partial class CategoryPatternSetting : PatternSetting
        {
            protected override bool Separate => true;
        }

        public partial class AngleEnumSetting : SettingsEnumDropdown<AngleEnum>
        {
            private readonly BindableBool customAngle = new BindableBool();
            private readonly Bindable<int> patternMask = new BindableInt();
            private readonly Bindable<int> jumpMask = new BindableInt();
            private readonly Bindable<int> streamMask = new BindableInt();
            private readonly Bindable<int> burstMask = new BindableInt();
            private readonly BindableBool separatePatterns = new BindableBool();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (SettingSourceObject is OsuModRandomV2 mod)
                {
                    customAngle.BindTo(mod.CustomAngle);
                    patternMask.BindTo(mod.PatternMask);
                    jumpMask.BindTo(mod.JumpPatterns);
                    streamMask.BindTo(mod.StreamPatterns);
                    burstMask.BindTo(mod.BurstPatterns);
                    separatePatterns.BindTo(mod.SeparatePatterns);
                    customAngle.BindValueChanged(_ => updateVisibility());
                    patternMask.BindValueChanged(_ => updateVisibility());
                    jumpMask.BindValueChanged(_ => updateVisibility());
                    streamMask.BindValueChanged(_ => updateVisibility());
                    burstMask.BindValueChanged(_ => updateVisibility());
                    separatePatterns.BindValueChanged(_ => updateVisibility(), true);
                }
            }

            private void updateVisibility()
            {
                int activeMask = separatePatterns.Value ? jumpMask.Value | streamMask.Value | burstMask.Value : patternMask.Value;
                if (customAngle.Value && (activeMask & known_pattern_mask) == 0)
                    Show();
                else
                    Hide();
            }
        }

        private partial class PatternCheckboxes : FillFlowContainer<OsuCheckbox>, IHasCurrentValue<int>
        {
            private readonly Bindable<int> current = new BindableInt();

            public Bindable<int> Current
            {
                get => current;
                set
                {
                    current.UnbindBindings();
                    current.BindTo(value);
                }
            }

            public PatternCheckboxes()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Full;
                Spacing = new Vector2(0, 4);

                foreach (var pattern in patterns)
                {
                    var checkbox = new OsuCheckbox(nubOnRight: false, nubSize: 12)
                    {
                        Width = 0.5f,
                        LabelText = pattern.Name,
                    };

                    checkbox.Current.BindValueChanged(value => current.Value = value.NewValue ? current.Value | pattern.Mask : current.Value & ~pattern.Mask);
                    current.BindValueChanged(value => checkbox.Current.Value = (value.NewValue & pattern.Mask) != 0, true);
                    current.BindDisabledChanged(disabled => checkbox.Current.Disabled = disabled, true);
                    Add(checkbox);
                }
            }
        }
    }
}
