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

namespace osu.Game.Offline
{
    /// <summary>Profile metadata lives beside, not inside, the shared lazer database.</summary>
    public sealed class RitsuProfileManager
    {
        private const string filename = "profiles.json";
        private readonly object sync = new object();
        private readonly Storage storage;
        private readonly RealmAccess realm;
        private readonly ProfileStore data;

        public event Action? Changed;
        public event Action<string>? ActiveProfileChanged;

        public RitsuProfileManager(Storage storage, RealmAccess realm)
        {
            this.storage = storage.GetStorageForDirectory("ritsu");
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
                var profile = new RitsuProfile { Id = Guid.NewGuid(), Name = "Joueur" };
                data = new ProfileStore { ActiveProfileId = profile.Id, Profiles = new List<RitsuProfile> { profile } };
                save();
            }
        }

        public Guid ActiveProfileId { get { lock (sync) return data.ActiveProfileId; } }
        public string ActiveProfileName { get { lock (sync) return activeProfile().Name; } }
        public bool IncludeRelaxAutopilot { get { lock (sync) return activeProfile().IncludeRelaxAutopilot; } }

        public IReadOnlyList<RitsuProfile> GetProfiles()
        {
            lock (sync)
                return data.Profiles.Select(p => new RitsuProfile { Id = p.Id, Name = p.Name, IncludeRelaxAutopilot = p.IncludeRelaxAutopilot }).ToArray();
        }

        public RitsuProfile GetActiveProfile()
        {
            lock (sync)
            {
                var profile = activeProfile();
                return new RitsuProfile { Id = profile.Id, Name = profile.Name, IncludeRelaxAutopilot = profile.IncludeRelaxAutopilot };
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
            return new RitsuProfileStatistics(scores.Count, pp, accuracy, best, scores.OrderByDescending(s => s.Date).ToArray(), history.ToArray());
        }

        /// <summary>
        /// Rank among local profiles only. It is reconstructed from their eligible scores using
        /// the same weighted PP formula as the current totals, and is not an online rank.
        /// </summary>
        public IReadOnlyList<RitsuRankPoint> GetLocalRankHistory(Guid profileId, string ruleset)
        {
            var profiles = GetProfiles();
            var events = profiles.SelectMany(profile => GetScores(profile.Id, ruleset)
                .Where(score => score.TotalScore > 0 && score.PP.HasValue
                                && score.Mods.All(mod => mod is not ModAutoplay)
                                && (profile.IncludeRelaxAutopilot || score.Mods.All(mod => mod.Acronym != "RX" && mod.Acronym != "AP")))
                .Select(score => (ProfileId: profile.Id, Score: score)))
                .OrderBy(e => e.Score.Date).ThenBy(e => e.Score.ID).ToArray();

            var best = profiles.ToDictionary(p => p.Id, _ => new Dictionary<string, double>());
            var totals = profiles.ToDictionary(p => p.Id, _ => 0.0);
            var history = new List<RitsuRankPoint>();
            foreach (var e in events)
            {
                var profileBest = best[e.ProfileId];
                double pp = e.Score.PP!.Value;
                if (!profileBest.TryGetValue(e.Score.BeatmapHash, out double previous) || pp > previous)
                {
                    profileBest[e.Score.BeatmapHash] = pp;
                    double weight = 1;
                    double total = 0;
                    foreach (double value in profileBest.Values.OrderByDescending(v => v))
                    {
                        total += value * weight;
                        weight *= 0.95;
                    }
                    totals[e.ProfileId] = total + (417.0 - 1.0 / 3.0) * (1.0 - Math.Pow(0.995, profileBest.Count));
                }

                if (best.TryGetValue(profileId, out var activeBest) && activeBest.Count > 0)
                {
                    int rank = 1 + totals.Count(pair => pair.Key != profileId && pair.Value > totals[profileId]);
                    history.Add(new RitsuRankPoint(e.Score.Date, rank));
                }
            }

            return history;
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
    public sealed record RitsuRankPoint(DateTimeOffset Date, int Rank);
    public sealed record RitsuProfileStatistics(int PlayCount, double PP, double Accuracy, IReadOnlyList<ScoreInfo> BestScores, IReadOnlyList<ScoreInfo> RecentScores, IReadOnlyList<RitsuPpPoint> History);
}
