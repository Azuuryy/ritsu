// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges.Events;
using osu.Framework.Lists;
using osu.Framework.Localisation;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu
{
    public partial class OsuInputManager : RulesetInputManager<OsuAction>
    {
        public SlimReadOnlyListWrapper<OsuAction> PressedActions => KeyBindingContainer.PressedActions;

        /// <summary>
        /// Whether gameplay input buttons should be allowed.
        /// Defaults to <c>true</c>, generally used for mods like Relax which turn off main inputs.
        /// </summary>
        /// <remarks>
        /// Of note, auxiliary inputs like the "smoke" key are left usable.
        /// </remarks>
        public bool AllowGameplayInputs
        {
            get => ((OsuKeyBindingContainer)KeyBindingContainer).AllowGameplayInputs;
            set => ((OsuKeyBindingContainer)KeyBindingContainer).AllowGameplayInputs = value;
        }

        /// <summary>
        /// Whether the user's cursor movement events should be accepted.
        /// Can be used to block only movement while still accepting button input.
        /// </summary>
        public bool AllowUserCursorMovement { get; set; } = true;

        protected override KeyBindingContainer<OsuAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new OsuKeyBindingContainer(ruleset, variant, unique);

        public bool CheckScreenSpaceActionPressJudgeable(Vector2 screenSpacePosition) =>
            // This is a very naive but simple approach.
            //
            // Based on user feedback of more nuanced scenarios (where touch doesn't behave as expected),
            // this can be expanded to a more complex implementation, but I'd still want to keep it as simple as we can.
            NonPositionalInputQueue.OfType<DrawableHitCircle.HitReceptor>().Any(c => c.CanBeHit() && c.ReceivePositionalInputAt(screenSpacePosition));

        public OsuInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.Unique)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new OsuTouchInputMapper(this) { RelativeSizeAxes = Axes.Both });
        }

        protected override bool Handle(UIEvent e)
        {
            if ((e is MouseMoveEvent || e is TouchMoveEvent) && !AllowUserCursorMovement) return false;

            return base.Handle(e);
        }

        internal bool TrackFlickMotion { get; set; }

        private readonly Queue<(double Time, Vector2 Position)> cursorHistory = new Queue<(double, Vector2)>();
        private (double Time, Vector2 Position)? lastCursorSample;

        protected override void HandleMousePositionChange(MousePositionChangeEvent e)
        {
            if (TrackFlickMotion)
                recordCursor(e.State.Mouse.Position);

            base.HandleMousePositionChange(e);
        }

        protected override void Update()
        {
            base.Update();
            if (TrackFlickMotion)
                recordCursor(CurrentState.Mouse.Position);
        }

        internal Vector2 GetFlickMovement(OsuPlayfield playfield)
        {
            recordCursor(CurrentState.Mouse.Position);
            if (cursorHistory.Count < 2)
                return Vector2.Zero;

            var first = cursorHistory.Peek();
            var second = cursorHistory.ElementAt(1);
            double cutoff = Time.Current - 40;
            Vector2 origin = first.Position;
            if (first.Time < cutoff && second.Time > cutoff)
                origin = Vector2.Lerp(first.Position, second.Position, (float)((cutoff - first.Time) / (second.Time - first.Time)));

            return playfield.ToLocalSpace(CurrentState.Mouse.Position) - playfield.ToLocalSpace(origin);
        }

        private void recordCursor(Vector2 position)
        {
            double now = Time.Current;
            if (lastCursorSample is { } previous)
            {
                if (now < previous.Time || now - previous.Time > 100)
                    cursorHistory.Clear();
                if (now == previous.Time && position == previous.Position)
                    return;
            }

            cursorHistory.Enqueue((now, position));
            lastCursorSample = (now, position);
            while (cursorHistory.Count > 256 || (cursorHistory.Count > 1 && cursorHistory.ElementAt(1).Time <= now - 40))
                cursorHistory.Dequeue();
        }

        private partial class OsuKeyBindingContainer : RulesetKeyBindingContainer
        {
            private bool allowGameplayInputs = true;

            /// <summary>
            /// Whether gameplay input buttons should be allowed.
            /// Defaults to <c>true</c>, generally used for mods like Relax which turn off main inputs.
            /// </summary>
            /// <remarks>
            /// Of note, auxiliary inputs like the "smoke" key are left usable.
            /// </remarks>
            public bool AllowGameplayInputs
            {
                get => allowGameplayInputs;
                set
                {
                    allowGameplayInputs = value;
                    ReloadMappings();
                }
            }

            public OsuKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
            }

            protected override void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
            {
                base.ReloadMappings(realmKeyBindings);

                if (!AllowGameplayInputs)
                    KeyBindings = KeyBindings.Where(static b => b.GetAction<OsuAction>() == OsuAction.Smoke).ToList();
            }
        }
    }

    public enum OsuAction
    {
        [LocalisableDescription(typeof(ActionStrings), nameof(ActionStrings.LeftButton))]
        LeftButton,

        [LocalisableDescription(typeof(ActionStrings), nameof(ActionStrings.RightButton))]
        RightButton,

        [LocalisableDescription(typeof(ActionStrings), nameof(ActionStrings.Smoke))]
        Smoke,

        [LocalisableDescription(typeof(OsuEditorStrings), nameof(OsuEditorStrings.HitCircleTool))]
        EditorHitCircleTool = 10000,

        [LocalisableDescription(typeof(OsuEditorStrings), nameof(OsuEditorStrings.SliderTool))]
        EditorSliderTool,

        [LocalisableDescription(typeof(OsuEditorStrings), nameof(OsuEditorStrings.SpinnerTool))]
        EditorSpinnerTool,

        [LocalisableDescription(typeof(OsuEditorStrings), nameof(OsuEditorStrings.GridFromPointsTool))]
        EditorGridFromPointsTool,

        [LocalisableDescription(typeof(OsuEditorStrings), nameof(OsuEditorStrings.ToggleGridSnap))]
        EditorToggleGridSnap,

        [LocalisableDescription(typeof(EditorStrings), nameof(EditorStrings.ToggleDistanceSnap))]
        EditorToggleDistanceSnap,
    }
}
