// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Opens the score simulation for the selected difficulty. Same accent as the results Screenshot button.
    /// </summary>
    public partial class FooterButtonSimulate : ScreenFooterButton
    {
        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            Text = "Simulate";
            Icon = FontAwesome.Solid.ChartLine;
            AccentColour = colour.Pink1;
            TooltipText = "Estimate PP for the selected difficulty (F4)";
            Action = () => game?.ShowRitsuSimulation();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            workingBeatmap.BindValueChanged(b => Enabled.Value = Offline.RitsuOffline.Enabled && b.NewValue is not DummyWorkingBeatmap, true);
        }
    }
}
