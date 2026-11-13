// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Statistics;
using osu.Game.Performance;
using osu.Game.IO;
using osu.Game.Commands;
using osu.Game.Library;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Database;
using osu.Game.Offline;
using osu.Game.Online.API;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Utils;

namespace osu.Game
{
    public partial class OsuGame
    {
        private RitsuFrameProfiler? ritsuFrameProfiler;

        private bool toggleRitsuPalette() => toggleRitsuPalette(null);

        private bool toggleRitsuPalette(string? prefix)
        {
            if (dialogOverlay?.CurrentDialog is RitsuCommandPalette palette)
            {
                palette.Hide();
                return true;
            }

            if (!RitsuOffline.Enabled || dialogOverlay?.IsLoaded != true || dialogOverlay.CurrentDialog != null
                || ScreenStack.CurrentScreen is not (MainMenu or SoloSongSelect or ResultsScreen))
                return false;

            dialogOverlay.Push(new RitsuCommandPalette(createRitsuCommands(), prefix));
            return true;
        }

        private RitsuCommandRegistry createRitsuCommands()
        {
            var library = Dependencies.Get<RitsuLibrary>();
            var realm = Dependencies.Get<RealmAccess>();
            var mode = Ruleset.Value.ShortName;

            BeatmapInfo? selectedDifficulty() =>
                !Beatmap.IsDefault && Beatmap.Value.BeatmapInfo is { Hidden: false, BeatmapSet.DeletePending: false } map ? map : null;

            RitsuCommand simple(string id, string alias, string title, Func<bool> available, Action action, bool keepOpen = false) =>
                new RitsuCommand(id, alias, title, string.Empty, available, input => string.IsNullOrEmpty(input)
                    ? new RitsuCommandInvocation(action, KeepOpen: keepOpen)
                    : RitsuCommandInvocation.Invalid($"Usage: {alias}"));

            RitsuCommand queueChoice(string action, string title, int direction = 0) =>
                new RitsuCommand($"queue.{action}", $"/queue {action}", title, string.Empty, () => ritsuQueue.Snapshot().Length > 0,
                    hash =>
                    {
                        var items = ritsuQueue.Snapshot();
                        int index = Array.FindIndex(items, item => item.Hash == hash);
                        if (index < 0 || index + direction < 0 || index + direction >= items.Length)
                            return RitsuCommandInvocation.Invalid("This queued difficulty is unavailable.");

                        return new RitsuCommandInvocation(() =>
                        {
                            if (direction == 0)
                                ritsuQueue.Remove(hash);
                            else
                                ritsuQueue.Move(hash, direction);
                        }, KeepOpen: true);
                    },
                    () =>
                    {
                        var items = ritsuQueue.Snapshot();
                        return items.Select((item, index) => (item, index))
                                    .Where(pair => pair.index + direction >= 0 && pair.index + direction < items.Length)
                                    .Select(pair => new RitsuCommandChoice(pair.item.Hash, $"{title} {pair.index + 1}. {pair.item.Title}")).ToArray();
                    });

            RitsuCommand sessionChoice(string action, string title, Action<RitsuSessionPreset> run) =>
                new RitsuCommand($"session.{action}", $"/session {action}", title, string.Empty, () => true,
                    id =>
                    {
                        var preset = Guid.TryParse(id, out var guid) ? library.Presets.FirstOrDefault(p => p.Id == guid) : null;
                        return preset == null ? RitsuCommandInvocation.Invalid("This session preset is unavailable.")
                            : new RitsuCommandInvocation(() => run(preset));
                    },
                    () => library.Presets.Select(p => new RitsuCommandChoice(p.Id.ToString(), $"{title} · {p.Name}")).ToArray());

            // The list lives for one palette opening. Resolve the chosen preset again before applying it.
            var presets = new Lazy<RitsuCommandChoice[]>(() =>
            {
                using var measurement = FrameProfileCapture.Measure("Palette.mod-presets");
                return realm.Run(r => r.All<ModPreset>().Where(p => !p.DeletePending).AsEnumerable()
                    .Where(p => p.Ruleset.ShortName == mode)
                    .Select(p => new RitsuCommandChoice(p.ID.ToString(), $"Load mod preset · {p.Name}")).ToArray());
            });

            return new RitsuCommandRegistry(new[]
            {
                simple("osu.map", "/map", "Map attributes and perfect PP", () => !Beatmap.IsDefault, () => ShowRitsuMap(false)),
                simple("osu.simulate", "/simulate", "Simulate PP", () => !Beatmap.IsDefault, () => ShowRitsuMap(true)),
                simple("osu.visualize", "/visualize", "Simulate PP", () => !Beatmap.IsDefault, () => ShowRitsuMap(true)),
                simple("osu.profile", "/profile", "Show active local profile statistics", () => true, () => showRitsuProfile(false)),
                simple("osu.top", "/top", "Browse best scores of the active profile", () => true, () => showRitsuProfile(true)),
                simple("recent", "/recent", "Open last local score", () => true, openRitsuRecent),
                simple("compare", "/compare", "Compare local scores", () => selectedDifficulty() != null, () =>
                {
                    if (selectedDifficulty() is { } map)
                        showRitsuCompare(map);
                }),
                simple("memo", "/memo", "Open difficulty memo", () => selectedDifficulty() != null, () =>
                {
                    if (selectedDifficulty() is { } map)
                        ShowRitsuMemo(map);
                }),
                simple("queue.add", "/queue add", "Add selected difficulty to the queue", () => ritsuQueue.CanAdd(selectedDifficulty()), () =>
                {
                    if (selectedDifficulty() is { } map)
                        QueueRitsuBeatmap(map);
                }, true),
                simple("queue.next", "/queue next", "Select next queued difficulty", () => ritsuQueue.Snapshot().Length > 0, nextRitsuQueueItem),
                queueChoice("remove", "Remove"),
                queueChoice("up", "Move up", -1),
                queueChoice("down", "Move down", 1),
                new RitsuCommand("mods", "/mods", "Load mod preset", string.Empty, () => Ruleset.Value.ShortName == mode, id =>
                {
                    string? json = Guid.TryParse(id, out var guid) ? realm.Run(r =>
                    {
                        var preset = r.Find<ModPreset>(guid);
                        return preset is { DeletePending: false } && preset.Ruleset.ShortName == mode ? preset.ModsJson : null;
                    }) : null;
                    return json == null ? RitsuCommandInvocation.Invalid("This mod preset is unavailable.")
                        : new RitsuCommandInvocation(() => loadRitsuModPreset(mode, json));
                }, () => presets.Value),
                simple("session.save", "/session save", "Save session preset", () => ScreenStack.CurrentScreen is SoloSongSelect, () =>
                {
                    if (ScreenStack.CurrentScreen is SoloSongSelect select)
                        saveRitsuSession(select);
                }),
                sessionChoice("load", "Load session", loadRitsuSession),
                sessionChoice("delete", "Delete session", preset =>
                    dialogOverlay.Push(new ConfirmDialog($"Delete {preset.Name}?", () => library.DeletePreset(preset.Id)))),
                new RitsuCommand("bpm", "/bpm", "Set target BPM", "[target] [--keep-ar]",
                    () => ScreenStack.CurrentScreen is SoloSongSelect && selectedDifficulty() != null && !SelectedMods.Disabled,
                    bindRitsuBpm),
                simple("profiler.start", "/profiler start", "Start a 30-second frame capture", () => OperatingSystem.IsWindows() && ritsuFrameProfiler?.CanStart != false, startRitsuProfiler),
                simple("profiler.stop", "/profiler stop", "Stop frame capture", () => ritsuFrameProfiler?.IsRunning == true, stopRitsuProfiler),
            }.Concat(ritsuLocalCommands()).ToArray());
        }

        private async void startRitsuProfiler()
        {
            try
            {
                var storage = Storage is OsuStorage osuStorage ? Host.GetStorage(osuStorage.DefaultStoragePath) : Storage;
                ritsuFrameProfiler ??= new RitsuFrameProfiler(storage.GetStorageForDirectory("ritsu/profiles/frame"), Host);
                var pending = ritsuFrameProfiler.Start(action => Scheduler.Add(action));
                if (!pending.IsCompleted)
                    postRitsuNotice("Frame capture started.");
                string message = await pending.ConfigureAwait(false);
                Scheduler.Add(() => postRitsuNotice(message));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Scheduler.Add(() => postRitsuNotice($"Could not export the frame profile: {e.Message}"));
            }
        }

        private void stopRitsuProfiler() => ritsuFrameProfiler?.Stop();

        private void withRitsuSongSelect(Action<SoloSongSelect> action)
        {
            PerformFromScreen(screen =>
            {
                if (screen is SoloSongSelect select)
                    action(select);
                else
                    screen.Push(new SoloSongSelect { RitsuOnEnter = action });
            }, new[] { typeof(SoloSongSelect) });
        }

        private void openRitsuRecent()
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            var score = profiles.GetScores(profiles.ActiveProfileId, Ruleset.Value.ShortName).MaxBy(s => s.Date);
            if (score == null)
                postRitsuNotice("This profile has no local scores in the current mode.");
            else
                PresentScoreFromLocalProfile(score);
        }

        private void showRitsuCompare(BeatmapInfo beatmap)
        {
            var profiles = Dependencies.Get<RitsuProfileManager>();
            Guid profile = profiles.ActiveProfileId;
            string mode = Ruleset.Value.ShortName;
            var scores = profiles.GetScores(profile, mode, beatmap.Hash);
            dialogOverlay.Push(new RitsuCompareDialog(scores.ToArray(), score =>
            {
                if (profiles.ActiveProfileId == profile && Ruleset.Value.ShortName == mode
                    && ScoreManager.Query(s => s.ID == score.ID && !s.DeletePending) != null)
                    PresentScoreFromLocalProfile(score);
                else
                    postRitsuNotice("This local score is no longer available.");
            }));
        }

        private void loadRitsuModPreset(string mode, string json)
        {
            var targetRuleset = RulesetStore.GetRuleset(mode);
            var mods = targetRuleset == null ? null : readRitsuMods(targetRuleset, json);
            if (mods == null)
            {
                postRitsuNotice("A mod or setting in this preset is unavailable.");
                return;
            }

            withRitsuSongSelect(_ =>
            {
                if (Ruleset.Value.ShortName == mode)
                    SelectedMods.Value = mods;
            });
        }

        private void saveRitsuSession(SoloSongSelect select)
        {
            var library = Dependencies.Get<RitsuLibrary>();
            string ruleset = Ruleset.Value.ShortName;
            string mods = JsonConvert.SerializeObject(SelectedMods.Value.Where(m => m.Type != ModType.System).Select(m => new APIMod(m)));
            string skin = SkinManager.CurrentSkinInfo.Value.ID.ToString();
            int hudVisibility = (int)LocalConfig.Get<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode);
            bool keyOverlay = LocalConfig.Get<bool>(OsuSetting.KeyOverlay);
            var search = select.RitsuFilters.CaptureSearch();

            dialogOverlay.Push(new RitsuInputDialog("Save session preset", string.Empty, name =>
            {
                if (name.Length == 0)
                    return "Enter a name for this session.";
                library.SavePreset(new RitsuSessionPreset(Guid.NewGuid(), name, ruleset, mods, skin, hudVisibility, keyOverlay, search));
                return null;
            }));
        }

        private void loadRitsuSession(RitsuSessionPreset preset)
        {
            var ruleset = RulesetStore.GetRuleset(preset.Ruleset);
            var mods = ruleset == null ? null : readRitsuMods(ruleset, preset.Mods);
            var skin = Guid.TryParse(preset.Skin, out var skinId)
                ? SkinManager.GetAllUsableSkins().FirstOrDefault(s => s.ID == skinId)
                : null;
            if (ruleset == null || mods == null || skin == null || !Enum.IsDefined((HUDVisibilityMode)preset.HudVisibility))
            {
                postRitsuNotice("The session preset contains an unavailable skin, mode, mod or HUD setting.");
                return;
            }

            withRitsuSongSelect(select =>
            {
                if (!select.RitsuFilters.HasCollection(preset.Search.Collection))
                {
                    postRitsuNotice("The session preset's starting collection is unavailable.");
                    return;
                }

                Ruleset.Value = ruleset;
                SelectedMods.Value = mods;
                SkinManager.CurrentSkinInfo.Value = skin;
                LocalConfig.SetValue(OsuSetting.HUDVisibilityMode, (HUDVisibilityMode)preset.HudVisibility);
                LocalConfig.SetValue(OsuSetting.KeyOverlay, preset.KeyOverlay);
                select.RitsuFilters.RestoreSearch(preset.Search);
            });
        }

        private static Mod[]? readRitsuMods(RulesetInfo info, string json)
        {
            try
            {
                var ruleset = info.CreateInstance();
                var configured = JsonConvert.DeserializeObject<APIMod[]>(json);
                if (configured == null)
                    return null;

                var mods = new List<Mod>();
                foreach (var item in configured)
                {
                    var mod = ruleset.CreateModFromAcronym(item.Acronym);
                    if (mod == null)
                        return null;
                    var keys = mod.GetSettingsSourceProperties().Select(p => p.Item2.Name.ToSnakeCase()).ToHashSet();
                    if (item.Settings.Keys.Any(k => !keys.Contains(k)))
                        return null;
                    mods.Add(item.ToMod(ruleset));
                }

                return ModUtils.CheckValidForGameplay(mods, out _) ? mods.ToArray() : null;
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
            {
                return null;
            }
        }

        private RitsuCommandInvocation bindRitsuBpm(string input)
        {
            var tokens = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            double? target = null;
            bool keepAr = false;
            foreach (string token in tokens)
            {
                if (token.Equals("--keep-ar", StringComparison.OrdinalIgnoreCase) && !keepAr)
                    keepAr = true;
                else if (!target.HasValue && RitsuTargetBpmDialog.TryParseBpm(token, out double bpm) && double.IsFinite(bpm) && bpm > 0)
                    target = bpm;
                else
                    return RitsuCommandInvocation.Invalid("Usage: /bpm [positive target] [--keep-ar]");
            }

            if (keepAr && (Ruleset.Value.CreateInstance().CreateModFromAcronym("DA") as ModDifficultyAdjust)?.RitsuApproachRate == null)
                return RitsuCommandInvocation.Invalid("This mode cannot preserve approach rate.");

            return new RitsuCommandInvocation(() => showRitsuTargetBpm(target, keepAr));
        }

        internal void ShowRitsuTargetBpm() => bindRitsuBpm(string.Empty).Run?.Invoke();

        private void showRitsuTargetBpm(double? target, bool keepAr)
        {
            if (Beatmap.IsDefault || SelectedMods.Disabled || dialogOverlay == null)
                return;

            var working = Beatmap.Value;
            var mode = Ruleset.Value;
            string originalMods = JsonConvert.SerializeObject(SelectedMods.Value.Select(mod => new APIMod(mod)));
            try
            {
                dialogOverlay.Push(new RitsuTargetBpmDialog(working.Beatmap, mode.CreateInstance(), SelectedMods.Value,
                    newMods => SelectedMods.Value = newMods, target, keepAr,
                    () => ReferenceEquals(working, Beatmap.Value) && mode.Equals(Ruleset.Value) && !SelectedMods.Disabled
                          && originalMods == JsonConvert.SerializeObject(SelectedMods.Value.Select(mod => new APIMod(mod)))));
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
                postRitsuNotice("The timing data for this beatmap is unavailable.");
            }
        }

        private void postRitsuNotice(string text) => Notifications.Post(new SimpleNotification { Text = text });

    }
}
