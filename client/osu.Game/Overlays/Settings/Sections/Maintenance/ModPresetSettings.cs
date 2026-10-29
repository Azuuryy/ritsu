// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class ModPresetSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.ModPresets;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        private SettingsButtonV2 undeleteButton = null!;
        private SettingsButtonV2 deleteAllButton = null!;
        private ISystemFileSelector? presetSelector;
        private Storage exportStorage = null!;

        [BackgroundDependencyLoader]
        private void load(IDialogOverlay? dialogOverlay, GameHost host, Storage storage)
        {
            exportStorage = storage.GetStorageForDirectory("exports");
            presetSelector = host.CreateSystemFileSelector(new[] { ".json" });
            if (presetSelector != null)
                presetSelector.Selected += file => Task.Run(() => importPresets(file.FullName));
            AddRange(new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = "Import mod presets from JSON",
                    Action = () =>
                    {
                        if (presetSelector != null) presetSelector.Present();
                        else notificationOverlay?.Post(new SimpleErrorNotification { Text = "File selection is unavailable on this platform." });
                    },
                },
                new SettingsButtonV2
                {
                    Text = "Export mod presets to JSON",
                    Action = () => Task.Run(exportPresets),
                },
                deleteAllButton = new DangerousSettingsButtonV2
                {
                    Text = MaintenanceSettingsStrings.DeleteAllModPresets,
                    Action = () =>
                    {
                        dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                        {
                            deleteAllButton.Enabled.Value = false;
                            Task.Run(deleteAllModPresets).ContinueWith(t => Schedule(onAllModPresetsDeleted, t));
                        }, DeleteConfirmationContentStrings.ModPresets));
                    }
                },
                undeleteButton = new SettingsButtonV2
                {
                    Text = MaintenanceSettingsStrings.RestoreAllRecentlyDeletedModPresets,
                    Action = () => Task.Run(undeleteModPresets).ContinueWith(t => Schedule(onModPresetsUndeleted, t))
                }
            });
        }

        private void importPresets(string path)
        {
            try
            {
                var items = JsonConvert.DeserializeObject<List<PresetTransfer>>(File.ReadAllText(path)) ?? throw new JsonException("Expected an array of presets.");
                int imported = 0;
                var skipped = new List<string>();
                realm.Write(r =>
                {
                    foreach (var item in items)
                    {
                        string shortName = item.RulesetShortName;
                        if (string.IsNullOrEmpty(shortName) || shortName is "mosu" or "modosu") shortName = "osu";
                        var ruleset = r.All<RulesetInfo>().FirstOrDefault(x => x.ShortName == shortName);
                        if (ruleset == null || string.IsNullOrWhiteSpace(item.Name)) { skipped.Add(item.Name); continue; }
                        if (r.All<ModPreset>().AsEnumerable().Any(p => !p.DeletePending && p.Ruleset.ShortName == shortName && string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                        { skipped.Add(item.Name); continue; }

                        var apiMods = JsonConvert.DeserializeObject<List<APIMod>>(item.ModsJson);
                        if (apiMods == null) { skipped.Add(item.Name); continue; }
                        var instance = ruleset.CreateInstance();
                        bool unknown = apiMods.Any(m =>
                        {
                            var mod = instance.CreateModFromAcronym(m.Acronym);
                            if (mod == null) return true;
                            var settings = mod.GetSettingsSourceProperties().Select(p => p.Item2.Name.ToSnakeCase()).ToHashSet();
                            return m.Settings.Keys.Any(k => !settings.Contains(k));
                        });
                        if (unknown) { skipped.Add(item.Name); continue; }
                        r.Add(new ModPreset { ID = Guid.NewGuid(), Name = item.Name, Description = item.Description, ModsJson = item.ModsJson, Ruleset = ruleset });
                        imported++;
                    }
                });
                string suffix = skipped.Count > 0 ? $" Skipped: {string.Join(", ", skipped)}." : string.Empty;
                Schedule(() => notificationOverlay?.Post(new SimpleNotification { Text = $"Imported {imported} presets.{suffix}" }));
            }
            catch (Exception ex)
            {
                Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification { Text = $"Preset import failed: {ex.Message}" }));
            }
        }

        private void exportPresets()
        {
            try
            {
                var items = realm.Run(r => r.All<ModPreset>().Where(p => !p.DeletePending).AsEnumerable().Select(p => new PresetTransfer
                {
                    Name = p.Name, Description = p.Description, ModsJson = p.ModsJson, RulesetShortName = p.Ruleset.ShortName,
                }).ToArray());
                string filename = $"ritsu_mod_presets_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json";
                using (var stream = exportStorage.CreateFileSafely(filename))
                using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(items, Formatting.Indented));
                Schedule(() =>
                {
                    notificationOverlay?.Post(new SimpleNotification { Text = $"Exported {items.Length} presets to {filename}." });
                    exportStorage.PresentFileExternally(filename);
                });
            }
            catch (Exception ex)
            {
                Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification { Text = $"Preset export failed: {ex.Message}" }));
            }
        }

        private sealed class PresetTransfer
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string ModsJson { get; set; } = string.Empty;
            public string RulesetShortName { get; set; } = string.Empty;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing) presetSelector?.Dispose();
            base.Dispose(isDisposing);
        }

        private bool deleteAllModPresets() =>
            realm.Write(r =>
            {
                bool anyDeleted = false;

                foreach (var preset in r.All<ModPreset>())
                {
                    anyDeleted |= !preset.DeletePending;
                    preset.DeletePending = true;
                }

                return anyDeleted;
            });

        private void onAllModPresetsDeleted(Task<bool> deletionTask)
        {
            deleteAllButton.Enabled.Value = true;

            if (deletionTask.IsCompletedSuccessfully)
                notificationOverlay?.Post(new ProgressCompletionNotification { Text = deletionTask.GetResultSafely() ? MaintenanceSettingsStrings.DeletedAllModPresets : MaintenanceSettingsStrings.NoModPresetsFoundToDelete });
            else if (deletionTask.IsFaulted)
                Logger.Error(deletionTask.Exception, "Failed to delete all mod presets");
        }

        private bool undeleteModPresets() =>
            realm.Write(r =>
            {
                bool anyRestored = false;

                foreach (var preset in r.All<ModPreset>().Where(preset => preset.DeletePending))
                {
                    anyRestored |= preset.DeletePending;
                    preset.DeletePending = false;
                }

                return anyRestored;
            });

        private void onModPresetsUndeleted(Task<bool> undeletionTask)
        {
            undeleteButton.Enabled.Value = true;

            if (undeletionTask.IsCompletedSuccessfully)
                notificationOverlay?.Post(new ProgressCompletionNotification { Text = undeletionTask.GetResultSafely() ? MaintenanceSettingsStrings.RestoredAllDeletedModPresets : MaintenanceSettingsStrings.NoModPresetsFoundToRestore });
            else if (undeletionTask.IsFaulted)
                Logger.Error(undeletionTask.Exception, "Failed to restore mod presets");
        }
    }
}
