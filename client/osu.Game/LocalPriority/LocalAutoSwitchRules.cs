// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// How the selected mods have to line up with an auto-switch rule's mods.
    /// </summary>
    public enum LocalAutoSwitchModMatch
    {
        /// <summary>
        /// Every one of the rule's mods is selected (other mods may be selected too).
        /// </summary>
        All,

        /// <summary>
        /// At least one of the rule's mods is selected.
        /// </summary>
        Any,

        /// <summary>
        /// Exactly the rule's mods are selected, nothing more and nothing less.
        /// </summary>
        Exact,
    }

    /// <summary>
    /// When a profile becomes active by itself: selecting a beatmap in song select that matches every condition that is filled in.
    /// </summary>
    public class LocalAutoSwitchRules
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The beatmap needs at least one of these tags (local tags, voted tags or the mapper's tags), e.g. "skillset/jumps".
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        public double? MinStars { get; set; }
        public double? MaxStars { get; set; }

        public double? MinBpm { get; set; }
        public double? MaxBpm { get; set; }

        /// <summary>
        /// Length in seconds.
        /// </summary>
        public double? MinLength { get; set; }

        public double? MaxLength { get; set; }

        /// <summary>
        /// Part of the mapper's name.
        /// </summary>
        public string Mapper { get; set; } = string.Empty;

        /// <summary>
        /// Part of the title, artist or difficulty name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Mods that have to be selected (picked in the mod rules picker; how they have to match: <see cref="ModMatch"/>).
        /// </summary>
        public List<APIMod> Mods { get; set; } = new List<APIMod>();

        public LocalAutoSwitchModMatch ModMatch { get; set; } = LocalAutoSwitchModMatch.All;

        /// <summary>
        /// The mods only count when their settings are exactly the picked ones (e.g. Double Time at 1.3x only).
        /// </summary>
        public bool ExactModSettings { get; set; }

        public bool HasAnyCondition =>
            Tags.Count > 0 || MinStars != null || MaxStars != null || MinBpm != null || MaxBpm != null || MinLength != null || MaxLength != null
            || !string.IsNullOrWhiteSpace(Mapper) || !string.IsNullOrWhiteSpace(Name) || Mods.Count > 0;

        /// <param name="beatmap">The selected beatmap.</param>
        /// <param name="selectedMods">The selected mods (only needed for rules with mods).</param>
        /// <param name="ruleset">The current ruleset, to read the rule's mods with (only needed for rules with mods).</param>
        public bool Matches(BeatmapInfo beatmap, IReadOnlyList<Mod> selectedMods, Ruleset? ruleset)
        {
            if (!Enabled || !HasAnyCondition)
                return false;

            if (Mods.Count > 0 && !modsMatch(selectedMods, ruleset))
                return false;

            if (Tags.Count > 0)
            {
                var beatmapTags = (LocalBeatmapTags.Current?.Get(beatmap) ?? Array.Empty<string>())
                                  .Concat(beatmap.Metadata.UserTags)
                                  .Concat(beatmap.Metadata.Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                  .Select(LocalBeatmapTags.Normalise)
                                  .ToHashSet();

                if (!Tags.Any(t => beatmapTags.Contains(LocalBeatmapTags.Normalise(t))))
                    return false;
            }

            if (!inRange(beatmap.StarRating, MinStars, MaxStars))
                return false;

            if (!inRange(beatmap.BPM, MinBpm, MaxBpm))
                return false;

            if (!inRange(beatmap.Length / 1000, MinLength, MaxLength))
                return false;

            if (!string.IsNullOrWhiteSpace(Mapper) && !contains(beatmap.Metadata.Author.Username, Mapper))
                return false;

            if (!string.IsNullOrWhiteSpace(Name)
                && !contains(beatmap.Metadata.Title, Name) && !contains(beatmap.Metadata.TitleUnicode, Name)
                && !contains(beatmap.Metadata.Artist, Name) && !contains(beatmap.Metadata.ArtistUnicode, Name)
                && !contains(beatmap.DifficultyName, Name))
                return false;

            return true;
        }

        private bool modsMatch(IReadOnlyList<Mod> selectedMods, Ruleset? ruleset)
        {
            if (ruleset == null)
                return false;

            // Autoplay / Cinema (Ctrl+Enter) and system mods never decide anything.
            var selected = selectedMods.Where(m => m is not ModAutoplay && m.Type != ModType.System).ToArray();

            // Mods this ruleset doesn't have can never be selected.
            var wanted = Mods.Select(m => m.ToMod(ruleset)).ToArray();

            bool isSelected(Mod want)
            {
                if (want is UnknownMod)
                    return false;

                var found = selected.FirstOrDefault(s => s.GetType() == want.GetType());
                return found != null && (!ExactModSettings || new APIMod(found).Equals(new APIMod(want)));
            }

            switch (ModMatch)
            {
                case LocalAutoSwitchModMatch.Any:
                    return wanted.Any(isSelected);

                case LocalAutoSwitchModMatch.Exact:
                    return wanted.All(isSelected) && selected.Length == wanted.Select(w => w.GetType()).Distinct().Count();

                default:
                    return wanted.All(isSelected);
            }
        }

        /// <summary>
        /// "all of HD, HR", "any of HD, HR" or "exactly HD, HR", plus "(exact settings)".
        /// </summary>
        public string DescribeMods()
        {
            if (Mods.Count == 0)
                return @"no mods";

            string mods = string.Join(@", ", Mods.Select(m => m.Acronym));
            string match = ModMatch switch
            {
                LocalAutoSwitchModMatch.Any => Mods.Count == 1 ? mods : $@"any of {mods}",
                LocalAutoSwitchModMatch.Exact => $@"exactly {mods}",
                _ => Mods.Count == 1 ? mods : $@"all of {mods}",
            };

            return ExactModSettings ? $@"{match} (exact settings)" : match;
        }

        private static bool inRange(double value, double? min, double? max) => (min == null || value >= min) && (max == null || value <= max);

        private static bool contains(string? text, string part) => text?.Contains(part.Trim(), StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Short description, e.g. "tags skillset/jumps · 5–7★".
        /// </summary>
        public string Describe()
        {
            var parts = new List<string>();

            if (Tags.Count > 0) parts.Add($@"tags {string.Join(@", ", Tags)}");
            if (MinStars != null || MaxStars != null) parts.Add($@"{FormatRange(MinStars, MaxStars)}★");
            if (MinBpm != null || MaxBpm != null) parts.Add($@"{FormatRange(MinBpm, MaxBpm)} BPM");
            if (MinLength != null || MaxLength != null) parts.Add($@"{FormatRange(MinLength, MaxLength)}s");
            if (!string.IsNullOrWhiteSpace(Mapper)) parts.Add($@"mapper ""{Mapper}""");
            if (!string.IsNullOrWhiteSpace(Name)) parts.Add($@"name ""{Name}""");
            if (Mods.Count > 0) parts.Add($@"mods {DescribeMods()}");

            return parts.Count == 0 ? @"no conditions" : string.Join(@" · ", parts);
        }

        /// <summary>
        /// "5-7", "5+", "-7", "6" (exactly) or "" for no limit.
        /// </summary>
        public static string FormatRange(double? min, double? max)
        {
            string f(double v) => v.ToString(@"0.##", CultureInfo.InvariantCulture);

            if (min != null && max != null)
                return min == max ? f(min.Value) : $@"{f(min.Value)}-{f(max.Value)}";
            if (min != null)
                return $@"{f(min.Value)}+";
            if (max != null)
                return $@"-{f(max.Value)}";

            return string.Empty;
        }

        /// <summary>
        /// Reads "5-7", "5+", "-7" or "6". Returns false for text that isn't a range.
        /// </summary>
        public static bool TryParseRange(string text, out double? min, out double? max)
        {
            min = max = null;
            text = (text ?? string.Empty).Trim().Replace(',', '.').Replace('–', '-');

            if (text.Length == 0)
                return true;

            static bool num(string s, out double v) => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

            if (text.EndsWith('+') && num(text[..^1], out double atLeast))
            {
                min = atLeast;
                return true;
            }

            int dash = text.IndexOf('-', 1);

            if (text.StartsWith('-') && num(text[1..], out double atMost))
            {
                max = atMost;
                return true;
            }

            if (dash > 0 && num(text[..dash], out double from) && num(text[(dash + 1)..], out double to))
            {
                min = Math.Min(from, to);
                max = Math.Max(from, to);
                return true;
            }

            if (num(text, out double exact))
            {
                min = max = exact;
                return true;
            }

            return false;
        }

        public LocalAutoSwitchRules Clone()
        {
            var clone = (LocalAutoSwitchRules)MemberwiseClone();
            clone.Tags = new List<string>(Tags);
            clone.Mods = Mods.Select(m => new APIMod { Acronym = m.Acronym, Settings = new Dictionary<string, object>(m.Settings) }).ToList();
            return clone;
        }
    }
}
