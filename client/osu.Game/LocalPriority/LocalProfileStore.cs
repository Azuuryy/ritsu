// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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
    /// Persists local profiles and their play statistics in "ritsu/local-profiles/" inside the (shared) osu! data folder.
    /// Nothing here is written to the realm database, so sharing the database with the official game stays safe.
    /// </summary>
    public class LocalProfileStore
    {
        public const string DIRECTORY = @"ritsu/local-profiles";
        public const string AVATAR_DIRECTORY = @"avatars";

        private const string profiles_file = DIRECTORY + @"/profiles.json";
        private const string performance_file = DIRECTORY + @"/performance-cache.json";

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
        private Dictionary<Guid, double?> performanceCache = new Dictionary<Guid, double?>();

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

            performanceCache = read<Dictionary<Guid, double?>>(performance_file) ?? new Dictionary<Guid, double?>();

            BeatmapTags = new LocalBeatmapTags(storage);

            // Song select only sees local tags through this (diagnostics can turn them off).
            LocalBeatmapTags.Current = LocalPriorityDiagnostics.IsDisabled(@"tags") ? null : BeatmapTags;
        }

        #region Profiles

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
                return data.Profiles.Where(p => p.AutoSwitch?.HasAnyCondition == true)
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
        /// Saves Settings → Profile Colors. Unlike <see cref="SetOptions"/> this doesn't raise <see cref="Changed"/>:
        /// nothing but the page colours depends on it, and a change would otherwise recalculate every profile's statistics.
        /// </summary>
        public void SetProfileColours(bool enabled, string colour)
        {
            lock (sync)
            {
                if (data.Options.ProfileColouring == enabled && data.Options.ProfileColour == colour)
                    return;

                var options = data.Options.Clone();
                options.ProfileColouring = enabled;
                options.ProfileColour = colour;
                data.Options = options;
                save();
            }
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
        public LocalProfile CreateProfile(string name, CountryCode country = CountryCode.Unknown, LocalDeathMode deathMode = LocalDeathMode.None) =>
            createProfile(name, country, deathMode, 0);

        private LocalProfile createProfile(string name, CountryCode country, LocalDeathMode deathMode, int deathNumber, LocalSpeedrun? speedrun = null)
        {
            LocalProfile profile;

            lock (sync)
            {
                name = validateName(name, null);

                var random = new Random();
                int userId;

                do
                    userId = LocalPriorityPolicy.LOCAL_PROFILE_ID_CEILING - random.Next(0, 1_000_000_000);
                while (data.Profiles.Any(p => p.UserId == userId));

                profile = new LocalProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = name,
                    Country = country,
                    Created = DateTimeOffset.Now,
                    DeathMode = deathMode,
                    Speedrun = speedrun?.Clone(),
                };

                data.Profiles.Add(profile);

                switch (deathMode)
                {
                    case LocalDeathMode.SuddenDeath:
                        data.SuddenDeathProfilesCreated = Math.Max(data.SuddenDeathProfilesCreated + 1, deathNumber);
                        break;

                    case LocalDeathMode.PerfectDeath:
                        data.PerfectDeathProfilesCreated = Math.Max(data.PerfectDeathProfilesCreated + 1, deathNumber);
                        break;
                }

                if (speedrun != null)
                    data.SpeedrunProfilesCreated = Math.Max(data.SpeedrunProfilesCreated + 1, deathNumber);

                save();
                profile = profile.Clone();
            }

            Changed?.Invoke();
            return profile;
        }

        /// <summary>
        /// Creates a profile with a free default name ("Player 1", "Player 2", ...).
        /// Sudden Death / Perfect Death ones are numbered by how many of them were ever created ("SD Profile 7", "PF Profile 3"; see <see cref="ResetDeathProfileCounts"/>).
        /// </summary>
        public LocalProfile CreateDefaultProfile(LocalDeathMode deathMode = LocalDeathMode.None)
        {
            string name;
            int n;

            string prefix = deathMode switch
            {
                LocalDeathMode.SuddenDeath => @"SD Profile",
                LocalDeathMode.PerfectDeath => @"PF Profile",
                _ => @"Player",
            };

            lock (sync)
            {
                n = deathMode switch
                {
                    LocalDeathMode.SuddenDeath => data.SuddenDeathProfilesCreated + 1,
                    LocalDeathMode.PerfectDeath => data.PerfectDeathProfilesCreated + 1,
                    _ => data.Profiles.Count + 1,
                };

                while (data.Profiles.Any(p => string.Equals(p.Name, $@"{prefix} {n}", StringComparison.OrdinalIgnoreCase)))
                    n++;
                name = $@"{prefix} {n}";
            }

            return createProfile(name, CountryCode.Unknown, deathMode, n);
        }

        /// <summary>
        /// A Speedrun profile with this goal, numbered like Sudden Death / Perfect Death ones by how many were ever created ("SP Profile 4");
        /// its timer starts once it is active.
        /// </summary>
        public LocalProfile CreateSpeedrunProfile(LocalSpeedrun goal)
        {
            string name;
            int n;

            lock (sync)
            {
                n = data.SpeedrunProfilesCreated + 1;

                while (data.Profiles.Any(p => string.Equals(p.Name, $@"SP Profile {n}", StringComparison.OrdinalIgnoreCase)))
                    n++;
                name = $@"SP Profile {n}";
            }

            var run = goal.Clone();
            run.Seconds = 0;
            run.Completed = null;

            return createProfile(name, CountryCode.Unknown, LocalDeathMode.None, n, run);
        }

        /// <summary>
        /// A copy of a Speedrun profile's goal and time, or null for other profiles.
        /// </summary>
        public LocalSpeedrun? GetSpeedrun(Guid id)
        {
            lock (sync)
                return data.Profiles.FirstOrDefault(p => p.Id == id)?.Speedrun?.Clone();
        }

        /// <summary>
        /// Saves a running Speedrun profile's time (<paramref name="completed"/>: the goal was reached and the time is final).
        /// Doesn't raise <see cref="Changed"/>: the time is saved every minute while the run goes on, which shouldn't recalculate anything.
        /// </summary>
        public void SetSpeedrunTime(Guid id, double seconds, bool completed)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);

                if (profile?.Speedrun == null || profile.Speedrun.Completed != null)
                    return;

                // Replaced, not changed in place: a save in progress may be writing the old one.
                var run = profile.Speedrun.Clone();
                run.Seconds = Math.Round(seconds, 2);

                if (completed)
                    run.Completed = DateTimeOffset.Now;

                profile.Speedrun = run;
                save();
            }
        }

        /// <summary>
        /// How many Sudden Death / Perfect Death profiles were created (they number the next ones).
        /// </summary>
        public (int SuddenDeath, int PerfectDeath) GetDeathProfileCounts()
        {
            lock (sync)
                return (data.SuddenDeathProfilesCreated, data.PerfectDeathProfilesCreated);
        }

        /// <summary>
        /// How many Speedrun profiles were created (they number the next ones).
        /// </summary>
        public int GetSpeedrunProfileCount()
        {
            lock (sync)
                return data.SpeedrunProfilesCreated;
        }

        /// <summary>
        /// Settings → Ritsu: start numbering Sudden Death / Perfect Death / Speedrun profiles from 1 again.
        /// </summary>
        public void ResetDeathProfileCounts()
        {
            lock (sync)
            {
                if (data.SuddenDeathProfilesCreated == 0 && data.PerfectDeathProfilesCreated == 0 && data.SpeedrunProfilesCreated == 0)
                    return;

                data.SuddenDeathProfilesCreated = 0;
                data.PerfectDeathProfilesCreated = 0;
                data.SpeedrunProfilesCreated = 0;
                save();
            }
        }

        /// <summary>
        /// A Sudden Death / Perfect Death profile failed a play: it is deleted once its last look at its scores is over.
        /// Saved right away, so the profile still goes if the game is closed before that (see <see cref="LocalProfileManager"/>).
        /// </summary>
        public void MarkDied(Guid id)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);

                if (profile == null || profile.Died)
                    return;

                profile.Died = true;
                save();
            }

            Changed?.Invoke();
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

        public void SetRankingOptions(Guid id, bool? includeRelaxAutopilot = null, bool? countAllAsRanked = null, bool? ignoreUnrankedMods = null)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                profile.IncludeRelaxAutopilot = includeRelaxAutopilot ?? profile.IncludeRelaxAutopilot;
                profile.CountAllAsRanked = countAllAsRanked ?? profile.CountAllAsRanked;
                profile.IgnoreUnrankedMods = ignoreUnrankedMods ?? profile.IgnoreUnrankedMods;
                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Remembers scores that were synced (moved) to the profile, with who they belonged to before, so they can be given back.
        /// Also forgets the given old-style linked owners (their scores are now moved instead of linked).
        /// </summary>
        public void AddSyncedScores(Guid id, IEnumerable<SyncedScore> scores, IEnumerable<LinkedScoreOwner>? migratedOwners = null)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                var known = profile.SyncedScores.Select(s => s.ScoreId).ToHashSet();
                // Lists are replaced, never changed in place: profile copies handed out by the store share them.
                profile.SyncedScores = profile.SyncedScores.Concat(scores.Where(s => known.Add(s.ScoreId)).Select(s => s.Clone())).ToList();

                if (migratedOwners != null)
                    profile.LinkedOwners = profile.LinkedOwners.Where(o => !migratedOwners.Contains(o)).ToList();

                save();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Returns (and forgets) the profile's synced scores, to give them back to their original players.
        /// </summary>
        public IReadOnlyList<SyncedScore> TakeSyncedScores(Guid id)
        {
            SyncedScore[] taken;

            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null || (profile.SyncedScores.Count == 0 && profile.LinkedOwners.Count == 0))
                    return Array.Empty<SyncedScore>();

                taken = profile.SyncedScores.ToArray();
                profile.SyncedScores = new List<SyncedScore>();
                profile.LinkedOwners = new List<LinkedScoreOwner>();
                save();
            }

            Changed?.Invoke();
            return taken;
        }

        /// <summary>
        /// Copies an image into the avatar folder and uses it as the profile picture. Pass null to remove the picture.
        /// </summary>
        /// <summary>
        /// Profile pictures are stored at most this many pixels wide / high. Avatars are drawn small everywhere,
        /// and full-size photos made every leaderboard reload decode megabytes of pixels (stutters when changing beatmaps).
        /// </summary>
        public const int AVATAR_SIZE = 256;

        /// <summary>
        /// Saves an image as a profile picture file (scaled down to <see cref="AVATAR_SIZE"/>, as PNG) and returns its file name.
        /// A fresh file name every time, so texture caches never show a stale picture.
        /// </summary>
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

                LocalDurableFile.Write(AvatarStorage, file, destination => image.SaveAsPng(destination));
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

                UnfinishedPlays.Remove(profile.UserId);
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

        private LocalUnfinishedPlays? unfinishedPlays;

        /// <summary>
        /// Failed and quit plays from the last day, for the profiles' Recent Plays (ritsu/local-profiles/unfinished-plays.json, read when first needed).
        /// </summary>
        public LocalUnfinishedPlays UnfinishedPlays
        {
            get
            {
                lock (sync)
                    return unfinishedPlays ??= new LocalUnfinishedPlays(storage);
            }
        }

        /// <summary>
        /// Records one attempt (finished, failed, quit or restarted) for a local profile.
        /// A failed or quit one (<paramref name="unfinished"/>) is also listed in the profile's Recent Plays for a day.
        /// </summary>
        public void RecordPlay(int userId, string ruleset, double playSeconds, IReadOnlyDictionary<HitResult, int> hits, string? beatmapHash = null,
                               LocalUnfinishedPlay? unfinished = null)
        {
            lock (sync)
            {
                var profile = data.Profiles.FirstOrDefault(p => p.UserId == userId);
                if (profile == null)
                    return;

                if (unfinished != null)
                    UnfinishedPlays.Add(unfinished);

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
        /// Fills in play statistics counted again from saved scores (keyed by profile user ID, then ruleset short name),
        /// e.g. after profiles.json was lost. Every value only goes up: attempts recorded without a score (failed, quit or
        /// restarted) are kept, and plays already counted aren't counted twice.
        /// </summary>
        /// <returns>How many profiles changed.</returns>
        public int MergeStatisticsFromScores(IReadOnlyDictionary<int, Dictionary<string, LocalPlayStatistics>> fromScores)
        {
            int changedProfiles = 0;

            lock (sync)
            {
                foreach (var profile in data.Profiles)
                {
                    if (!fromScores.TryGetValue(profile.UserId, out var perRuleset))
                        continue;

                    bool changed = false;

                    foreach (var (ruleset, counted) in perRuleset)
                    {
                        // Replaced, not changed in place: profile copies handed out by the store share the dictionaries.
                        var merged = profile.Stats.TryGetValue(ruleset, out var current) ? current.Clone() : new LocalPlayStatistics();

                        if (merged.MergeMaximum(counted))
                        {
                            profile.Stats[ruleset] = merged;
                            changed = true;
                        }
                    }

                    if (changed)
                        changedProfiles++;
                }

                if (changedProfiles > 0)
                    save();
            }

            if (changedProfiles > 0)
                Changed?.Invoke();

            return changedProfiles;
        }

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

        public void MovePlay(Guid scoreId, int fromUserId, Guid toProfileId, string ruleset, double playSeconds, IReadOnlyDictionary<HitResult, int> hits, string? beatmapHash, DateTimeOffset date)
        {
            lock (sync)
            {
                var source = data.Profiles.FirstOrDefault(p => p.UserId == fromUserId);

                if (source != null)
                {
                    int countBefore = source.SyncedScores.Count;
                    source.SyncedScores = source.SyncedScores.Where(s => s.ScoreId != scoreId).ToList();
                    int removedSynced = countBefore - source.SyncedScores.Count;

                    if (removedSynced == 0 && source.Stats.TryGetValue(ruleset, out var sourceStats))
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

        public bool TryGetCachedPerformance(Guid scoreId, out double? pp)
        {
            lock (sync)
                return performanceCache.TryGetValue(scoreId, out pp);
        }

        /// <summary>
        /// Remembers calculated PP. A null value marks a score whose beatmap difficulty couldn't be calculated (broken or unsupported beatmap):
        /// it is never calculated again (no repeated errors), and <see cref="CalculationsFailed"/> tells about new ones once.
        /// </summary>
        public void SetCachedPerformance(IReadOnlyDictionary<Guid, double?> values)
        {
            if (values.Count == 0)
                return;

            int newlyFailed = 0;

            lock (sync)
            {
                foreach (var (id, pp) in values)
                {
                    if (pp == null && !(performanceCache.TryGetValue(id, out double? existing) && existing == null))
                        newlyFailed++;

                    performanceCache[id] = pp;
                }

                // Written with profiles.json, shortly after and outside the lock (lookups don't wait for the disk).
                performanceSavePending = true;
                scheduleFlush();
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
                return performanceCache.Where(p => p.Value == null).Select(p => p.Key).ToArray();
        }

        #endregion

        private T? read<T>(string filename)
            where T : class
            => LocalDurableFile.Read(storage, filename, JsonConvert.DeserializeObject<T>);

        private bool savePending;
        private bool profilesSavePending;
        private bool performanceSavePending;

        /// <summary>
        /// Keeps writes of the same files in order (the PP cache is written outside <see cref="sync"/>).
        /// </summary>
        private readonly object writeLock = new object();

        /// <summary>
        /// Saves profiles.json shortly after a change, on a background thread: several changes in a row (switching profiles,
        /// recording a play) become one write, and the update thread never serialises the whole file.
        /// Call with the lock held (every change does).
        /// </summary>
        private void save()
        {
            profilesSavePending = true;
            scheduleFlush();
        }

        /// <summary>
        /// Call with the lock held.
        /// </summary>
        private void scheduleFlush()
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
            lock (writeLock)
            {
                Dictionary<Guid, double?>? performanceSnapshot = null;

                lock (sync)
                {
                    if (!savePending)
                        return;

                    savePending = false;

                    if (profilesSavePending)
                    {
                        profilesSavePending = false;
                        write(profiles_file, data);
                    }

                    if (performanceSavePending)
                    {
                        performanceSavePending = false;
                        performanceSnapshot = new Dictionary<Guid, double?>(performanceCache);
                    }
                }

                if (performanceSnapshot != null)
                    write(performance_file, performanceSnapshot);
            }
        }

        private void write(string filename, object value)
        {
            try
            {
                // Straight into the file, without building the whole text in memory first; flushed to disk before it replaces the old one.
                LocalDurableFile.Write(storage, filename, stream =>
                {
                    using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 4096, true))
                    using (var json = new JsonTextWriter(writer))
                        JsonSerializer.CreateDefault(new JsonSerializerSettings { Formatting = Formatting.Indented }).Serialize(json, value);
                });
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
            public List<LocalProfile> Profiles { get; set; } = new List<LocalProfile>();

            /// <summary>
            /// How many Sudden Death / Perfect Death profiles were ever created (deleted ones included), numbering the next ones.
            /// </summary>
            public int SuddenDeathProfilesCreated { get; set; }

            public int PerfectDeathProfilesCreated { get; set; }

            /// <summary>
            /// How many Speedrun profiles were ever created (deleted ones included), numbering the next ones.
            /// </summary>
            public int SpeedrunProfilesCreated { get; set; }

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

        /// <summary>
        /// Whether the score is this profile's, the same way its statistics pick them: its user ID (also carried by synced and
        /// transferred plays), or an old-style linked owner.
        /// </summary>
        public bool OwnsScore(osu.Game.Scoring.ScoreInfo score) =>
            score.RealmUser.OnlineID == UserId
            || LinkedOwners.Any(o => o.UserId == score.RealmUser.OnlineID && o.Username == score.RealmUser.Username);

        public string Name { get; set; } = string.Empty;

        public CountryCode Country { get; set; }

        /// <summary>
        /// File name inside "ritsu/local-profiles/avatars", or null for the default picture.
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
        /// Plays on ranked / approved beatmaps give PP whatever mods they used (Relax / Autopilot still follow <see cref="IncludeRelaxAutopilot"/>).
        /// </summary>
        public bool IgnoreUnrankedMods { get; set; }

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

        /// <summary>
        /// Sudden Death / Perfect Death profiles always play with that mod (it can't be turned off) and are deleted, with their scores, after a failed play.
        /// </summary>
        public LocalDeathMode DeathMode { get; set; }

        /// <summary>
        /// Failed a play as a Sudden Death / Perfect Death profile; deleted when its scores were shown, or on the next start.
        /// </summary>
        public bool Died { get; set; }

        /// <summary>
        /// A Speedrun profile's goal and time (see <see cref="LocalSpeedrun"/>), or null for other profiles.
        /// </summary>
        public LocalSpeedrun? Speedrun { get; set; }

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
            IgnoreUnrankedMods = IgnoreUnrankedMods,
            // Play statistics (with their per-beatmap and per-day counters) are read through GetPlayStatistics only;
            // copies handed out by the store leave them out, which keeps looking up profiles cheap.
            Stats = new Dictionary<string, LocalPlayStatistics>(),
            // Shared, not copied: the store replaces these lists instead of changing them (a profile can have hundreds of synced plays).
            LinkedOwners = LinkedOwners,
            SyncedScores = SyncedScores,
            ModRules = ModRules?.Clone(),
            AutoSwitch = AutoSwitch?.Clone(),
            SectionOrder = new List<string>(SectionOrder ?? new List<string>()),
            DeathMode = DeathMode,
            Died = Died,
            Speedrun = Speedrun?.Clone(),
        };

        public override string ToString() => Name;
    }

    /// <summary>
    /// Special profile kinds made from the profile page's "+" right-click menu.
    /// </summary>
    public enum LocalDeathMode
    {
        None,

        /// <summary>
        /// Always plays with Sudden Death; a failed play deletes the profile.
        /// </summary>
        SuddenDeath,

        /// <summary>
        /// Always plays with Perfect; a failed play deletes the profile.
        /// </summary>
        PerfectDeath,
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
        public bool ProfilesWithModRules { get; set; } = true;

        /// <summary>
        /// Settings → Profile Colors: tint every profile page (except Sudden Death / Perfect Death ones) with <see cref="ProfileColour"/>.
        /// </summary>
        public bool ProfileColouring { get; set; }

        /// <summary>
        /// The profile page tint, as "#RRGGBB". Its hue picks the colours like a supporter's profile colour; its saturation how strong they are.
        /// </summary>
        public string ProfileColour { get; set; } = DEFAULT_PROFILE_COLOUR;

        /// <summary>
        /// osu!'s usual pink (the page's colours when colouring is off).
        /// </summary>
        public const string DEFAULT_PROFILE_COLOUR = @"#FF0088";

        /// <summary>
        /// Show a notification when a profile is switched to (or back) automatically.
        /// </summary>
        public bool AutoSwitchNotifications { get; set; } = true;

        /// <summary>
        /// A dead Sudden Death / Perfect Death profile's page stays open for a minute instead of 30 seconds before the profile is deleted.
        /// </summary>
        public bool LongerLastLook { get; set; }

        /// <summary>
        /// Check auto-switch rules again when a beatmap is started from song select.
        /// </summary>
        public bool SwitchOnBeatmapStart { get; set; } = true;

        /// <summary>
        /// Calculate and show "Total Accuracy" on the profile page.
        /// </summary>
        public bool ShowTotalAccuracy { get; set; } = true;

        /// <summary>
        /// Plays on beatmaps marked LOCAL (changed in the editor) can give PP. When off, profiles leave them out entirely.
        /// </summary>
        public bool LocalMapsGivePerformance { get; set; }

        /// <summary>
        /// Every local profile counts every beatmap and mod as ranked.
        /// </summary>
        public bool CountEverythingAsRanked { get; set; }

        /// <summary>
        /// Scores that don't count for PP are listed (faded) among the best scores instead of being left out.
        /// </summary>
        public bool ShowNonCountingScores { get; set; } = true;

        public LocalProfileOptions Clone() => (LocalProfileOptions)MemberwiseClone();

        public bool Equals(LocalProfileOptions? other) => other != null
                                                          && other.DeleteKeepingScoresWithoutAsking == DeleteKeepingScoresWithoutAsking
                                                          && other.DeleteWithScoresWithoutAsking == DeleteWithScoresWithoutAsking
                                                          && other.SuggestSyncingSameNamePlays == SuggestSyncingSameNamePlays
                                                          && other.ProfilesWithModRules == ProfilesWithModRules
                                                          && other.ProfileColouring == ProfileColouring
                                                          && other.ProfileColour == ProfileColour
                                                          && other.LocalMapsGivePerformance == LocalMapsGivePerformance
                                                          && other.AutoSwitchNotifications == AutoSwitchNotifications
                                                          && other.LongerLastLook == LongerLastLook
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
        [JsonIgnore]
        public double PlayTimeSeconds { get; set; }

        /// <summary>
        /// <see cref="PlayTimeSeconds"/> as saved: tenths of a second are plenty (the running total in memory stays exact).
        /// </summary>
        [JsonProperty(nameof(PlayTimeSeconds))]
        private double savedPlayTimeSeconds
        {
            get => Math.Round(PlayTimeSeconds, 1);
            set => PlayTimeSeconds = value;
        }

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

        /// <summary>
        /// Raises every value to at least the one in <paramref name="other"/> (per hit result, beatmap and day).
        /// </summary>
        /// <returns>Whether anything changed.</returns>
        public bool MergeMaximum(LocalPlayStatistics other)
        {
            bool changed = false;

            if (other.PlayCount > PlayCount)
            {
                PlayCount = other.PlayCount;
                changed = true;
            }

            if (other.PlayTimeSeconds > PlayTimeSeconds + 0.5)
            {
                PlayTimeSeconds = other.PlayTimeSeconds;
                changed = true;
            }

            if (other.LastPlayed != null && (LastPlayed == null || other.LastPlayed > LastPlayed))
            {
                LastPlayed = other.LastPlayed;
                changed = true;
            }

            Hits ??= new Dictionary<HitResult, long>();
            BeatmapPlays ??= new Dictionary<string, long>();
            DailyPlays ??= new Dictionary<string, long>();

            changed |= raise(Hits, other.Hits);
            changed |= raise(BeatmapPlays, other.BeatmapPlays);
            changed |= raise(DailyPlays, other.DailyPlays);

            return changed;

            static bool raise<TKey>(Dictionary<TKey, long> target, Dictionary<TKey, long>? source)
                where TKey : notnull
            {
                bool any = false;

                foreach (var (key, value) in source ?? new Dictionary<TKey, long>())
                {
                    if (value > target.GetValueOrDefault(key))
                    {
                        target[key] = value;
                        any = true;
                    }
                }

                return any;
            }
        }

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
