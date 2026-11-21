// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Offline;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;
using Realms;
using osu.Game.Users;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Switches between the online account and local profiles, and keeps the UI informed about profile changes.
    /// </summary>
    public partial class LocalProfileManager : Component
    {
        public LocalProfileStore Store { get; }
        internal RitsuProfileManager Profiles { get; }

        [Resolved]
        internal BeatmapDifficultyCache DifficultyCache { get; private set; } = null!;
        private readonly bool linkedStorage;
        private readonly bool migratingProfiles;

        /// <summary>
        /// The active local profile, or null when the online account is in use. Updated on the update thread.
        /// </summary>
        public IBindable<LocalProfile?> ActiveProfile => activeProfile;

        /// <summary>
        /// Incremented (on the update thread) whenever any profile data changes. Bind to this to refresh displays.
        /// </summary>
        public IBindable<int> Revision => revision;

        private readonly Bindable<LocalProfile?> activeProfile = new Bindable<LocalProfile?>();
        private readonly BindableInt revision = new BindableInt();

        /// <summary>
        /// Settings → Ritsu: local profiles may use the signed-in online account for read-only lookups
        /// (beatmaps, beatmap tags, leaderboards). Off by default. Scores are never submitted either way.
        /// </summary>
        public Bindable<bool> LocalProfilesUseOnlineAccount { get; } = new Bindable<bool>();

        /// <summary>
        /// Settings → Ritsu → Local profiles: "Delete profile (keep its scores)" without asking.
        /// </summary>
        public Bindable<bool> DeleteKeepingScoresWithoutAsking { get; } = new Bindable<bool>();

        /// <summary>
        /// Settings → Ritsu → Local profiles: "Delete profile and all its scores" without asking.
        /// </summary>
        public Bindable<bool> DeleteWithScoresWithoutAsking { get; } = new Bindable<bool>();

        /// <summary>
        /// Settings → Ritsu → Local profiles: suggest syncing plays saved under the same name after creating / renaming a profile.
        /// </summary>
        public Bindable<bool> SuggestSyncingSameNamePlays { get; } = new Bindable<bool>(true);

        /// <summary>
        /// Settings → Ritsu → Local profiles: show "new profile with mod rules" buttons.
        /// </summary>
        public Bindable<bool> ProfilesWithModRules { get; } = new Bindable<bool>();

        /// <summary>
        /// Settings → Ritsu: check auto-switch rules once more when a beatmap is started (without waiting for the 2 seconds).
        /// </summary>
        public Bindable<bool> SwitchOnBeatmapStart { get; } = new Bindable<bool>(true);

        /// <summary>
        /// Settings → Ritsu: calculate and show "Total Accuracy" (over every recorded hit) on the profile page.
        /// </summary>
        public Bindable<bool> ShowTotalAccuracy { get; } = new Bindable<bool>(true);

        /// <summary>
        /// Settings → Ritsu: every local profile counts every beatmap and mod as ranked (like each profile's own option).
        /// </summary>
        public Bindable<bool> CountEverythingAsRanked { get; } = new Bindable<bool>();

        /// <summary>
        /// Settings → Ritsu: scores that don't count for PP stay in the profile's best scores list (faded, not weighted).
        /// </summary>
        public Bindable<bool> ShowNonCountingScores { get; } = new Bindable<bool>();

        /// <summary>
        /// The game's selected mods (set by <see cref="OsuGameBase"/>), corrected to follow the active profile's mod rules.
        /// </summary>
        private Bindable<IReadOnlyList<Mod>>? gameMods;

        private readonly IBindable<RulesetInfo> gameRuleset = new Bindable<RulesetInfo>();

        /// <summary>
        /// The active profile and its rules as last applied, to notice switches and rule edits.
        /// </summary>
        private string? appliedRulesKey;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> rulesetBindable { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private LeaderboardManager? leaderboardManager { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        /// <summary>
        /// Textures for local profile pictures (files in "osu-local/avatars").
        /// </summary>
        public LargeTextureStore AvatarTextures { get; private set; } = null!;

        private readonly Dictionary<string, Texture?> avatarTextureCache = new Dictionary<string, Texture?>();

        /// <summary>
        /// A profile picture's texture, decoded once and shared by every avatar showing it (leaderboard rows are recreated on every beatmap change).
        /// Safe to call from background loading threads.
        /// </summary>
        public Texture? GetAvatarTexture(string file)
        {
            lock (avatarTextureCache)
            {
                // The cache keeps one reference alive, so the image is decoded and uploaded only once.
                // Every avatar gets its own reference to the same upload: avatars release theirs when they go away,
                // and handing the same reference to several of them freed it while others still drew it.
                if (!avatarTextureCache.ContainsKey(file))
                    avatarTextureCache[file] = AvatarTextures.Get(file);

                return avatarTextureCache[file] == null ? null : AvatarTextures.Get(file);
            }
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, IRenderer renderer)
        {
            AvatarTextures = new LargeTextureStore(renderer, host.CreateTextureLoaderStore(new StorageBackedResourceStore(Store.AvatarStorage)));
        }

        public LocalProfileManager(LocalProfileStore store, RitsuProfileManager profiles, bool linkedStorage, bool migratingProfiles)
        {
            Store = store;
            Profiles = profiles;
            this.linkedStorage = linkedStorage;
            this.migratingProfiles = migratingProfiles;
            activeProfile.Value = store.ActiveProfile;
            LocalProfilesUseOnlineAccount.Value = store.LocalProfilesUseOnlineAccount;
            readOptions();
        }

        /// <summary>
        /// Called by <see cref="OsuGameBase"/> with the game's selected mods, so profile mod rules can be applied to them.
        /// </summary>
        public void BindGameMods(Bindable<IReadOnlyList<Mod>> mods) => gameMods = mods;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Store.Changed += onStoreChanged;
            Store.CalculationsFailed += onCalculationsFailed;
            LocalProfilesUseOnlineAccount.BindValueChanged(v => Store.SetLocalProfilesUseOnlineAccount(v.NewValue));

            DeleteKeepingScoresWithoutAsking.BindValueChanged(_ => writeOptions());
            DeleteWithScoresWithoutAsking.BindValueChanged(_ => writeOptions());
            SuggestSyncingSameNamePlays.BindValueChanged(_ => writeOptions());
            ProfilesWithModRules.BindValueChanged(_ => writeOptions());
            AutoSwitchNotifications.BindValueChanged(_ => writeOptions());
            SwitchOnBeatmapStart.BindValueChanged(_ => writeOptions());
            ShowTotalAccuracy.BindValueChanged(_ => writeOptions());
            CountEverythingAsRanked.BindValueChanged(_ => writeOptions());
            ShowNonCountingScores.BindValueChanged(_ => writeOptions());

            if (api is ILocalProfileApi apiAccess)
                apiAccess.OnlineAccountRequested += onOnlineAccountRequested;

            gameRuleset.BindTo(rulesetBindable);

            LocalPriorityDiagnostics.LogState();

            // Profiles with auto-switch rules: check whenever another beatmap is selected.
            if (!LocalPriorityDiagnostics.IsDisabled(@"autoswitch"))
            {
                selectedBeatmap.BindTo(gameBeatmap);
                updateAutoSwitchActive();
                selectedBeatmap.BindValueChanged(_ => scheduleAutoSwitchCheck());

                // Rules with mods: changing the selected mods counts like selecting another beatmap.
                gameMods?.BindValueChanged(_ => scheduleAutoSwitchCheck());
                gameRuleset.BindValueChanged(_ => scheduleAutoSwitchCheck());
            }

            if (migratingProfiles)
                migrateProfileScores();

            Profiles.Changed += onProfilesChanged;

            // Pictures saved at full size by older builds are scaled down once, off the update thread.
            System.Threading.Tasks.Task.Run(Store.ShrinkOversizedAvatars);

            if (gameMods != null && !LocalPriorityDiagnostics.IsDisabled(@"modrules"))
            {
                gameMods.BindValueChanged(_ => enforceModRules());
                gameRuleset.BindValueChanged(_ => Scheduler.AddOnce(enforceModRules));
            }

            applyStoreState();
        }

        private bool readingOptions;

        private void readOptions()
        {
            readingOptions = true;

            var options = Store.Options;
            DeleteKeepingScoresWithoutAsking.Value = options.DeleteKeepingScoresWithoutAsking;
            DeleteWithScoresWithoutAsking.Value = options.DeleteWithScoresWithoutAsking;
            SuggestSyncingSameNamePlays.Value = options.SuggestSyncingSameNamePlays;
            ProfilesWithModRules.Value = options.ProfilesWithModRules;
            AutoSwitchNotifications.Value = options.AutoSwitchNotifications;
            SwitchOnBeatmapStart.Value = options.SwitchOnBeatmapStart;
            ShowTotalAccuracy.Value = options.ShowTotalAccuracy;
            CountEverythingAsRanked.Value = options.CountEverythingAsRanked;
            ShowNonCountingScores.Value = options.ShowNonCountingScores;

            readingOptions = false;
        }

        private void writeOptions()
        {
            // Values being refreshed from the store one by one: don't write half-updated options back.
            if (readingOptions)
                return;

            Store.SetOptions(new LocalProfileOptions
            {
                DeleteKeepingScoresWithoutAsking = DeleteKeepingScoresWithoutAsking.Value,
                DeleteWithScoresWithoutAsking = DeleteWithScoresWithoutAsking.Value,
                SuggestSyncingSameNamePlays = SuggestSyncingSameNamePlays.Value,
                ProfilesWithModRules = ProfilesWithModRules.Value,
                AutoSwitchNotifications = AutoSwitchNotifications.Value,
                SwitchOnBeatmapStart = SwitchOnBeatmapStart.Value,
                ShowTotalAccuracy = ShowTotalAccuracy.Value,
                CountEverythingAsRanked = CountEverythingAsRanked.Value,
                ShowNonCountingScores = ShowNonCountingScores.Value,
            });
        }

        /// <summary>
        /// Puts the local profiles in this order (drag and drop in the profile page / switcher).
        /// </summary>
        public void Reorder(IReadOnlyList<Guid> order) => Store.ReorderProfiles(order);

        public void SetModRules(Guid id, LocalModRules? rules) => Store.SetModRules(id, rules);

        /// <summary>
        /// Remembers the profile page's section order (dragging the section tabs).
        /// </summary>
        public void SetSectionOrder(Guid id, IEnumerable<string> order) => Store.SetSectionOrder(id, order);

        public void SetAutoSwitch(Guid id, LocalAutoSwitchRules? rules) => Store.SetAutoSwitch(id, rules);

        #region Auto-switching

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> gameBeatmap { get; set; } = null!;

        private readonly IBindable<WorkingBeatmap> selectedBeatmap = new Bindable<WorkingBeatmap>();

        /// <summary>
        /// The profile switched to automatically, and the one that was active before it (null: the online account).
        /// While the automatically chosen profile stays active, a beatmap matching no profile switches back.
        /// </summary>
        private Guid? autoSwitchedTo;

        private Guid? activeBeforeAutoSwitch;

        private void checkAutoSwitch()
        {
            // Only while choosing beatmaps in song select (the main menu's music changes beatmaps too).
            if (game?.ScreenStack?.CurrentScreen is not Screens.Select.SoloSongSelect || selectedBeatmap.Disabled)
                return;

            var beatmap = selectedBeatmap.Value?.BeatmapInfo;

            if (beatmap == null || beatmap.Metadata == null)
                return;

            var candidates = Store.GetAutoSwitchRules();
            var active = activeProfile.Value;

            // Nothing to do without auto-switching profiles, unless one was switched to and may need switching back.
            if (candidates.Count == 0 && autoSwitchedTo == null)
                return;

            var selectedMods = gameMods?.Value ?? Array.Empty<Mod>();
            var ruleset = candidates.Any(c => c.Rules.Mods.Count > 0) ? gameRuleset.Value?.CreateInstance() : null;

            var found = candidates.FirstOrDefault(c => c.Rules.Matches(beatmap, selectedMods, ruleset));
            var match = found.Rules != null ? found : ((Guid Id, string Name, LocalAutoSwitchRules Rules)?)null;

            // Switched by hand since the automatic switch: nothing to go back to.
            if (autoSwitchedTo != null && active?.Id != autoSwitchedTo)
            {
                autoSwitchedTo = null;
                activeBeforeAutoSwitch = null;
            }

            if (match is (Guid matchId, string matchName, LocalAutoSwitchRules matchRules))
            {
                if (active?.Id == matchId)
                    return;

                if (autoSwitchedTo == null)
                    activeBeforeAutoSwitch = active?.Id;

                autoSwitchedTo = matchId;
                Switch(matchId);
                notify($@"Switched to ""{matchName}"" for this beatmap ({matchRules.Describe()}).");
                return;
            }

            // No profile wants this beatmap: go back to the profile from before the automatic switch.
            if (autoSwitchedTo != null && active?.Id == autoSwitchedTo)
            {
                var back = activeBeforeAutoSwitch;
                autoSwitchedTo = null;
                activeBeforeAutoSwitch = null;

                var backProfile = back != null ? Store.GetProfile(back.Value) : null;

                if (back == null || backProfile != null)
                {
                    Switch(back);
                    notify(back == null ? @"Switched back to the online account." : $@"Switched back to ""{backProfile!.Name}"".");
                }
            }
        }

        /// <summary>
        /// Song select changes the selected beatmap constantly while scrolling; profiles only switch once a beatmap stays selected this long.
        /// </summary>
        private const double auto_switch_dwell_time = 2000;

        /// <summary>
        /// Whether any profile has auto-switch rules (or one was switched to and may need switching back).
        /// Without them, beatmap changes cost nothing at all here.
        /// </summary>
        private bool autoSwitchActive;

        private bool autoSwitchCheckPending;
        private double lastBeatmapChangeTime;

        private void updateAutoSwitchActive() => autoSwitchActive = !LocalPriorityDiagnostics.IsDisabled(@"autoswitch") && (autoSwitchedTo != null || Store.GetAutoSwitchRules().Count > 0);

        /// <summary>
        /// Only remembers when the beatmap changed (no work, nothing allocated); <see cref="Update"/> checks once it stays selected.
        /// </summary>
        private void scheduleAutoSwitchCheck()
        {
            if (!autoSwitchActive)
                return;

            autoSwitchCheckPending = true;
            lastBeatmapChangeTime = Time.Current;
        }

        protected override void Update()
        {
            base.Update();

            // While the mod rules picker previews mods, the game's mods aren't the player's choice: wait until it closes.
            if (autoSwitchCheckPending && modPreview == null && Time.Current - lastBeatmapChangeTime >= auto_switch_dwell_time)
            {
                autoSwitchCheckPending = false;
                checkAutoSwitch();
            }
        }

        /// <summary>
        /// Song select calls this when a play starts, so a play started right after picking a beatmap is still set on the right profile.
        /// </summary>
        public void CheckAutoSwitchNow()
        {
            if (!autoSwitchActive || !SwitchOnBeatmapStart.Value)
                return;

            autoSwitchCheckPending = false;
            checkAutoSwitch();
        }

        /// <summary>
        /// Settings → Ritsu: notifications when a profile is switched to (or back) automatically.
        /// </summary>
        public Bindable<bool> AutoSwitchNotifications { get; } = new Bindable<bool>(true);

        /// <summary>
        /// At most one auto-switch notification per this many milliseconds (going back and forth doesn't flood the notifications).
        /// </summary>
        private const double notification_cooldown = 3000;

        private double lastNotificationTime = double.MinValue;

        /// <summary>
        /// Set by <see cref="OsuGame"/>: shows a notification (e.g. after switching profiles automatically).
        /// </summary>
        public Action<Overlays.Notifications.Notification>? PostNotification { get; set; }

        private void notify(string text)
        {
            if (!AutoSwitchNotifications.Value || Time.Current - lastNotificationTime < notification_cooldown)
                return;

            lastNotificationTime = Time.Current;
            PostNotification?.Invoke(new Overlays.Notifications.SimpleNotification { Text = text, Icon = Framework.Graphics.Sprites.FontAwesome.Solid.UserCheck });
        }

        #endregion

        #region Mod rules

        /// <summary>
        /// The active profile's rule mods, made for the current ruleset (mods the ruleset doesn't have are skipped).
        /// </summary>
        private IReadOnlyList<Mod> ruleModsForCurrentRuleset(LocalModRules rules)
        {
            var ruleset = gameRuleset.Value?.CreateInstance();

            if (ruleset == null)
                return Array.Empty<Mod>();

            return rules.Mods.Select(m => m.ToMod(ruleset)).Where(m => m is not UnknownMod).ToArray();
        }

        /// <summary>
        /// Autoplay / Cinema (Ctrl+Enter) and system mods are never touched by profile rules.
        /// </summary>
        private static bool exemptFromRules(Mod mod) => mod is ModAutoplay || mod.Type == ModType.System;

        /// <summary>
        /// "Only allow these mods, with these exact settings": puts the profile's settings back on the selected mods
        /// (song select calls this when a play starts, so settings changed in mod select don't carry into the play).
        /// </summary>
        public void ResetFixedModSettings()
        {
            if (mod_rules_off)
                return;

            var rules = activeProfile.Value?.ModRules;

            if (gameMods == null || gameMods.Disabled || rules == null || rules.BlockSelected || !rules.OnlyAllowSelectedSettings)
                return;

            var ruleMods = ruleModsForCurrentRuleset(rules);

            foreach (var mod in gameMods.Value)
            {
                var fixedMod = ruleMods.FirstOrDefault(r => r.GetType() == mod.GetType());

                if (fixedMod != null && !sameModAndSettings(fixedMod, mod))
                    mod.CopyFrom(fixedMod);
            }
        }

        /// <summary>
        /// Whether the active profile's mod rules allow selecting this mod (mod select greys out and blocks the others).
        /// </summary>
        public bool IsModAllowed(Mod mod)
        {
            if (mod_rules_off)
                return true;

            var rules = activeProfile.Value?.ModRules;

            if (rules == null || !rules.HasAnyRule || exemptFromRules(mod))
                return true;

            bool listed = rules.Mods.Any(m => m.Acronym == mod.Acronym);

            if (rules.BlockSelected)
                return !listed;

            if (rules.OnlyAllowSelected || rules.OnlyAllowSelectedSettings)
                return listed;

            return true;
        }

        private ModPreview? modPreview;

        /// <summary>
        /// While choosing a profile's mod rules over song select, the chosen mods are put on the game for real
        /// (track speed, star rating, beatmap attributes behave like song select's own mod select).
        /// Profile rules are paused meanwhile; disposing the preview puts the previous selection back.
        /// Returns null if the game's mods can't be changed right now.
        /// </summary>
        public ModPreview? BeginModPreview()
        {
            if (gameMods == null || gameMods.Disabled || modPreview != null)
                return null;

            return modPreview = new ModPreview(this, gameMods.Value.Select(m => m.DeepClone()).ToArray());
        }

        public class ModPreview : IDisposable
        {
            private readonly LocalProfileManager manager;
            private readonly IReadOnlyList<Mod> previousMods;
            private bool disposed;

            internal ModPreview(LocalProfileManager manager, IReadOnlyList<Mod> previousMods)
            {
                this.manager = manager;
                this.previousMods = previousMods;
            }

            /// <summary>
            /// Show these mods on the game (the same instances, so setting changes apply live).
            /// </summary>
            public void Set(IReadOnlyList<Mod> mods)
            {
                if (disposed || manager.gameMods == null || manager.gameMods.Disabled)
                    return;

                manager.gameMods.Value = mods.ToArray(); // always a new list, so the game notices setting changes too
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;

                if (manager.gameMods != null && !manager.gameMods.Disabled)
                    manager.gameMods.Value = previousMods;

                manager.modPreview = null;
                manager.applyModRulesIfChanged();
                manager.enforceModRules();
            }
        }

        private static bool sameModAndSettings(Mod a, Mod b) => a.GetType() == b.GetType() && new APIMod(a).Equals(new APIMod(b));

        /// <summary>
        /// Corrects the game's selected mods to follow the active profile's rules (blocked mods removed, only allowed mods kept,
        /// fixed settings restored). Leaves them alone while they can't be changed (gameplay, results, multiplayer rooms).
        /// </summary>
        private void enforceModRules()
        {
            var rules = activeProfile.Value?.ModRules;

            if (modPreview != null || gameMods == null || gameMods.Disabled || rules == null || !rules.HasAnyRule)
                return;

            var ruleMods = ruleModsForCurrentRuleset(rules);
            var current = gameMods.Value;
            var result = current.ToList();

            if (rules.BlockSelected)
                result.RemoveAll(m => !exemptFromRules(m) && ruleMods.Any(r => r.GetType() == m.GetType()));
            else if (rules.OnlyAllowSelected || rules.OnlyAllowSelectedSettings)
                result.RemoveAll(m => !exemptFromRules(m) && ruleMods.All(r => r.GetType() != m.GetType()));

            if (!rules.BlockSelected && rules.OnlyAllowSelectedSettings)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    var fixedMod = ruleMods.FirstOrDefault(r => r.GetType() == result[i].GetType());

                    if (fixedMod != null && !sameModAndSettings(fixedMod, result[i]))
                        result[i] = fixedMod.DeepClone();
                }
            }

            bool changed = result.Count != current.Count || result.Where((m, i) => !ReferenceEquals(m, current[i])).Any();

            if (changed)
                gameMods.Value = result;
        }

        /// <summary>
        /// When a profile with "select these mods when switching" becomes active (or its rules change), select its mods.
        /// </summary>
        /// <summary>
        /// Diagnostics: profile mod rules turned off (OSU_LOCAL_DISABLE=modrules).
        /// </summary>
        private static readonly bool mod_rules_off = LocalPriorityDiagnostics.IsDisabled(@"modrules");

        private void applyModRulesIfChanged()
        {
            // While mods are being previewed, rules wait until the preview ends (it calls this again).
            if (modPreview != null || mod_rules_off)
                return;

            var profile = activeProfile.Value;
            var rules = profile?.ModRules;

            string? key = profile == null
                ? null
                : $@"{profile.Id}|{(rules == null ? string.Empty : Newtonsoft.Json.JsonConvert.SerializeObject(rules))}";

            if (key == appliedRulesKey)
                return;

            appliedRulesKey = key;

            if (gameMods == null || rules == null || !rules.HasAnyRule)
                return;

            if (rules.ApplyOnSwitch && !rules.BlockSelected)
            {
                if (gameMods.Disabled)
                {
                    // Can't change mods right now (e.g. in gameplay): try again when they can be changed.
                    appliedRulesKey = null;
                    Scheduler.AddDelayed(applyModRulesIfChanged, 500);
                    return;
                }

                var ruleMods = ruleModsForCurrentRuleset(rules);
                var kept = gameMods.Value.Where(m => ruleMods.All(r => r.GetType() != m.GetType()) && !rules.OnlyAllowSelected && !rules.OnlyAllowSelectedSettings);
                var combined = ruleMods.Select(m => m.DeepClone()).Concat(kept).ToList();

                if (!ModUtils.CheckCompatibleSet(combined))
                    combined = ruleMods.Select(m => m.DeepClone()).ToList();

                gameMods.Value = combined;
            }

            enforceModRules();
        }

        #endregion

        public bool IsLocalProfileActive => activeProfile.Value != null;

        public IReadOnlyList<LocalProfile> GetProfiles() => Store.GetProfiles();

        /// <summary>
        /// Switch to a local profile, or to the online account when <paramref name="profileId"/> is null.
        /// </summary>
        public void Switch(Guid? profileId)
        {
            Store.SetActiveProfile(profileId);

            // Store changes are normally picked up a frame later. On the update thread, apply the switch right away instead,
            // so a play started in the same frame (auto-switch as song select starts a play) already uses the new profile:
            // its user, PP settings and mod rules.
            if (IsLoaded && ThreadSafety.IsUpdateThread)
                applyStoreState();
        }

        public LocalProfile CreateProfile(string name, CountryCode country = CountryCode.Unknown) =>
            mirrorNewProfile(Store.CreateProfile(name, country));

        public LocalProfile CreateDefaultProfile() => mirrorNewProfile(Store.CreateDefaultProfile());

        private LocalProfile mirrorNewProfile(LocalProfile profile)
        {
            try
            {
                Profiles.EnsureProfile(profile.Id, profile.Name);
                return profile;
            }
            catch
            {
                Store.DeleteProfile(profile.Id);
                throw;
            }
        }

        public void Rename(Guid id, string name)
        {
            Profiles.RenameProfile(id, name);
            Store.RenameProfile(id, name);
        }

        public void SetCountry(Guid id, CountryCode country)
        {
            Profiles.SetCountry(id, country);
            Store.SetCountry(id, country);
            refreshScoreDisplays();
        }

        public void SetAvatar(Guid id, FileInfo? image)
        {
            Store.SetAvatar(id, image);
            refreshScoreDisplays();
        }

        /// <summary>
        /// Fetch the song select leaderboard again, so names, flags and pictures on it are up to date.
        /// </summary>
        private void refreshScoreDisplays() => Schedule(() =>
        {
            if (leaderboardManager?.CurrentCriteria is LeaderboardCriteria criteria)
                leaderboardManager.FetchWithCriteria(criteria, forceRefresh: true);
        });

        public void SetRankingOptions(Guid id, bool? includeRelaxAutopilot = null, bool? countAllAsRanked = null) =>
            Store.SetRankingOptions(id, includeRelaxAutopilot, countAllAsRanked);

        public void Delete(Guid id, bool deleteScores = false)
        {
            var profiles = Store.GetProfiles();
            int index = profiles.ToList().FindIndex(profile => profile.Id == id);

            if (index < 0)
                return;

            if (deleteScores)
            {
                var deletable = getProfileScores(id, localPlaysOnly: true);
                if (deletable.Count > 0)
                    scoreManager.Delete(deletable);
            }

            Profiles.RemoveMirroredProfile(id);

            if (activeProfile.Value?.Id == id)
            {
                var next = index > 0 ? profiles[index - 1] : profiles.Count > 1 ? profiles[index + 1] : null;
                Store.SetActiveProfile(next?.Id);
            }

            Store.DeleteProfile(id);
        }

        public (IReadOnlyList<LinkedScoreOwner> Owners, int ScoreCount) FindSameNameScores(Guid profileId)
        {
            var profiles = Store.GetProfiles();
            var profile = profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null)
                return (Array.Empty<LinkedScoreOwner>(), 0);

            var profileUserIds = profiles.Select(p => p.UserId).ToHashSet();
            var candidates = realm.Run(r => r.All<ScoreInfo>()
                .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.Username)} ==[c] $0 AND {nameof(ScoreInfo.DeletePending)} == false", profile.Name)
                .AsEnumerable()
                .Where(score => !profileUserIds.Contains(score.UserID) && !Profiles.GetScoreProfileId(score.ID).HasValue)
                .Select(score => new LinkedScoreOwner { UserId = score.UserID, Username = score.RealmUser.Username })
                .ToArray());
            return (candidates.Distinct().ToArray(), candidates.Length);
        }

        public void SyncScores(Guid id, IEnumerable<LinkedScoreOwner> owners) => associateOwners(id, owners, synced: true);

        private void associateOwners(Guid id, IEnumerable<LinkedScoreOwner> owners, bool synced)
        {
            var scores = realm.Run(r => owners.SelectMany(owner => r.All<ScoreInfo>()
                .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0 AND {nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.Username)} == $1 AND {nameof(ScoreInfo.DeletePending)} == false",
                    owner.UserId, owner.Username).AsEnumerable()).Detach());
            Profiles.AssociateStoredScores(id, scores, synced);
        }

        private void migrateProfileScores()
        {
            foreach (var profile in Store.GetProfiles())
            {
                var previouslySynced = profile.SyncedScores.Select(score => score.ScoreId).ToHashSet();
                var scores = realm.Run(r => r.All<ScoreInfo>()
                    .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0 AND {nameof(ScoreInfo.DeletePending)} == false", profile.UserId)
                    .AsEnumerable().Detach());

                Profiles.AssociateStoredScores(profile.Id, scores.Where(score => !previouslySynced.Contains(score.ID)).ToArray(), synced: false);
                Profiles.AssociateStoredScores(profile.Id, scores.Where(score => previouslySynced.Contains(score.ID)).ToArray(), synced: true);
                if (profile.LinkedOwners.Count > 0)
                    associateOwners(profile.Id, profile.LinkedOwners, synced: true);
            }

            Store.CompleteRitsuMigration();
        }

        public (int Deleted, int Preserved) CountScoresForDeletion(Guid id)
        {
            var ids = Profiles.GetProfilePlayIds(id);
            return realm.Run(r =>
            {
                int deleted = 0;
                int preserved = 0;
                foreach (var item in ids)
                {
                    if (r.Find<ScoreInfo>(item.Key) is not { DeletePending: false })
                        continue;

                    if (!linkedStorage && item.Value)
                        deleted++;
                    else
                        preserved++;
                }

                return (deleted, preserved);
            });
        }

        public bool CanDeleteScore(Guid profileId, Guid scoreId) =>
            !linkedStorage && Profiles.IsLocalPlay(profileId, scoreId);

        public void RemoveScore(Guid profileId, ScoreInfo score)
        {
            if (Profiles.GetScoreProfileId(score.ID) != profileId)
                return;

            if (CanDeleteScore(profileId, score.ID))
                scoreManager.Delete(score);

            Profiles.RemoveScoreAssociations(profileId, score.ID);
        }

        public void UnsyncScores(Guid id) => Profiles.RemoveScoreAssociations(id);

        public void TransferScore(ScoreInfo score, Guid targetProfileId)
        {
            if (Store.GetProfile(targetProfileId) == null)
                return;

            Guid? source = Profiles.GetScoreProfileId(score.ID);
            if (!source.HasValue)
            {
                Profiles.AssignStoredScore(score.ID, targetProfileId);
                return;
            }

            bool trackPlay = Profiles.IsLocalPlay(source.Value, score.ID);
            if (!Profiles.TransferScore(score.ID, source.Value, targetProfileId) || !trackPlay)
                return;

            double rate = ModUtils.CalculateRateWithMods(score.Mods);
            double seconds = score.BeatmapInfo != null && rate > 0 ? score.BeatmapInfo.Length / 1000 / rate : 0;
            Store.MovePlay(source.Value, targetProfileId, score.Ruleset.ShortName, seconds,
                new Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>(score.Statistics), score.BeatmapInfo?.MD5Hash, score.Date);
        }

        private List<ScoreInfo> getProfileScores(Guid profileId, bool localPlaysOnly = false)
        {
            var ids = Profiles.GetProfilePlayIds(profileId);
            return realm.Run(r => ids.Where(item => !localPlaysOnly || !linkedStorage && item.Value)
                .Select(item => r.Find<ScoreInfo>(item.Key))
                .OfType<ScoreInfo>().Where(score => !score.DeletePending).Detach());
        }

        private HashSet<Guid> getDeletableScoreIds() => linkedStorage
            ? new HashSet<Guid>()
            : Store.GetProfiles().SelectMany(profile => Profiles.GetProfilePlayIds(profile.Id))
                   .Where(item => item.Value).Select(item => item.Key).ToHashSet();

        #region Scores that can't be calculated

        /// <summary>
        /// Set by <see cref="OsuGame"/>: shows a dialog (e.g. confirming removal of scores that can't be calculated).
        /// </summary>
        public Action<Overlays.Dialog.PopupDialog>? PushDialog { get; set; }

        /// <summary>
        /// Beatmaps whose difficulty can't be calculated: told once (they are never calculated again), with the option to remove their scores.
        /// </summary>
        private void onCalculationsFailed(int count) => Schedule(() =>
        {
            PostNotification?.Invoke(new Overlays.Notifications.SimpleNotification
            {
                Text = $@"{count:N0} score(s) have no PP: their beatmap's difficulty can't be calculated (broken or unsupported beatmap). "
                       + @"They are ignored from now on. Click to remove them from local profiles.",
                Icon = Framework.Graphics.Sprites.FontAwesome.Solid.ExclamationTriangle,
                Activated = () =>
                {
                    RequestRemoveUncalculableScores();
                    return true;
                },
            });
        });

        private List<ScoreInfo> uncalculableScores(Realm r)
        {
            var deletableIds = getDeletableScoreIds();

            return Store.GetFailedCalculations()
                        .Select(id => r.Find<ScoreInfo>(id))
                        .Where(s => s != null && !s.DeletePending && deletableIds.Contains(s.ID))
                        .ToList()!;
        }

        public int CountUncalculableScores() => realm.Run(r => uncalculableScores(r).Count);

        /// <summary>
        /// Asks, then deletes local profiles' scores whose beatmap difficulty can't be calculated.
        /// </summary>
        public void RequestRemoveUncalculableScores()
        {
            int count = CountUncalculableScores();

            if (count == 0)
            {
                PostNotification?.Invoke(new Overlays.Notifications.SimpleNotification { Text = @"No local profile has scores that can't be calculated." });
                return;
            }

            PushDialog?.Invoke(new RemoveUncalculableScoresDialog(count, RemoveUncalculableScores));
        }

        public void RemoveUncalculableScores()
        {
            realm.Run(r =>
            {
                var scores = uncalculableScores(r);

                if (scores.Count > 0)
                    scoreManager.Delete(scores);
            });

            Schedule(() => revision.Value++);
            refreshScoreDisplays();
        }

        private partial class RemoveUncalculableScoresDialog : Overlays.Dialog.DangerousActionDialog
        {
            public RemoveUncalculableScoresDialog(int count, Action remove)
            {
                HeaderText = @"Remove scores that can't be calculated?";
                BodyText = $@"{count:N0} score(s) of local profiles are on beatmaps whose difficulty can't be calculated. They will be deleted.";
                DangerousAction = remove;
            }
        }

        #endregion

        #region Plays with impossible dates

        /// <summary>
        /// Plays dated before this (e.g. replays without a proper date, shown as 1 January 0001) can't be real.
        /// </summary>
        public static readonly DateTime OLDEST_VALID_PLAY_DATE = new DateTime(2000, 1, 1);

        private List<ScoreInfo> scoresBefore2000(Realm r)
        {
            var deletableIds = getDeletableScoreIds();
            var cutoff = new DateTimeOffset(OLDEST_VALID_PLAY_DATE, TimeSpan.Zero);

            return r.All<ScoreInfo>()
                    .Filter($@"{nameof(ScoreInfo.Date)} < $0 AND {nameof(ScoreInfo.DeletePending)} == false", cutoff)
                    .AsEnumerable()
                    .Where(s => deletableIds.Contains(s.ID))
                    .ToList();
        }

        /// <summary>
        /// Local profiles' scores and recorded plays dated before the year 2000.
        /// </summary>
        public (int Scores, long Plays) CountPlaysBefore2000() =>
            (realm.Run(r => scoresBefore2000(r).Count), Store.CountDailyPlaysBefore(OLDEST_VALID_PLAY_DATE));

        /// <summary>
        /// Settings → Ritsu emergency button: deletes local profiles' scores dated before the year 2000
        /// and forgets plays recorded on such days (play history and play count).
        /// </summary>
        public void RemovePlaysBefore2000()
        {
            realm.Run(r =>
            {
                var scores = scoresBefore2000(r);

                if (scores.Count > 0)
                    scoreManager.Delete(scores);
            });

            Store.RemoveDailyPlaysBefore(OLDEST_VALID_PLAY_DATE);

            Schedule(() => revision.Value++);
            refreshScoreDisplays();
        }

        #endregion

        #region Replay import

        /// <summary>
        /// Opens the folder picker for importing replays as this profile's plays (the screen closes every overlay).
        /// </summary>
        public void BeginReplayImport(Guid profileId)
        {
            var profile = Store.GetProfile(profileId);

            if (profile == null || game == null)
                return;

            Guid id = profile.Id;
            pickFolder($@"Pick a folder with replays (.osr) to import as ""{profile.Name}"" (subfolders included)", folder => ImportReplays(id, folder.FullName));
        }

        private void pickFolder(string header, Action<DirectoryInfo> selected) =>
            game?.PerformFromScreen(screen => osu.Framework.Screens.ScreenExtensions.Push(screen, new LocalFolderSelectScreen(header, selected)));

        /// <summary>
        /// Imports every replay (.osr) in the folder and its subfolders, then moves the imported plays to the profile like syncing does
        /// (they count as its plays, and "unsync" gives them back to the player named in the replay). The files themselves are left alone.
        /// Replays already on a local profile stay where they are; replays of beatmaps that aren't imported can't be imported.
        /// </summary>
        public void ImportReplays(Guid profileId, string folder)
        {
            var profile = Store.GetProfile(profileId);

            if (profile == null)
                return;

            var notification = new Overlays.Notifications.ProgressNotification
            {
                Text = $@"Looking for replays to import as ""{profile.Name}""...",
                State = Overlays.Notifications.ProgressNotificationState.Active,
            };

            PostNotification?.Invoke(notification);

            System.Threading.Tasks.Task.Run(async () =>
            {
                string[] files;

                try
                {
                    files = Directory.EnumerateFiles(folder, @"*.osr", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        MatchCasing = MatchCasing.CaseInsensitive,
                    }).ToArray();
                }
                catch (Exception e)
                {
                    osu.Framework.Logging.Logger.Error(e, $@"Ritsu: could not read the replay folder ""{folder}"".");
                    notification.Text = @"Could not read that folder.";
                    notification.State = Overlays.Notifications.ProgressNotificationState.Cancelled;
                    return;
                }

                var imported = (await scoreManager.Import(notification, files.Select(f => new ImportTask(f)).ToArray(),
                    new ImportParameters { PreserveExisting = true }).ConfigureAwait(false))
                               .Select(live => live.ID)
                               .ToArray();

                Schedule(() => assignImportedReplays(profileId, files.Length, imported));
            });
        }

        private void assignImportedReplays(Guid profileId, int found, IReadOnlyList<Guid> importedIds)
        {
            var profile = Store.GetProfile(profileId);

            if (profile == null || found == 0)
                return;

            var scores = realm.Run(r => importedIds.Distinct().Select(id => r.Find<ScoreInfo>(id))
                .OfType<ScoreInfo>().Where(score => !score.DeletePending).Detach());
            var available = scores.Where(score => !Profiles.GetScoreProfileId(score.ID).HasValue).ToArray();
            Profiles.AssociateStoredScores(profileId, available, synced: true);

            int alreadyOnProfiles = scores.Count - available.Length;
            var parts = new List<string> { $@"{available.Length:N0} of {found:N0} replays imported as ""{profile.Name}""." };

            if (alreadyOnProfiles > 0)
                parts.Add($@"{alreadyOnProfiles:N0} were already on a local profile.");

            int failed = found - importedIds.Count;

            if (failed > 0)
                parts.Add($@"{failed:N0} could not be imported (beatmap not imported, or not a valid replay).");

            PostNotification?.Invoke(new Overlays.Notifications.SimpleNotification
            {
                Text = string.Join(@" ", parts),
                Icon = Framework.Graphics.Sprites.FontAwesome.Solid.FileImport,
            });
        }

        #endregion

        #region Replay export

        [Resolved]
        private Storage gameStorage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private BeatmapManager? beatmapManager { get; set; }

        public const int TOP_REPLAYS_EXPORT_COUNT = 50;

        /// <summary>
        /// Opens the folder picker, then saves the profile's replays (.osr) there: its top 50 PP scores in the current mode
        /// (numbered in PP order), or every score it has in any mode.
        /// </summary>
        public void BeginReplayExport(Guid profileId, bool topOnly)
        {
            var profile = Store.GetProfile(profileId);

            if (profile == null)
                return;

            string what = topOnly ? $@"the top {TOP_REPLAYS_EXPORT_COUNT} replays ({gameRuleset.Value?.Name ?? @"current mode"})" : @"all replays";
            pickFolder($@"Pick a folder to export {what} of ""{profile.Name}"" to", folder => exportReplays(profile, topOnly, folder.FullName));
        }

        private void exportReplays(LocalProfile profile, bool topOnly, string folder)
        {
            var ruleset = gameRuleset.Value;

            var notification = new Overlays.Notifications.ProgressNotification
            {
                Text = $@"Exporting replays of ""{profile.Name}""...",
                State = Overlays.Notifications.ProgressNotificationState.Active,
            };

            PostNotification?.Invoke(notification);

            System.Threading.Tasks.Task.Run(async () =>
            {
                var token = notification.CancellationToken;
                List<ScoreInfo> scores;

                if (topOnly)
                {
                    if (ruleset == null || beatmapManager == null)
                        scores = new List<ScoreInfo>();
                    else
                    {
                        var statistics = await LocalProfileStatistics.CalculateAsync(profile, ruleset, realm, beatmapManager, this, token).ConfigureAwait(false);
                        var top = statistics.BestScores.Where(s => s.CountsForPerformance).Take(TOP_REPLAYS_EXPORT_COUNT).ToArray();

                        LocalScoreEntry.Load(top, realm);
                        scores = top.Select(s => s.Score).OfType<ScoreInfo>().ToList();
                    }
                }
                else
                {
                    scores = getProfileScores(profile.Id).OrderBy(score => score.Date).ToList();
                }

                if (scores.Count == 0)
                {
                    notification.Text = $@"""{profile.Name}"" has no replays to export.";
                    notification.State = Overlays.Notifications.ProgressNotificationState.Cancelled;
                    return;
                }

                var exporter = new LegacyScoreExporter(gameStorage);
                int exported = 0;
                int withoutReplay = 0;

                try
                {
                    Directory.CreateDirectory(folder);

                    for (int i = 0; i < scores.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        var score = scores[i];

                        // Scores without a replay (e.g. from before replays were kept) have nothing to export.
                        if (score.Files.Count == 0)
                        {
                            withoutReplay++;
                            continue;
                        }

                        string name = $@"{score.GetDisplayString()} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";
                        if (topOnly)
                            name = $@"#{i + 1:00} {name}";

                        name = name.GetValidFilename();
                        if (name.Length > LegacyExporter<ScoreInfo>.MAX_FILENAME_LENGTH)
                            name = name[..LegacyExporter<ScoreInfo>.MAX_FILENAME_LENGTH];

                        string path = Path.Combine(folder, name + @".osr");

                        for (int n = 2; File.Exists(path); n++)
                            path = Path.Combine(folder, $@"{name} ({n}).osr");

                        using (var stream = File.Create(path))
                            exporter.ExportToStream(score, stream, null, token);

                        exported++;
                        notification.Text = $@"Exported {exported} of {scores.Count} replays";
                        notification.Progress = (float)(i + 1) / scores.Count;
                    }
                }
                catch (OperationCanceledException)
                {
                    notification.State = Overlays.Notifications.ProgressNotificationState.Cancelled;
                    return;
                }
                catch (Exception e)
                {
                    osu.Framework.Logging.Logger.Error(e, $@"Ritsu: could not export replays to ""{folder}"".");
                    notification.Text = $@"Export failed after {exported} replays. Check logs for more information.";
                    notification.State = Overlays.Notifications.ProgressNotificationState.Cancelled;
                    return;
                }

                notification.CompletionText = withoutReplay > 0
                    ? $@"Exported {exported} replays to {folder} ({withoutReplay} scores have no replay)."
                    : $@"Exported {exported} replays to {folder}.";
                notification.State = Overlays.Notifications.ProgressNotificationState.Completed;
            });
        }

        #endregion

        public void RecordPlay(int userId, string ruleset, double playSeconds, IReadOnlyDictionary<osu.Game.Rulesets.Scoring.HitResult, int> hits, string? beatmapHash = null) =>
            Store.RecordPlay(userId, ruleset, playSeconds, hits, beatmapHash);

        /// <summary>
        /// A beatmap was entered: played, or watched as a replay (any gameplay screen). Called by the game on the update thread.
        /// </summary>
        public event Action? BeatmapEntered;

        public void NotifyBeatmapEntered() => BeatmapEntered?.Invoke();

        private void onStoreChanged() => Schedule(applyStoreState);

        private void onProfilesChanged() => Schedule(() =>
        {
            revision.Value++;
            refreshScoreDisplays();
        });

        private void applyStoreState()
        {
            var profile = Store.ActiveProfile;
            if (profile != null && Profiles.ActiveProfileId != profile.Id)
                Profiles.SelectProfile(profile.Id);

            activeProfile.Value = profile;
            LocalProfilesUseOnlineAccount.Value = Store.LocalProfilesUseOnlineAccount;
            readOptions();
            updateAutoSwitchActive();
            revision.Value++;
            applyToApi();
            applyModRulesIfChanged();
        }

        private void applyToApi()
        {
            var profile = activeProfile.Value;

            // Settings → Ritsu "Count everything as ranked" applies to every local profile (not the online account).
            LocalPriorityPolicy.CountAllAsRanked = profile != null && (profile.CountAllAsRanked || Store.Options.CountEverythingAsRanked);
            LocalPriorityPolicy.CountRelaxAutopilot = profile?.IncludeRelaxAutopilot == true;

            if (api is ILocalProfileApi apiAccess)
            {
                apiAccess.SetLocalProfilesUseOnlineAccount(Store.LocalProfilesUseOnlineAccount);
                apiAccess.SetOfflineProfile(profile?.ToUser());
            }
            else
            {
                LocalPriorityPolicy.LocalProfilesUseOnlineAccount = Store.LocalProfilesUseOnlineAccount;
                LocalPriorityPolicy.OfflineProfileActive = profile != null;
            }
        }

        private void onOnlineAccountRequested() => Store.SetActiveProfile(null);

        protected override void Dispose(bool isDisposing)
        {
            Store.Changed -= onStoreChanged;
            Profiles.Changed -= onProfilesChanged;
            Store.CalculationsFailed -= onCalculationsFailed;
            Store.Flush();

            if (api is ILocalProfileApi apiAccess)
                apiAccess.OnlineAccountRequested -= onOnlineAccountRequested;

            base.Dispose(isDisposing);
        }
    }
}
