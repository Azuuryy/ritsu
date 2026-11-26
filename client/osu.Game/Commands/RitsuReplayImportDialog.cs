// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Offline;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Scoring.Legacy;
using osuTK;

namespace osu.Game
{
    public partial class OsuGame
    {
        internal void ShowRitsuReplayImport(Guid? profileId = null)
        {
            // osu!local: import into the active local profile ("Import replays (.osr) from a folder…" on its profile page).
            var localProfileManager = Dependencies.Get<LocalPriority.LocalProfileManager>();

            if (profileId == null && localProfileManager?.ActiveProfile.Value is { } localProfile)
            {
                localProfileManager.BeginReplayImport(localProfile.Id);
                return;
            }

            var profiles = Dependencies.Get<RitsuProfileManager>();
            var importer = new RitsuReplayImporter(RulesetStore, BeatmapManager, Dependencies.Get<BeatmapDifficultyCache>(), ScoreManager, profiles, Dependencies.Get<osu.Game.Database.RealmAccess>(), Storage);
            dialogOverlay.Push(new Commands.RitsuReplayImportDialog(importer, profiles.GetProfiles(), profileId ?? profiles.ActiveProfileId));
        }
    }
}

namespace osu.Game.Commands
{
    internal partial class RitsuReplayImportDialog : PopupDialog
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private readonly RitsuReplayImporter importer;
        private readonly FormFileSelector file;
        private readonly OsuDropdown<ProfileChoice> profile;
        private readonly OsuDropdown<ReplayChoice> source;
        private IReadOnlyList<RitsuSharedReplay> sharedReplays = Array.Empty<RitsuSharedReplay>();
        private readonly PopupDialogOkButton importButton;
        private readonly PopupDialogCancelButton cancelButton;

        public RitsuReplayImportDialog(RitsuReplayImporter importer, IReadOnlyList<RitsuProfile> profiles, Guid initialProfile)
        {
            this.importer = importer;
            HeaderText = "Import replay into a local profile";
            BodyText = "Choose an .osr file or a score from the linked library and a destination profile. Original scores and files are kept. Some map-transforming mods may replay differently.";
            var choices = profiles.Select(item => new ProfileChoice(item.Id, item.Name)).ToArray();
            profile = new OsuDropdown<ProfileChoice>
            {
                RelativeSizeAxes = Axes.X,
                Items = choices,
                Current = { Value = choices.FirstOrDefault(item => item.Id == initialProfile) ?? choices[0] },
            };
            source = new OsuDropdown<ReplayChoice>
            {
                RelativeSizeAxes = Axes.X,
                Items = new[] { new ReplayChoice(null, false, "Choose an .osr file") },
            };
            source.Current.Value = source.Items.First();
            file = new FormFileSelector(".osr")
            {
                RelativeSizeAxes = Axes.X,
                Caption = "Replay file",
                PlaceholderText = "Select an .osr replay",
            };
            MainContent.Add(new PopoverContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[] { profile, source, file },
                },
            });
            importButton = new PopupDialogOkButton { Text = "Import", HideDialogOnAction = false, Action = import };
            cancelButton = new PopupDialogCancelButton { Text = "Close" };
            Buttons = new PopupDialogButton[] { importButton, cancelButton };
            Task.Run(importer.GetSharedReplays).ContinueWith(task => Schedule(() =>
            {
                if (IsDisposed) return;
                if (task.IsFaulted)
                {
                    Logger.Error(task.Exception!.GetBaseException(), "Could not list MOsu scores in the linked library.");
                    return;
                }
                sharedReplays = task.GetResultSafely();
                var selected = source.Current.Value;
                source.Items = new[] { new ReplayChoice(null, false, "Choose an .osr file") }
                    .Concat(sharedReplays.Count > 0 ? new[] { new ReplayChoice(null, true, $"Import all {sharedReplays.Count} MOsu scores from the linked library") } : Array.Empty<ReplayChoice>())
                    .Concat(sharedReplays.Select(item => new ReplayChoice(item.Id, false, item.ToString()))).ToArray();
                source.Current.Value = source.Items.FirstOrDefault(item => item.Id == selected.Id && item.All == selected.All) ?? source.Items.First();
            }), TaskScheduler.Default);
        }

        private void import()
        {
            var selectedFile = file.Current.Value;
            var selectedSource = source.Current.Value;
            if (!selectedSource.All && !selectedSource.Id.HasValue && selectedFile == null)
            {
                BodyText = "Choose an .osr file first.";
                return;
            }

            Guid profileId = profile.Current.Value.Id;
            importButton.Enabled.Value = false;
            cancelButton.Enabled.Value = false;
            BodyText = "Importing replay...";
            Task.Run(async () =>
            {
                if (selectedSource.All)
                {
                    int imported = 0;
                    int already = 0;
                    var failures = new List<string>();
                    int warnings = 0;
                    foreach (var replay in sharedReplays)
                    {
                        try
                        {
                            var result = await importer.ImportSharedAsync(replay.Id, profileId).ConfigureAwait(false);
                            if (result.AlreadyAssociated) already++; else imported++;
                            if (result.Warning != null) warnings++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"Could not import MOsu score {replay.Id}.");
                            failures.Add($"{replay.Title}: {ex.Message}");
                        }
                    }
                    string summary = $"Imported {imported}; already assigned {already}; failed {failures.Count}.";
                    if (warnings > 0) summary += $" {warnings} replay(s) have compatibility or PP warnings; check them in game.";
                    if (failures.Count > 0) summary += " " + string.Join("; ", failures.Take(3)) + (failures.Count > 3 ? ". See the local runtime log for the remaining failures." : string.Empty);
                    return summary;
                }

                var importedReplay = selectedSource.Id.HasValue
                    ? await importer.ImportSharedAsync(selectedSource.Id.Value, profileId).ConfigureAwait(false)
                    : await importer.ImportAsync(selectedFile!.FullName, profileId).ConfigureAwait(false);
                string message = importedReplay.AlreadyAssociated ? "This replay is already assigned to this profile." : "Replay imported into the selected profile.";
                return importedReplay.Warning == null ? message : $"{message} {importedReplay.Warning}";
            }).ContinueWith(task => Schedule(() =>
            {
                if (IsDisposed) return;
                importButton.Enabled.Value = true;
                cancelButton.Enabled.Value = true;
                if (task.IsFaulted || task.IsCanceled)
                {
                    var error = task.Exception?.GetBaseException();
                    if (error != null) Logger.Error(error, "Could not import local replay.");
                    BodyText = error is LegacyScoreDecoder.BeatmapNotFoundException missing
                        ? $"The replay's beatmap is not available locally (hash: {missing.Hash})."
                        : $"Import failed: {error?.Message ?? "Operation cancelled."}";
                    return;
                }

                BodyText = task.GetResultSafely();
            }), TaskScheduler.Default);
        }

        private sealed record ReplayChoice(Guid? Id, bool All, string Label)
        {
            public override string ToString() => Label;
        }

        private sealed record ProfileChoice(Guid Id, string Name)
        {
            public override string ToString() => Name;
        }
    }
}
