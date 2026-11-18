// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;

namespace osu.Game.Commands
{
    internal sealed record RitsuCommand(
        string Id, string Alias, string Description, string Options, Func<bool> Available,
        Func<string, RitsuCommandInvocation> Bind, Func<RitsuCommandChoice[]>? Choices = null);

    internal sealed record RitsuCommandChoice(string Id, string Title);

    internal sealed record RitsuCommandInvocation(Action? Run, string? Error = null, bool KeepOpen = false)
    {
        public static RitsuCommandInvocation Invalid(string message) => new RitsuCommandInvocation(null, message);
    }

    internal sealed record RitsuPaletteEntry(RitsuCommand Command, RitsuCommandChoice? Choice)
    {
        public string Title => Choice?.Title ?? Command.Description;
        public string Alias => Command.Alias;
    }

    internal sealed class RitsuCommandRegistry
    {
        private readonly RitsuCommand[] commands;

        public RitsuCommandRegistry(params RitsuCommand[] commands)
        {
            this.commands = commands;
            if (commands.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != commands.Length
                || commands.Select(c => c.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count() != commands.Length)
                throw new ArgumentException("Command IDs and aliases must be unique.", nameof(commands));
        }

        public RitsuPaletteEntry[] Entries(string? prefix = null) =>
            commands.Where(c => (prefix == null || c.Alias.StartsWith(prefix, StringComparison.Ordinal)) && c.Available()).SelectMany(c => c.Choices == null
                ? new[] { new RitsuPaletteEntry(c, null) }
                : c.Choices().Select(choice => new RitsuPaletteEntry(c, choice))).ToArray();

        public RitsuCommand? Match(string input) =>
            commands.Where(c => input.StartsWith(c.Alias, StringComparison.OrdinalIgnoreCase)
                                && (input.Length == c.Alias.Length || char.IsWhiteSpace(input[c.Alias.Length])))
                    .OrderByDescending(c => c.Alias.Length).FirstOrDefault();

        public static RitsuCommandInvocation Bind(RitsuCommand command, string input) =>
            command.Available() ? command.Bind(input) : RitsuCommandInvocation.Invalid("This command is unavailable here.");
    }
}
