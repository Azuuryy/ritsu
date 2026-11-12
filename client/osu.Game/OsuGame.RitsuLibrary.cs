// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Screens;
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
using osu.Game.Scoring.Legacy;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Utils;

namespace osu.Game
{
    public partial class OsuGame
    {
        private readonly List<RitsuQueueItem> ritsuQueue = new List<RitsuQueueItem>();

        private bool toggleRitsuPalette()
        {
            if (dialogOverlay?.CurrentDialog is RitsuCommandPalette palette)
            {
                palette.Hide();
                return true;
            }

            if (!RitsuOffline.Enabled || dialogOverlay?.IsLoaded != true || dialogOverlay.CurrentDialog != null
                || ScreenStack.CurrentScreen is not (MainMenu or SoloSongSelect or ResultsScreen))
                return false;

            dialogOverlay.Push(new RitsuCommandPalette(ritsuPaletteEntries));
            return true;
        }

        private RitsuPaletteEntry[] ritsuPaletteEntries()
        {
            var library = Dependencies.Get<RitsuLibrary>();
            var entries = new List<RitsuPaletteEntry>
            {
                new RitsuPaletteEntry("Open last local score", "/recent", openRitsuRecent),
            };

            if (!Beatmap.IsDefault && Beatmap.Value.BeatmapInfo is { Hidden: false } difficulty && difficulty.BeatmapSet?.DeletePending == false)
            {
                if (ritsuQueue.All(item => item.Hash != difficulty.MD5Hash))
                    entries.Add(new RitsuPaletteEntry("Add selected difficulty to the queue", "/queue add", () => QueueRitsuBeatmap(difficulty), true));
                entries.Add(new RitsuPaletteEntry("Open difficulty memo", "/memo", () => ShowRitsuMemo(difficulty)));
            }

            if (ritsuQueue.Count > 0)
            {
                entries.Add(new RitsuPaletteEntry("Select next queued difficulty", "/queue next", nextRitsuQueueItem));

                foreach (var (item, index) in ritsuQueue.Select((item, index) => (item, index)))
                {
                    entries.Add(new RitsuPaletteEntry($"Remove {index + 1}. {item.Title}", "/queue remove", () => ritsuQueue.Remove(item), true));

                    if (index > 0)
                        entries.Add(new RitsuPaletteEntry($"Move up {index + 1}. {item.Title}", "/queue up", () => moveRitsuQueueItem(item, -1), true));
                    if (index < ritsuQueue.Count - 1)
                        entries.Add(new RitsuPaletteEntry($"Move down {index + 1}. {item.Title}", "/queue down", () => moveRitsuQueueItem(item, 1), true));
                }
            }

            string mode = Ruleset.Value.ShortName;
            var presets = Dependencies.Get<RealmAccess>().Run(r => r.All<ModPreset>().Where(p => !p.DeletePending).AsEnumerable()
                .Where(p => p.Ruleset.ShortName == mode).Select(p => (p.Name, p.ModsJson)).ToArray());

            foreach (var preset in presets)
                entries.Add(new RitsuPaletteEntry($"Load mod preset · {preset.Name}", "/mods", () => loadRitsuModPreset(mode, preset.ModsJson)));

            if (ScreenStack.CurrentScreen is SoloSongSelect select)
            {
                entries.Add(new RitsuPaletteEntry("Save session preset", "/session save", () => saveRitsuSession(select)));
                if (!Beatmap.IsDefault)
                    entries.Add(new RitsuPaletteEntry("Set target BPM", "/bpm", ShowRitsuTargetBpm));
            }

            foreach (var preset in library.Presets)
            {
                entries.Add(new RitsuPaletteEntry($"Load session · {preset.Name}", "/session load", () => loadRitsuSession(preset)));
                entries.Add(new RitsuPaletteEntry($"Delete session · {preset.Name}", "/session delete", () =>
                    dialogOverlay.Push(new ConfirmDialog($"Delete {preset.Name}?", () => library.DeletePreset(preset.Id)))));
            }

            return entries.ToArray();
        }

        internal void QueueRitsuBeatmap(BeatmapInfo beatmap)
        {
            if (string.IsNullOrEmpty(beatmap.MD5Hash) || ritsuQueue.Any(item => item.Hash == beatmap.MD5Hash))
                return;
            ritsuQueue.Add(new RitsuQueueItem(beatmap.MD5Hash, $"{beatmap.Metadata.Title} [{beatmap.DifficultyName}]"));
        }

        private void moveRitsuQueueItem(RitsuQueueItem item, int direction)
        {
            int from = ritsuQueue.IndexOf(item);
            int to = from + direction;
            if (from >= 0 && to >= 0 && to < ritsuQueue.Count)
                (ritsuQueue[from], ritsuQueue[to]) = (ritsuQueue[to], ritsuQueue[from]);
        }

        private void nextRitsuQueueItem()
        {
            withRitsuSongSelect(select =>
            {
                while (ritsuQueue.Count > 0)
                {
                    var entry = ritsuQueue[0];
                    ritsuQueue.RemoveAt(0);
                    var map = BeatmapManager.QueryBeatmap(b => b.MD5Hash == entry.Hash && !b.Hidden);
                    if (map?.BeatmapSet is not { DeletePending: false, Protected: false })
                        continue;

                    select.SelectQueuedBeatmap(map);
                    return;
                }

                postRitsuNotice("No queued difficulties are available in this library.");
            });
        }

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

        internal void ShowRitsuTargetBpm()
        {
            if (Beatmap.IsDefault || SelectedMods.Disabled || dialogOverlay == null)
                return;

            var working = Beatmap.Value;
            try
            {
                dialogOverlay.Push(new RitsuTargetBpmDialog(working.Beatmap, Ruleset.Value.CreateInstance(), SelectedMods.Value,
                    newMods => SelectedMods.Value = newMods));
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
                postRitsuNotice("The timing data for this beatmap is unavailable.");
            }
        }

        internal void ShowRitsuMemo(BeatmapInfo beatmap, ScoreInfo? result = null)
        {
            if (dialogOverlay == null || string.IsNullOrEmpty(beatmap.MD5Hash))
                return;

            var library = Dependencies.Get<RitsuLibrary>();
            var profiles = Dependencies.Get<RitsuProfileManager>();
            Guid profileId = profiles.ActiveProfileId;
            var marked = profiles.GetScores(profileId, Ruleset.Value.ShortName).Where(s => s.BeatmapHash == beatmap.Hash);
            if (result?.BeatmapHash == beatmap.Hash && ScoreManager.Query(s => s.ID == result.ID && !s.DeletePending) is { } localScore)
                marked = marked.Append(localScore);

            var candidates = marked.DistinctBy(s => s.ID).OrderByDescending(s => s.Date).ToArray();
            dialogOverlay.Push(new RitsuMemoDialog(beatmap.DifficultyName, library.GetMemo(profileId, beatmap.MD5Hash), candidates,
                replayId =>
                {
                    try
                    {
                        var score = ScoreManager.Query(s => s.ID == replayId && !s.DeletePending && s.BeatmapHash == beatmap.Hash);
                        return score == null ? null : ScoreManager.GetScore(score)?.Replay?.Frames.LastOrDefault()?.Time;
                    }
                    catch (Exception e) when (e is IOException or InvalidOperationException or LegacyScoreDecoder.BeatmapNotFoundException)
                    {
                        return null;
                    }
                },
                memo => library.SaveMemo(profileId, beatmap.MD5Hash, memo),
                (replayId, time) =>
                {
                    var score = ScoreManager.Query(s => s.ID == replayId && !s.DeletePending && s.BeatmapHash == beatmap.Hash);
                    if (score != null)
                        presentScore(score, ScorePresentType.Gameplay, true, time);
                    else
                        postRitsuNotice("This replay is no longer available.");
                }));
        }

        private void postRitsuNotice(string text) => Notifications.Post(new SimpleNotification { Text = text });

        private sealed record RitsuQueueItem(string Hash, string Title);
    }
}
