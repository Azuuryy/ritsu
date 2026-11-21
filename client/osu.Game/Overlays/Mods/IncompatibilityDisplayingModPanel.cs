// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Overlays.Mods
{
    public partial class IncompatibilityDisplayingModPanel : ModPanel, IHasCustomTooltip<Mod>
    {
        private readonly BindableBool incompatible = new BindableBool();

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        public IncompatibilityDisplayingModPanel(ModState modState)
            : base(modState)
        {
        }

        public IncompatibilityDisplayingModPanel(Mod mod)
            : base(mod)
        {
        }

        protected override void LoadComplete()
        {
            selectedMods.BindValueChanged(_ => UpdateIncompatibility(), true);
            incompatible.BindValueChanged(_ => Scheduler.AddOnce(UpdateState));
            // base call will run `UpdateState()` first time and finish transforms.
            base.LoadComplete();
        }

        protected void UpdateIncompatibility()
        {
            conflicting = IsIncompatibleWithSelected(selectedMods.Value);

            // osu!local: mods the active local profile's rules don't allow are shown like incompatible ones (and can't be selected).
            disallowedByProfile = !IgnoreLocalProfileRules && localProfiles?.IsModAllowed(Mod) == false && !Active.Value;

            incompatible.Value = conflicting || disallowedByProfile;
        }

        [Resolved(canBeNull: true)]
        private osu.Game.LocalPriority.LocalProfileManager? localProfiles { get; set; }

        private bool conflicting;
        private bool disallowedByProfile;

        /// <summary>
        /// osu!local: conflicting mods can't be clicked at all (instead of replacing the conflicting selection).
        /// </summary>
        protected bool BlockConflictingSelection { get; set; }

        /// <summary>
        /// osu!local: don't apply the active profile's mod rules (used while choosing a profile's mod rules).
        /// </summary>
        protected bool IgnoreLocalProfileRules { get; set; }

        protected override void Select()
        {
            if (disallowedByProfile || (BlockConflictingSelection && conflicting))
                return;

            base.Select();
        }

        protected virtual bool IsIncompatibleWithSelected(IReadOnlyList<Mod> selectedMods) =>
            selectedMods.Count > 0
            && selectedMods.All(selected => selected.GetType() != Mod.GetType())
            && !ModUtils.CheckCompatibleSet(selectedMods.Append(Mod));

        protected override Colour4 BackgroundColour => incompatible.Value ? ColourProvider.Background6 : base.BackgroundColour;
        protected override Colour4 ForegroundColour => incompatible.Value ? ColourProvider.Background5 : base.ForegroundColour;

        protected override void UpdateState()
        {
            base.UpdateState();
            SwitchContainer.FadeColour(incompatible.Value ? Colour4.Gray : Colour4.White, TRANSITION_DURATION, Easing.OutQuint);
        }

        #region IHasCustomTooltip

        public ITooltip<Mod> GetCustomTooltip() => new IncompatibilityDisplayingTooltip(overlayColourProvider);

        public Mod TooltipContent => Mod;

        #endregion
    }
}
