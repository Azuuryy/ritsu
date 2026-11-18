// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Commands
{
    internal partial class RitsuMapCalculationDialog : PopupDialog
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly WorkingBeatmap working;
        private readonly RulesetInfo mode;
        private readonly Mod[] mods;
        private readonly OsuSpriteText result;
        public RitsuMapCalculationDialog(WorkingBeatmap working, RulesetInfo mode, Mod[] mods)
        {
            this.working = working;
            this.mode = mode;
            this.mods = mods;
            HeaderText = "Map attributes";
            BodyText = $"{working.BeatmapInfo.Metadata.Title} [{working.BeatmapInfo.DifficultyName}] · {(mods.Length == 0 ? "NM" : string.Join(", ", mods.Select(m => m.Acronym)))}";
            var content = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 6) };
            MainContent.Add(content);
            content.Add(result = new OsuSpriteText { Text = "Calculating map attributes…" });
            Buttons = new[] { new PopupDialogCancelButton { Text = "Close" } };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            _ = prepare(cancellation.Token);
        }

        private async Task prepare(CancellationToken token)
        {
            try
            {
                string text = await Task.Run(() =>
                {
                    using var simulation = new RitsuSimulation(working, mode, mods, token);
                    return $"{simulation.Summary} · {simulation.PerfectPP:0.##} perfect PP";
                }, token).ConfigureAwait(false);

                if (!token.IsCancellationRequested)
                    Schedule(() => result.Text = text);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not calculate local beatmap attributes.");
                if (!token.IsCancellationRequested)
                    Schedule(() => result.Text = "Map attributes could not be calculated.");
            }
        }

        protected override void PopOut()
        {
            cancellation.Cancel();
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                cancellation.Dispose();
            base.Dispose(isDisposing);
        }

    }
}
