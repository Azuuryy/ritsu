using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class PerfectPerformanceSettings : SettingsSubsection
    {
        private readonly Bindable<PerfectPerformanceWarmup.RunState> state = new Bindable<PerfectPerformanceWarmup.RunState>();
        private readonly Bindable<string> progress = new Bindable<string>();

        protected override LocalisableString Header => "Max PP";

        [BackgroundDependencyLoader(true)]
        private void load(PerfectPerformanceWarmup? warmup)
        {
            if (warmup == null)
            {
                Hide();
                return;
            }

            var note = new SettingsNote();
            var toggle = new SettingsButtonV2
            {
                Action = warmup.StopOrStartFull,
                Keywords = new[] { "Precalculate full Max PP grid", "Stop Max PP precalculation", "Resume Max PP precalculation" },
            };
            state.BindTarget = warmup.State;
            state.BindValueChanged(change =>
            {
                toggle.Text = change.NewValue switch
                {
                    PerfectPerformanceWarmup.RunState.Running => "Stop Max PP precalculation",
                    PerfectPerformanceWarmup.RunState.Stopping => "Stopping Max PP precalculation…",
                    PerfectPerformanceWarmup.RunState.Paused => "Resume Max PP precalculation",
                    _ => "Precalculate full Max PP grid",
                };
                toggle.Enabled.Value = change.NewValue != PerfectPerformanceWarmup.RunState.Stopping;
            }, true);

            progress.BindTarget = warmup.Status;
            progress.BindValueChanged(change =>
            {
                note.Current.Value = string.IsNullOrEmpty(change.NewValue)
                    ? null
                    : new SettingsNote.Data(change.NewValue, SettingsNote.Type.Informational);
            }, true);
            Add(toggle);
            Add(note);
        }
    }
}
