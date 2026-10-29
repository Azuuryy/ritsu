using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Modosu.Integration
{
    /// <summary>Exports the same perfect-score PP and stars that lazer caches for map selection.</summary>
    public partial class ModosuSelectionPerformanceExport : CompositeDrawable
    {
        [Resolved] private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;
        [Resolved] private IBindable<RulesetInfo> ruleset { get; set; } = null!;
        [Resolved] private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;
        [Resolved] private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private readonly long processStartTime = new DateTimeOffset(Process.GetCurrentProcess().StartTime).ToUnixTimeMilliseconds();
        private string? currentKey;
        private CancellationTokenSource? cancellation;
        private ModosuPerformanceFrame? selected;
        private long lastUpdate = -1000;
        private long retryAfter;

        public ModosuSelectionPerformanceExport() => AlwaysPresent = true;

        protected override void Update()
        {
            base.Update();
            long now = Environment.TickCount64;
            if (now - lastUpdate < 250) return;
            lastUpdate = now;

            string hash = beatmap.Value.BeatmapInfo.MD5Hash;
            if (ruleset.Value.ShortName != ModosuRuleset.SHORT_NAME || string.IsNullOrEmpty(hash))
            {
                clear();
                return;
            }

            // Serialising includes setting edits on the same mod instances, not just acronym changes.
            string json = new ScoreInfo { Mods = mods.Value.ToArray() }.ModsJson;
            string key = $"{hash}:{json}";
            if (key != currentKey || selected == null && now >= retryAfter)
            {
                cancellation?.Cancel();
                cancellation?.Dispose();
                cancellation = new CancellationTokenSource();
                var token = cancellation.Token;
                currentKey = key;
                selected = null;
                retryAfter = long.MaxValue;
                var mapInfo = beatmap.Value.BeatmapInfo;
                string mapHash = hash;
                var rulesetInfo = ruleset.Value;
                var clonedMods = mods.Value.Select(mod => mod.DeepClone()).ToArray();
                _ = calculate(mapInfo, mapHash, rulesetInfo, clonedMods, json, key, token);
            }

            if (selected != null)
                ModosuPerformanceFile.PublishSelection(selected with { UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private async Task calculate(BeatmapInfo map, string mapHash, RulesetInfo mode, Mod[] selectedMods, string json, string key, CancellationToken token)
        {
            try
            {
                var difficulty = await difficultyCache.GetDifficultyAsync(map, mode, selectedMods, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var frame = new ModosuPerformanceFrame
                {
                    ProcessId = Environment.ProcessId,
                    ProcessStartTime = processStartTime,
                    SessionId = Guid.NewGuid().ToString("N"),
                    BeatmapHash = mapHash,
                    ModsJson = json,
                    Phase = "selection",
                    Pp = ModosuPerformanceFrame.Finite(difficulty?.PerformanceAttributes?.Total),
                    MaximumPp = ModosuPerformanceFrame.Finite(difficulty?.PerformanceAttributes?.Total),
                    TotalStars = ModosuPerformanceFrame.Finite(difficulty?.Stars),
                    LiveStars = ModosuPerformanceFrame.Finite(difficulty?.Stars),
                    MaximumCombo = difficulty?.MaxCombo ?? 0
                };
                Schedule(() =>
                {
                    if (IsDisposed || token.IsCancellationRequested || key != currentKey) return;
                    selected = frame.Pp.HasValue && frame.TotalStars.HasValue ? frame : null;
                    retryAfter = Environment.TickCount64 + 3000;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Logger.Error(exception, "Could not export Modosu selection performance for tosu.");
                Schedule(() => { if (key == currentKey) retryAfter = Environment.TickCount64 + 3000; });
            }
        }

        private void clear()
        {
            if (currentKey != null)
                ModosuPerformanceFile.PublishSelection(new ModosuPerformanceFrame { Phase = "closed", ProcessId = Environment.ProcessId });
            cancellation?.Cancel();
            selected = null;
            currentKey = null;
            retryAfter = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            clear();
            cancellation?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
