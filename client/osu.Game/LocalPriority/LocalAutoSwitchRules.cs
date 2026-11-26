// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;

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
    /// Every field is a comma-separated list that matches when any of its entries does (see <see cref="LocalAutoSwitchCondition"/>).
    /// </summary>
    public class LocalAutoSwitchRules
    {
        /// <summary>
        /// The beatmap needs at least one of these tags (local tags, voted tags or the mapper's tags), e.g. "skillset/jumps".
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Star rating condition, e.g. "5-7", "&gt;6.5" or "4, 6".
        /// </summary>
        public string Stars { get; set; } = string.Empty;

        /// <summary>
        /// BPM condition, e.g. "180, 190" or "&gt;=200".
        /// </summary>
        public string Bpm { get; set; } = string.Empty;

        /// <summary>
        /// Length condition in seconds, e.g. "&lt;90" or "60-240".
        /// </summary>
        public string Length { get; set; } = string.Empty;

        /// <summary>
        /// Parts of the mapper's name (any of them).
        /// </summary>
        public string Mapper { get; set; } = string.Empty;

        /// <summary>
        /// Parts of the title, artist or difficulty name (any of them).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        // Rules saved before the conditions were text kept min/max numbers. They are read once and turned into the text fields.
        [JsonProperty]
        private double? MinStars { set => legacyStars.min = value; }

        [JsonProperty]
        private double? MaxStars { set => legacyStars.max = value; }

        [JsonProperty]
        private double? MinBpm { set => legacyBpm.min = value; }

        [JsonProperty]
        private double? MaxBpm { set => legacyBpm.max = value; }

        [JsonProperty]
        private double? MinLength { set => legacyLength.min = value; }

        [JsonProperty]
        private double? MaxLength { set => legacyLength.max = value; }

        private (double? min, double? max) legacyStars, legacyBpm, legacyLength;

        [OnDeserialized]
        private void onDeserialized(StreamingContext context)
        {
            static string upgrade(string current, (double? min, double? max) old) =>
                string.IsNullOrWhiteSpace(current) ? LocalAutoSwitchCondition.FromRange(old.min, old.max) : current;

            Stars = upgrade(Stars, legacyStars);
            Bpm = upgrade(Bpm, legacyBpm);
            Length = upgrade(Length, legacyLength);
            legacyStars = legacyBpm = legacyLength = default;
        }

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
            Tags.Count > 0 || !string.IsNullOrWhiteSpace(Stars) || !string.IsNullOrWhiteSpace(Bpm) || !string.IsNullOrWhiteSpace(Length)
            || !string.IsNullOrWhiteSpace(Mapper) || !string.IsNullOrWhiteSpace(Name) || Mods.Count > 0;

        /// <param name="beatmap">The selected beatmap.</param>
        /// <param name="selectedMods">The selected mods (only needed for rules with mods).</param>
        /// <param name="ruleset">The current ruleset, to read the rule's mods with (only needed for rules with mods).</param>
        public bool Matches(BeatmapInfo beatmap, IReadOnlyList<Mod> selectedMods, Ruleset? ruleset)
        {
            if (!HasAnyCondition)
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

            // Compared the way song select shows them: stars to two decimals, whole BPM, whole seconds.
            if (!LocalAutoSwitchCondition.MatchesNumber(Stars, Math.Round(beatmap.StarRating, 2)))
                return false;

            if (!LocalAutoSwitchCondition.MatchesNumber(Bpm, FormatUtils.RoundBPM(beatmap.BPM)))
                return false;

            if (!LocalAutoSwitchCondition.MatchesNumber(Length, Math.Floor(beatmap.Length / 1000)))
                return false;

            if (!LocalAutoSwitchCondition.MatchesText(Mapper, beatmap.Metadata.Author.Username))
                return false;

            if (!LocalAutoSwitchCondition.MatchesText(Name, beatmap.Metadata.Title, beatmap.Metadata.TitleUnicode,
                    beatmap.Metadata.Artist, beatmap.Metadata.ArtistUnicode, beatmap.DifficultyName))
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

        /// <summary>
        /// Short description, e.g. "tags skillset/jumps · 5–7★".
        /// </summary>
        public string Describe()
        {
            var parts = new List<string>();

            if (Tags.Count > 0) parts.Add($@"tags {string.Join(@", ", Tags)}");
            if (!string.IsNullOrWhiteSpace(Stars)) parts.Add($@"{Stars}★");
            if (!string.IsNullOrWhiteSpace(Bpm)) parts.Add($@"{Bpm} BPM");
            if (!string.IsNullOrWhiteSpace(Length)) parts.Add($@"{Length}s");
            if (!string.IsNullOrWhiteSpace(Mapper)) parts.Add($@"mapper ""{Mapper}""");
            if (!string.IsNullOrWhiteSpace(Name)) parts.Add($@"name ""{Name}""");
            if (Mods.Count > 0) parts.Add($@"mods {DescribeMods()}");

            return parts.Count == 0 ? @"no conditions" : string.Join(@" · ", parts);
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
