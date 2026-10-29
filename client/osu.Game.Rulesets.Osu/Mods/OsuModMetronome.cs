// Adapted from Modosu (Azuuryy), derived from MOsu (p-720), GPL-3.0.
// See LICENCE-Modosu.txt in the distribution.
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModMetronome : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IModosuMod
    {
        public override bool Ranked => false;
        public override bool AlwaysValidForSubmission => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;

        public override string Name => "Metronome";

        public override LocalisableString Description =>
            "Plays metronome ticks at a configurable rate relative to the beatmap BPM, with a higher-pitched accent on the downbeat.";

        public override string Acronym => "ME";

        public override ModType Type => ModType.Fun;

        public override IconUsage? Icon => OsuIcon.Metronome;

        [SettingSource(
            "Tempo scale",
            "Choose the metronome rate relative to the beatmap BPM.",
            SettingControlType = typeof(MetronomeScaleSetting))]
        public BindableInt TempoScale { get; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Default = 4,
        };

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            double firstHitTime = drawableRuleset.Beatmap.HitObjects.FirstOrDefault()?.StartTime ?? 0;
            drawableRuleset.Overlays.Add(new DividedMetronomeBeat(firstHitTime, TempoScale.Value));
        }

        public partial class MetronomeScaleSetting : SettingsDropdown<int>
        {
            public MetronomeScaleSetting() => Items = new[] { 1, 2, 4, 8, 12 };

            protected override OsuDropdown<int> CreateDropdown() => new MetronomeScaleDropdown();
        }

        public partial class MetronomeScaleDropdown : OsuDropdown<int>
        {
            public MetronomeScaleDropdown() => RelativeSizeAxes = Axes.X;

            protected override LocalisableString GenerateItemText(int item) => $"{item / 4.0:0.##}x";
        }

        private sealed partial class DividedMetronomeBeat : MetronomeBeat
        {
            private readonly int beatsPerTick;

            public DividedMetronomeBeat(double firstHitTime, int tempoScale)
                : base(firstHitTime)
            {
                // Retain quarter-beat units for existing scores and presets.
                // Use the fewest subdivisions possible, so no unnecessary beats are skipped.
                int commonFactor = greatestCommonDivisor(tempoScale, 4);
                Divisor = tempoScale / commonFactor;
                beatsPerTick = 4 / commonFactor;
                MinimumBeatLength = 0;
            }

            private static int greatestCommonDivisor(int a, int b)
            {
                while (b != 0)
                    (a, b) = (b, a % b);
                return a;
            }

            protected override void OnNewBeat(
                int beatIndex,
                TimingControlPoint timingPoint,
                EffectControlPoint effectPoint,
                ChannelAmplitudes amplitudes)
            {
                if (beatIndex % beatsPerTick != 0)
                    return;

                if (Divisor == 1)
                {
                    base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);
                    return;
                }

                // BeatSyncedContainer counts each subdivision as a beat. Scale the time signature
                // so accents still correspond to beatmap downbeats.
                var scaledTimingPoint = new TimingControlPoint();
                scaledTimingPoint.CopyFrom(timingPoint);
                scaledTimingPoint.TimeSignature =
                    new TimeSignature(timingPoint.TimeSignature.Numerator * Divisor);
                scaledTimingPoint.BeatLength = timingPoint.BeatLength / Divisor;

                base.OnNewBeat(beatIndex, scaledTimingPoint, effectPoint, amplitudes);
            }
        }
    }
}
