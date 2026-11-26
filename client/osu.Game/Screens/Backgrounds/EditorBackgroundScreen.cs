// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Performance;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Backgrounds
{
    public partial class EditorBackgroundScreen : BackgroundScreen
    {
        private readonly Container dimContainer;

        private CancellationTokenSource? cancellationTokenSource;
        private Bindable<float> dimLevel = null!;
        private Bindable<bool> showStoryboard = null!;
        private Bindable<bool> showVideo = null!;
        private Bindable<float> blurLevel = null!;
        private Bindable<bool> lightenDuringBreaks = null!;
        private readonly EditorBeatmap editorBeatmap;
        private float? appliedDim;

        private readonly EditorClock clock;

        private BeatmapBackgroundWithStoryboard background = null!;

        private readonly Container content;
        private readonly Box blackBox;

        // We retrieve IBindable<WorkingBeatmap> from our dependency cache instead of passing WorkingBeatmap directly into EditorBackgroundScreen.
        // Otherwise, DummyWorkingBeatmap will be erroneously passed in whenever creating a new beatmap (since the Schedule() in the Editor that populates
        // a new WorkingBeatmap with correct values generally runs after EditorBackgroundScreen is created), which causes any background changes to not be displayed.
        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        public EditorBackgroundScreen(EditorBeatmap editorBeatmap, EditorClock clock)
        {
            this.editorBeatmap = editorBeatmap;
            this.clock = clock;
            InternalChild = dimContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // This adds overdraw but makes transitions not suck.
                    // There's probably a better way to do this, but it's high effort.
                    blackBox = new Box
                    {
                        Colour = Color4.Black,
                        RelativeSizeAxes = Axes.Both,
                    },
                    content = new EditorSkinProvidingContainer(editorBeatmap)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            dimLevel = config.GetBindable<float>(OsuSetting.EditorDim);
            showStoryboard = config.GetBindable<bool>(OsuSetting.EditorShowStoryboard);

            showVideo = config.GetBindable<bool>(OsuSetting.EditorShowVideo);
            blurLevel = config.GetBindable<float>(OsuSetting.EditorBackgroundBlur);
            lightenDuringBreaks = config.GetBindable<bool>(OsuSetting.EditorLightenDuringBreaks);
            content.Child = background = createContent();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            showStoryboard.BindValueChanged(_ => RefreshBackgroundAsync());
            showVideo.BindValueChanged(_ => RefreshBackgroundAsync());
            blurLevel.BindValueChanged(_ => updateBlur(), true);
        }

        protected override void Update()
        {
            base.Update();
            float target = dimLevel.Value;
            if (lightenDuringBreaks.Value)
            {
                double time = clock.CurrentTime;
                foreach (var period in editorBeatmap.Breaks)
                {
                    if (time < period.StartTime)
                        break;
                    if (time < period.EndTime)
                    {
                        target = Math.Max(target - UserDimContainer.BREAK_LIGHTEN_AMOUNT, 0);
                        break;
                    }
                }
            }
            if (appliedDim != target)
            {
                appliedDim = target;
                dimContainer.FadeColour(OsuColour.Gray(1 - target), 500, Easing.OutQuint);
            }
        }

        private void updateBlur()
        {
            background.BlurTo(new Vector2(PotatoMode.Active ? 0 : blurLevel.Value * BackgroundScreenBeatmap.USER_BLUR_FACTOR), 200, Easing.OutQuint);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            blackBox.LifetimeEnd = LatestTransformEndTime;
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            cancellationTokenSource?.Cancel();
            // The storyboard will do weird things with clock time changing on exit, so let's just hide it instead.
            background.UnloadStoryboard();

            return base.OnExiting(e);
        }

        public void RefreshBackgroundAsync()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            LoadComponentAsync(createContent(), d =>
            {
                background = d;
                content.Child = d;
                updateBlur();
            }, (cancellationTokenSource = new CancellationTokenSource()).Token);
        }

        private BeatmapBackgroundWithStoryboard createContent() => new BeatmapBackgroundWithStoryboard(beatmap.Value, includeStoryboard: showStoryboard.Value, includeVideo: showVideo.Value)
        {
            RelativeSizeAxes = Axes.Both,
        };

        public override bool Equals(BackgroundScreen? other)
        {
            if (other is not EditorBackgroundScreen otherBeatmapBackground)
                return false;

            return base.Equals(other) && beatmap == otherBeatmapBackground.beatmap;
        }
    }
}
