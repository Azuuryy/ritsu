// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Offline;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Scoring;
using osu.Game.Users;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Persists local profiles and their play statistics in "osu-local/" inside the (shared) osu! data folder.
    /// Nothing here is written to the realm database, so sharing the database with the official game stays safe.
    /// </summary>
    public class LocalProfileStore
    {
        public const string DIRECTORY = @"osu-local";
        public const string AVATAR_DIRECTORY = @"avatars";

        private const string profiles_file = DIRECTORY + @"/profiles.json";
        private const string performance_file = DIRECTORY + @"/performance-cache-v2.json";

        public const int MAX_NAME_LENGTH = 32;

        /// <summary>
        /// Invoked (on any thread) whenever profiles, the active profile or statistics change.
        /// </summary>
        public event Action? Changed;

        public Storage AvatarStorage { get; }

        /// <summary>
        /// Tags given to beatmaps locally (song select's beatmap details, search, profile auto-switching).
        /// </summary>
        public LocalBeatmapTags BeatmapTags { get; }

        private readonly Storage storage;
        private readonly object sync = new object();

        private StoreData data = new StoreData();
        private readonly object performanceSaveLock = new object();
        private Dictionary<Guid, ScorePerformance> performanceCache = new Dictionary<Guid, ScorePerformance>();

        public LocalProfileStore(Storage storage)
        {
            this.storage = storage;
            AvatarStorage = storage.GetStorageForDirectory($@"{DIRECTORY}/{AVATAR_DIRECTORY}");

            data = read<StoreData>(profiles_file) ?? new StoreData();
            data.Profiles ??= new List<LocalProfile>();
            data.Options ??= new LocalProfileOptions();
            data.PerformanceExclusions = (data.PerformanceExclusions ?? new LocalPerformanceExclusions()).Clone();
            LocalPriorityPolicy.PerformanceExclusions = data.PerformanceExclusions.Clone();

            foreach (var p in data.Profiles)
            {
                p.Stats ??= new Dictionary<string, LocalPlayStatistics>();
                p.LinkedOwners ??= new List<LinkedScoreOwner>();
                p.SyncedScores ??= new List<SyncedScore>();
                p.SectionOrder ??= new List<string>();
            }

            if (data.ActiveProfileId != null && data.Profiles.All(p => p.Id != data.ActiveProfileId))
                data.ActiveProfileId = null;

            performanceCache = read<Dictionary<Guid, ScorePerformance>>(performance_file) ?? new Dictionary<Guid, ScorePerformance>();

            BeatmapTags = new LocalBeatmapTags(storage);

            // Song select only sees local tags through this (diagnostics can turn them off).
            LocalBeatmapTags.Current = LocalPriorityDiagnostics.IsDisabled(@"tags") ? null : BeatmapTags;
        }

        #region Profiles

        internal bool MigrateRitsuProfiles(RitsuProfileManager profiles)
        {
            if (data.RitsuProfilesMigrated)
                return false;

            bool hadProfiles = data.Profiles.Count > 0;

            foreach (var old in profiles.LoadedExistingProfiles ? profiles.GetProfiles() : Array.Empty<RitsuProfile>())
            {
                var existing = data.Profiles.FirstOrDefault(profile => profile.Id == old.Id
                    || string.Equals(profile.Name, old.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (data.ActiveProfileId == existing.Id)
                        data.ActiveProfileId = old.Id;

                    existing.Id = old.Id;
                    continue;
                }

                var profile = new LocalProfile
                {
                    Id = old.Id,
                    UserId = allocateUserId(),
                    Name = old.Name,
                    Country = old.CountryCode,
                    IncludeRelaxAutopilot = old.IncludeRelaxAutopilot,
                    Created = DateTimeOffset.Now,
                };

                if (old.AvatarFileName != null && profiles.AvatarStorage.Exists(old.AvatarFileName)
                    && profiles.AvatarStorage.GetStream(old.AvatarFileName) is Stream image)
                {
                    using (image)
                    using (var target = AvatarStorage.CreateFileSafely(old.AvatarFileName))
                        image.CopyTo(target);
                    profile.AvatarFile = old.AvatarFileName;
                }

                data.Profiles.Add(profile);
            }

            if (!hadProfiles && profiles.LoadedExistingProfiles)
                data.ActiveProfileId = profiles.ActiveProfileId;

            foreach (var profile in data.Profiles)
                profiles.EnsureProfile(profile.Id, profile.Name);

            return true;
        }

        internal void CompleteRitsuMigration()
        {
            data.RitsuProfilesMigrated = true;
            save();
            Flush();
        }

        public IReadOnlyList<LocalProfile> GetProfiles()
        {
            lock (sync)
                return data.Profiles.Select(p => p.Clone()).ToArray();
        }

        public LocalProfile? GetProfile(Guid id)
        {
            lock (sync)
                return data.Profiles.FirstOrDefault(p => p.Id == id)?.Clone();
        }

        /// <summary>
        /// Profiles that switch in automatically (in profile order), without copying anything else — checked on beatmap changes.
        /// </summary>
        public IReadOnlyList<(Guid Id, string Name, LocalAutoSwitchRules Rules)> GetAutoSwitchRules()
        {
            lock (sync)
            {
                return data.Profiles.Where(p => p.AutoSwitch?.Enabled == true && p.AutoSwitch.HasAnyCondition)
                           .Select(p => (p.Id, p.Name, p.AutoSwitch!.Clone()))
                           .ToArray();
            }
        }

        /// <summary>
        /// A local profile's picture file (for avatars on leaderboards), without copying the profile.
        /// </summary>
        public string? GetAvatarFile(int userId)
        {
            lock (sync)
                return data.Profiles.FirstOrDefault(p => p.UserId == userId)?.AvatarFile;
        }

        public LocalProfile? GetProfileByUserId(int userId)
        {
            lock (sync)
                return data.Profiles.FirstOrDefault(p => p.UserId == userId)?.Clone();
        }

        /// <summary>
        /// The active local profile, or null when the online account is in use.
        /// </summary>
        public LocalProfile? ActiveProfile
        {
            get
            {
                lock (sync)
                    return data.ActiveProfileId == null ? null : data.Profiles.FirstOrDefault(p => p.Id == data.ActiveProfileId)?.Clone();
            }
        }

        /// <summary>
        /// Whether local profiles may use the signed-in online account for read-only lookups (beatmaps, tags, leaderboards).
        /// </summary>
        public bool LocalProfilesUseOnlineAccount
        {
            get
            {
                lock (sync)
                    return data.LocalProfilesUseOnlineAccount;
            }
        }

        public void SetLocalProfilesUseOnlineAccount(bool value)
        {
            lock (sync)
            {
                if (data.LocalProfilesUseOnlineAccount == value)
                    return;

                data.LocalProfilesUseOnlineAccount = value;
                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Settings → Ritsu → Local profiles (confirmations, sync suggestion, profiles with mod rules).
        /// </summary>
        public LocalProfileOptions Options
        {
            get
            {
                lock (sync)
                    return data.Options.Clone();
            }
        }

        public void SetOptions(LocalProfileOptions options)
        {
            lock (sync)
            {
                if (data.Options.Equals(options))
                    return;

                data.Options = options.Clone();
                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Puts the profiles in this order (ids not listed keep their relative order at the end).
        /// </summary>
        public void ReorderProfiles(IReadOnlyList<Guid> order)
        {
            lock (sync)
            {
                var reordered = order.Select(id => data.Profiles.FirstOrDefault(p => p.Id == id))
                                     .Where(p => p != null)
                                     .Select(p => p!)
                                     .Distinct()
                                     .ToList();

                reordered.AddRange(data.Profiles.Where(p => !reordered.Contains(p)));

                if (reordered.SequenceEqual(data.Profiles))
                    return;

                data.Profiles = reordered;
                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Sets (or with null, removes) when the profile is switched to automatically.
        /// </summary>
        public void SetAutoSwitch(Guid id, LocalAutoSwitchRules? rules)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                profile.AutoSwitch = rules?.Clone();
                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Remembers the order of the profile page's sections. The page reorders itself, so no change notification is sent
        /// (that would recalculate the page's statistics).
        /// </summary>
        public void SetSectionOrder(Guid id, IEnumerable<string> order)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                profile.SectionOrder = order.ToList();
                save();
            }
        }

        /// <summary>
        /// Sets (or with null, removes) the profile's mod rules.
        /// </summary>
        public void SetModRules(Guid id, LocalModRules? rules)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                profile.ModRules = rules?.Clone();
                save();
            }

            Changed?.Invoke();
        }

        public void SetActiveProfile(Guid? id)
        {
            lock (sync)
            {
                if (id != null && data.Profiles.All(p => p.Id != id))
                    return;

                if (data.ActiveProfileId == id)
                    return;

                data.ActiveProfileId = id;
                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Creates a new local profile. Throws <see cref="ArgumentException"/> with a user-facing message on invalid input.
        /// </summary>
        public LocalProfile CreateProfile(string name, CountryCode country = CountryCode.Unknown)
        {
            LocalProfile profile;

            lock (sync)
            {
                name = validateName(name, null);

                profile = new LocalProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = allocateUserId(),
                    Name = name,
                    Country = country,
                    Created = DateTimeOffset.Now,
                };

                data.Profiles.Add(profile);
                save();
                profile = profile.Clone();
            }

            Changed?.Invoke();
            return profile;
        }

        /// <summary>
        /// Creates a profile with a free default name ("Player 1", "Player 2", ...).
        /// </summary>
        private int allocateUserId()
        {
            int id;
            do
                id = LocalPriorityPolicy.LOCAL_PROFILE_ID_CEILING - Random.Shared.Next(0, 1_000_000_000);
            while (data.Profiles.Any(profile => profile.UserId == id));

            return id;
        }

        public LocalProfile CreateDefaultProfile()
        {
            string name;

            lock (sync)
            {
                int n = data.Profiles.Count + 1;
                while (data.Profiles.Any(p => string.Equals(p.Name, $@"Player {n}", StringComparison.OrdinalIgnoreCase)))
                    n++;
                name = $@"Player {n}";
            }

            return CreateProfile(name);
        }

        public void RenameProfile(Guid id, string name)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException(@"That profile no longer exists.");
                profile.Name = validateName(name, id);
                save();
            }

            Changed?.Invoke();
        }

        public void SetCountry(Guid id, CountryCode country)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null || profile.Country == country)
                    return;

                profile.Country = country;
                save();
            }

            Changed?.Invoke();
        }

        public void SetRankingOptions(Guid id, bool? includeRelaxAutopilot = null, bool? countAllAsRanked = null)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                profile.IncludeRelaxAutopilot = includeRelaxAutopilot ?? profile.IncludeRelaxAutopilot;
                profile.CountAllAsRanked = countAllAsRanked ?? profile.CountAllAsRanked;
                save();
            }

            Changed?.Invoke();
        }

        public const int AVATAR_SIZE = 256;

        private string writeAvatar(Guid id, Stream source)
        {
            string file = $@"{id:N}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png";

            using (var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(source))
            {
                if (image.Width > AVATAR_SIZE || image.Height > AVATAR_SIZE)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(AVATAR_SIZE, AVATAR_SIZE),
                        Mode = ResizeMode.Max,
                    }));
                }

                using (var destination = AvatarStorage.CreateFileSafely(file))
                    image.SaveAsPng(destination);
            }

            return file;
        }

        /// <summary>
        /// Pictures set by older Ritsu builds were kept at full size: scale them down once (run in the background at startup).
        /// </summary>
        public void ShrinkOversizedAvatars()
        {
            foreach (var profile in GetProfiles().Where(p => p.AvatarFile != null))
            {
                string oldFile = profile.AvatarFile!;

                try
                {
                    SixLabors.ImageSharp.ImageInfo? info;

                    using (var stream = AvatarStorage.GetStream(oldFile))
                        info = stream == null ? null : SixLabors.ImageSharp.Image.Identify(stream);

                    if (info == null || (info.Width <= AVATAR_SIZE && info.Height <= AVATAR_SIZE))
                        continue;

                    string newFile;

                    using (var stream = AvatarStorage.GetStream(oldFile))
                        newFile = writeAvatar(profile.Id, stream);

                    lock (sync)
                    {
                        var stored = data.Profiles.FirstOrDefault(p => p.Id == profile.Id);

                        if (stored == null || stored.AvatarFile != oldFile)
                        {
                            AvatarStorage.Delete(newFile);
                            continue;
                        }

                        stored.AvatarFile = newFile;
                        save();
                    }

                    AvatarStorage.Delete(oldFile);
                    Changed?.Invoke();
                }
                catch (Exception e)
                {
                    Logger.Log($@"Ritsu: could not scale down the picture of ""{profile.Name}"": {e.Message}");
                }
            }
        }

        public void SetAvatar(Guid id, FileInfo? image)
        {
            string? newFile = null;

            if (image != null)
            {
                using (var source = image.OpenRead())
                    newFile = writeAvatar(id, source);
            }

            string? oldFile;

            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);

                if (profile == null)
                {
                    if (newFile != null)
                        AvatarStorage.Delete(newFile);
                    return;
                }

                oldFile = profile.AvatarFile;
                profile.AvatarFile = newFile;
                save();
            }

            if (oldFile != null)
            {
                try
                {
                    AvatarStorage.Delete(oldFile);
                }
                catch (Exception e)
                {
                    Logger.Log($@"Could not delete old local profile picture {oldFile}: {e.Message}");
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Removes a profile from the list. Its scores stay in the database (they are simply no longer linked to a profile).
        /// </summary>
        public void DeleteProfile(Guid id)
        {
            string? avatar;

            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                avatar = profile.AvatarFile;
                data.Profiles.Remove(profile);
                if (data.ActiveProfileId == id)
                    data.ActiveProfileId = null;
                save();
            }

            if (avatar != null)
            {
                try
                {
                    AvatarStorage.Delete(avatar);
                }
                catch
                {
                    // not important.
                }
            }

            Changed?.Invoke();
        }

        private string validateName(string name, Guid? except)
        {
            name = (name ?? string.Empty).Trim();

            if (name.Length == 0)
                throw new ArgumentException(@"Please type a name for the profile.");
            if (name.Length > MAX_NAME_LENGTH)
                throw new ArgumentException($@"Profile names can be at most {MAX_NAME_LENGTH} characters long.");
            if (data.Profiles.Any(p => p.Id != except && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($@"A profile called ""{name}"" already exists.");

            return name;
        }

        #endregion

        #region Play statistics

        /// <summary>
        /// Records one attempt (finished, failed, quit or restarted) for a local profile.
        /// </summary>
        public void RecordPlay(int userId, string ruleset, double playSeconds, IReadOnlyDictionary<HitResult, int> hits, string? beatmapHash = null)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.UserId == userId);
                if (profile == null)
                    return;

                if (!profile.Stats.TryGetValue(ruleset, out var stats))
                    profile.Stats[ruleset] = stats = new LocalPlayStatistics();

                stats.PlayCount++;
                stats.PlayTimeSeconds += Math.Max(0, playSeconds);
                stats.LastPlayed = DateTimeOffset.Now;
                stats.AddPlay(beatmapHash, DateTimeOffset.Now, 1);

                foreach (var (result, count) in hits)
                {
                    if (count <= 0)
                        continue;

                    stats.Hits.TryGetValue(result, out long existing);
                    stats.Hits[result] = existing + count;
                }

                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// A score transferred between profiles takes its play with it: one play (its hits and play time) is added to the profile it goes to,
        /// and taken off the local profile it came from (never below zero). A synced play leaving its profile is just forgotten there,
        /// since it was counted from the score rather than recorded as an attempt.
        /// </summary>
        /// <summary>
        /// Plays recorded on days before <paramref name="cutoff"/> (every profile and ruleset), as counted in the play history.
        /// </summary>
        public long CountDailyPlaysBefore(DateTime cutoff)
        {
            lock (sync)
                return data.Profiles.SelectMany(p => p.Stats.Values).SelectMany(s => s.DailyPlays ?? new Dictionary<string, long>())
                           .Where(d => isBefore(d.Key, cutoff)).Sum(d => Math.Max(0, d.Value));
        }

        /// <summary>
        /// Forgets plays recorded on days before <paramref name="cutoff"/> (every profile and ruleset): they leave the play history
        /// and the play count.
        /// </summary>
        public void RemoveDailyPlaysBefore(DateTime cutoff)
        {
            bool changed = false;

            lock (sync)
            {
                foreach (var stats in data.Profiles.SelectMany(p => p.Stats.Values))
                {
                    if (stats.DailyPlays == null)
                        continue;

                    var old = stats.DailyPlays.Where(d => isBefore(d.Key, cutoff)).ToArray();

                    if (old.Length == 0)
                        continue;

                    // Replaced, not changed in place: profile copies handed out by the store share the dictionaries.
                    stats.DailyPlays = stats.DailyPlays.Where(d => !isBefore(d.Key, cutoff)).ToDictionary(d => d.Key, d => d.Value);
                    stats.PlayCount = Math.Max(0, stats.PlayCount - old.Sum(d => Math.Max(0, d.Value)));
                    changed = true;
                }

                if (changed)
                    save();
            }

            if (changed)
                Changed?.Invoke();
        }

        private static bool isBefore(string day, DateTime cutoff) =>
            DateTime.TryParseExact(day, LocalPlayStatistics.DAY_FORMAT, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date) && date < cutoff;

        public void MovePlay(Guid sourceProfileId, Guid toProfileId, string ruleset, double playSeconds, IReadOnlyDictionary<HitResult, int> hits, string? beatmapHash, DateTimeOffset date)
        {
            lock (sync)
            {
                var source = data.Profiles.FirstOrDefault(p => p.Id == sourceProfileId);

                if (source != null)
                {
                    if (source.Stats.TryGetValue(ruleset, out var sourceStats))
                    {
                        sourceStats.PlayCount = Math.Max(0, sourceStats.PlayCount - 1);
                        sourceStats.AddPlay(beatmapHash, date, -1);
                        sourceStats.PlayTimeSeconds = Math.Max(0, sourceStats.PlayTimeSeconds - Math.Max(0, playSeconds));

                        foreach (var (result, count) in hits)
                        {
                            if (count <= 0 || !sourceStats.Hits.TryGetValue(result, out long existing))
                                continue;

                            sourceStats.Hits[result] = Math.Max(0, existing - count);
                        }
                    }
                }

                var target = data.Profiles.FirstOrDefault(p => p.Id == toProfileId);

                if (target != null)
                {
                    if (!target.Stats.TryGetValue(ruleset, out var targetStats))
                        target.Stats[ruleset] = targetStats = new LocalPlayStatistics();

                    targetStats.PlayCount++;
                    targetStats.AddPlay(beatmapHash, date, 1);
                    targetStats.PlayTimeSeconds += Math.Max(0, playSeconds);

                    foreach (var (result, count) in hits)
                    {
                        if (count <= 0)
                            continue;

                        targetStats.Hits.TryGetValue(result, out long existing);
                        targetStats.Hits[result] = existing + count;
                    }
                }

                save();
            }

            Changed?.Invoke();
        }

        public LocalPlayStatistics GetPlayStatistics(Guid profileId, string ruleset)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == profileId);
                return profile != null && profile.Stats.TryGetValue(ruleset, out var stats) ? stats.Clone() : new LocalPlayStatistics();
            }
        }

        #endregion

        #region PP exclusions

        /// <summary>
        /// A copy of what is excluded from PP.
        /// </summary>
        public LocalPerformanceExclusions PerformanceExclusions
        {
            get
            {
                lock (sync)
                    return data.PerformanceExclusions.Clone();
            }
        }

        public void SetExcludedFromPerformance(LocalPerformanceExclusionKind kind, Guid id, bool excluded)
        {
            lock (sync)
            {
                // Replaced, never changed in place: copies handed out share nothing, but keep it the same as the other lists.
                var exclusions = data.PerformanceExclusions.Clone();

                var list = kind switch
                {
                    LocalPerformanceExclusionKind.Score => exclusions.Scores,
                    LocalPerformanceExclusionKind.Beatmap => exclusions.Beatmaps,
                    _ => exclusions.BeatmapSets,
                };

                if (list.Contains(id) == excluded)
                    return;

                if (excluded)
                    list.Add(id);
                else
                    list.Remove(id);

                data.PerformanceExclusions = exclusions;
                LocalPriorityPolicy.PerformanceExclusions = exclusions.Clone();
                save();
            }

            Changed?.Invoke();
        }

        #endregion

        #region Performance cache

        internal sealed record ScorePerformance(string Key, double? PP);

        internal bool TryGetCachedPerformance(Guid scoreId, string key, out double? pp)
        {
            lock (sync)
            {
                if (performanceCache.TryGetValue(scoreId, out var entry) && entry.Key == key)
                {
                    pp = entry.PP;
                    return true;
                }

                pp = null;
                return false;
            }
        }

        /// <summary>
        /// Remembers calculated PP. A null value marks a score whose beatmap difficulty couldn't be calculated (broken or unsupported beatmap):
        /// it is never calculated again (no repeated errors), and <see cref="CalculationsFailed"/> tells about new ones once.
        /// </summary>
        internal void SetCachedPerformance(IReadOnlyDictionary<Guid, ScorePerformance> values)
        {
            if (values.Count == 0)
                return;

            int newlyFailed = 0;

            lock (performanceSaveLock)
            {
                Dictionary<Guid, ScorePerformance> snapshot;
                lock (sync)
                {
                    foreach (var (id, entry) in values)
                    {
                        if (entry.PP == null && (!performanceCache.TryGetValue(id, out var existing) || existing != entry))
                            newlyFailed++;

                        performanceCache[id] = entry;
                    }

                    snapshot = new Dictionary<Guid, ScorePerformance>(performanceCache);
                }

                write(performance_file, snapshot);
            }

            if (newlyFailed > 0)
                CalculationsFailed?.Invoke(newlyFailed);
        }

        /// <summary>
        /// Invoked (from any thread) with how many scores newly failed to calculate.
        /// </summary>
        public event Action<int>? CalculationsFailed;

        /// <summary>
        /// Scores whose beatmap difficulty couldn't be calculated.
        /// </summary>
        public IReadOnlyList<Guid> GetFailedCalculations()
        {
            lock (sync)
                return performanceCache.Where(p => p.Value.PP == null).Select(p => p.Key).ToArray();
        }

        #endregion

        private T? read<T>(string filename)
            where T : class
        {
            try
            {
                if (!storage.Exists(filename))
                    return null;

                using (var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open))
                using (var reader = new StreamReader(stream))
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Could not read {filename}; starting with empty data. The broken file was kept as {filename}.broken");

                try
                {
                    storage.Move(filename, filename + @".broken");
                }
                catch
                {
                    // ignore.
                }

                return null;
            }
        }

        private bool savePending;

        /// <summary>
        /// Saves profiles.json shortly after a change, on a background thread: several changes in a row (switching profiles,
        /// recording a play) become one write, and the update thread never serialises the whole file.
        /// Call with the lock held (every change does).
        /// </summary>
        private void save()
        {
            if (savePending)
                return;

            savePending = true;

            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => Flush());
        }

        /// <summary>
        /// Writes pending changes now (also used when the game closes).
        /// </summary>
        public void Flush()
        {
            lock (sync)
            {
                if (!savePending)
                    return;

                savePending = false;
                write(profiles_file, data);
            }
        }

        private void write(string filename, object value)
        {
            try
            {
                using (var stream = storage.CreateFileSafely(filename))
                using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(value, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Could not save {filename}");
            }
        }

        private class StoreData
        {
            public int Version { get; set; } = 1;
            public Guid? ActiveProfileId { get; set; }
            public bool RitsuProfilesMigrated { get; set; }
            public List<LocalProfile> Profiles { get; set; } = new List<LocalProfile>();

            /// <summary>
            /// Settings → Ritsu: local profiles may use the signed-in online account for beatmap / tag / leaderboard lookups.
            /// </summary>
            public bool LocalProfilesUseOnlineAccount { get; set; }

            public LocalProfileOptions Options { get; set; } = new LocalProfileOptions();

            /// <summary>
            /// Plays, beatmaps and beatmap sets that never give PP (every local profile).
            /// </summary>
            public LocalPerformanceExclusions PerformanceExclusions { get; set; } = new LocalPerformanceExclusions();
        }
    }

    public class LocalProfile
    {
        public Guid Id { get; set; }

        /// <summary>
        /// The (negative) user ID saved into every score set on this profile. Never changes, so renames keep their scores.
        /// </summary>
        public int UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public CountryCode Country { get; set; }

        /// <summary>
        /// File name inside "osu-local/avatars", or null for the default picture.
        /// </summary>
        public string? AvatarFile { get; set; }

        public DateTimeOffset Created { get; set; }

        /// <summary>
        /// Count Relax / Autopilot plays towards this profile's PP.
        /// </summary>
        public bool IncludeRelaxAutopilot { get; set; }

        /// <summary>
        /// Count every play as if it was set on a ranked / approved beatmap with ranked mods (this profile only).
        /// </summary>
        public bool CountAllAsRanked { get; set; }

        /// <summary>
        /// Per-ruleset play statistics (keyed by ruleset short name).
        /// </summary>
        public Dictionary<string, LocalPlayStatistics> Stats { get; set; } = new Dictionary<string, LocalPlayStatistics>();

        /// <summary>
        /// Players whose scores already were in the database under this profile's name and were synced to it (shown as this profile's plays).
        /// The database itself is never changed: the scores keep their original player, this list only links them.
        /// </summary>
        public List<LinkedScoreOwner> LinkedOwners { get; set; } = new List<LinkedScoreOwner>();

        /// <summary>
        /// Scores synced to this profile (moved to it in the database), with their original player, so "unsync" can give them back.
        /// Old-style <see cref="LinkedOwners"/> are turned into these on startup.
        /// </summary>
        public List<SyncedScore> SyncedScores { get; set; } = new List<SyncedScore>();

        /// <summary>
        /// Mods this profile blocks / only allows / applies on switching, or null for no rules.
        /// </summary>
        public LocalModRules? ModRules { get; set; }

        /// <summary>
        /// Switch to this profile automatically when a matching beatmap is selected in song select, or null for never.
        /// </summary>
        public LocalAutoSwitchRules? AutoSwitch { get; set; }

        /// <summary>
        /// The order of the profile page's sections (e.g. "scores", "historical"), set by dragging their tabs; empty for the default order.
        /// </summary>
        public List<string> SectionOrder { get; set; } = new List<string>();

        public APIUser ToUser() => new APIUser
        {
            Id = UserId,
            Username = Name,
            CountryCode = Country,
        };

        public LocalProfile Clone() => new LocalProfile
        {
            Id = Id,
            UserId = UserId,
            Name = Name,
            Country = Country,
            AvatarFile = AvatarFile,
            Created = Created,
            IncludeRelaxAutopilot = IncludeRelaxAutopilot,
            CountAllAsRanked = CountAllAsRanked,
            // Play statistics (with their per-beatmap and per-day counters) are read through GetPlayStatistics only;
            // copies handed out by the store leave them out, which keeps looking up profiles cheap.
            Stats = new Dictionary<string, LocalPlayStatistics>(),
            // Shared, not copied: the store replaces these lists instead of changing them (a profile can have hundreds of synced plays).
            LinkedOwners = LinkedOwners,
            SyncedScores = SyncedScores,
            ModRules = ModRules?.Clone(),
            AutoSwitch = AutoSwitch?.Clone(),
            SectionOrder = new List<string>(SectionOrder ?? new List<string>()),
        };

        public override string ToString() => Name;
    }

    /// <summary>
    /// Settings → Ritsu → Local profiles.
    /// </summary>
    public class LocalProfileOptions : IEquatable<LocalProfileOptions>
    {
        /// <summary>
        /// "Delete profile (keep its scores)" without a confirmation.
        /// </summary>
        public bool DeleteKeepingScoresWithoutAsking { get; set; }

        /// <summary>
        /// "Delete profile and all its scores" without a confirmation.
        /// </summary>
        public bool DeleteWithScoresWithoutAsking { get; set; }

        /// <summary>
        /// After creating / renaming a profile, suggest syncing plays already saved under its name.
        /// </summary>
        public bool SuggestSyncingSameNamePlays { get; set; } = true;

        /// <summary>
        /// Shows a second "new profile" button that first opens mod select, to create a profile with mod rules.
        /// </summary>
        public bool ProfilesWithModRules { get; set; }

        /// <summary>
        /// Show a notification when a profile is switched to (or back) automatically.
        /// </summary>
        public bool AutoSwitchNotifications { get; set; } = true;

        /// <summary>
        /// Check auto-switch rules again when a beatmap is started from song select.
        /// </summary>
        public bool SwitchOnBeatmapStart { get; set; } = true;

        /// <summary>
        /// Calculate and show "Total Accuracy" on the profile page.
        /// </summary>
        public bool ShowTotalAccuracy { get; set; } = true;

        /// <summary>
        /// Every local profile counts every beatmap and mod as ranked.
        /// </summary>
        public bool CountEverythingAsRanked { get; set; }

        /// <summary>
        /// Scores that don't count for PP are listed (faded) among the best scores instead of being left out.
        /// </summary>
        public bool ShowNonCountingScores { get; set; }

        public LocalProfileOptions Clone() => (LocalProfileOptions)MemberwiseClone();

        public bool Equals(LocalProfileOptions? other) => other != null
                                                          && other.DeleteKeepingScoresWithoutAsking == DeleteKeepingScoresWithoutAsking
                                                          && other.DeleteWithScoresWithoutAsking == DeleteWithScoresWithoutAsking
                                                          && other.SuggestSyncingSameNamePlays == SuggestSyncingSameNamePlays
                                                          && other.ProfilesWithModRules == ProfilesWithModRules
                                                          && other.AutoSwitchNotifications == AutoSwitchNotifications
                                                          && other.SwitchOnBeatmapStart == SwitchOnBeatmapStart
                                                          && other.ShowTotalAccuracy == ShowTotalAccuracy
                                                          && other.CountEverythingAsRanked == CountEverythingAsRanked
                                                          && other.ShowNonCountingScores == ShowNonCountingScores;

        public override bool Equals(object? obj) => obj is LocalProfileOptions other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            HashCode.Combine(DeleteKeepingScoresWithoutAsking, DeleteWithScoresWithoutAsking, SuggestSyncingSameNamePlays, ProfilesWithModRules, AutoSwitchNotifications),
            HashCode.Combine(SwitchOnBeatmapStart, ShowTotalAccuracy, CountEverythingAsRanked, ShowNonCountingScores));
    }

    /// <summary>
    /// What a profile does with mods. The mods are kept as acronyms + settings (like mod presets), so they work in every ruleset that has them.
    /// </summary>
    public class LocalModRules
    {
        public List<APIMod> Mods { get; set; } = new List<APIMod>();

        /// <summary>
        /// The chosen mods can't be used on this profile.
        /// </summary>
        public bool BlockSelected { get; set; }

        /// <summary>
        /// Only the chosen mods can be used (any settings).
        /// </summary>
        public bool OnlyAllowSelected { get; set; }

        /// <summary>
        /// Only the chosen mods can be used, and only with the chosen settings (e.g. Difficulty Adjust stays at CS 9).
        /// </summary>
        public bool OnlyAllowSelectedSettings { get; set; }

        /// <summary>
        /// The chosen mods are selected whenever this profile becomes active.
        /// </summary>
        public bool ApplyOnSwitch { get; set; }

        public bool HasAnyRule => Mods.Count > 0 && (BlockSelected || OnlyAllowSelected || OnlyAllowSelectedSettings || ApplyOnSwitch);

        public LocalModRules Clone() => new LocalModRules
        {
            Mods = Mods.Select(m => new APIMod { Acronym = m.Acronym, Settings = new Dictionary<string, object>(m.Settings) }).ToList(),
            BlockSelected = BlockSelected,
            OnlyAllowSelected = OnlyAllowSelected,
            OnlyAllowSelectedSettings = OnlyAllowSelectedSettings,
            ApplyOnSwitch = ApplyOnSwitch,
        };

        /// <summary>
        /// Short description, e.g. "Only HD, HR (exact settings) · applied on switch".
        /// </summary>
        public string Describe()
        {
            string mods = string.Join(@", ", Mods.Select(m => m.Acronym));
            var parts = new List<string>();

            if (BlockSelected)
                parts.Add($@"Blocks {mods}");
            else if (OnlyAllowSelectedSettings)
                parts.Add($@"Only {mods} (exact settings)");
            else if (OnlyAllowSelected)
                parts.Add($@"Only {mods}");

            if (ApplyOnSwitch && !BlockSelected)
                parts.Add(parts.Count == 0 ? $@"Selects {mods} on switch" : @"selected on switch");

            return parts.Count == 0 ? mods : string.Join(@" · ", parts);
        }
    }

    /// <summary>
    /// A score moved to a local profile by syncing, and the player it belonged to before.
    /// </summary>
    public class SyncedScore
    {
        public Guid ScoreId { get; set; }

        public int OriginalUserId { get; set; }

        public string OriginalUsername { get; set; } = string.Empty;

        public CountryCode OriginalCountry { get; set; }

        public SyncedScore Clone() => (SyncedScore)MemberwiseClone();
    }

    /// <summary>
    /// A player (user ID + name, exactly as saved in the scores) whose scores count as a local profile's own.
    /// </summary>
    public class LinkedScoreOwner : IEquatable<LinkedScoreOwner>
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public LinkedScoreOwner Clone() => new LinkedScoreOwner { UserId = UserId, Username = Username };

        public bool Equals(LinkedScoreOwner? other) => other != null && other.UserId == UserId && other.Username == Username;

        public override bool Equals(object? obj) => obj is LinkedScoreOwner other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(UserId, Username);
    }

    /// <summary>
    /// Counted for every attempt (like the osu! website does), not only for saved scores.
    /// </summary>
    public class LocalPlayStatistics
    {
        public long PlayCount { get; set; }

        /// <summary>
        /// Real seconds spent playing: how far into the map each attempt got (first to last object), adjusted for rate-changing mods.
        /// </summary>
        public double PlayTimeSeconds { get; set; }

        public Dictionary<HitResult, long> Hits { get; set; } = new Dictionary<HitResult, long>();

        public DateTimeOffset? LastPlayed { get; set; }

        /// <summary>
        /// Counted plays per beatmap (keyed by the beatmap's MD5 hash), for "Most Played Beatmaps".
        /// </summary>
        public Dictionary<string, long> BeatmapPlays { get; set; } = new Dictionary<string, long>();

        /// <summary>
        /// Counted plays per local day ("yyyy-MM-dd"), for "Play History" (by month or by week).
        /// </summary>
        public Dictionary<string, long> DailyPlays { get; set; } = new Dictionary<string, long>();

        public const string DAY_FORMAT = @"yyyy-MM-dd";

        public long Count(HitResult result) => Hits.TryGetValue(result, out long c) ? c : 0;

        /// <summary>
        /// Adds (or with a negative <paramref name="delta"/>, removes) a play on a beatmap / day, never below zero.
        /// </summary>
        public void AddPlay(string? beatmapHash, DateTimeOffset date, long delta)
        {
            if (!string.IsNullOrEmpty(beatmapHash))
                BeatmapPlays[beatmapHash] = Math.Max(0, BeatmapPlays.GetValueOrDefault(beatmapHash) + delta);

            string day = date.ToLocalTime().ToString(DAY_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
            DailyPlays[day] = Math.Max(0, DailyPlays.GetValueOrDefault(day) + delta);
        }

        /// <summary>
        /// Total hits as the website counts them: every basic judgement that isn't a miss (300 / 100 / 50 and mania's geki / katu).
        /// </summary>
        public long TotalHits => Count(HitResult.Perfect) + Count(HitResult.Great) + Count(HitResult.Good) + Count(HitResult.Ok) + Count(HitResult.Meh);

        public LocalPlayStatistics Clone() => new LocalPlayStatistics
        {
            PlayCount = PlayCount,
            PlayTimeSeconds = PlayTimeSeconds,
            Hits = new Dictionary<HitResult, long>(Hits),
            LastPlayed = LastPlayed,
            BeatmapPlays = new Dictionary<string, long>(BeatmapPlays ?? new Dictionary<string, long>()),
            DailyPlays = new Dictionary<string, long>(DailyPlays ?? new Dictionary<string, long>()),
        };
    }
}
