// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// The text conditions of an auto-switch rule.
    /// <para>
    /// Every field is a comma-separated list, and the field matches when ANY of its entries matches ("180, 190" = 180 BPM or 190 BPM, nothing else).
    /// </para>
    /// <para>
    /// A number entry is "6" / "=6", "&lt;7", "&lt;=7", "&gt;5", "&gt;=5", "5-7", "5+" or "-7". Several comparisons in one entry all have to hold
    /// ("&gt;5 &lt;7" = above 5 and below 7). Decimals use a dot ("5.5"), since the comma separates entries.
    /// </para>
    /// </summary>
    public static class LocalAutoSwitchCondition
    {
        private enum Op
        {
            Equal,
            Less,
            LessOrEqual,
            Greater,
            GreaterOrEqual,
        }

        /// <summary>
        /// The entries of a comma-separated field, trimmed, without empty ones.
        /// </summary>
        public static List<string> Split(string? text) =>
            (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        /// <summary>
        /// Whether a text field (mapper, name) matches: any of its entries is a part of one of the texts.
        /// An empty field always matches.
        /// </summary>
        public static bool MatchesText(string? condition, params string?[] texts)
        {
            var parts = Split(condition);

            return parts.Count == 0
                   || parts.Any(part => texts.Any(t => t?.Contains(part, StringComparison.OrdinalIgnoreCase) == true));
        }

        /// <summary>
        /// Whether a number field matches <paramref name="value"/>. An empty field always matches; one that can't be read never does.
        /// </summary>
        public static bool MatchesNumber(string? condition, double value)
        {
            if (Split(condition).Count == 0)
                return true;

            if (!tryParse(condition!, out var alternatives, out _))
                return false;

            return alternatives.Any(all => all.All(c => test(c.op, c.value, value)));
        }

        /// <summary>
        /// Checks a number field and tidies the commas ("&gt;=5 ,180" → "&gt;=5, 180"). Returns false with the entry that can't be read.
        /// </summary>
        public static bool TryNormaliseNumber(string? text, out string normalised, out string? badEntry)
        {
            normalised = string.Empty;

            if (!tryParse(text ?? string.Empty, out var alternatives, out badEntry))
                return false;

            // Keep the way each entry was written ("5-7" stays "5-7"), only the spacing around the commas is tidied.
            normalised = string.Join(@", ", Split(text));
            return true;
        }

        /// <summary>
        /// The old min/max range as a number field: "5-7", "&gt;=5", "&lt;=7", "6" or "".
        /// </summary>
        public static string FromRange(double? min, double? max)
        {
            if (min != null && max != null)
                return min == max ? f(min.Value) : $@"{f(min.Value)}-{f(max.Value)}";
            if (min != null)
                return $@">={f(min.Value)}";
            if (max != null)
                return $@"<={f(max.Value)}";

            return string.Empty;
        }

        private static string f(double v) => v.ToString(@"0.##", CultureInfo.InvariantCulture);

        private static bool test(Op op, double target, double value) => op switch
        {
            Op.Less => value < target,
            Op.LessOrEqual => value <= target,
            Op.Greater => value > target,
            Op.GreaterOrEqual => value >= target,
            _ => Math.Abs(value - target) < 1e-9,
        };

        // "5-7" (a range, also with an en dash), or one comparison: optional operator, then a number, optionally followed by "+".
        private static readonly Regex range_regex = new Regex(@"^(\d+(?:\.\d+)?)\s*[-–]\s*(\d+(?:\.\d+)?)$", RegexOptions.Compiled);

        private static readonly Regex comparison_regex = new Regex(@"\G\s*(<=|>=|≤|≥|=<|=>|<|>|=|[-–])?\s*(\d+(?:\.\d+)?)(\+)?\s*", RegexOptions.Compiled);

        private static bool tryParse(string text, out List<List<(Op op, double value)>> alternatives, out string? badEntry)
        {
            alternatives = new List<List<(Op, double)>>();
            badEntry = null;

            foreach (string entry in Split(text))
            {
                var all = new List<(Op, double)>();

                var range = range_regex.Match(entry);

                if (range.Success)
                {
                    double a = num(range.Groups[1].Value), b = num(range.Groups[2].Value);
                    all.Add((Op.GreaterOrEqual, Math.Min(a, b)));
                    all.Add((Op.LessOrEqual, Math.Max(a, b)));
                    alternatives.Add(all);
                    continue;
                }

                int position = 0;

                while (position < entry.Length)
                {
                    var m = comparison_regex.Match(entry, position);

                    if (!m.Success || m.Length == 0)
                    {
                        badEntry = entry;
                        return false;
                    }

                    string op = m.Groups[1].Value;
                    bool plus = m.Groups[3].Success;
                    double value = num(m.Groups[2].Value);

                    if (plus && op.Length > 0)
                    {
                        badEntry = entry;
                        return false;
                    }

                    all.Add((plus ? Op.GreaterOrEqual : op switch
                    {
                        "<" => Op.Less,
                        "<=" or "=<" or "≤" or "-" or "–" => Op.LessOrEqual,
                        ">" => Op.Greater,
                        ">=" or "=>" or "≥" => Op.GreaterOrEqual,
                        _ => Op.Equal,
                    }, value));

                    position += m.Length;
                }

                alternatives.Add(all);
            }

            return true;
        }

        private static double num(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
