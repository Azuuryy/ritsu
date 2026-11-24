// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
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
        /// Incremented (on the update thread) when something that can change a local profile's PP changed: scores, PP options,
        /// exclusions, ranking options, synced plays. Cosmetic changes (names, order, pictures, recorded attempts) leave it alone,
        /// so PP that is already known can be shown again without recalculating it.
        /// </summary>
        public IBindable<int> PerformanceRevision => performanceRevision;

        private readonly BindableInt performanceRevision = new BindableInt();

        /// <summary>
        /// For PP settings kept outside the store (other patches): PP is calculated again everywhere.
        /// </summary>
        public void InvalidatePerformance() => Schedule(() =>
        {
            performanceRevision.Value++;
            revision.Value++;
        });

        private IDisposable? scoreSubscription;

        private (LocalProfileOptions Options, LocalPerformanceExclusions Exclusions, (Guid, int, bool, bool, bool, object, object)[] Profiles)? lastPerformanceInputs;

        /// <summary>
        /// Bumps <see cref="PerformanceRevision"/> when a store change touched anything PP depends on.
        /// </summary>
        private void checkPerformanceInputs()
        {
            var options = Store.Options;
            var exclusions = Store.PerformanceExclusions;
            // Linked owners and synced plays are replaced (never changed in place) by the store, so comparing the lists themselves is enough.
            var profiles = Store.GetProfiles()
                                .Select(p => (p.Id, p.UserId, p.CountAllAsRanked, p.IncludeRelaxAutopilot, p.IgnoreUnrankedMods, (object)p.LinkedOwners, (object)p.SyncedScores))
                                .ToArray();

            if (lastPerformanceInputs is var (lastOptions, lastExclusions, lastProfiles)
                && lastOptions.Equals(options)
                && lastExclusions.Scores.SequenceEqual(exclusions.Scores)
                && lastExclusions.Beatmaps.SequenceEqual(exclusions.Beatmaps)
                && lastExclusions.BeatmapSets.SequenceEqual(exclusions.BeatmapSets)
                && lastProfiles.SequenceEqual(profiles))
                return;

            lastPerformanceInputs = (options, exclusions, profiles);
            performanceRevision.Value++;
        }

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
        /// Settings → Profile Colors: tint profile pages with <see cref="ProfileColour"/> (not Sudden Death / Perfect Death ones, they keep red / amber).
        /// </summary>
        public Bindable<bool> ProfileColouring { get; } = new Bindable<bool>();

        /// <summary>
        /// Settings → Profile Colors: the colour profile pages are tinted with while <see cref="ProfileColouring"/> is on.
        /// </summary>
        public Bindable<Colour4> ProfileColour { get; } = new Bindable<Colour4>(Colour4.FromHex(LocalProfileOptions.DEFAULT_PROFILE_COLOUR));

        private ScheduledDelegate? pendingProfileColourWrite;

        /// <summary>
        /// Settings → Ritsu: check auto-switch rules once more when a beatmap is started (without waiting for the 2 seconds).
        /// </summary>
        public Bindable<bool> SwitchOnBeatmapStart { get; } = new Bindable<bool>(true);

        /// <summary>
        /// Settings → Ritsu: calculate and show "Total Accuracy" (over every recorded hit) on the profile page.
        /// </summary>
        public Bindable<bool> ShowTotalAccuracy { get; } = new Bindable<bool>(true);

        /// <summary>
        /// Settings → Ritsu: plays on beatmaps marked LOCAL (changed in the editor) can give PP. When off, profiles leave them out entirely.
        /// </summary>
        public Bindable<bool> LocalMapsGivePerformance { get; } = new Bindable<bool>(true);

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

        public LocalProfileManager(LocalProfileStore store)
        {
            Store = store;
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

            // Any change to scores (saved, deleted, transferred, imported) can change PP.
            scoreSubscription = realm.RegisterForNotifications(r => r.All<ScoreInfo>(), (_, changes) =>
            {
                if (changes != null)
                    performanceRevision.Value++;
            });

            Store.Changed += onStoreChanged;
            Store.CalculationsFailed += onCalculationsFailed;
            LocalProfilesUseOnlineAccount.BindValueChanged(v => Store.SetLocalProfilesUseOnlineAccount(v.NewValue));

            DeleteKeepingScoresWithoutAsking.BindValueChanged(_ => writeOptions());
            DeleteWithScoresWithoutAsking.BindValueChanged(_ => writeOptions());
            SuggestSyncingSameNamePlays.BindValueChanged(_ => writeOptions());
            ProfilesWithModRules.BindValueChanged(_ => writeOptions());
            ProfileColouring.BindValueChanged(_ => writeProfileColours(true));
            // The picker changes it every frame while dragged: save once it settles.
            ProfileColour.BindValueChanged(_ => writeProfileColours(false));
            AutoSwitchNotifications.BindValueChanged(_ => writeOptions());
            SwitchOnBeatmapStart.BindValueChanged(_ => writeOptions());
            ShowTotalAccuracy.BindValueChanged(_ => writeOptions());
            LocalMapsGivePerformance.BindValueChanged(_ => writeOptions());
            CountEverythingAsRanked.BindValueChanged(e =>
            {
                if (readingOptions)
                    return;

                var before = Store.ActiveProfile;
                writeOptions();

                if (before != null)
                    ShowRankingChange(before, e.OldValue, gameRuleset.Value);
            });
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

            migrateLinkedOwners();
            deleteDeadProfiles();

            // Pictures saved at full size by older builds are scaled down once, off the update thread.
            System.Threading.Tasks.Task.Run(Store.ShrinkOversizedAvatars);

            if (gameMods != null && !LocalPriorityDiagnostics.IsDisabled(@"modrules"))
            {
                gameMods.BindValueChanged(_ => enforceModRules());
                gameRuleset.BindValueChanged(_ => Scheduler.AddOnce(enforceModRules));
            }

            applyToApi();
            applyModRulesIfChanged();
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
            ProfileColouring.Value = options.ProfileColouring;

            // A colour still waiting to be saved is newer than the stored one.
            if (pendingProfileColourWrite == null)
                ProfileColour.Value = parseProfileColour(options.ProfileColour);

            AutoSwitchNotifications.Value = options.AutoSwitchNotifications;
            SwitchOnBeatmapStart.Value = options.SwitchOnBeatmapStart;
            LocalMapsGivePerformance.Value = options.LocalMapsGivePerformance;
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
                ProfileColouring = ProfileColouring.Value,
                ProfileColour = ProfileColour.Value.ToHex(),
                AutoSwitchNotifications = AutoSwitchNotifications.Value,
                SwitchOnBeatmapStart = SwitchOnBeatmapStart.Value,
                LocalMapsGivePerformance = LocalMapsGivePerformance.Value,
                ShowTotalAccuracy = ShowTotalAccuracy.Value,
                CountEverythingAsRanked = CountEverythingAsRanked.Value,
                ShowNonCountingScores = ShowNonCountingScores.Value,
            });
        }

        private void writeProfileColours(bool now)
        {
            if (readingOptions)
                return;

            pendingProfileColourWrite?.Cancel();
            pendingProfileColourWrite = null;

            if (now)
            {
                Store.SetProfileColours(ProfileColouring.Value, ProfileColour.Value.ToHex());
                return;
            }

            pendingProfileColourWrite = Scheduler.AddDelayed(() =>
            {
                pendingProfileColourWrite = null;
                Store.SetProfileColours(ProfileColouring.Value, ProfileColour.Value.ToHex());
            }, 500);
        }

        private static Colour4 parseProfileColour(string? hex)
            => Colour4.TryParseHex(hex ?? string.Empty, out var colour) ? colour.Opacity(1f) : Colour4.FromHex(LocalProfileOptions.DEFAULT_PROFILE_COLOUR);

        /// <summary>
        /// The page colours for a profile: red for Sudden Death, amber for Perfect Death, otherwise Settings → Profile Colors (or the usual pink).
        /// </summary>
        public (int hue, float saturation) GetProfilePageColours(LocalProfile profile)
        {
            switch (profile.DeathMode)
            {
                case LocalDeathMode.SuddenDeath:
                    return (OverlayColourScheme.Red.GetHue(), 1);

                case LocalDeathMode.PerfectDeath:
                    // Amber: osu!'s "orange" (45) comes out olive on the dark page colours, the website's yellowish profiles sit lower.
                    return (30, 1);
            }

            if (!ProfileColouring.Value)
                return (OverlayColourScheme.Pink.GetHue(), 1);

            var hsv = ProfileColour.Value.ToHSV();
            return ((int)MathF.Round(hsv.X * 360) % 360, hsv.Y);
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

            // Sudden Death / Perfect Death profiles: make sure the play starts with their mod.
            if (activeProfile.Value?.DeathMode is LocalDeathMode.SuddenDeath or LocalDeathMode.PerfectDeath)
                enforceModRules();

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

            // Sudden Death / Perfect Death profiles: their mod is always on, so mods that conflict with it can't be picked.
            var deathMod = deathModForCurrentRuleset();

            if (deathMod != null && !exemptFromRules(mod) && mod.GetType() != deathMod.GetType() && !ModUtils.CheckCompatibleSet(new[] { deathMod, mod }))
                return false;

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

        /// <summary>
        /// The mod a Sudden Death / Perfect Death profile always plays with, made for the current ruleset (null for other profiles).
        /// </summary>
        private Mod? deathModForCurrentRuleset()
        {
            string? acronym = activeProfile.Value?.DeathMode switch
            {
                LocalDeathMode.SuddenDeath => @"SD",
                LocalDeathMode.PerfectDeath => @"PF",
                _ => null,
            };

            var ruleset = gameRuleset.Value;

            if (acronym == null || ruleset == null)
                return null;

            // Asked for every mod panel on every selection change: made once per ruleset and mode, shared (never selected itself, see enforceModRules).
            string key = $@"{ruleset.ShortName}:{acronym}";

            if (deathModKey != key)
            {
                deathModKey = key;
                cachedDeathMod = ruleset.CreateInstance().CreateModFromAcronym(acronym);

                if (cachedDeathMod is ModFailCondition failCondition)
                    failCondition.Restart.Value = false;
            }

            return cachedDeathMod;
        }

        private string? deathModKey;
        private Mod? cachedDeathMod;

        /// <summary>
        /// Whether this selected mod can't be turned off: the active Sudden Death / Perfect Death profile's own mod.
        /// </summary>
        public bool IsModLocked(Mod mod)
        {
            if (mod_rules_off || activeProfile.Value == null || activeProfile.Value.DeathMode == LocalDeathMode.None)
                return false;

            return mod is (ModSuddenDeath or ModPerfect) && deathModForCurrentRuleset()?.GetType() == mod.GetType();
        }

        private static readonly string restart_on_fail_label =
            typeof(ModFailCondition).GetProperty(nameof(ModFailCondition.Restart))?.GetCustomAttribute<SettingSourceAttribute>()?.Label.ToString() ?? @"Restart on fail";

        /// <summary>
        /// Mod customisation leaves this setting out: Sudden Death / Perfect Death profiles never restart on fail
        /// (their other settings, like Sudden Death's "Also fail when missing a slider tail", stay).
        /// </summary>
        public bool IsModSettingHidden(Mod mod, Drawable control)
        {
            if (mod_rules_off || mod is not ModFailCondition || activeProfile.Value == null || activeProfile.Value.DeathMode == LocalDeathMode.None)
                return false;

            return control is Overlays.Settings.SettingsCheckbox checkbox && checkbox.LabelText.ToString() == restart_on_fail_label;
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

            if (rules != null && !rules.HasAnyRule)
                rules = null;

            if (modPreview != null || gameMods == null || gameMods.Disabled || mod_rules_off)
                return;

            var deathMod = deathModForCurrentRuleset();

            if (rules == null && deathMod == null)
                return;

            var current = gameMods.Value;
            var result = current.ToList();

            if (rules != null)
                applyRules(rules, result);

            // Sudden Death / Perfect Death profiles: their mod is always selected, never restarts on fail, and mods conflicting with it go.
            if (deathMod != null && !result.Any(m => m is ModAutoplay))
            {
                int index = result.FindIndex(m => m.GetType() == deathMod.GetType());

                if (index < 0)
                {
                    result.RemoveAll(m => !exemptFromRules(m) && !ModUtils.CheckCompatibleSet(new[] { deathMod, m }));
                    result.Add(deathMod.DeepClone());
                }

                // Neither it nor other fail mods kept alongside it (e.g. Accuracy Challenge with Sudden Death) restart on fail.
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i] is ModFailCondition other && other.Restart.Value)
                    {
                        var corrected = result[i].DeepClone();
                        ((ModFailCondition)corrected).Restart.Value = false;
                        result[i] = corrected;
                    }
                }
            }

            bool changed = result.Count != current.Count || result.Where((m, i) => !ReferenceEquals(m, current[i])).Any();

            if (changed)
                gameMods.Value = result;
        }

        private void applyRules(LocalModRules rules, List<Mod> result)
        {
            var ruleMods = ruleModsForCurrentRuleset(rules);

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
        }

        /// <summary>
        /// When a profile with "select these mods when switching" becomes active (or its rules change), select its mods.
        /// </summary>
        /// <summary>
        /// Diagnostics: profile mod rules turned off (OSU_LOCAL_DISABLE=modrules).
        /// </summary>
        private static readonly bool mod_rules_off = LocalPriorityDiagnostics.IsDisabled(@"modrules");

        /// <summary>
        /// The mods selected before switching to a Sudden Death / Perfect Death profile (with their ruleset),
        /// selected again once a profile without a forced mod is active (switching away, or the profile was deleted).
        /// </summary>
        private (string Ruleset, APIMod[] Mods)? modsBeforeDeathProfile;

        /// <summary>
        /// Remembers or restores the mods around Sudden Death / Perfect Death profiles. Returns false if the mods can't be changed right now.
        /// </summary>
        private bool restoreModsAfterDeathProfile(LocalProfile? profile)
        {
            if (gameMods == null)
                return true;

            bool forcedMod = profile?.DeathMode is LocalDeathMode.SuddenDeath or LocalDeathMode.PerfectDeath;

            if (forcedMod)
            {
                // Remember once: switching between Sudden Death and Perfect Death profiles keeps the mods from before the first one.
                if (modsBeforeDeathProfile == null && gameRuleset.Value != null)
                    modsBeforeDeathProfile = (gameRuleset.Value.ShortName, gameMods.Value.Where(m => !exemptFromRules(m)).Select(m => new APIMod(m)).ToArray());

                return true;
            }

            if (modsBeforeDeathProfile is not (string rulesetName, APIMod[] remembered))
                return true;

            if (gameMods.Disabled)
                return false;

            modsBeforeDeathProfile = null;

            var ruleset = gameRuleset.Value?.CreateInstance();

            // Mods from another ruleset (the ruleset was changed meanwhile) can't come back.
            if (ruleset == null || ruleset.RulesetInfo.ShortName != rulesetName)
                return true;

            var restored = remembered.Select(m => m.ToMod(ruleset)).Where(m => m is not UnknownMod).ToList();

            if (!ModUtils.CheckCompatibleSet(restored))
                return true;

            gameMods.Value = restored;
            return true;
        }

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

            if (!restoreModsAfterDeathProfile(profile))
            {
                // Can't change mods right now (e.g. in gameplay): try again when they can be changed.
                appliedRulesKey = null;
                Scheduler.AddDelayed(applyModRulesIfChanged, 500);
                return;
            }

            if (gameMods == null || rules == null || !rules.HasAnyRule)
            {
                // Sudden Death / Perfect Death profiles select their mod on switching (retried while mods can't be changed).
                if (gameMods != null && profile?.DeathMode is LocalDeathMode.SuddenDeath or LocalDeathMode.PerfectDeath)
                {
                    if (gameMods.Disabled)
                    {
                        appliedRulesKey = null;
                        Scheduler.AddDelayed(applyModRulesIfChanged, 500);
                        return;
                    }

                    enforceModRules();
                }

                return;
            }

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

        public LocalProfile CreateProfile(string name, CountryCode country = CountryCode.Unknown) => Store.CreateProfile(name, country);

        public LocalProfile CreateDefaultProfile(LocalDeathMode deathMode = LocalDeathMode.None) => Store.CreateDefaultProfile(deathMode);

        public (int SuddenDeath, int PerfectDeath) GetDeathProfileCounts() => Store.GetDeathProfileCounts();

        public void ResetDeathProfileCounts() => Store.ResetDeathProfileCounts();

        #region Sudden Death / Perfect Death profiles

        /// <summary>
        /// The dead profile's scores open together with the fail screen's buttons (<see cref="FailScreenShown"/>);
        /// if gameplay is left before that (quit during the fail animation, which takes 2.5 seconds), they open after this long anyway.
        /// </summary>
        private const double death_fallback_time = 2500;

        /// <summary>
        /// How long the dead profile's scores stay open at most.
        /// </summary>
        private const double death_last_look_time = 60_000;

        /// <summary>
        /// A dead Sudden Death / Perfect Death profile's scores should open now (the profile page does this, on the update thread).
        /// Closing the page calls <see cref="FinishProfileDeath"/>.
        /// </summary>
        public event Action<Guid>? ShowLastScores;

        /// <summary>
        /// The last look at a dead profile's scores is over: the profile page closes, then the profile is deleted.
        /// </summary>
        public event Action<Guid>? ProfileDeathFinished;

        private Guid? dyingProfileId;
        private bool lastScoresShown;
        private ScheduledDelegate? deathTimeout;
        private ScheduledDelegate? deathFallback;

        /// <summary>
        /// When (game clock time) the dying profile's last look at its scores ends, for the profile page's countdown. Null until it started.
        /// </summary>
        public double? LastLookEndTime { get; private set; }

        /// <summary>
        /// Called by gameplay when a play fails: if it was the active Sudden Death / Perfect Death profile's play, the profile dies.
        /// Returns whether it did (gameplay then blocks retrying).
        /// </summary>
        public bool BeginProfileDeath(int userId)
        {
            var profile = activeProfile.Value;

            if (profile == null || profile.DeathMode == LocalDeathMode.None || profile.UserId != userId || mod_rules_off || dyingProfileId != null)
                return false;

            Guid id = profile.Id;

            // Saved right away: if the game is closed before the deletion, the profile goes on the next start.
            Store.MarkDied(id);
            dyingProfileId = id;
            lastScoresShown = false;
            LastLookEndTime = null;

            deathFallback = Scheduler.AddDelayed(showLastScores, death_fallback_time);
            return true;
        }

        /// <summary>
        /// Called by gameplay when the fail screen's buttons appear: a dying profile's scores open at the same time.
        /// </summary>
        public void FailScreenShown()
        {
            if (dyingProfileId != null)
                showLastScores();
        }

        private void showLastScores()
        {
            if (dyingProfileId is not Guid id || lastScoresShown)
                return;

            lastScoresShown = true;
            deathFallback?.Cancel();

            // The minute starts now, as the page opens (it counts it down).
            LastLookEndTime = Time.Current + death_last_look_time;
            deathTimeout = Scheduler.AddDelayed(() => FinishProfileDeath(id), death_last_look_time);

            if (ShowLastScores == null)
                FinishProfileDeath(id);
            else
                ShowLastScores.Invoke(id);
        }

        /// <summary>
        /// Ends a dead profile's last look at its scores: closes the profile page, goes back to song select (leaving gameplay)
        /// and deletes the profile with all its scores and play statistics.
        /// </summary>
        public void FinishProfileDeath(Guid id)
        {
            if (dyingProfileId != id)
                return;

            dyingProfileId = null;
            LastLookEndTime = null;
            deathTimeout?.Cancel();
            deathTimeout = null;
            deathFallback?.Cancel();
            deathFallback = null;

            ProfileDeathFinished?.Invoke(id);

            if (game == null)
            {
                deleteDeadProfile(id);
                return;
            }

            // Deleted once back at song select, so nothing from the play (e.g. a replay saved from the fail screen) is added after it.
            game.PerformFromScreen(_ => deleteDeadProfile(id), new[] { typeof(Screens.Select.SoloSongSelect) });
        }

        private void deleteDeadProfile(Guid id)
        {
            var profile = Store.GetProfile(id);

            if (profile == null || !profile.Died)
                return;

            try
            {
                Delete(id, deleteScores: true);
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, $@"Ritsu: could not delete the failed profile ""{profile.Name}"".");
            }
        }

        /// <summary>
        /// Profiles that died but whose deletion didn't happen (the game was closed during the last look at their scores) go now.
        /// </summary>
        private void deleteDeadProfiles()
        {
            foreach (var dead in Store.GetProfiles().Where(p => p.Died).ToArray())
                deleteDeadProfile(dead.Id);
        }

        #endregion

        public void Rename(Guid id, string name)
        {
            Store.RenameProfile(id, name);
            updateScoresOf(id);
        }

        public void SetCountry(Guid id, CountryCode country)
        {
            Store.SetCountry(id, country);
            updateScoresOf(id);
        }

        public void SetAvatar(Guid id, FileInfo? image)
        {
            Store.SetAvatar(id, image);
            refreshScoreDisplays();
        }

        /// <summary>
        /// The profile's own scores carry its name and flag in the database (that's what leaderboards and the official client show),
        /// so they follow a rename or new flag.
        /// </summary>
        private void updateScoresOf(Guid id)
        {
            var profile = Store.GetProfile(id);

            if (profile == null)
                return;

            realm.Write(r =>
            {
                var scores = r.All<ScoreInfo>()
                              .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0", profile.UserId);

                foreach (var score in scores)
                {
                    if (score.RealmUser.Username == profile.Name && score.RealmUser.CountryCode == profile.Country)
                        continue;

                    score.RealmUser = new Models.RealmUser
                    {
                        OnlineID = profile.UserId,
                        Username = profile.Name,
                        CountryCode = profile.Country,
                    };
                }
            });

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

        public void SetRankingOptions(Guid id, bool? includeRelaxAutopilot = null, bool? countAllAsRanked = null, bool? ignoreUnrankedMods = null)
        {
            // Changing what counts for PP on the active profile shows the PP / rank difference on the toolbar, like excluding a play.
            var before = Store.ActiveProfile;
            bool countEverything = Store.Options.CountEverythingAsRanked;

            Store.SetRankingOptions(id, includeRelaxAutopilot, countAllAsRanked, ignoreUnrankedMods);

            if (before?.Id == id)
                ShowRankingChange(before, countEverything, gameRuleset.Value);
        }

        /// <summary>
        /// Delete a profile. If it is the active one, the profile to its left becomes active (or the one to its right;
        /// the online account only if no local profile is left).
        /// </summary>
        /// <param name="id">The profile to delete.</param>
        /// <param name="deleteScores">Also delete every score set on this profile from the (shared) database.</param>
        public void Delete(Guid id, bool deleteScores = false)
        {
            var profiles = Store.GetProfiles();
            int index = profiles.ToList().FindIndex(p => p.Id == id);

            if (index < 0)
                return;

            var profile = profiles[index];

            if (activeProfile.Value?.Id == id)
            {
                var next = index > 0 ? profiles[index - 1] : profiles.Count > 1 ? profiles[index + 1] : null;
                Store.SetActiveProfile(next?.Id);
            }

            // Synced plays (set before the profile existed) go back to their original players first, so they survive the deletion.
            UnsyncScores(id);

            if (deleteScores)
            {
                // Only this profile's own scores: they carry its unique negative player ID.
                realm.Run(r =>
                {
                    var scores = r.All<ScoreInfo>()
                                  .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0 AND {nameof(ScoreInfo.DeletePending)} == false", profile.UserId)
                                  .ToList();

                    if (scores.Count > 0)
                        scoreManager.Delete(scores);
                });
            }

            Store.DeleteProfile(id);
        }

        /// <summary>
        /// Scores already in the database under this profile's name (any letter case) that aren't the profile's own yet:
        /// for example plays made on the online account or as a guest in osu!lazer, or plays of a deleted local profile.
        /// Scores of other local profiles, or ones already synced to another profile, are left out.
        /// </summary>
        public (IReadOnlyList<LinkedScoreOwner> Owners, int ScoreCount) FindSameNameScores(Guid profileId)
        {
            var profiles = Store.GetProfiles();
            var profile = profiles.FirstOrDefault(p => p.Id == profileId);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                return (Array.Empty<LinkedScoreOwner>(), 0);

            var otherProfileIds = profiles.Where(p => p.Id != profileId).Select(p => p.UserId).ToHashSet();
            var takenOwners = profiles.SelectMany(p => p.LinkedOwners).ToHashSet();

            var found = realm.Run(r => r.All<ScoreInfo>()
                                        .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.Username)} ==[c] $0 AND {nameof(ScoreInfo.DeletePending)} == false", profile.Name)
                                        .AsEnumerable()
                                        .Select(s => new LinkedScoreOwner { UserId = s.RealmUser.OnlineID, Username = s.RealmUser.Username })
                                        .ToList());

            var candidates = found.Where(o => o.UserId != profile.UserId && !otherProfileIds.Contains(o.UserId) && !takenOwners.Contains(o)).ToList();

            return (candidates.Distinct().ToArray(), candidates.Count);
        }

        /// <summary>
        /// Sync these players' scores to the profile: they are moved to it in the database (its user ID, name and flag),
        /// so they show its picture and name everywhere and follow renames. Their original player is remembered for "unsync".
        /// </summary>
        public void SyncScores(Guid id, IEnumerable<LinkedScoreOwner> owners)
        {
            var profile = Store.GetProfile(id);

            if (profile == null)
                return;

            var ownerList = owners.ToList();
            var synced = new List<SyncedScore>();

            realm.Write(r =>
            {
                foreach (var owner in ownerList)
                {
                    if (owner.UserId == profile.UserId)
                        continue;

                    var scores = r.All<ScoreInfo>()
                                  .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0 AND {nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.Username)} == $1",
                                      owner.UserId, owner.Username)
                                  .ToList();

                    foreach (var score in scores)
                    {
                        synced.Add(new SyncedScore
                        {
                            ScoreId = score.ID,
                            OriginalUserId = score.RealmUser.OnlineID,
                            OriginalUsername = score.RealmUser.Username,
                            OriginalCountry = score.RealmUser.CountryCode,
                        });

                        score.RealmUser = new Models.RealmUser
                        {
                            OnlineID = profile.UserId,
                            Username = profile.Name,
                            CountryCode = profile.Country,
                        };
                    }
                }
            });

            Store.AddSyncedScores(id, synced, ownerList);
            refreshScoreDisplays();
        }

        /// <summary>
        /// What "delete profile and all its scores" affects: scores that would be deleted (set on the profile or transferred to it),
        /// and synced plays that go back to their original player instead.
        /// </summary>
        public (int Deleted, int GivenBack) CountScoresForDeletion(Guid id)
        {
            var profile = Store.GetProfile(id);

            if (profile == null)
                return (0, 0);

            var syncedIds = profile.SyncedScores.Select(s => s.ScoreId).ToHashSet();

            var ownIds = realm.Run(r => r.All<ScoreInfo>()
                                         .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0 AND {nameof(ScoreInfo.DeletePending)} == false", profile.UserId)
                                         .AsEnumerable()
                                         .Select(s => s.ID)
                                         .ToList());

            int givenBack = ownIds.Count(syncedIds.Contains);
            return (ownIds.Count - givenBack, givenBack);
        }

        /// <summary>
        /// Give the profile's synced scores back to the players they belonged to before syncing.
        /// </summary>
        public void UnsyncScores(Guid id)
        {
            var synced = Store.TakeSyncedScores(id);

            if (synced.Count == 0)
                return;

            realm.Write(r =>
            {
                foreach (var s in synced)
                {
                    var score = r.Find<ScoreInfo>(s.ScoreId);

                    if (score == null)
                        continue;

                    score.RealmUser = new Models.RealmUser
                    {
                        OnlineID = s.OriginalUserId,
                        Username = s.OriginalUsername,
                        CountryCode = s.OriginalCountry,
                    };
                }
            });

            refreshScoreDisplays();
        }

        /// <summary>
        /// Profiles synced by older Ritsu builds only linked the plays; move those plays to the profile now (once).
        /// </summary>
        private void migrateLinkedOwners()
        {
            foreach (var profile in Store.GetProfiles().Where(p => p.LinkedOwners.Count > 0))
            {
                try
                {
                    SyncScores(profile.Id, profile.LinkedOwners);
                }
                catch (Exception e)
                {
                    osu.Framework.Logging.Logger.Error(e, $@"Ritsu: could not move synced plays to profile ""{profile.Name}"".");
                }
            }
        }

        /// <summary>
        /// Moves a score to another local profile: the score's player in the database becomes that profile
        /// (its user ID, name and flag), so it counts there everywhere, including local leaderboards.
        /// </summary>
        public void TransferScore(ScoreInfo score, Guid targetProfileId)
        {
            var target = Store.GetProfile(targetProfileId);

            if (target == null)
                return;

            int? sourceUserId = null;
            string? rulesetName = null;
            double seconds = 0;
            Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>? hits = null;
            string? beatmapHash = null;
            DateTimeOffset scoreDate = DateTimeOffset.Now;

            realm.Write(r =>
            {
                var databased = r.Find<ScoreInfo>(score.ID);

                if (databased == null || databased.RealmUser.OnlineID == target.UserId)
                    return;

                sourceUserId = databased.RealmUser.OnlineID;
                rulesetName = databased.Ruleset.ShortName;
                hits = new Dictionary<osu.Game.Rulesets.Scoring.HitResult, int>(databased.Statistics);
                beatmapHash = databased.BeatmapInfo?.MD5Hash; // plays are counted per beatmap MD5 hash (scores keep a different hash)
                scoreDate = databased.Date;

                double rate = ModUtils.CalculateRateWithMods(databased.Mods);
                if (databased.BeatmapInfo != null && rate > 0)
                    seconds = databased.BeatmapInfo.Length / 1000 / rate;

                databased.RealmUser = new Models.RealmUser
                {
                    OnlineID = target.UserId,
                    Username = target.Name,
                    CountryCode = target.Country,
                };
            });

            // The play moves with the score: +1 play count (its hits and play time) for the profile it goes to,
            // and the same taken off the local profile it came from.
            if (sourceUserId != null && rulesetName != null && hits != null)
                Store.MovePlay(score.ID, sourceUserId.Value, target.Id, rulesetName, seconds, hits, beatmapHash, scoreDate);

            // The profile page rebuilds on a revision change; the song select leaderboard is fetched again so the new name shows right away.
            Schedule(() => revision.Value++);
            refreshScoreDisplays();
        }

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
            var profileUserIds = Store.GetProfiles().Select(p => p.UserId).ToHashSet();

            return Store.GetFailedCalculations()
                        .Select(id => r.Find<ScoreInfo>(id))
                        .Where(s => s != null && !s.DeletePending && profileUserIds.Contains(s.RealmUser.OnlineID))
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
            var profileUserIds = Store.GetProfiles().Select(p => p.UserId).ToHashSet();
            var cutoff = new DateTimeOffset(OLDEST_VALID_PLAY_DATE, TimeSpan.Zero);

            return r.All<ScoreInfo>()
                    .Filter($@"{nameof(ScoreInfo.Date)} < $0 AND {nameof(ScoreInfo.DeletePending)} == false", cutoff)
                    .AsEnumerable()
                    .Where(s => profileUserIds.Contains(s.RealmUser.OnlineID))
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

        /// <summary>
        /// Settings button: counts every local profile's saved scores (replays) again as plays (play count, play time, hits,
        /// most played beatmaps and play history) and fills in its statistics with them (values only go up, see
        /// <see cref="LocalProfileStore.MergeStatisticsFromScores"/>). Synced plays are left out, since the profile page counts
        /// them on top. Runs in the background.
        /// </summary>
        public System.Threading.Tasks.Task<(int Profiles, int Scores)> RebuildStatisticsFromScoresAsync() => System.Threading.Tasks.Task.Run(() =>
        {
            var profiles = Store.GetProfiles().ToDictionary(p => p.UserId);
            var synced = profiles.Values.SelectMany(p => p.SyncedScores).Select(s => s.ScoreId).ToHashSet();
            var cutoff = new DateTimeOffset(OLDEST_VALID_PLAY_DATE, TimeSpan.Zero);
            var counted = new Dictionary<int, Dictionary<string, LocalPlayStatistics>>();
            int scoreCount = 0;

            realm.Run(r =>
            {
                foreach (var score in r.All<ScoreInfo>().Filter($@"{nameof(ScoreInfo.DeletePending)} == false"))
                {
                    int userId = score.RealmUser.OnlineID;

                    if (!profiles.ContainsKey(userId) || synced.Contains(score.ID) || score.Date < cutoff)
                        continue;

                    if (!counted.TryGetValue(userId, out var perRuleset))
                        counted[userId] = perRuleset = new Dictionary<string, LocalPlayStatistics>();

                    string ruleset = score.Ruleset.ShortName;

                    if (!perRuleset.TryGetValue(ruleset, out var stats))
                        perRuleset[ruleset] = stats = new LocalPlayStatistics();

                    stats.PlayCount++;

                    // A score of a ruleset that isn't installed (or with mods that can't be read) counts at normal speed.
                    double rate = 1;

                    try
                    {
                        if (score.Ruleset.Available)
                            rate = ModUtils.CalculateRateWithMods(score.Mods);
                    }
                    catch (Exception)
                    {
                        rate = 1;
                    }

                    if (score.BeatmapInfo != null && rate > 0)
                        stats.PlayTimeSeconds += score.BeatmapInfo.Length / 1000 / rate;

                    if (stats.LastPlayed == null || score.Date > stats.LastPlayed)
                        stats.LastPlayed = score.Date;

                    // Plays are counted per beatmap MD5 hash (scores keep a different hash), like recorded attempts.
                    stats.AddPlay(score.BeatmapInfo?.MD5Hash, score.Date, 1);

                    foreach (var (result, count) in score.Statistics)
                    {
                        if (count <= 0)
                            continue;

                        stats.Hits.TryGetValue(result, out long existing);
                        stats.Hits[result] = existing + count;
                    }

                    scoreCount++;
                }
            });

            int changed = Store.MergeStatisticsFromScores(counted);

            Schedule(() => revision.Value++);
            return (changed, scoreCount);
        });

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

                var imported = (await scoreManager.Import(notification, files.Select(f => new ImportTask(f)).ToArray()).ConfigureAwait(false))
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

            var profileUserIds = Store.GetProfiles().Select(p => p.UserId).ToHashSet();
            var synced = new List<SyncedScore>();
            int alreadyOnProfiles = 0;

            realm.Write(r =>
            {
                foreach (var id in importedIds.Distinct())
                {
                    var score = r.Find<ScoreInfo>(id);

                    if (score == null)
                        continue;

                    // Already imported before and on a local profile (this one or another): leave it there.
                    if (profileUserIds.Contains(score.RealmUser.OnlineID))
                    {
                        alreadyOnProfiles++;
                        continue;
                    }

                    synced.Add(new SyncedScore
                    {
                        ScoreId = score.ID,
                        OriginalUserId = score.RealmUser.OnlineID,
                        OriginalUsername = score.RealmUser.Username,
                        OriginalCountry = score.RealmUser.CountryCode,
                    });

                    score.RealmUser = new Models.RealmUser
                    {
                        OnlineID = profile.UserId,
                        Username = profile.Name,
                        CountryCode = profile.Country,
                    };
                }
            });

            if (synced.Count > 0)
                Store.AddSyncedScores(profileId, synced);

            refreshScoreDisplays();

            var parts = new List<string> { $@"{synced.Count:N0} of {found:N0} replays imported as ""{profile.Name}""." };

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
                        var statistics = await LocalProfileStatistics.CalculateAsync(profile, ruleset, realm, beatmapManager, Store, token).ConfigureAwait(false);
                        var top = statistics.BestScores.Where(s => s.CountsForPerformance).Take(TOP_REPLAYS_EXPORT_COUNT).ToArray();

                        LocalScoreEntry.Load(top, realm);
                        scores = top.Select(s => s.Score).OfType<ScoreInfo>().ToList();
                    }
                }
                else
                {
                    scores = realm.Run(r => r.All<ScoreInfo>()
                                             .Filter($@"{nameof(ScoreInfo.User)}.{nameof(Models.RealmUser.OnlineID)} == $0 AND {nameof(ScoreInfo.DeletePending)} == false", profile.UserId)
                                             .AsEnumerable()
                                             .OrderBy(s => s.Date)
                                             .Select(s => s.Detach())
                                             .ToList());
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

        public void RecordPlay(int userId, string ruleset, double playSeconds, IReadOnlyDictionary<osu.Game.Rulesets.Scoring.HitResult, int> hits, string? beatmapHash = null,
                               LocalUnfinishedPlay? unfinished = null) =>
            Store.RecordPlay(userId, ruleset, playSeconds, hits, beatmapHash, unfinished);

        /// <summary>
        /// A beatmap was entered: played, or watched as a replay (any gameplay screen). Called by the game on the update thread.
        /// </summary>
        public event Action? BeatmapEntered;

        public void NotifyBeatmapEntered() => BeatmapEntered?.Invoke();

        private void onStoreChanged() => Schedule(applyStoreState);

        private void applyStoreState()
        {
            activeProfile.Value = Store.ActiveProfile;
            LocalProfilesUseOnlineAccount.Value = Store.LocalProfilesUseOnlineAccount;
            readOptions();
            updateAutoSwitchActive();
            checkPerformanceInputs();
            revision.Value++;
            applyToApi();
            applyModRulesIfChanged();
        }

        private void applyToApi()
        {
            var profile = activeProfile.Value;

            // Settings → Ritsu "Count everything as ranked" applies to every local profile (not the online account).
            LocalPriorityPolicy.CountAllAsRanked = profile != null && (profile.CountAllAsRanked || Store.Options.CountEverythingAsRanked);
            LocalPriorityPolicy.LocalMapsGivePerformance = Store.Options.LocalMapsGivePerformance;
            LocalPriorityPolicy.CountRelaxAutopilot = profile?.IncludeRelaxAutopilot == true;
            LocalPriorityPolicy.IgnoreUnrankedMods = profile?.IgnoreUnrankedMods == true;

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
            Store.CalculationsFailed -= onCalculationsFailed;
            scoreSubscription?.Dispose();

            // A colour picked less than half a second before quitting.
            if (pendingProfileColourWrite != null)
            {
                pendingProfileColourWrite.Cancel();
                pendingProfileColourWrite = null;
                Store.SetProfileColours(ProfileColouring.Value, ProfileColour.Value.ToHex());
            }

            Store.Flush();

            if (api is ILocalProfileApi apiAccess)
                apiAccess.OnlineAccountRequested -= onOnlineAccountRequested;

            base.Dispose(isDisposing);
        }
    }
}
