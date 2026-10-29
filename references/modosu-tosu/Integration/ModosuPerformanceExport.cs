using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Modosu.Integration
{
    /// <summary>
    /// Runs the game's PP pipeline against the actual post-mod gameplay beatmap, independently of the skin.
    /// No custom mod is translated to a vanilla acronym or removed from the score.
    /// </summary>
    public sealed class ModosuPerformanceExport : IDisposable
    {
        private static readonly long processStartTime = new DateTimeOffset(Process.GetCurrentProcess().StartTime).ToUnixTimeMilliseconds();
        private readonly GameplayState state;
        private readonly Func<double> getTime;
        private readonly int retries;
        private readonly Action<ModosuPerformanceFrame> publish;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        private readonly CancellationTokenSource cancellation = new();
        private readonly PerformanceCalculator? calculator;
        private readonly ScoreInfo scoreInfo;
        private readonly Task<List<TimedDifficultyAttributes>> attributesTask;
        private long lastUpdate = long.MinValue;
        private double? lastJudgementTime;
        private bool disposed;
        private bool showingResults;
        private bool reportedError;
        private ModosuPerformanceFrame? resultFrame;
        private string? resultDate;
        private double? maximumPp;

        public Task<List<TimedDifficultyAttributes>> Ready => attributesTask;

        public ModosuPerformanceExport(GameplayState state, Func<double> getTime, int retries = 0,
                                      Action<ModosuPerformanceFrame>? publish = null)
        {
            this.state = state;
            this.getTime = getTime;
            this.retries = retries;
            this.publish = publish ?? ModosuPerformanceFile.Publish;
            if (publish == null)
                ModosuPerformanceFile.BeginSession(sessionId);

            // Use the same cloned mod types and post-mod beatmap as lazer's PerformancePointsCounter.
            Mod[] mods = state.Mods.Select(mod => mod.DeepClone()).ToArray();
            scoreInfo = new ScoreInfo(state.Beatmap.BeatmapInfo, state.Ruleset.RulesetInfo) { Mods = mods };
            calculator = state.Ruleset.CreatePerformanceCalculator();
            var workingBeatmap = new GameplayWorkingBeatmap(state.Beatmap);
            CancellationToken token = cancellation.Token;
            attributesTask = Task.Run(() => state.Ruleset.CreateDifficultyCalculator(workingBeatmap).CalculateTimed(mods, token), token);
            state.ScoreProcessor.NewJudgement += onJudgement;
            state.ScoreProcessor.JudgementReverted += onJudgement;
            state.ScoreProcessor.OnResetFromReplayFrame += onReplayReset;
            lastJudgementTime = state.LastJudgementResult.Value?.HitObject.GetEndTime();
            Update(force: true);
        }

        private void onJudgement(JudgementResult judgement) => lastJudgementTime = judgement.HitObject.GetEndTime();
        private void onReplayReset() => lastJudgementTime = getTime();

        public void Update(bool force = false)
        {
            if (disposed || showingResults && !force)
                return;

            long now = Environment.TickCount64;
            if (!force && now - lastUpdate < 100)
                return;
            lastUpdate = now;

            try
            {
                state.ScoreProcessor.PopulateScore(scoreInfo);
                List<TimedDifficultyAttributes>? attributes = attributesTask.IsCompletedSuccessfully ? attributesTask.Result : null;
                if (attributesTask.IsFaulted && !reportedError)
                {
                    Logger.Error(attributesTask.Exception!, "Could not calculate native Modosu difficulty for tosu.");
                    reportedError = true;
                }

                var fullAttributes = attributes?.LastOrDefault()?.Attributes;
                DifficultyAttributes? currentAttributes = null;
                if (attributes?.Count > 0 && lastJudgementTime.HasValue)
                {
                    int index = attributes.BinarySearch(new TimedDifficultyAttributes(lastJudgementTime.Value, null!));
                    if (index < 0)
                        index = ~index - 1;
                    currentAttributes = attributes[Math.Clamp(index, 0, attributes.Count - 1)].Attributes;
                }

                var usedAttributes = showingResults ? fullAttributes : currentAttributes;
                PerformanceAttributes? performance = usedAttributes == null ? null : calculator?.Calculate(scoreInfo, usedAttributes);
                var osuPerformance = performance as OsuPerformanceAttributes;
                PerformanceAttributes? fcPerformance = null;
                PerformanceAttributes? maxAchievablePerformance = null;
                if (fullAttributes != null && calculator != null)
                {
                    maximumPp ??= ModosuPerformanceFrame.Finite(calculator.Calculate(ModosuProjectedScores.Perfect(scoreInfo, fullAttributes.MaxCombo), fullAttributes).Total);
                    if (ModosuProjectedScores.Create(scoreInfo, fullAttributes.MaxCombo, state.ScoreProcessor) is { } projected)
                    {
                        fcPerformance = calculator.Calculate(projected.fc, fullAttributes);
                        maxAchievablePerformance = calculator.Calculate(projected.remaining, fullAttributes);
                    }
                }
                var frame = new ModosuPerformanceFrame
                {
                    ProcessId = Environment.ProcessId,
                    ProcessStartTime = processStartTime,
                    SessionId = sessionId,
                    BeatmapHash = state.Score.ScoreInfo.BeatmapInfo?.MD5Hash ?? string.Empty,
                    ModsJson = state.Score.ScoreInfo.ModsJson,
                    Phase = showingResults ? "results" : "play",
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Time = getTime(),
                    Retries = retries,
                    Date = resultDate ?? state.Score.ScoreInfo.Date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    Score = scoreInfo.TotalScore,
                    MaxCombo = scoreInfo.MaxCombo,
                    MaximumCombo = fullAttributes?.MaxCombo ?? 0,
                    Hits = new ModosuPerformanceHits
                    {
                        Great = scoreInfo.Statistics.GetValueOrDefault(HitResult.Great),
                        Ok = scoreInfo.Statistics.GetValueOrDefault(HitResult.Ok),
                        Meh = scoreInfo.Statistics.GetValueOrDefault(HitResult.Meh),
                        Miss = scoreInfo.Statistics.GetValueOrDefault(HitResult.Miss)
                    },
                    Pp = ModosuPerformanceFrame.Finite(performance?.Total ?? (fullAttributes != null ? 0 : null)),
                    MaximumPp = maximumPp,
                    FcPp = ModosuPerformanceFrame.Finite(fcPerformance?.Total),
                    MaxAchievablePp = ModosuPerformanceFrame.Finite(maxAchievablePerformance?.Total),
                    FcDetails = details(fcPerformance as OsuPerformanceAttributes),
                    LiveStars = ModosuPerformanceFrame.Finite(usedAttributes?.StarRating ?? (fullAttributes != null ? 0 : null)),
                    TotalStars = ModosuPerformanceFrame.Finite(fullAttributes?.StarRating),
                    Details = details(osuPerformance)
                };

                if (showingResults)
                    resultFrame = frame;
                publish(frame);
            }
            catch (Exception exception)
            {
                if (!reportedError)
                    Logger.Error(exception, "Could not export native Modosu performance for tosu.");
                reportedError = true;
            }
        }

        private static ModosuPerformanceDetails? details(OsuPerformanceAttributes? performance) => performance == null ? null : new ModosuPerformanceDetails
        {
            Aim = ModosuPerformanceFrame.Finite(performance.Aim) ?? 0,
            Speed = ModosuPerformanceFrame.Finite(performance.Speed) ?? 0,
            Accuracy = ModosuPerformanceFrame.Finite(performance.Accuracy) ?? 0,
            Flashlight = ModosuPerformanceFrame.Finite(performance.Flashlight) ?? 0
        };

        /// <summary>Capture managed score values on the game thread before asynchronous result processing.</summary>
        public void CaptureResult(ScoreInfo? completedScore = null)
        {
            // Results receive a prepared clone; the original gameplay score can still contain its initial date.
            resultDate = (completedScore ?? state.Score.ScoreInfo).Date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            showingResults = true;
            Update(force: true);
        }

        /// <summary>Publish the exact PP value used by Modosu's result persistence.</summary>
        public void PublishFinal(double? pp)
        {
            var frame = resultFrame;
            if (frame != null)
                publish(frame with { Pp = ModosuPerformanceFrame.Finite(pp), UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            state.ScoreProcessor.NewJudgement -= onJudgement;
            state.ScoreProcessor.JudgementReverted -= onJudgement;
            state.ScoreProcessor.OnResetFromReplayFrame -= onReplayReset;
            cancellation.Cancel();
            if (!showingResults)
                publish(new ModosuPerformanceFrame { ProcessId = Environment.ProcessId, SessionId = sessionId, Phase = "closed", UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            cancellation.Dispose();
        }

        // As in lazer's HUD, returning the already transformed beatmap avoids applying mods twice.
        private sealed class GameplayWorkingBeatmap(IBeatmap beatmap) : WorkingBeatmap(beatmap.BeatmapInfo, null)
        {
            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) => beatmap;
            protected override IBeatmap GetBeatmap() => beatmap;
            public override Texture GetBackground() => throw new NotSupportedException();
            protected override Track GetBeatmapTrack() => throw new NotSupportedException();
            protected override ISkin GetSkin() => throw new NotSupportedException();
            public override Stream GetStream(string storagePath) => throw new NotSupportedException();
        }
    }
}
