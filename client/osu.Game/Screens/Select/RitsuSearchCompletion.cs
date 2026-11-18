// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Select
{
    internal static class RitsuSearchCompletion
    {
        private static readonly string[] keys =
        {
            "stars", "ar", "hp", "cs", "od", "bpm", "length", "lastplayed", "ranked", "submitted", "played",
            "divisor", "status", "mapper", "artist", "title", "diff", "source", "tag", "pbmisses", "playedthisweek",
        };

        private static readonly Regex token = new Regex(@"\b(?<key>\w+)(?<op>[!<>]?[=:]|[<>])?(?<value>[^\s]*)$", RegexOptions.Compiled);

        public static Completion[] Get(string query, int caret)
        {
            var match = token.Match(query[..caret]);
            int start = match.Success ? match.Index : caret;
            var key = match.Groups["key"];
            var op = match.Groups["op"];
            var value = match.Groups["value"];
            int end = caret;
            while (end < query.Length && !char.IsWhiteSpace(query[end]))
                end++;

            IEnumerable<string> suggestions;
            if (!op.Success)
            {
                string[] operators = key.Value.ToLowerInvariant() switch
                {
                    "lastplayed" => new[] { "<", "<=", ">", ">=" },
                    "played" or "playedthisweek" or "status" => new[] { "=", "!=" },
                    _ => new[] { "=", "<", "<=", ">", ">=" },
                };
                suggestions = keys.Contains(key.Value, StringComparer.OrdinalIgnoreCase)
                    ? operators.Select(o => key.Value + o)
                    : keys.Where(k => k.StartsWith(key.Value, StringComparison.OrdinalIgnoreCase)).Select(k => k + (k == "lastplayed" ? ">" : "="));
            }
            else
            {
                string[] values = key.Value.ToLowerInvariant() switch
                {
                    "played" or "playedthisweek" => new[] { "yes", "no" },
                    "status" => Enum.GetNames<BeatmapOnlineStatus>(),
                    "length" => new[] { "30s", "1m", "2m", "5m" },
                    "lastplayed" => new[] { "1d", "7d", "1M", "1y" },
                    "pbmisses" => new[] { "0", "1", "2", "3" },
                    _ => Array.Empty<string>(),
                };
                suggestions = values.Where(v => v.StartsWith(value.Value, StringComparison.OrdinalIgnoreCase))
                                    .Select(v => key.Value + op.Value + v);
            }

            return suggestions.Where(s => s != query[start..end]).Take(8).Select(s =>
            {
                bool complete = !s.EndsWith('=') && !s.EndsWith('<') && !s.EndsWith('>');
                string insert = complete && end == query.Length ? s + " " : s;
                return new Completion(s, query[..start] + insert + query[end..], start + insert.Length);
            }).ToArray();
        }

        public sealed record Completion(string Label, string Query, int Caret);
    }
}
