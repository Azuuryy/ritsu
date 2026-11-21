using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class PerfectPerformanceSettings : SettingsSubsection
    {
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
            var toggle = new SettingsButtonV2 { Action = warmup.ToggleFull };
            toggle.Text = "Precalculate full Max PP grid";
            var progress = warmup.Status.GetBoundCopy();
            progress.BindValueChanged(change =>
            {
                toggle.Text = warmup.IsFullRunning ? "Stop Max PP precalculation" : "Precalculate full Max PP grid";
                note.Current.Value = string.IsNullOrEmpty(change.NewValue)
                    ? null
                    : new SettingsNote.Data(change.NewValue, SettingsNote.Type.Informational);
            }, true);
            Add(toggle);
            Add(note);
        }
    }
}
