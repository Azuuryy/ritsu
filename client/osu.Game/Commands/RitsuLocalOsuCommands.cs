// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Commands;
using osu.Game.Graphics.UserInterface;
using osu.Game.Offline;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Select;

namespace osu.Game
{
    public partial class OsuGame
    {
        /// <summary>
        /// Whether the keyboard shortcut can open the score simulation right now: a difficulty is selected, overlays are allowed
        /// and the current screen is neither gameplay, the editor nor the simulation itself.
        /// </summary>
        internal bool CanShowRitsuSimulation => RitsuOffline.Enabled
                                               && !Beatmap.IsDefault
                                               && OverlayActivationMode.Value == OverlayActivation.All
                                               && ScreenStack?.CurrentScreen is { } screen
                                               && screen is not (Editor or RitsuSimulationScreen);

        internal void ShowRitsuSimulation()
        {
            if (CanShowRitsuSimulation)
                ShowRitsuMap(true);
        }

        internal void ShowRitsuMap(bool simulate, BeatmapInfo? difficulty = null)
        {
            if ((difficulty == null && Beatmap.IsDefault) || dialogOverlay == null)
                return;

            if (simulate && ScreenStack.CurrentScreen is RitsuSimulationScreen)
                return;

            var working = difficulty == null ? Beatmap.Value : BeatmapManager.GetWorkingBeatmap(difficulty);
            if (simulate)
            {
                if (ScreenStack.CurrentScreen is SoloSongSelect select)
                    select.Push(new RitsuSimulationScreen(working));
                else
                    withRitsuSongSelect(songSelect => songSelect.Push(new RitsuSimulationScreen(working)));
                return;
            }

            var mode = Ruleset.Value;
            if (working.BeatmapInfo.Ruleset.OnlineID != 0 && !working.BeatmapInfo.Ruleset.Equals(mode))
                mode = working.BeatmapInfo.Ruleset;

            var mods = mode.Equals(Ruleset.Value) ? SelectedMods.Value.Select(m => m.DeepClone()).ToArray() : Array.Empty<Mod>();
            dialogOverlay.Push(new RitsuMapCalculationDialog(working, mode, mods));
        }

        private void showRitsuProfile(bool top)
        {
            // osu!local: the active profile's statistics and best scores are on its profile page.
            if (Dependencies.Get<LocalPriority.LocalProfileManager>()?.IsLocalProfileActive == true && localProfileOverlay != null)
            {
                waitForReady(() => localProfileOverlay, overlay => overlay.Show());
                return;
            }

            var profiles = Dependencies.Get<RitsuProfileManager>();
            var profile = profiles.GetActiveProfile();
            var statistics = profiles.GetStatistics(profile.Id, Ruleset.Value.ShortName);
            if (top && statistics.BestScores.Count > 0)
            {
                var scores = statistics.BestScores.Take(50).ToArray();
                dialogOverlay.Push(new RitsuTopScoresDialog(scores, score =>
                {
                    if (profiles.ActiveProfileId == profile.Id && ScoreManager.Query(s => s.ID == score.ID && !s.DeletePending) != null)
                        PresentScoreFromLocalProfile(score);
                }));
                return;
            }

            postRitsuNotice(top ? $"{profile.Name} has no best scores in this mode."
                : $"{profile.Name} · {Ruleset.Value.ShortName} · {statistics.PlayCount:N0} plays · {statistics.PP:0.##} PP · {statistics.DisplayAccuracy}");
        }
    }
}

namespace osu.Game.Commands
{
    internal partial class RitsuTopScoresDialog : PopupDialog
    {
        public RitsuTopScoresDialog(ScoreInfo[] scores, Action<ScoreInfo> open)
        {
            HeaderText = "Best scores · active profile";
            var choices = scores.Select(score => new ScoreChoice(score)).ToArray();
            var dropdown = new OsuDropdown<ScoreChoice> { RelativeSizeAxes = Axes.X, Items = choices, Current = { Value = choices[0] } };
            MainContent.Add(dropdown);
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "Open result", Action = () => open(dropdown.Current.Value.Score) },
                new PopupDialogCancelButton { Text = "Close" },
            };
        }

        private sealed record ScoreChoice(ScoreInfo Score)
        {
            public override string ToString() => $"{Score.BeatmapInfo?.Metadata.Title ?? "Unknown map"} [{Score.BeatmapInfo?.DifficultyName}] · {Score.PP:0.##} PP";
        }
    }

}
