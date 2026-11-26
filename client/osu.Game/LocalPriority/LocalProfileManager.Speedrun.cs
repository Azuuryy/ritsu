// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Diagnostics;
using osu.Framework.Threading;
using osu.Game.Rulesets;

namespace osu.Game.LocalPriority
{
    public partial class LocalProfileManager
    {
        /// <summary>
        /// A running Speedrun profile's time is saved this often (and on switching away or closing the game), so a crash loses little.
        /// </summary>
        private const double speedrun_save_interval = 60_000;

        /// <summary>
        /// The Speedrun profile whose time is running (the active one, until its goal is reached), or null.
        /// </summary>
        private Guid? speedrunProfileId;

        private double speedrunSecondsBefore;
        private readonly Stopwatch speedrunStopwatch = new Stopwatch();
        private ScheduledDelegate? speedrunSaveLoop;

        /// <summary>
        /// A Speedrun profile reached its goal: its id and final time in seconds. Raised on the update thread.
        /// </summary>
        public event Action<Guid, double>? SpeedrunCompleted;

        public LocalProfile CreateSpeedrunProfile(LocalSpeedrun goal) => Store.CreateSpeedrunProfile(goal);

        public int GetSpeedrunProfileCount() => Store.GetSpeedrunProfileCount();

        /// <summary>
        /// Whether this profile's speedrun timer is running right now (cheap; the profile page asks every frame).
        /// </summary>
        public bool IsSpeedrunRunning(Guid id) => speedrunProfileId == id;

        /// <summary>
        /// The running speedrun's time so far, in seconds (only for the profile <see cref="IsSpeedrunRunning"/> is true for).
        /// </summary>
        public double RunningSpeedrunSeconds => speedrunSecondsBefore + speedrunStopwatch.Elapsed.TotalSeconds;

        /// <summary>
        /// Starts or stops the speedrun timer to follow the active profile. Time counts only while a Speedrun profile is active.
        /// </summary>
        private void updateSpeedrunTimer()
        {
            var profile = activeProfile.Value;
            Guid? id = profile?.Speedrun != null && !profile.Died ? profile.Id : null;

            if (id == speedrunProfileId)
                return;

            stopSpeedrunTimer();

            if (id is not Guid newId)
                return;

            var run = Store.GetSpeedrun(newId);

            if (run == null || run.Completed != null)
                return;

            speedrunProfileId = newId;
            speedrunSecondsBefore = run.Seconds;
            speedrunStopwatch.Restart();
            speedrunSaveLoop = Scheduler.AddDelayed(saveSpeedrunTime, speedrun_save_interval, true);
        }

        private void saveSpeedrunTime()
        {
            if (speedrunProfileId is Guid id)
                Store.SetSpeedrunTime(id, RunningSpeedrunSeconds, false);
        }

        private void stopSpeedrunTimer()
        {
            saveSpeedrunTime();
            resetSpeedrunTimer();
        }

        private void resetSpeedrunTimer()
        {
            speedrunSaveLoop?.Cancel();
            speedrunSaveLoop = null;
            speedrunStopwatch.Reset();
            speedrunProfileId = null;
        }

        /// <summary>
        /// A profile's PP total for a ruleset is known (after a play, a deleted score, or from the profile page): when it is the running
        /// Speedrun profile and its goal is reached, the run ends: the timer stops and a notification shows the time. Any thread.
        /// </summary>
        public void CheckSpeedrunGoal(Guid profileId, RulesetInfo ruleset, double pp)
        {
            string rulesetName = ruleset.ShortName;

            Schedule(() =>
            {
                if (speedrunProfileId != profileId)
                    return;

                var run = Store.GetSpeedrun(profileId);

                if (run == null || run.Completed != null || run.Ruleset != rulesetName || !run.IsReachedBy(pp))
                    return;

                double seconds = Math.Round(RunningSpeedrunSeconds, 2);

                resetSpeedrunTimer();
                Store.SetSpeedrunTime(profileId, seconds, true);
                SpeedrunCompleted?.Invoke(profileId, seconds);

                PostNotification?.Invoke(new LocalSpeedrunCompletedNotification(seconds));
            });
        }
    }
}
