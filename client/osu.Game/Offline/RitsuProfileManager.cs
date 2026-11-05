// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Realms;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Users;
using osu.Game.Online.API.Requests.Responses;
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
                using (var stream = this.storage.GetStream(filename, FileAccess.Read, FileMode.Open))
                using (var reader = new StreamReader(stream))
                data = JsonConvert.DeserializeObject<ProfileStore>(reader.ReadToEnd())
                       ?? throw new InvalidDataException("Ritsu profile metadata is empty or invalid.");
                if ((data.Version != 1 && data.Version != 2) || data.Profiles == null || data.Scores == null || data.Profiles.Count == 0
                    || data.Profiles.All(p => p.Id != data.ActiveProfileId))
                    throw new InvalidDataException("Ritsu profile metadata is invalid or has an unsupported version.");

                foreach (var profile in data.Profiles)
                {
                    profile.ExcludedBeatmapIds ??= new HashSet<Guid>();
                    profile.ExcludedBeatmapSetIds ??= new HashSet<Guid>();
                    profile.IncludedBeatmapIds ??= new HashSet<Guid>();
                }

                if (data.Version == 1)
                    migrateLegacyExclusions();
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
            string? avatarFileName = saveImage(profileId, imageFile, "profile");

            lock (sync)
            {
                var profile = data.Profiles.SingleOrDefault(p => p.Id == profileId)
                              ?? throw new InvalidOperationException("This local profile no longer exists.");
                profile.AvatarFileName = avatarFileName;
                save();
            }

            Changed?.Invoke();
        }

        private string? saveImage(Guid profileId, FileInfo? imageFile, string prefix)
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

                avatarFileName = $"{prefix}-{profileId:N}-{Guid.NewGuid():N}";
                using var stream = avatarStorage.CreateFileSafely($"{avatarFileName}.png");
                image.SaveAsPng(stream);
            }

            return avatarFileName;
        }

        public void SetCountry(Guid profileId, CountryCode country)
        {
            if (!Enum.IsDefined(country)) throw new ArgumentOutOfRangeException(nameof(country));
            lock (sync)
            {
                var profile = data.Profiles.Single(p => p.Id == profileId);
                profile.CountryCode = country;
                profile.FlagFileName = null;
                save();
            }
            Changed?.Invoke();
        }

        public void SetFlagImage(Guid profileId, FileInfo? file)
        {
            string? flagFileName = saveImage(profileId, file, "flag");
            lock (sync)
            {
                var profile = data.Profiles.Single(p => p.Id == profileId);
                profile.FlagFileName = flagFileName;
                save();
            }
            Changed?.Invoke();
        }

        public static APIUser CreateUser(RitsuProfile profile) => new APIUser
        {
            Id = APIUser.SYSTEM_USER_ID,
            Username = profile.Name,
            CountryCode = profile.CountryCode,
            LocalAvatarFileName = profile.AvatarFileName,
            LocalFlagFileName = profile.FlagFileName,
        };

        public IReadOnlyList<RitsuProfile> GetProfiles()
        {
            lock (sync)
                return data.Profiles.Select(cloneProfile).ToArray();
        }

        public RitsuProfile GetActiveProfile()
        {
            lock (sync)
            {
                var profile = activeProfile();
                return cloneProfile(profile);
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

        public void SetScoreIncludedInPP(Guid scoreId, bool include)
        {
            lock (sync)
            {
                if (!data.Scores.TryGetValue(scoreId, out var metadata))
                    return;
                metadata.IncludeInPP = include;
                save();
            }
            Changed?.Invoke();
        }

        public bool IsScoreIncludedInPP(Guid scoreId)
        {
            var score = realm.Run(r => r.Find<ScoreInfo>(scoreId)?.Detach());
            lock (sync)
            {
                if (!data.Scores.TryGetValue(scoreId, out var metadata))
                    return true;
                var profile = data.Profiles.FirstOrDefault(p => p.Id == metadata.ProfileId);
                return profile != null && metadata.IncludeInPP && (score == null || isScoreIncludedInPP(profile, score));
            }
        }

        public bool AreBeatmapScoresIncludedInPP(Guid profileId, Guid beatmapId, Guid? beatmapSetId = null)
        {
            var matchingScores = getMatchingScores(profileId, score => score.BeatmapInfo?.ID == beatmapId);
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null || profile.ExcludedBeatmapIds.Contains(beatmapId))
                    return false;
                if (beatmapSetId.HasValue && profile.ExcludedBeatmapSetIds.Contains(beatmapSetId.Value)
                    && !profile.IncludedBeatmapIds.Contains(beatmapId))
                    return false;
                return matchingScores.All(score => !data.Scores.TryGetValue(score.ID, out var metadata) || metadata.IncludeInPP);
            }
        }

        public void SetBeatmapScoresIncludedInPP(Guid profileId, Guid beatmapId, bool include, Guid? beatmapSetId = null)
        {
            var matchingScores = getMatchingScores(profileId, score => score.BeatmapInfo?.ID == beatmapId);
            bool changed = false;
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                    return;

                changed |= include ? profile.ExcludedBeatmapIds.Remove(beatmapId) : profile.ExcludedBeatmapIds.Add(beatmapId);
                bool parentSetExcluded = beatmapSetId.HasValue && profile.ExcludedBeatmapSetIds.Contains(beatmapSetId.Value);
                if (include && parentSetExcluded)
                    changed |= profile.IncludedBeatmapIds.Add(beatmapId);
                else
                    changed |= profile.IncludedBeatmapIds.Remove(beatmapId);

                foreach (var score in matchingScores)
                {
                    if (!data.Scores.TryGetValue(score.ID, out var metadata) || metadata.IncludeInPP == true)
                        continue;
                    if (!include)
                        continue;
                    metadata.IncludeInPP = true;
                    changed = true;
                }
                if (changed) save();
            }
            if (changed) Changed?.Invoke();
        }

        public bool AreBeatmapSetScoresIncludedInPP(Guid profileId, Guid beatmapSetId)
        {
            var matchingScores = getMatchingScores(profileId, score => score.BeatmapInfo?.BeatmapSet?.ID == beatmapSetId);
            var beatmapIds = getBeatmapIdsInSet(beatmapSetId);
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == profileId);
                return profile != null && !profile.ExcludedBeatmapSetIds.Contains(beatmapSetId)
                       && !profile.ExcludedBeatmapIds.Overlaps(beatmapIds)
                       && matchingScores.All(score => isScoreIncludedInPP(profile, score));
            }
        }

        public void SetBeatmapSetScoresIncludedInPP(Guid profileId, Guid beatmapSetId, bool include)
        {
            var matchingScores = getMatchingScores(profileId, score => score.BeatmapInfo?.BeatmapSet?.ID == beatmapSetId);
            var beatmapIds = getBeatmapIdsInSet(beatmapSetId);
            bool changed = false;
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                    return;

                changed |= include ? profile.ExcludedBeatmapSetIds.Remove(beatmapSetId) : profile.ExcludedBeatmapSetIds.Add(beatmapSetId);
                foreach (Guid beatmapId in beatmapIds)
                {
                    changed |= profile.IncludedBeatmapIds.Remove(beatmapId);
                    changed |= profile.ExcludedBeatmapIds.Remove(beatmapId);
                }
                if (include)
                {
                    foreach (var score in matchingScores)
                    {
                        if (!data.Scores.TryGetValue(score.ID, out var metadata) || metadata.IncludeInPP)
                            continue;
                        metadata.IncludeInPP = true;
                        changed = true;
                    }
                }
                if (changed) save();
            }
            if (changed) Changed?.Invoke();
        }

        private Guid[] getBeatmapIdsInSet(Guid beatmapSetId) => realm.Run(r => r.All<BeatmapInfo>()
            .Filter($"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.ID)} == $0", beatmapSetId)
            .Select(beatmap => beatmap.ID).ToArray());

        private bool isScoreIncludedInPP(RitsuProfile profile, ScoreInfo score)
        {
            if (data.Scores.TryGetValue(score.ID, out var metadata) && !metadata.IncludeInPP)
                return false;
            return isBeatmapIncludedInPP(profile, score);
        }

        private static bool isBeatmapIncludedInPP(RitsuProfile profile, ScoreInfo score)
        {
            if (score.BeatmapInfo == null)
                return true;
            if (profile.ExcludedBeatmapIds.Contains(score.BeatmapInfo.ID))
                return false;
            return !profile.ExcludedBeatmapSetIds.Contains(score.BeatmapInfo.BeatmapSet?.ID ?? Guid.Empty)
                   || profile.IncludedBeatmapIds.Contains(score.BeatmapInfo.ID);
        }

        private ScoreInfo[] getMatchingScores(Guid profileId, Func<ScoreInfo, bool> predicate)
        {
            HashSet<Guid> ids;
            lock (sync)
                ids = data.Scores.Where(s => s.Value.ProfileId == profileId).Select(s => s.Key).ToHashSet();
            return realm.Run(r => ids.Select(id => r.Find<ScoreInfo>(id))
                .Where(score => score != null && !score.DeletePending && predicate(score!))
                .Select(score => score!)
                .Detach().ToArray());
        }

        public RitsuResultRanks? GetResultRanks(Guid scoreId, Guid beatmapId, string ruleset)
        {
            Guid profileId;
            lock (sync)
            {
                if (!data.Scores.TryGetValue(scoreId, out var metadata) || metadata.Ruleset != ruleset)
                    return null;
                profileId = metadata.ProfileId;
            }

            var mapScores = realm.Run(r => r.All<ScoreInfo>().Filter(
                $"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0"
                + $" AND {nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.Hash)} == {nameof(ScoreInfo.BeatmapHash)}"
                + $" AND {nameof(ScoreInfo.Ruleset)}.{nameof(osu.Game.Rulesets.RulesetInfo.ShortName)} == $1"
                + $" AND {nameof(ScoreInfo.DeletePending)} == false", beatmapId, ruleset).Detach()).OrderByTotalScore().ToArray();

            int allIndex = Array.FindIndex(mapScores, s => s.ID == scoreId);
            if (allIndex < 0)
                return null;

            HashSet<Guid> profileScoreIds;
            lock (sync)
                profileScoreIds = data.Scores.Where(s => s.Value.ProfileId == profileId && s.Value.Ruleset == ruleset).Select(s => s.Key).ToHashSet();

            var profileScores = mapScores.Where(s => profileScoreIds.Contains(s.ID)).ToArray();
            int profileIndex = Array.FindIndex(profileScores, s => s.ID == scoreId);
            var eligible = getEligibleScores(profileId, ruleset, out _);
            var best = bestScores(eligible);
            int bestIndex = Array.FindIndex(best, s => s.ID == scoreId);
            double previousPP = totalPP(bestScores(eligible.Where(s => s.ID != scoreId)));
            double currentPP = totalPP(best);
            return new RitsuResultRanks(bestIndex >= 0 ? bestIndex + 1 : null, allIndex + 1, profileIndex + 1,
                previousPP, currentPP);
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

            RitsuProfile? profile;
            lock (sync)
                profile = data.Scores.TryGetValue(score.ID, out var metadata)
                    ? data.Profiles.FirstOrDefault(p => p.Id == metadata.ProfileId)
                    : null;

            if (profile != null)
            {
                score.User.Username = profile.Name;
                score.User.CountryCode = profile.CountryCode;
                score.User.LocalAvatarFileName = profile.AvatarFileName;
                score.User.LocalFlagFileName = profile.FlagFileName;
            }
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
            var eligible = getEligibleScores(id, ruleset, out var scores);
            var best = bestScores(eligible);
            double factor = 1;
            double pp = totalPP(best);
            double accuracy = 0;
            foreach (var score in best)
            {
                accuracy += score.Accuracy * factor;
                factor *= 0.95;
            }
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

        public ScoreInfo[] GetBestScores(Guid id, string ruleset) => bestScores(getEligibleScores(id, ruleset, out _));

        private List<ScoreInfo> getEligibleScores(Guid id, string ruleset, out List<ScoreInfo> scores)
        {
            bool include;
            HashSet<Guid> excludedScoreIds;
            RitsuProfile? profile;
            lock (sync)
            {
                var storedProfile = data.Profiles.FirstOrDefault(p => p.Id == id);
                profile = storedProfile == null ? null : cloneProfile(storedProfile);
                include = profile?.IncludeRelaxAutopilot ?? false;
                excludedScoreIds = data.Scores.Where(s => s.Value.ProfileId == id && !s.Value.IncludeInPP).Select(s => s.Key).ToHashSet();
            }
            scores = GetScores(id, ruleset).Where(s => s.TotalScore > 0 && s.Mods.All(m => m is not ModAutoplay)).ToList();
            return scores.Where(s => !excludedScoreIds.Contains(s.ID) && s.PP.HasValue
                                     && (profile == null || isBeatmapIncludedInPP(profile, s))
                                     && (include || s.Mods.All(m => m.Acronym != "RX" && m.Acronym != "AP"))).ToList();
        }

        private static ScoreInfo[] bestScores(IEnumerable<ScoreInfo> eligible) => eligible.GroupBy(s => s.BeatmapHash)
            .Select(g => g.OrderByDescending(s => s.PP).First()).OrderByDescending(s => s.PP).ToArray();

        private static double totalPP(IReadOnlyList<ScoreInfo> best)
        {
            double factor = 1;
            double total = 0;
            foreach (var score in best)
            {
                total += score.PP!.Value * factor;
                factor *= 0.95;
            }
            return total + (417.0 - 1.0 / 3.0) * (1.0 - Math.Pow(0.995, best.Count));
        }

        private RitsuProfile activeProfile() => data.Profiles.Single(p => p.Id == data.ActiveProfileId);

        private static RitsuProfile cloneProfile(RitsuProfile profile) => new RitsuProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            IncludeRelaxAutopilot = profile.IncludeRelaxAutopilot,
            AvatarFileName = profile.AvatarFileName,
            CountryCode = profile.CountryCode,
            FlagFileName = profile.FlagFileName,
            ExcludedBeatmapIds = profile.ExcludedBeatmapIds.ToHashSet(),
            ExcludedBeatmapSetIds = profile.ExcludedBeatmapSetIds.ToHashSet(),
            IncludedBeatmapIds = profile.IncludedBeatmapIds.ToHashSet(),
        };

        private string validateName(string name, Guid? except = null)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 32)
                throw new ArgumentException("Choose a profile name between 1 and 32 characters.");
            if (data.Profiles.Any(p => p.Id != except && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("This profile name already exists.");
            return name;
        }

        private void migrateLegacyExclusions()
        {
            var scoreIds = data.Scores.Keys.ToHashSet();
            var scoreLocations = realm.Run(r => scoreIds.Select(id => r.Find<ScoreInfo>(id))
                .Where(score => score != null && !score.DeletePending && score.BeatmapInfo != null)
                .Select(score => (ScoreId: score!.ID, BeatmapId: (Guid?)score.BeatmapInfo!.ID, BeatmapSetId: (Guid?)score.BeatmapInfo.BeatmapSet?.ID, Date: score.Date))
                .ToArray());

            bool hasLegacyExclusion(IEnumerable<(Guid ScoreId, Guid? BeatmapId, Guid? BeatmapSetId, DateTimeOffset Date)> group)
            {
                var locations = group.ToArray();
                var excluded = locations.Where(location => !data.Scores[location.ScoreId].IncludeInPP).ToArray();
                return excluded.Length > 0 && (excluded.Length == locations.Length
                                               || excluded.Max(location => location.Date)
                                               <= locations.Where(location => data.Scores[location.ScoreId].IncludeInPP).Min(location => location.Date));
            }

            foreach (var profile in data.Profiles)
            {
                var profileScores = scoreLocations.Where(location => data.Scores[location.ScoreId].ProfileId == profile.Id).ToArray();
                var excludedIds = new HashSet<Guid>();
                foreach (var group in profileScores.Where(location => location.BeatmapId.HasValue).GroupBy(location => location.BeatmapId!.Value))
                {
                    if (hasLegacyExclusion(group))
                    {
                        profile.ExcludedBeatmapIds.Add(group.Key);
                        foreach (var location in group) excludedIds.Add(location.ScoreId);
                    }
                }
                foreach (var group in profileScores.Where(location => location.BeatmapSetId.HasValue).GroupBy(location => location.BeatmapSetId!.Value))
                {
                    var beatmapGroups = group.Where(location => location.BeatmapId.HasValue).GroupBy(location => location.BeatmapId!.Value);
                    if (beatmapGroups.Any() && beatmapGroups.All(hasLegacyExclusion))
                    {
                        profile.ExcludedBeatmapSetIds.Add(group.Key);
                        foreach (var location in group) excludedIds.Add(location.ScoreId);
                    }
                }

                foreach (Guid scoreId in excludedIds)
                    data.Scores[scoreId].IncludeInPP = true;
            }

            data.Version = 2;
            save();
        }

        private void save()
        {
            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        public sealed class ProfileStore
        {
            public int Version { get; set; } = 2;
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
        public CountryCode CountryCode { get; set; }
        public string? FlagFileName { get; set; }
        public HashSet<Guid> ExcludedBeatmapIds { get; set; } = new HashSet<Guid>();
        public HashSet<Guid> ExcludedBeatmapSetIds { get; set; } = new HashSet<Guid>();
        public HashSet<Guid> IncludedBeatmapIds { get; set; } = new HashSet<Guid>();
        public override string ToString() => Name;
    }

    public sealed class RitsuScoreMetadata
    {
        public Guid ProfileId { get; set; }
        public string Ruleset { get; set; } = string.Empty;
        public int CalculatorVersion { get; set; }
        public string ModsJson { get; set; } = string.Empty;
        public bool IncludeInPP { get; set; } = true;
    }

    public sealed record RitsuPpPoint(DateTimeOffset Date, double PP);
    public sealed record RitsuResultRanks(int? BestPlay, int AllMapScores, int ProfileMapScores, double PreviousPP, double CurrentPP);

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
