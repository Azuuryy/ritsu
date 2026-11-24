// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Failed and quit plays of local profiles, shown in the profile's Recent Plays like the website shows them (rank F, no PP, not clickable).
    /// They are never saved as scores: they are kept in osu-local/unfinished-plays.json and forgotten after <see cref="KEEP_FOR"/>.
    /// </summary>
    public class LocalUnfinishedPlays
    {
        public static readonly TimeSpan KEEP_FOR = TimeSpan.FromHours(24);

        private const string file = LocalProfileStore.DIRECTORY + @"/unfinished-plays.json";

        private readonly Storage storage;
        private readonly object sync = new object();

        private List<LocalUnfinishedPlay> plays;

        public LocalUnfinishedPlays(Storage storage)
        {
            this.storage = storage;
            plays = read() ?? new List<LocalUnfinishedPlay>();

            if (prune())
                write();
        }

        public void Add(LocalUnfinishedPlay play)
        {
            lock (sync)
            {
                prune();
                plays.Add(play);
                write();
            }
        }

        /// <summary>
        /// A profile's unfinished plays in one ruleset from the last <see cref="KEEP_FOR"/>, newest first.
        /// </summary>
        public IReadOnlyList<LocalUnfinishedPlay> Get(int userId, string ruleset)
        {
            lock (sync)
            {
                if (prune())
                    write();

                return plays.Where(p => p.UserId == userId && p.Ruleset == ruleset).OrderByDescending(p => p.Date).ToArray();
            }
        }

        /// <summary>
        /// Forgets a profile's unfinished plays (the profile was deleted).
        /// </summary>
        public void Remove(int userId)
        {
            lock (sync)
            {
                if (plays.RemoveAll(p => p.UserId == userId) > 0)
                    write();
            }
        }

        private bool prune()
        {
            var cutoff = DateTimeOffset.Now - KEEP_FOR;
            return plays.RemoveAll(p => p.Date < cutoff) > 0;
        }

        private List<LocalUnfinishedPlay>? read()
        {
            try
            {
                if (!storage.Exists(file))
                    return null;

                using (var stream = storage.GetStream(file, FileAccess.Read, FileMode.Open))
                using (var reader = new StreamReader(stream))
                    return JsonConvert.DeserializeObject<List<LocalUnfinishedPlay>>(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                // Only the last day of failed / quit plays: nothing worth keeping a broken copy of.
                Logger.Error(e, $@"Ritsu: could not read {file}; starting without unfinished plays.");
                return null;
            }
        }

        private void write()
        {
            try
            {
                using (var stream = storage.CreateFileSafely(file))
                using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(plays, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Ritsu: could not save {file}.");
            }
        }
    }

    /// <summary>
    /// One failed or quit play: just what a Recent Plays row shows.
    /// </summary>
    public class LocalUnfinishedPlay
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int UserId { get; set; }
        public string Ruleset { get; set; } = string.Empty;
        public string BeatmapMD5 { get; set; } = string.Empty;
        public List<APIMod> Mods { get; set; } = new List<APIMod>();
        public Dictionary<HitResult, int> Statistics { get; set; } = new Dictionary<HitResult, int>();
        public long TotalScore { get; set; }
        public double Accuracy { get; set; }
        public int MaxCombo { get; set; }
        public DateTimeOffset Date { get; set; }

        public static LocalUnfinishedPlay From(ScoreInfo score, string beatmapMD5) => new LocalUnfinishedPlay
        {
            UserId = score.UserID,
            Ruleset = score.Ruleset.ShortName,
            BeatmapMD5 = beatmapMD5,
            Mods = score.Mods.Select(m => new APIMod(m)).ToList(),
            // Only the judgements that happened (the row's hover lists hits and misses, nothing else).
            Statistics = score.Statistics.Where(s => s.Value > 0).ToDictionary(s => s.Key, s => s.Value),
            TotalScore = score.TotalScore,
            Accuracy = score.Accuracy,
            MaxCombo = score.MaxCombo,
            Date = DateTimeOffset.Now,
        };

        /// <summary>
        /// A score (not in the database) for the Recent Plays row, with its beatmap looked up by MD5 hash.
        /// </summary>
        public ScoreInfo ToScore(Realms.Realm realm)
        {
            var ruleset = realm.Find<RulesetInfo>(Ruleset)?.Detach() ?? new RulesetInfo { ShortName = Ruleset };
            var beatmap = string.IsNullOrEmpty(BeatmapMD5) ? null : realm.All<BeatmapInfo>().FirstOrDefault(b => b.MD5Hash == BeatmapMD5)?.Detach();

            var score = new ScoreInfo(beatmap, ruleset)
            {
                Rank = ScoreRank.F,
                Passed = false,
                TotalScore = TotalScore,
                Accuracy = Accuracy,
                MaxCombo = MaxCombo,
                Date = Date,
                Statistics = new Dictionary<HitResult, int>(Statistics),
                BeatmapHash = beatmap?.Hash ?? string.Empty,
            };

            score.APIMods = Mods.ToArray();

            // A deleted beatmap shows as "(beatmap not found)".
            if (beatmap == null)
                score.BeatmapInfo = null;

            return score;
        }
    }
}
