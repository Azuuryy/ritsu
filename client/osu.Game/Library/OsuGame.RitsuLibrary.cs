// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Library;
using osu.Game.Offline;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens;
using osu.Game.Screens.Select;

namespace osu.Game
{
    public partial class OsuGame
    {
        private readonly RitsuBeatmapQueue ritsuQueue = new RitsuBeatmapQueue();

        internal void QueueRitsuBeatmap(BeatmapInfo beatmap) => ritsuQueue.Add(beatmap);

        internal RitsuBeatmapQueue RitsuQueue => ritsuQueue;

        internal BeatmapInfo? ResolveRitsuQueueBeatmap(string hash)
        {
            var map = BeatmapManager.QueryBeatmap(b => b.MD5Hash == hash && !b.Hidden);
            return RitsuBeatmapQueue.IsAvailable(map) ? map : null;
        }

        internal WorkingBeatmap GetRitsuQueueWorkingBeatmap(BeatmapInfo map) => BeatmapManager.GetWorkingBeatmap(map);

        internal void SelectRitsuQueueItem(SongSelect select, string hash)
        {
            var map = ResolveRitsuQueueBeatmap(hash);
            if (map != null)
                select.SelectQueuedBeatmap(map);
            else
                postRitsuNotice("This queued difficulty is unavailable.");

            ritsuQueue.Remove(hash);
        }

        internal void SelectNextRitsuQueueItem(SongSelect select)
        {
            if (!ritsuQueue.SelectNext(ResolveRitsuQueueBeatmap, select.SelectQueuedBeatmap))
                postRitsuNotice("No queued difficulties are available in this library.");
        }

        private void nextRitsuQueueItem()
        {
            withRitsuSongSelect(SelectNextRitsuQueueItem);
        }

        internal void ShowRitsuMemo(BeatmapInfo beatmap, ScoreInfo? result = null)
        {
            if (dialogOverlay == null || string.IsNullOrEmpty(beatmap.MD5Hash))
                return;

            var library = Dependencies.Get<RitsuLibrary>();
            var profiles = Dependencies.Get<RitsuProfileManager>();
            Guid profileId = profiles.ActiveProfileId;
            var marked = profiles.GetScores(profileId, Ruleset.Value.ShortName).Where(s => s.BeatmapHash == beatmap.Hash);
            if (result?.BeatmapHash == beatmap.Hash && ScoreManager.Query(s => s.ID == result.ID && !s.DeletePending) is { } localScore)
                marked = marked.Append(localScore);

            var candidates = marked.DistinctBy(s => s.ID).OrderByDescending(s => s.Date).ToArray();
            dialogOverlay.Push(new RitsuMemoDialog(beatmap.DifficultyName, library.GetMemo(profileId, beatmap.MD5Hash), candidates,
                replayId =>
                {
                    try
                    {
                        var score = ScoreManager.Query(s => s.ID == replayId && !s.DeletePending && s.BeatmapHash == beatmap.Hash);
                        return score == null ? null : ScoreManager.GetScore(score)?.Replay?.Frames.LastOrDefault()?.Time;
                    }
                    catch (Exception e) when (e is IOException or InvalidOperationException or LegacyScoreDecoder.BeatmapNotFoundException)
                    {
                        return null;
                    }
                },
                memo => library.SaveMemo(profileId, beatmap.MD5Hash, memo),
                (replayId, time) =>
                {
                    var score = ScoreManager.Query(s => s.ID == replayId && !s.DeletePending && s.BeatmapHash == beatmap.Hash);
                    if (score != null)
                        presentScore(score, ScorePresentType.Gameplay, true, time);
                    else
                        postRitsuNotice("This replay is no longer available.");
                }));
        }

    }
}
