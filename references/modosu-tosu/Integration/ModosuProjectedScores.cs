using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Modosu.Integration
{
    /// <summary>Constructs explicit hypothetical scores; the game's calculator still determines their PP.</summary>
    public static class ModosuProjectedScores
    {
        public static ScoreInfo Perfect(ScoreInfo current, int maximumCombo) => new ScoreInfo(current.BeatmapInfo!, current.Ruleset)
        {
            Passed = true,
            Mods = current.Mods,
            Accuracy = 1,
            MaxCombo = maximumCombo,
            Combo = maximumCombo,
            Statistics = new Dictionary<HitResult, int>(current.MaximumStatistics),
            MaximumStatistics = new Dictionary<HitResult, int>(current.MaximumStatistics)
        };

        public static (ScoreInfo remaining, ScoreInfo fc)? Create(ScoreInfo current, int maximumCombo, ScoreProcessor processor)
        {
            // Replay header seeks can restore aggregate statistics without the earlier judgement history.
            // Do not guess which slider tails were judged when that history is incomplete.
            if (current.HitEvents.Count != current.Statistics.Values.Sum()) return null;
            var judged = new Dictionary<HitResult, int>();
            foreach (var hit in current.HitEvents)
            {
                var maximum = hit.HitObject.Judgement.MaxResult;
                judged[maximum] = judged.GetValueOrDefault(maximum) + 1;
            }

            var remaining = Perfect(current, maximumCombo);
            remaining.Statistics = new Dictionary<HitResult, int>(current.Statistics);
            int remainingCombo = 0;
            foreach (var (result, total) in current.MaximumStatistics)
            {
                int count = Math.Max(0, total - judged.GetValueOrDefault(result));
                remaining.Statistics[result] = remaining.Statistics.GetValueOrDefault(result) + count;
                if (result.IncreasesCombo()) remainingCombo += count;
            }
            remaining.MaxCombo = Math.Min(maximumCombo, Math.Max(current.MaxCombo, current.Combo + remainingCombo));
            remaining.Combo = Math.Min(maximumCombo, current.Combo + remainingCombo);
            remaining.Accuracy = accuracy(remaining, processor);

            var fc = Perfect(current, maximumCombo);
            fc.Statistics = new Dictionary<HitResult, int>(remaining.Statistics);
            fc.Statistics[HitResult.Great] = fc.Statistics.GetValueOrDefault(HitResult.Great) + fc.Statistics.GetValueOrDefault(HitResult.Miss);
            fc.Statistics[HitResult.Miss] = 0;
            fc.Statistics[HitResult.ComboBreak] = 0;
            foreach (var result in new[] { HitResult.LargeTickHit, HitResult.SmallTickHit, HitResult.SliderTailHit })
                fc.Statistics[result] = current.MaximumStatistics.GetValueOrDefault(result);
            fc.Statistics[HitResult.LargeTickMiss] = 0;
            fc.Statistics[HitResult.SmallTickMiss] = 0;
            fc.Accuracy = accuracy(fc, processor);
            return (remaining, fc);
        }

        private static double accuracy(ScoreInfo score, ScoreProcessor processor)
        {
            double maximum = score.MaximumStatistics.Where(pair => pair.Key.AffectsAccuracy())
                                  .Sum(pair => (double)pair.Value * processor.GetBaseScoreForResult(pair.Key));
            double actual = score.Statistics.Where(pair => pair.Key.AffectsAccuracy())
                                 .Sum(pair => (double)pair.Value * processor.GetBaseScoreForResult(pair.Key));
            return maximum > 0 ? Math.Clamp(actual / maximum, 0, 1) : 1;
        }
    }
}
