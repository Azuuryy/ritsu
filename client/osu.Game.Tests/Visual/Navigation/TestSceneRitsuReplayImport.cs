// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Offline;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Online.API;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.IO.Legacy;
using osu.Game.Overlays;
using osu.Game.Overlays.ToriiBriefing;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneRitsuReplayImport : OsuGameTestScene
    {
        [Test]
        public void TestImportAndReimportReplay()
        {
            RitsuReplayImporter importer = null!;
            RitsuProfileManager profiles = null!;
            Task<RitsuReplayImportResult> operation = null!;
            string path = null!;
            byte[] original = null!;
            Guid profile = Guid.Empty;
            Guid scoreId = Guid.Empty;
            Guid sourceId = Guid.Empty;
            Guid beatmapId = Guid.Empty;
            Guid otherProfile = Guid.Empty;
            Task exportOperation = null!;
            string profileExportFilename = null!;

            AddUntilStep("wait for dialog overlay", () => Game.Dependencies.Get<IDialogOverlay>() is osu.Framework.Graphics.Drawable drawable && drawable.IsLoaded);
            AddStep("open import dialog", () => Game.ShowRitsuReplayImport());
            AddUntilStep("import dialog is visible", () => Game.Dependencies.Get<IDialogOverlay>().CurrentDialog != null);
            AddStep("close import dialog", () => Game.Dependencies.Get<IDialogOverlay>().CurrentDialog!.Hide());
            AddStep("prepare a local replay", () =>
            {
                var imported = Game.BeatmapManager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely();
                var beatmap = Game.BeatmapManager.GetAllUsableBeatmapSets().Single(set => set.ID == imported.ID).Beatmaps[0].Detach();
                beatmapId = beatmap.ID;
                var info = TestResources.CreateTestScoreInfo(beatmap);
                info.Mods = Array.Empty<osu.Game.Rulesets.Mods.Mod>();
                var score = new Score
                {
                    ScoreInfo = info,
                    Replay = new Replay { Frames = new List<ReplayFrame> { new OsuReplayFrame(2000, new Vector2(256, 192)) } },
                };
                using var stream = new MemoryStream();
                new LegacyScoreEncoder(score, Game.BeatmapManager.GetWorkingBeatmap(beatmap).Beatmap).Encode(stream, true);
                original = stream.ToArray();
                path = Path.Combine(Path.GetTempPath(), $"ritsu-test-{Guid.NewGuid():N}.osr");
                File.WriteAllBytes(path, original);
                profiles = Game.Dependencies.Get<RitsuProfileManager>();
                profile = profiles.ActiveProfileId;
                importer = new RitsuReplayImporter(Game.Dependencies.Get<IRulesetStore>(), Game.BeatmapManager,
                    Game.Dependencies.Get<BeatmapDifficultyCache>(), Game.ScoreManager, profiles,
                    Game.Dependencies.Get<RealmAccess>(), Game.Dependencies.Get<Storage>());
            });
            AddStep("import replay", () => operation = Task.Run(() => importer.ImportAsync(path, profile)));
            AddUntilStep("wait for import", () => operation.IsCompleted);
            AddStep("verify score and original", () =>
            {
                var result = operation.GetResultSafely();
                scoreId = result.ScoreId;
                Assert.That(result.AlreadyAssociated, Is.False);
                Assert.That(profiles.GetScoreProfileId(scoreId), Is.EqualTo(profile));
                var saved = Game.ScoreManager.Query(s => s.ID == scoreId);
                Assert.That(saved, Is.Not.Null);
                Assert.That(Game.ScoreManager.GetScore(saved!)!.Replay.Frames, Is.Not.Empty);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            });
            AddStep("reimport replay", () => operation = Task.Run(() => importer.ImportAsync(path, profile)));
            AddUntilStep("wait for reimport", () => operation.IsCompleted);
            AddStep("verify idempotency", () =>
            {
                var result = operation.GetResultSafely();
                Assert.That(result.ScoreId, Is.EqualTo(scoreId));
                Assert.That(result.AlreadyAssociated, Is.True);
            });
            AddStep("try importing to another profile", () =>
            {
                profiles.AddProfile("Other");
                var other = profiles.GetProfiles().Single(item => item.Name == "Other");
                otherProfile = other.Id;
                operation = Task.Run(() => importer.ImportAsync(path, other.Id));
            });
            AddUntilStep("wait for refused import", () => operation.IsCompleted);
            AddStep("verify reassignment was refused", () =>
            {
                Assert.That(operation.Exception?.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
                Assert.That(operation.Exception!.GetBaseException().Message, Does.Contain("another profile"));
                Assert.That(profiles.GetScoreProfileId(scoreId), Is.EqualTo(profile));
            });
            AddStep("transfer score and export with destination name", () =>
            {
                Assert.That(profiles.TransferScore(scoreId, profile, otherProfile), Is.True);
                var saved = Game.ScoreManager.Query(s => s.ID == scoreId)!;
                var menu = RitsuScoreExportMenu.Create(profiles, Game.Dependencies.Get<IDialogOverlay>(), Game.ScoreManager, saved);
                Assert.That(menu, Is.Not.Null);
                Assert.That(menu!.Text.ToString(), Is.EqualTo("Export original replay"));
                exportOperation = Game.ScoreManager.Export(saved);
            });
            AddUntilStep("wait for profile export", () => exportOperation.IsCompleted);
            AddStep("verify exported replay and original", () =>
            {
                exportOperation.WaitSafely();
                var exports = Game.Dependencies.Get<Storage>().GetStorageForDirectory("exports");
                profileExportFilename = exports.GetFiles(string.Empty, "*.osr").Single();
                using var stream = exports.GetStream(profileExportFilename);
                using var exported = new MemoryStream();
                stream.CopyTo(exported);
                byte[] exportedBytes = exported.ToArray();
                var before = replayParts(original);
                var after = replayParts(exportedBytes);
                Assert.That(after.Prefix, Is.EqualTo(before.Prefix));
                Assert.That(after.Suffix, Is.EqualTo(before.Suffix));
                var decoded = new RitsuLegacyScoreDecoder(Game.Dependencies.Get<IRulesetStore>(), Game.BeatmapManager).Parse(new MemoryStream(exportedBytes));
                Assert.That(decoded.ScoreInfo.User.Username, Is.EqualTo("Other"));
                Assert.That(decoded.Replay.Frames, Is.Not.Empty);
                Assert.That(profiles.GetScoreProfileId(scoreId), Is.EqualTo(otherProfile));
                Assert.That(Game.ScoreManager.Query(s => s.ID == scoreId)!.User.Username, Is.EqualTo("peppy"));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            });
            AddStep("export original replay", () => exportOperation = Game.ScoreManager.Export(Game.ScoreManager.Query(s => s.ID == scoreId)!, original: true));
            AddUntilStep("wait for original export", () => exportOperation.IsCompleted);
            AddStep("verify original export is unchanged", () =>
            {
                exportOperation.WaitSafely();
                var exports = Game.Dependencies.Get<Storage>().GetStorageForDirectory("exports");
                string originalExport = exports.GetFiles(string.Empty, "*.osr").Single(name => name != profileExportFilename);
                using var stream = exports.GetStream(originalExport);
                using var output = new MemoryStream();
                stream.CopyTo(output);
                Assert.That(output.ToArray(), Is.EqualTo(original));
            });
            AddStep("create a linked MOsu score", () =>
            {
                Game.Realm.Write(realm =>
                {
                    var ruleset = realm.Find<RulesetInfo>("mosu") ?? realm.Add(new RulesetInfo("mosu", "MOsu", string.Empty, -1));
                    var beatmap = realm.Find<BeatmapInfo>(beatmapId)!;
                    var source = new ScoreInfo(beatmap, ruleset)
                    {
                        ID = Guid.NewGuid(),
                        BeatmapHash = beatmap.Hash,
                        Hash = Convert.ToHexStringLower(SHA256.HashData(original)),
                    };
                    realm.Add(source);
                    Game.ScoreManager.AddFile(source, new MemoryStream(original), "replay.osr", realm);
                    sourceId = source.ID;
                });
            });
            AddStep("find linked replay", () => Assert.That(importer.GetSharedReplays().Any(replay => replay.Id == sourceId), Is.True));
            AddStep("import linked replay", () => operation = Task.Run(() => importer.ImportSharedAsync(sourceId, profile)));
            AddUntilStep("wait for linked replay", () => operation.IsCompleted);
            AddStep("verify linked score is preserved", () =>
            {
                var result = operation.GetResultSafely();
                Assert.That(result.ScoreId, Is.Not.EqualTo(sourceId));
                Assert.That(profiles.GetScoreProfileId(result.ScoreId), Is.EqualTo(profile));
                Assert.That(profiles.GetScoreProfileId(sourceId), Is.Null);
                var source = Game.ScoreManager.Query(s => s.ID == sourceId);
                Assert.That(source, Is.Not.Null);
                Assert.That(source!.DeletePending, Is.False);
                Assert.That(source.Ruleset.ShortName, Is.EqualTo("mosu"));
                Assert.That(source.Files.Count, Is.EqualTo(1));
            });
            AddStep("reimport linked replay", () => operation = Task.Run(() => importer.ImportSharedAsync(sourceId, profile)));
            AddUntilStep("wait for linked replay reimport", () => operation.IsCompleted);
            AddStep("verify linked replay idempotency", () =>
            {
                Assert.That(operation.GetResultSafely().AlreadyAssociated, Is.True);
                File.Delete(path);
            });
        }

        [Test]
        public void TestCatalogueIsUnavailableOffline()
        {
            AddStep("open catalogue", () => Game.ToggleBeatmapListing());
            AddUntilStep("show offline message", () => Game.ChildrenOfType<BeatmapListingOverlay>().Any(overlay => overlay.IsPresent
                && overlay.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString().Contains("online catalogue is unavailable offline"))));
            AddAssert("no online catalogue is created", () => !Game.ChildrenOfType<RitsuBeatmapCatalogue>().Any());
        }

        [TestCase("FSA")]
        [TestCase("SA")]
        [TestCase("RDV2")]
        [TestCase("NL")]
        [TestCase("CG")]
        public void TestConfiguredMosuReplayImport(string acronym)
        {
            RitsuReplayImporter importer = null!;
            RitsuProfileManager profiles = null!;
            Task<RitsuReplayImportResult> operation = null!;
            string path = null!;
            byte[] original = null!;
            Guid profile = Guid.Empty;
            ScoreInfo savedScore = null!;
            ReplayPlayer replayPlayer = null!;
            AddStep("prepare replay with MOsu settings", () =>
            {
                var imported = Game.BeatmapManager.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).GetResultSafely();
                var beatmap = Game.BeatmapManager.GetAllUsableBeatmapSets().Single(set => set.ID == imported.ID).Beatmaps.First(item => item.Ruleset.ShortName == "osu").Detach();
                var settings = acronym switch
                {
                    "FSA" => new Dictionary<string, object> { ["start_spacing"] = 1.5, ["end_spacing"] = 0.5 },
                    "SA" => new Dictionary<string, object> { ["object_spacing"] = 1.5 },
                    "NL" => new Dictionary<string, object> { ["beat_divisor"] = 2, ["deathstreams_slowing_down"] = false },
                    "CG" => new Dictionary<string, object> { ["divisor"] = 2, ["distance"] = 60 },
                    _ => new Dictionary<string, object> { ["seed"] = 12345, ["aim_distance_multiplier"] = 1.5 },
                };
                var info = TestResources.CreateTestScoreInfo(beatmap);
                info.APIMods = new[] { new APIMod { Acronym = acronym, Settings = settings } };
                var working = Game.BeatmapManager.GetWorkingBeatmap(beatmap);
                var playable = working.GetPlayableBeatmap(info.Ruleset, info.Mods);
                var score = new Score
                {
                    ScoreInfo = info,
                    Replay = new OsuAutoGenerator(playable, info.Mods).Generate(),
                };
                using var stream = new MemoryStream();
                new LegacyScoreEncoder(score, playable).Encode(stream, true);
                original = stream.ToArray();
                path = Path.Combine(Path.GetTempPath(), $"ritsu-mosu-test-{Guid.NewGuid():N}.osr");
                File.WriteAllBytes(path, original);
                profiles = Game.Dependencies.Get<RitsuProfileManager>();
                profile = profiles.ActiveProfileId;
                importer = new RitsuReplayImporter(Game.Dependencies.Get<IRulesetStore>(), Game.BeatmapManager,
                    Game.Dependencies.Get<BeatmapDifficultyCache>(), Game.ScoreManager, profiles,
                    Game.Dependencies.Get<RealmAccess>(), Game.Dependencies.Get<Storage>());
            });
            AddStep("import configured replay", () => operation = Task.Run(() => importer.ImportAsync(path, profile)));
            AddUntilStep("wait for configured replay", () => operation.IsCompleted);
            AddStep("verify saved settings and replay", () =>
            {
                var result = operation.GetResultSafely();
                Assert.That(result.Warning, Does.Contain("transformation"));
                Assert.That(profiles.GetScoreProfileId(result.ScoreId), Is.EqualTo(profile));
                var saved = savedScore = Game.ScoreManager.Query(s => s.ID == result.ScoreId)!;
                switch (acronym)
                {
                    case "FSA":
                        var spacing = saved.Mods.OfType<OsuModSpacingOvertime>().Single();
                        Assert.That(spacing.StartSpacing.Value, Is.EqualTo(1.5f));
                        Assert.That(spacing.EndSpacing.Value, Is.EqualTo(0.5f));
                        break;
                    case "SA":
                        Assert.That(saved.Mods.OfType<OsuModSpacingAdjust>().Single().ObjectSpacing.Value, Is.EqualTo(1.5f));
                        break;
                    case "NL":
                        var noSliding = saved.Mods.OfType<OsuModNoSliding>().Single();
                        Assert.That(noSliding.BeatDivisor.Value, Is.EqualTo(2));
                        Assert.That(noSliding.DeathstreamsSlowingDown.Value, Is.False);
                        break;
                    case "CG":
                        var generation = saved.Mods.OfType<OsuModCircleGeneration>().Single();
                        Assert.That(generation.Divisor.Value, Is.EqualTo(2));
                        Assert.That(generation.Distance.Value, Is.EqualTo(60));
                        break;
                    default:
                        var random = saved.Mods.OfType<OsuModRandomV2>().Single();
                        Assert.That(random.Seed.Value, Is.EqualTo(12345));
                        Assert.That(random.AimDistanceMultiplier.Value, Is.EqualTo(1.5f));
                        break;
                }
                var loaded = Game.ScoreManager.GetScore(saved)!;
                Assert.That(loaded.Replay.Frames, Is.Not.Empty);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
                File.Delete(path);
            });
            AddUntilStep("wait for welcome overlay", () => Game.ChildrenOfType<ToriiBriefingOverlay>().Any(overlay => overlay.IsLoaded));
            AddStep("dismiss welcome overlay", () => Game.ChildrenOfType<ToriiBriefingOverlay>().Single().Hide());
            AddUntilStep("welcome overlay is closed", () => !Game.ChildrenOfType<ToriiBriefingOverlay>().Single().IsPresent);
            AddStep("play imported replay", () => Game.PresentScore(savedScore, ScorePresentType.Gameplay));
            AddUntilStep("replay player loads", () => Game.ScreenStack.CurrentScreen is ReplayPlayer player && player.LoadedBeatmapSuccessfully);
            AddUntilStep("replay clock runs", () => ((ReplayPlayer)Game.ScreenStack.CurrentScreen).ChildrenOfType<GameplayClockContainer>().Any(clock => clock.IsRunning));
            AddAssert("player retains imported score and mod", () =>
            {
                replayPlayer = (ReplayPlayer)Game.ScreenStack.CurrentScreen;
                string expectedAcronym = acronym == "FSA" ? "SOT" : acronym;
                return replayPlayer.Score.ScoreInfo.ID == savedScore.ID
                       && replayPlayer.Score.ScoreInfo.Mods.Any(mod => mod.Acronym == expectedAcronym)
                       && replayPlayer.Score.Replay.Frames.Count > 0;
            });
            AddStep("speed up replay", () => replayPlayer.ChildrenOfType<MasterGameplayClockContainer>().Single().UserPlaybackRate.Value = 2);
            AddUntilStep("replay completes", () => replayPlayer.GameplayState.HasCompleted);
            AddAssert("replay finishes without misses", () => replayPlayer.GameplayState.HasPassed
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Great) > 0
                && replayPlayer.GameplayState.ScoreProcessor.HighestCombo.Value == replayPlayer.GameplayState.ScoreProcessor.MaximumCombo
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss) == 0
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Meh) == 0
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Ok) == 0
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.LargeTickMiss) == 0
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.SmallTickMiss) == 0
                && replayPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.ComboBreak) == 0);
        }

        private static (byte[] Prefix, byte[] Suffix) replayParts(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new SerializationReader(stream);
            reader.ReadByte();
            reader.ReadInt32();
            reader.ReadString();
            int start = (int)stream.Position;
            reader.ReadString();
            int end = (int)stream.Position;
            return (bytes[..start], bytes[end..]);
        }
    }
}
