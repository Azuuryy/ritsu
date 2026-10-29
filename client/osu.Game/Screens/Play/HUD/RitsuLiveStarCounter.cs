// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.HUD
{
    public partial class RitsuLiveStarCounter : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayState? gameplay { get; set; }

        [Resolved(canBeNull: true)]
        private ScoreProcessor? scoreProcessor { get; set; }

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly OsuSpriteText text;
        private List<TimedDifficultyAttributes>? attributes;
        private JudgementResult? lastJudgement;

        public RitsuLiveStarCounter()
        {
            AutoSizeAxes = Axes.Both;
            InternalChild = text = new OsuSpriteText { Text = "★ --", Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold) };
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache cache)
        {
            // The skin editor can instantiate this component outside a gameplay session.
            if (gameplay == null)
                return;
            var workingBeatmap = new GameplayWorkingBeatmap(gameplay.Beatmap);
            var mods = gameplay.Mods.Select(m => m.DeepClone()).ToArray();
            cache.GetTimedDifficultyAttributesAsync(workingBeatmap, gameplay.Ruleset, mods, cancellation.Token)
                 .ContinueWith(task => Schedule(() =>
                 {
                     attributes = task.GetResultSafely();
                     if (lastJudgement != null) updateJudgement(lastJudgement);
                 }), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement += updateJudgement;
                scoreProcessor.JudgementReverted += updateJudgement;
            }
            if (gameplay?.LastJudgementResult.Value != null)
                updateJudgement(gameplay.LastJudgementResult.Value);
        }

        private void updateJudgement(JudgementResult judgement)
        {
            lastJudgement = judgement;
            if (attributes == null || attributes.Count == 0) return;
            int index = attributes.BinarySearch(new TimedDifficultyAttributes(judgement.HitObject.GetEndTime(), null!));
            if (index < 0) index = ~index - 1;
            double stars = attributes[Math.Clamp(index, 0, attributes.Count - 1)].Attributes.StarRating;
            text.Text = $"★ {stars:0.00}";
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (scoreProcessor != null)
                {
                    scoreProcessor.NewJudgement -= updateJudgement;
                    scoreProcessor.JudgementReverted -= updateJudgement;
                }
                cancellation.Cancel();
                cancellation.Dispose();
            }
            base.Dispose(isDisposing);
        }

        private class GameplayWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap beatmap;
            public GameplayWorkingBeatmap(IBeatmap beatmap) : base(beatmap.BeatmapInfo, null) => this.beatmap = beatmap;
            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) => beatmap;
            protected override IBeatmap GetBeatmap() => beatmap;
            public override Texture GetBackground() => throw new NotImplementedException();
            protected override Track GetBeatmapTrack() => throw new NotImplementedException();
            protected internal override ISkin GetSkin() => throw new NotImplementedException();
            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
