// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Bindables;
using osu.Framework.Input.StateChanges;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Configuration;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModAutopilot : Mod, IUpdatableByPlayfield, IApplicableToDrawableRuleset<OsuHitObject>, IAffectsDifficulty
    {
        public override string Name => "Autopilot";
        public override string Acronym => "AP";
        public override IconUsage? Icon => OsuIcon.ModAutopilot;
        public override ModType Type => ModType.Automation;
        public override LocalisableString Description => @"Automatic cursor movement - just follow the rhythm.";

        [SettingSource("Linearisation", "Make cursor movement more linear.")]
        public BindableInt Linearisation { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        public override Type[] IncompatibleMods => new[]
        {
            typeof(OsuModSpunOut),
            typeof(ModRelax),
            typeof(ModAutoplay),
            typeof(OsuModMagnetised),
            typeof(OsuModRepel),
            typeof(ModTouchDevice)
        };

        private OsuInputManager inputManager = null!;

        private List<OsuReplayFrame> replayFrames = null!;

        private int currentFrame = -1;

        public void Update(Playfield playfield)
        {
            if (replayFrames.Count == 0)
                return;

            double time = playfield.Clock.CurrentTime;
            while (currentFrame + 1 < replayFrames.Count && replayFrames[currentFrame + 1].Time <= time)
                currentFrame++;

            Vector2 position;
            if (currentFrame < 0)
                position = replayFrames[0].Position;
            else if (currentFrame + 1 >= replayFrames.Count)
                position = replayFrames[currentFrame].Position;
            else
            {
                OsuReplayFrame previous = replayFrames[currentFrame];
                OsuReplayFrame next = replayFrames[currentFrame + 1];
                double duration = next.Time - previous.Time;
                float progress = duration <= 0 ? 1 : (float)Math.Clamp((time - previous.Time) / duration, 0, 1);
                position = Vector2.Lerp(previous.Position, next.Position, progress);
            }

            new MousePositionAbsoluteInput { Position = playfield.ToScreenSpace(position) }.Apply(inputManager.CurrentState, inputManager);

            // TODO: Implement the functionality to automatically spin spinners
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // Grab the input manager to disable the user's cursor, and for future use
            inputManager = ((DrawableOsuRuleset)drawableRuleset).KeyBindingInputManager;
            inputManager.AllowUserCursorMovement = false;

            // Generate the replay frames the cursor should follow
            replayFrames = new OsuAutoGenerator(drawableRuleset.Beatmap, drawableRuleset.Mods)
            {
                FrameRate = 500,
                Linearisation = Linearisation.Value,
            }.Generate().Frames.Cast<OsuReplayFrame>().ToList();
        }
    }
}
