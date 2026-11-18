using System.Diagnostics;
using Newtonsoft.Json;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Offline;

internal static class ProfileWriteMeasurements
{
    public static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ritsu-profile-write-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (int count in new[] { 100, 10_000, 100_000 })
                measure(Path.Combine(directory, count.ToString()), count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void measure(string directory, int count)
    {
        var storage = new NativeStorage(directory);
        var profile = new RitsuProfile { Id = Guid.NewGuid(), Name = "Performance fixture" };
        var seed = new RitsuProfileManager.ProfileStore
        {
            ActiveProfileId = profile.Id,
            Profiles = new List<RitsuProfile> { profile },
        };

        for (int i = 0; i < count; i++)
        {
            seed.Scores.Add(Guid.NewGuid(), new RitsuScoreMetadata
            {
                ProfileId = profile.Id,
                Ruleset = "osu",
                CalculatorVersion = 1,
                ModsJson = """[{"acronym":"HD"},{"acronym":"DT"}]""",
            });
        }

        string filename = storage.GetStorageForDirectory("ritsu").GetFullPath("profiles.json", true);
        using (var writer = File.CreateText(filename))
            JsonSerializer.CreateDefault().Serialize(writer, seed);

        using var realm = new RealmAccess(storage, "scores");
        var manager = new RitsuProfileManager(storage, realm);
        manager.AssociateNewScore(Guid.NewGuid(), profile.Id, "osu", 1, "[]");
        var timings = new List<double>();
        var allocations = new List<long>();
        for (int i = 0; i < 9; i++)
        {
            Guid id = Guid.NewGuid();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            manager.AssociateNewScore(id, profile.Id, "osu", 1, "[]");
            timings.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            allocations.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
        }

        timings.Sort();
        allocations.Sort();
        var reloaded = new RitsuProfileManager(storage, realm);
        if (reloaded.GetProfiles().Single().Id != profile.Id || seed.Scores.Keys.Any(id => reloaded.GetScoreProfileId(id) != profile.Id))
            throw new InvalidDataException("Profile associations did not survive reopening.");

        Console.WriteLine(FormattableString.Invariant($"{count,6} scores | {new FileInfo(filename).Length,9} bytes | median {timings[4],7:F2} ms | max {timings[^1],7:F2} ms | median {allocations[4],8} bytes allocated"));
    }
}
