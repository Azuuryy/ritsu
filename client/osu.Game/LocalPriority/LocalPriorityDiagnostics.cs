// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Switches to turn Ritsu features off one by one at launch, to find which one costs performance
    /// (compare the debug graphs with clean osu!lazer). Set before starting the game, comma separated:
    /// <code>OSU_LOCAL_DISABLE=tags,autoswitch</code> (or use local-priority/osu-local-diagnose.sh / .cmd).
    /// </summary>
    public static class LocalPriorityDiagnostics
    {
        public const string ENVIRONMENT_VARIABLE = @"OSU_LOCAL_DISABLE";

        /// <summary>
        /// Every switch, with what it turns off.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> FEATURES = new Dictionary<string, string>
        {
            [@"tags"] = @"local beatmap tags (song select details row and search)",
            [@"autoswitch"] = @"profile auto-switching on beatmap changes",
            [@"avatars"] = @"local profile pictures on leaderboards / results",
            [@"modrules"] = @"profile mod rules (enforcing, greying out mods, settings reset at play start)",
            [@"leaderboard"] = @"forcing the Local leaderboard while the network is off",
            [@"switcher-menu"] = @"the full-screen right-click menu layer around the profile switcher",
            [@"profile-ui"] = @"the local profile page and the profile switcher (not loaded at all)",
            [@"toolbar"] = @"the top bar's local profile name / picture updates",
            [@"recording"] = @"recording plays for local profile statistics",
        };

        private static readonly HashSet<string> disabled = parse();

        private static HashSet<string> parse()
        {
            string value = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE) ?? string.Empty;

            var set = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim().ToLowerInvariant())
                           .ToHashSet();

            if (set.Contains(@"all"))
                set.UnionWith(FEATURES.Keys);

            return set;
        }

        /// <summary>
        /// Whether a feature was turned off with <see cref="ENVIRONMENT_VARIABLE"/>.
        /// </summary>
        public static bool IsDisabled(string feature) => disabled.Contains(feature);

        /// <summary>
        /// Written to the log at startup, so a test run shows what was off.
        /// </summary>
        public static void LogState()
        {
            if (disabled.Count == 0)
                return;

            Logger.Log($@"Ritsu diagnostics: turned off: {string.Join(@", ", disabled.OrderBy(d => d))}", LoggingTarget.Runtime, LogLevel.Important);
        }
    }
}
