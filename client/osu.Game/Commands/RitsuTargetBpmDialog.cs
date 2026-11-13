// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Commands
{
    internal partial class RitsuTargetBpmDialog : PopupDialog
    {
        public RitsuTargetBpmDialog(IBeatmap beatmap, Ruleset ruleset, IReadOnlyList<Mod> selected, Action<Mod[]> apply, double? initialTarget = null,
                                    bool keepApproachRate = false, Func<bool>? canApply = null)
        {
            HeaderText = "Set target BPM";
            Icon = FontAwesome.Solid.Music;

            double referenceBpm = 60000 / beatmap.GetMostCommonBeatLength();
            var segments = beatmap.ControlPointInfo.TimingPoints
                                  .Where(point => double.IsFinite(point.BPM) && point.BPM > 0)
                                  .Select(point => new Segment(point.Time, point.BPM)).ToArray();
            if (segments.Length == 0)
                segments = new[] { new Segment(0, referenceBpm) };

            bool variable = segments.Select(s => s.Bpm).Distinct().Skip(1).Any();
            var section = new OsuDropdown<Segment>
            {
                RelativeSizeAxes = Axes.X,
                Items = segments,
                Current = { Value = segments.MinBy(s => Math.Abs(s.Bpm - referenceBpm))! },
            };
            var target = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                PlaceholderText = "Target BPM",
                ReleaseFocusOnCommit = false,
            };
            if (initialTarget.HasValue)
                target.Text = initialTarget.Value.ToString(CultureInfo.CurrentCulture);

            var keepAr = new OsuCheckbox
            {
                LabelText = "Keep original approach rate",
                Current = { Value = keepApproachRate },
            };
            bool supportsAr = (ruleset.CreateModFromAcronym("DA") as ModDifficultyAdjust)?.RitsuApproachRate != null;
            var preview = new OsuSpriteText { RelativeSizeAxes = Axes.X };
            var save = new PopupDialogOkButton { Text = "Apply", HideDialogOnAction = false };
            Mod[]? result = null;

            void refresh()
            {
                if (!TryParseBpm(target.Text, out double bpm))
                {
                    result = null;
                    preview.Text = $"Reference {section.Current.Value.Bpm:0.##} BPM. Enter a positive target.";
                }
                else
                {
                    double reference = section.Current.Value.Bpm;
                    string? error = TryBuildMods(ruleset, selected, reference, bpm, out result, out double achieved);
                    double? effectiveAr = null;

                    if (error == null && keepAr.Current.Value)
                        error = TryCompensateAr(ruleset, beatmap.Difficulty.ApproachRate, achieved / reference, ref result, out effectiveAr);

                    string ar = effectiveAr.HasValue ? $" · effective AR {effectiveAr:0.##}" : string.Empty;
                    preview.Text = error ?? $"Playback BPM {achieved:0.##} · {achieved / reference:0.##}×{ar}";
                }
            }

            void confirm()
            {
                if (result == null)
                    return;
                if (canApply?.Invoke() == false)
                {
                    preview.Text = "The selected difficulty, mode or mods changed. Reopen this dialog.";
                    return;
                }

                apply(result);
                Hide();
            }

            save.Action = confirm;
            target.OnCommit += (_, _) => confirm();
            target.Current.ValueChanged += _ => refresh();
            section.Current.ValueChanged += _ => refresh();
            keepAr.Current.ValueChanged += _ => refresh();
            var children = new List<Drawable>();
            if (variable)
                children.Add(section);
            children.Add(target);
            if (supportsAr)
                children.Add(keepAr);
            children.Add(preview);

            MainContent.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = 35, Vertical = 8 },
                Spacing = new Vector2(0, 6),
                ChildrenEnumerable = children,
            });

            Buttons = new PopupDialogButton[] { save, new PopupDialogCancelButton { Text = "Cancel" } };
            refresh();
        }

        internal static bool TryParseBpm(string input, out double bpm) =>
            double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out bpm)
            || double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out bpm);

        internal static string? TryBuildMods(Ruleset ruleset, IReadOnlyList<Mod> selected, double reference, double target,
                                             out Mod[]? result, out double achieved)
        {
            result = null;
            achieved = 0;
            if (!double.IsFinite(target) || !double.IsFinite(reference) || target <= 0 || reference <= 0)
                return "Enter a positive target BPM.";

            double rate = Math.Round(target / reference, 2, MidpointRounding.AwayFromZero);
            var mods = selected.Where(mod => mod is not ModRateAdjust).ToList();
            if (mods.Any(mod => mod is IApplicableToRate))
                return "Remove the variable speed mod before setting a target BPM.";

            if (rate != 1)
            {
                if (ruleset.CreateModFromAcronym(rate > 1 ? "DT" : "HT") is not ModRateAdjust speed)
                    return "This mode does not support changing the rate.";
                if (rate < speed.SpeedChange.MinValue || rate > speed.SpeedChange.MaxValue)
                    return "The target BPM is outside the available speed range.";

                speed.SpeedChange.Value = rate;
                mods.Add(speed);
            }

            if (!ModUtils.CheckValidForGameplay(mods, out _))
                return "The target rate conflicts with the selected mods.";

            achieved = reference * rate;
            result = mods.ToArray();
            return null;
        }

        internal static string? TryCompensateAr(Ruleset ruleset, float originalAr, double rate, ref Mod[]? mods, out double? effectiveAr)
        {
            effectiveAr = null;
            var result = mods!.ToList();
            mods = null;
            var existing = result.OfType<ModDifficultyAdjust>().FirstOrDefault();
            var adjust = (ModDifficultyAdjust?)existing?.DeepClone() ?? ruleset.CreateModFromAcronym("DA") as ModDifficultyAdjust;
            if (adjust?.RitsuApproachRate is not { } ar)
                return "This mode cannot preserve approach rate.";

            double originalPreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(originalAr, ar.PreemptRange);
            double targetPreempt = originalPreempt * rate;
            float configuredAr = (float)IBeatmapDifficultyInfo.InverseDifficultyRange(targetPreempt, ar.PreemptRange);
            float min = adjust.ExtendedLimits.Value ? ar.Setting.ExtendedMinValue ?? ar.Setting.MinValue : ar.Setting.MinValue;
            float max = adjust.ExtendedLimits.Value ? ar.Setting.ExtendedMaxValue ?? ar.Setting.MaxValue : ar.Setting.MaxValue;
            if (!float.IsFinite(configuredAr) || configuredAr < min || configuredAr > max)
                return "Keeping the original approach rate would exceed Difficulty Adjust limits.";

            ar.Setting.Value = configuredAr;
            if (existing != null)
                result[result.IndexOf(existing)] = adjust;
            else
                result.Add(adjust);

            if (!ModUtils.CheckValidForGameplay(result, out _))
                return "Difficulty Adjust conflicts with the selected mods.";

            int appliedPreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(ar.Setting.Value!.Value, ar.PreemptRange);
            // The inverse is continuous, while hit objects use integer milliseconds. A float
            // rounded just below an integer boundary can introduce one additional millisecond.
            if (appliedPreempt < Math.Floor(targetPreempt) && configuredAr > min)
            {
                ar.Setting.Value = MathF.BitDecrement(configuredAr);
                appliedPreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(ar.Setting.Value!.Value, ar.PreemptRange);
            }

            double actualPreempt = appliedPreempt / rate;
            effectiveAr = IBeatmapDifficultyInfo.InverseDifficultyRange(actualPreempt, ar.PreemptRange);
            mods = result.ToArray();
            return null;
        }

        private sealed record Segment(double Time, double Bpm)
        {
            public override string ToString() => $"{TimeSpan.FromMilliseconds(Math.Max(0, Time)):mm\\:ss} · {Bpm:0.##} BPM";
        }
    }
}
