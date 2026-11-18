using System.Globalization;
using System.Security.Cryptography;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

internal static class DecoyMeasurements
{
    public static void Run(string path)
    {
        var ruleset = new OsuRuleset();
        using var reader = new LineBufferedReader(File.OpenRead(path));
        var beatmap = new LegacyBeatmapDecoder().Decode(reader);
        if (beatmap.BeatmapInfo.Ruleset.OnlineID != 0)
            throw new InvalidDataException("The selected beatmap must use osu!standard.");
        beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
        var working = new TestWorkingBeatmap(beatmap);
        var calculator = ruleset.CreateDifficultyCalculator(working);

        using var file = File.OpenRead(path);
        Console.WriteLine($"SHA256: {Convert.ToHexStringLower(SHA256.HashData(file))}");
        Console.WriteLine("Perfect-play estimates; changing the interval or appearance does not measure human difficulty.");
        Console.WriteLine("interval_beats,note_spacing_beats,appearance,objects,decoys,stars,pp,bonus_percent");
        foreach (int interval in new[] { 0, 2, 4, 8, 16, 32 })
        foreach (double spacing in interval == 0 ? new[] { 0.5 } : new[] { 0.5, 0.25 })
        foreach (var style in interval == 0 ? new[] { DecoyCircleStyle.RealFake } : new[] { DecoyCircleStyle.RealFake, DecoyCircleStyle.Grayscale })
        {
            Mod[] mods = interval == 0 ? Array.Empty<Mod>() : new Mod[] { new OsuModDecoyCircles { IntervalBeats = { Value = interval }, MinimumNoteSpacing = { Value = spacing }, Style = { Value = style } } };
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
            var difficulty = (OsuDifficultyAttributes)calculator.Calculate(mods);
            var processor = ruleset.CreateScoreProcessor();
            processor.Mods.Value = mods;
            processor.ApplyBeatmap(playable);
            var score = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Accuracy = 1,
                Mods = mods,
                MaxCombo = processor.MaximumCombo,
                Statistics = processor.MaximumStatistics,
                MaximumStatistics = processor.MaximumStatistics,
            };
            double pp = ruleset.CreatePerformanceCalculator().Calculate(score, difficulty).Total;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{interval},{spacing:F2},{(interval == 0 ? "none" : style.ToString())},{playable.HitObjects.Count},{playable.HitObjects.OfType<DecoyCircle>().Count()},{difficulty.StarRating:F4},{pp:F3},{difficulty.DecoyPerformanceBonus * 100:F3}"));
        }
    }
}
