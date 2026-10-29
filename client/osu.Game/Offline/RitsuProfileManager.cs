// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Offline
{
    /// <summary>Profile metadata lives beside, not inside, the shared lazer database.</summary>
    public sealed class RitsuProfileManager
    {
        private const string filename = "profiles.json";
        private readonly object sync = new object();
        private readonly Storage storage;
        private readonly Storage avatarStorage;
        private readonly RealmAccess realm;
        private readonly ProfileStore data;

        public event Action? Changed;
        public event Action<string>? ActiveProfileChanged;

        public RitsuProfileManager(Storage storage, RealmAccess realm)
        {
            this.storage = storage.GetStorageForDirectory("ritsu");
            avatarStorage = this.storage.GetStorageForDirectory("avatars");
            this.realm = realm;

            if (this.storage.Exists(filename))
            {
                using var stream = this.storage.GetStream(filename, FileAccess.Read, FileMode.Open);
                using var reader = new StreamReader(stream);
                data = JsonConvert.DeserializeObject<ProfileStore>(reader.ReadToEnd())
                       ?? throw new InvalidDataException("Ritsu profile metadata is empty or invalid.");
                if (data.Version != 1 || data.Profiles == null || data.Scores == null || data.Profiles.Count == 0
                    || data.Profiles.All(p => p.Id != data.ActiveProfileId))
                    throw new InvalidDataException("Ritsu profile metadata is invalid or has an unsupported version.");
            }
            else
            {
                var profile = new RitsuProfile { Id = Guid.NewGuid(), Name = "Guest" };
                data = new ProfileStore { ActiveProfileId = profile.Id, Profiles = new List<RitsuProfile> { profile } };
                save();
            }
        }

        public Guid ActiveProfileId { get { lock (sync) return data.ActiveProfileId; } }
        public string ActiveProfileName { get { lock (sync) return activeProfile().Name; } }
        public bool IncludeRelaxAutopilot { get { lock (sync) return activeProfile().IncludeRelaxAutopilot; } }
        public Storage AvatarStorage => avatarStorage;

        public string? GetAvatarFileName(Guid profileId)
        {
            lock (sync)
                return data.Profiles.FirstOrDefault(p => p.Id == profileId)?.AvatarFileName;
        }

        public void SetAvatar(Guid profileId, FileInfo? imageFile)
        {
            string? avatarFileName = null;

            if (imageFile != null)
            {
                if (!imageFile.Exists)
                    throw new FileNotFoundException("The selected profile picture could not be found.", imageFile.FullName);

                if (imageFile.Length > 10 * 1024 * 1024)
                    throw new InvalidDataException("Profile pictures must be 10 MB or smaller.");

                using var image = Image.Load<Rgba32>(imageFile.FullName);
                if (image.Width > 4096 || image.Height > 4096)
                    throw new InvalidDataException("Profile pictures must be 4096 × 4096 pixels or smaller.");

                avatarFileName = $"profile-{profileId:N}-{Guid.NewGuid():N}";
                using var stream = avatarStorage.CreateFileSafely($"{avatarFileName}.png");
                image.SaveAsPng(stream);
            }

            lock (sync)
            {
                var profile = data.Profiles.SingleOrDefault(p => p.Id == profileId)
                              ?? throw new InvalidOperationException("This local profile no longer exists.");
                profile.AvatarFileName = avatarFileName;
                save();
            }

            Changed?.Invoke();
        }

        public IReadOnlyList<RitsuProfile> GetProfiles()
        {
            lock (sync)
                return data.Profiles.Select(p => new RitsuProfile { Id = p.Id, Name = p.Name, IncludeRelaxAutopilot = p.IncludeRelaxAutopilot, AvatarFileName = p.AvatarFileName }).ToArray();
        }

        public RitsuProfile GetActiveProfile()
        {
            lock (sync)
            {
                var profile = activeProfile();
                return new RitsuProfile { Id = profile.Id, Name = profile.Name, IncludeRelaxAutopilot = profile.IncludeRelaxAutopilot, AvatarFileName = profile.AvatarFileName };
            }
        }

        public void AddProfile(string name)
        {
            lock (sync)
            {
                name = validateName(name);
                var profile = new RitsuProfile { Id = Guid.NewGuid(), Name = name };
                data.Profiles.Add(profile);
                data.ActiveProfileId = profile.Id;
                save();
            }
            ActiveProfileChanged?.Invoke(name);
            Changed?.Invoke();
        }

        public void SelectProfile(Guid id)
        {
            string name;
            lock (sync)
            {
                var profile = data.Profiles.Single(p => p.Id == id);
                data.ActiveProfileId = id;
                name = profile.Name;
                save();
            }
            ActiveProfileChanged?.Invoke(name);
            Changed?.Invoke();
        }

        public void RenameActiveProfile(string name) => RenameProfile(ActiveProfileId, name);

        public void RenameProfile(Guid id, string name)
        {
            bool isActive;
            lock (sync)
            {
                name = validateName(name, id);
                data.Profiles.Single(p => p.Id == id).Name = name;
                isActive = data.ActiveProfileId == id;
                save();
            }
            if (isActive)
                ActiveProfileChanged?.Invoke(name);
            Changed?.Invoke();
        }

        public void DeleteActiveProfile() => DeleteProfile(ActiveProfileId);

        public void DeleteProfile(Guid id)
        {
            string? newActiveName = null;
            lock (sync)
            {
                if (data.Profiles.Count <= 1)
                    throw new InvalidOperationException("At least one local profile must remain.");
                if (data.Profiles.All(p => p.Id != id))
                    throw new InvalidOperationException("This local profile no longer exists.");
                data.Profiles.RemoveAll(p => p.Id == id);
                foreach (Guid score in data.Scores.Where(s => s.Value.ProfileId == id).Select(s => s.Key).ToArray())
                    data.Scores.Remove(score);
                if (data.ActiveProfileId == id)
                {
                    data.ActiveProfileId = data.Profiles[0].Id;
                    newActiveName = activeProfile().Name;
                }
                save();
            }
            if (newActiveName != null)
                ActiveProfileChanged?.Invoke(newActiveName);
            Changed?.Invoke();
        }

        public void SetIncludeRelaxAutopilot(bool include)
        {
            lock (sync)
            {
                activeProfile().IncludeRelaxAutopilot = include;
                save();
            }
            Changed?.Invoke();
        }

        public void AssociateNewScore(Guid scoreId, Guid profileId, string ruleset, int calculatorVersion, string modsJson)
        {
            lock (sync)
            {
                if (data.Profiles.All(p => p.Id != profileId) || data.Scores.ContainsKey(scoreId))
                    return;
                data.Scores.Add(scoreId, new RitsuScoreMetadata
                {
                    ProfileId = profileId,
                    Ruleset = ruleset,
                    CalculatorVersion = calculatorVersion,
                    ModsJson = modsJson,
                });
                save();
            }
            Changed?.Invoke();
        }

        public bool BelongsToActiveProfile(Guid scoreId)
        {
            lock (sync)
                return data.Scores.TryGetValue(scoreId, out var metadata) && metadata.ProfileId == data.ActiveProfileId;
        }

        /// <summary>
        /// Uses an existing Ritsu association for display only; never changes the shared score database.
        /// </summary>
        public void ApplyProfileNameToDetachedScore(ScoreInfo score)
        {
            if (score.IsManaged)
                throw new InvalidOperationException("Profile names can only be applied to detached scores.");

            string? profileName;
            lock (sync)
                profileName = data.Scores.TryGetValue(score.ID, out var metadata)
                    ? data.Profiles.FirstOrDefault(p => p.Id == metadata.ProfileId)?.Name
                    : null;

            if (profileName != null)
                score.User.Username = profileName;
        }

        public List<ScoreInfo> GetScores(Guid profileId, string ruleset)
        {
            HashSet<Guid> ids;
            lock (sync)
                ids = data.Scores.Where(s => s.Value.ProfileId == profileId && s.Value.Ruleset == ruleset).Select(s => s.Key).ToHashSet();

            // Realm's LINQ provider cannot compare a linked object's ShortName. Resolve
            // scores by their primary keys, then inspect the linked ruleset in memory.
            var scores = realm.Run(r => ids.Select(id => r.Find<ScoreInfo>(id))
                                           .Where(s => s != null && !s.DeletePending && s.Ruleset.ShortName == ruleset)
                                           .Select(s => s!)
                                           .Detach());
            foreach (var score in scores)
                ApplyProfileNameToDetachedScore(score);
            return scores;
        }

        public RitsuProfileStatistics GetStatistics(string ruleset) => GetStatistics(ActiveProfileId, ruleset);

        public RitsuProfileStatistics GetStatistics(Guid id, string ruleset)
        {
            bool include;
            lock (sync) include = data.Profiles.FirstOrDefault(p => p.Id == id)?.IncludeRelaxAutopilot ?? false;
            var scores = GetScores(id, ruleset).Where(s => s.TotalScore > 0 && s.Mods.All(m => m is not ModAutoplay)).ToList();
            var eligible = scores.Where(s => s.PP.HasValue && (include || s.Mods.All(m => m.Acronym != "RX" && m.Acronym != "AP"))).ToList();
            var best = eligible.GroupBy(s => s.BeatmapHash).Select(g => g.OrderByDescending(s => s.PP).First()).OrderByDescending(s => s.PP).ToArray();
            double factor = 1;
            double pp = 0;
            double accuracy = 0;
            foreach (var score in best)
            {
                pp += score.PP!.Value * factor;
                accuracy += score.Accuracy * factor;
                factor *= 0.95;
            }
            pp += (417.0 - 1.0 / 3.0) * (1.0 - Math.Pow(0.995, best.Length));
            if (best.Length > 0)
                accuracy *= 100.0 / (20 * (1 - Math.Pow(0.95, best.Length)));
            var historicalBest = new Dictionary<string, double>();
            var history = new List<RitsuPpPoint>();
            foreach (var score in eligible.OrderBy(s => s.Date))
            {
                double value = score.PP!.Value;
                if (!historicalBest.TryGetValue(score.BeatmapHash, out double previous) || value > previous)
                    historicalBest[score.BeatmapHash] = value;
                double weight = 1;
                double total = 0;
                foreach (double bestValue in historicalBest.Values.OrderByDescending(v => v))
                {
                    total += bestValue * weight;
                    weight *= 0.95;
                }
                total += (417.0 - 1.0 / 3.0) * (1.0 - Math.Pow(0.995, historicalBest.Count));
                history.Add(new RitsuPpPoint(score.Date, total));
            }
            int? estimatedGlobalRank = ruleset == "osu" && pp > 0 ? RitsuRankEstimator.EstimateGlobalRank(pp) : null;
            return new RitsuProfileStatistics(scores.Count, pp, accuracy, estimatedGlobalRank, best, scores.OrderByDescending(s => s.Date).ToArray(), history.ToArray());
        }

        private RitsuProfile activeProfile() => data.Profiles.Single(p => p.Id == data.ActiveProfileId);

        private string validateName(string name, Guid? except = null)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 32)
                throw new ArgumentException("Choose a profile name between 1 and 32 characters.");
            if (data.Profiles.Any(p => p.Id != except && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("This profile name already exists.");
            return name;
        }

        private void save()
        {
            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        public sealed class ProfileStore
        {
            public int Version { get; set; } = 1;
            public Guid ActiveProfileId { get; set; }
            public List<RitsuProfile> Profiles { get; set; } = new List<RitsuProfile>();
            public Dictionary<Guid, RitsuScoreMetadata> Scores { get; set; } = new Dictionary<Guid, RitsuScoreMetadata>();
        }
    }

    public sealed class RitsuProfile
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IncludeRelaxAutopilot { get; set; }
        public string? AvatarFileName { get; set; }
        public override string ToString() => Name;
    }

    public sealed class RitsuScoreMetadata
    {
        public Guid ProfileId { get; set; }
        public string Ruleset { get; set; } = string.Empty;
        public int CalculatorVersion { get; set; }
        public string ModsJson { get; set; } = string.Empty;
    }

    public sealed record RitsuPpPoint(DateTimeOffset Date, double PP);
    public sealed record RitsuProfileStatistics(int PlayCount, double PP, double Accuracy, int? EstimatedGlobalRank, IReadOnlyList<ScoreInfo> BestScores, IReadOnlyList<ScoreInfo> RecentScores, IReadOnlyList<RitsuPpPoint> History);

    /// <summary>
    /// Estimates global rank by linearly interpolating the September 2026 sampled osu! PP-to-rank curve.
    /// This is an offline estimate, not an official leaderboard position.
    /// </summary>
    internal static class RitsuRankEstimator
    {
        // Curve copied from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
        // Its source is attributed to a September 2026 osu-local-profiles sample of 10,000 profiles.
        private static readonly (double PP, int Rank)[] rankCurve =
        {
            (2.11, 2852394), (45.65, 2142296), (139.97, 1750230), (375.42, 1336787),
            (721.22, 1020639), (1148.03, 779960), (1621.4, 595532), (2125.51, 454565),
            (2708.02, 335739), (3302.75, 247883), (3978.95, 177279), (4665.01, 126882),
            (5403.43, 87234), (6164.53, 58191), (6911.02, 39304), (7677.87, 26024),
            (8519.1, 16828), (9518.04, 9896), (10741.3, 4951), (11762, 2910),
            (13010.5, 1549), (15002.6, 553), (17051.7, 211), (18305.4, 120),
        };

        public static int EstimateGlobalRank(double pp)
        {
            if (pp <= rankCurve[0].PP)
                return rankCurve[0].Rank;
            if (pp >= rankCurve[^1].PP)
                return rankCurve[^1].Rank;

            for (int i = 1; i < rankCurve.Length; i++)
            {
                if (pp <= rankCurve[i].PP)
                {
                    var previous = rankCurve[i - 1];
                    var next = rankCurve[i];
                    double progress = (pp - previous.PP) / (next.PP - previous.PP);
                    return (int)Math.Round(previous.Rank + (next.Rank - previous.Rank) * progress);
                }
            }

            return rankCurve[^1].Rank;
        }
    }
}
