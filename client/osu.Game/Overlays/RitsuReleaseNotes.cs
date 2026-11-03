using System.IO;

namespace osu.Game.Overlays
{
    public static class RitsuReleaseNotes
    {
        public static string Version { get; } = read("Ritsu.VERSION", "unknown").Trim();
        public static string Current { get; } = read("Ritsu.CHANGELOG.md", "Release notes are unavailable.");

        private static string read(string name, string fallback)
        {
            using var stream = typeof(RitsuReleaseNotes).Assembly.GetManifestResourceStream(name);
            if (stream == null) return fallback;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
