// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Realms;
using osu.Framework.Platform;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Offline
{
    public sealed class RitsuReplayImporter
    {
        private readonly IRulesetStore rulesets;
        private readonly BeatmapManager beatmaps;
        private readonly BeatmapDifficultyCache difficultyCache;
        private readonly ScoreManager scores;
        private readonly RitsuProfileManager profiles;
        private readonly RealmAccess realm;
        private readonly Storage fileStorage;

        public RitsuReplayImporter(IRulesetStore rulesets, BeatmapManager beatmaps, BeatmapDifficultyCache difficultyCache, ScoreManager scores, RitsuProfileManager profiles, RealmAccess realm, Storage storage)
        {
            this.rulesets = rulesets;
            this.beatmaps = beatmaps;
            this.difficultyCache = difficultyCache;
            this.scores = scores;
            this.profiles = profiles;
            this.realm = realm;
            fileStorage = storage.GetStorageForDirectory("files");
        }

        public IReadOnlyList<RitsuSharedReplay> GetSharedReplays() => realm.Run(r => r.All<ScoreInfo>()
            .Filter("DeletePending == false AND (Ruleset.ShortName == $0 OR Ruleset.ShortName == $1)", "mosu", "modosu")
            .AsEnumerable()
            .Select(toSharedReplay)
            .OrderByDescending(score => score.Date).ToArray());

        private static RitsuSharedReplay toSharedReplay(ScoreInfo score) => new RitsuSharedReplay(score.ID, score.RealmUser.Username,
            score.BeatmapInfo?.Metadata.Title ?? "Unknown beatmap", score.BeatmapInfo?.DifficultyName ?? "Unknown difficulty",
            score.Date, score.Files.FirstOrDefault(file => file.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase))?.File.GetStoragePath());

        public async Task<RitsuReplayImportResult> ImportSharedAsync(Guid sourceId, Guid profileId, CancellationToken cancellationToken = default)
        {
            var source = realm.Run(r =>
            {
                var score = r.Find<ScoreInfo>(sourceId);
                return score is { DeletePending: false } && score.Ruleset.ShortName is "mosu" or "modosu" ? toSharedReplay(score) : null;
            }) ?? throw new InvalidOperationException("The original MOsu score is no longer available.");
            if (source.FilePath == null || !fileStorage.Exists(source.FilePath))
                throw new FileNotFoundException("The original score has no replay file in the linked library.");
            byte[] bytes;
            using (var stream = fileStorage.GetStream(source.FilePath) ?? throw new FileNotFoundException("The linked replay file is unavailable."))
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                bytes = buffer.ToArray();
            }
            string contentHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            string importHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"ritsu-mosu:{sourceId:N}:{contentHash}")));
            return await importBytes(bytes, profileId, importHash, cancellationToken).ConfigureAwait(false);
        }

        public async Task<RitsuReplayImportResult> ImportAsync(string path, Guid profileId, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(Path.GetExtension(path), ".osr", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Select an .osr replay file.");
            if (profiles.GetProfiles().All(profile => profile.Id != profileId))
                throw new InvalidOperationException("The selected profile no longer exists.");

            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return await importBytes(bytes, profileId, null, cancellationToken).ConfigureAwait(false);
        }

        private async Task<RitsuReplayImportResult> importBytes(byte[] bytes, Guid profileId, string? modelHash, CancellationToken cancellationToken)
        {
            if (profiles.GetProfiles().All(profile => profile.Id != profileId))
                throw new InvalidOperationException("The selected profile no longer exists.");
            var decoded = new RitsuLegacyScoreDecoder(rulesets, beatmaps).Parse(new MemoryStream(bytes));
            if (decoded.Replay == null || decoded.Replay.Frames.Count == 0)
                throw new InvalidDataException("The replay contains no frames.");

            var score = decoded.ScoreInfo;
            if (score.BeatmapInfo == null)
                throw new InvalidDataException("The replay's beatmap is not available locally.");
            score.BeatmapInfo = score.BeatmapInfo.Detach();
            score.Ruleset = score.Ruleset.Detach();
            string rulesetName = score.Ruleset.ShortName;
            bool variantWarning = score.Mods.Any(mod => mod is IModosuMod or IBeatmapVariant || mod.Acronym == "SOT");
            string hash = modelHash ?? Convert.ToHexStringLower(SHA256.HashData(bytes));
            var existing = scores.Query(item => item.Hash == hash && !item.DeletePending);
            if (existing == null && scores.Query(item => item.Hash == hash && item.DeletePending) != null)
                throw new InvalidOperationException("A deleted score has the same replay. Restore it before importing.");

            int calculatorVersion = 0;
            string? warning = variantWarning ? "The map transformation may differ from the original client; check the replay in game." : null;
            if (existing == null)
            {
                try
                {
                    var ruleset = score.Ruleset.CreateInstance();
                    calculatorVersion = ruleset.CreateDifficultyCalculator(beatmaps.GetWorkingBeatmap(score.BeatmapInfo)).Version;
                    var difficulty = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo, score.Ruleset, score.Mods, cancellationToken).ConfigureAwait(false);
                    var calculator = ruleset.CreatePerformanceCalculator();
                    if (difficulty?.DifficultyAttributes != null && calculator != null)
                    {
                        double pp = calculator.Calculate(score, difficulty.Value.DifficultyAttributes).Total;
                        if (double.IsFinite(pp)) score.PP = pp;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Could not calculate PP for imported replay.");
                }

                if (!score.PP.HasValue)
                    warning = string.IsNullOrEmpty(warning) ? "PP could not be calculated." : warning + " PP could not be calculated.";

                cancellationToken.ThrowIfCancellationRequested();
                using var archive = new ImportTask(new MemoryStream(bytes), "replay.osr").GetReader();
                var imported = scores.Import(score, archive, new ImportParameters { PreserveExisting = true, ModelHashOverride = modelHash }, cancellationToken)
                               ?? throw new InvalidOperationException("The replay could not be saved.");
                existing = scores.Query(item => item.ID == imported.ID && !item.DeletePending)
                           ?? throw new InvalidOperationException("The saved score is no longer available.");
            }
            else if (!existing.PP.HasValue)
            {
                warning = string.IsNullOrEmpty(warning) ? "The existing score has no PP estimate." : warning + " The existing score has no PP estimate.";
            }

            Guid? previousProfile = profiles.GetScoreProfileId(existing.ID);
            if (previousProfile.HasValue && previousProfile != profileId)
                throw new InvalidOperationException("This score already belongs to another profile. Use Transfer score to profile instead.");
            if (existing.Ruleset.ShortName != rulesetName)
                throw new InvalidOperationException("The existing score uses a different ruleset.");
            bool alreadyAssociated = previousProfile == profileId;
            if (!alreadyAssociated)
                profiles.AssociateNewScore(existing.ID, profileId, rulesetName, calculatorVersion, existing.ModsJson,
                    ppKey: calculatorVersion > 0 && existing.PP.HasValue && existing.BeatmapInfo is { } map
                        ? BeatmapDifficultyCache.CreateStablePerformanceKey(calculatorVersion, rulesetName, map.Hash, existing.ModsJson)
                        : null);
            if (profiles.GetScoreProfileId(existing.ID) != profileId)
                throw new InvalidOperationException("The score was saved, but could not be assigned to the selected profile.");

            return new RitsuReplayImportResult(existing.ID, alreadyAssociated, existing.PP.HasValue, warning);
        }
    }

    public sealed record RitsuReplayImportResult(Guid ScoreId, bool AlreadyAssociated, bool HasPP, string? Warning);

    public sealed record RitsuSharedReplay(Guid Id, string Username, string Title, string Difficulty, DateTimeOffset Date, string? FilePath)
    {
        public override string ToString() => $"{Date.LocalDateTime:g} · {Username} · {Title} [{Difficulty}]";
    }
}
