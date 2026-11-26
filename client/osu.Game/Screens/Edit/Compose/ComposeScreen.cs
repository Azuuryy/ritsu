// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Screens.Edit.Compose
{
    public partial class ComposeScreen : EditorScreenWithTimeline, IGameplaySettings
    {
        [Resolved]
        private Clipboard hostClipboard { get; set; } = null!;

        [Resolved]
        private EditorClock clock { get; set; }

        [Resolved]
        private IGameplaySettings globalGameplaySettings { get; set; }

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; }

        private Bindable<string> clipboard { get; set; }

        private HitObjectComposer composer;
        private ModPreviewContainer previewContainer;
        private Editor previewEditor;
        private ScheduledDelegate previewUpdate;
        private CancellationTokenSource previewCancellation;
        private ModSettingChangeTracker previewSettings;
        private IEditorChangeHandler previewChanges;
        private Bindable<bool> previewEnabled;
        private Bindable<IReadOnlyList<Mod>> previewMods;

        public ComposeScreen()
            : base(EditorScreenMode.Compose)
        {
        }

        private Ruleset ruleset;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            ruleset = parent.Get<IBindable<WorkingBeatmap>>().Value.BeatmapInfo.Ruleset.CreateInstance();
            composer = ruleset?.CreateHitObjectComposer();

            // make the composer available to the timeline and other components in this screen.
            if (composer != null)
                dependencies.CacheAs(composer);

            return dependencies;
        }

        protected override Drawable CreateMainContent()
        {
            if (ruleset == null || composer == null)
                return new ScreenWhiteBox.UnderConstructionMessage(ruleset == null ? "This beatmap" : $"{ruleset.Description}'s composer");

            return wrapSkinnableContent(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    composer,
                    previewContainer = new ModPreviewContainer { RelativeSizeAxes = Axes.Both }
                }
            });
        }

        protected override Drawable CreateTimelineContent()
        {
            if (ruleset == null || composer == null)
                return base.CreateTimelineContent();

            TimelineBreakDisplay breakDisplay = new TimelineBreakDisplay
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Height = 0.75f,
            };

            return wrapSkinnableContent(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[]
                {
                    // We want to display this below hitobjects to better expose placement objects visually.
                    // It needs to be above the blueprint container to handle drags on breaks though.
                    breakDisplay.CreateProxy(),
                    new TimelineBlueprintContainer(composer),
                    breakDisplay
                }
            });
        }

        private Drawable wrapSkinnableContent(Drawable content)
        {
            Debug.Assert(ruleset != null);

            return new EditorSkinProvidingContainer(EditorBeatmap).WithChild(content);
        }

        [BackgroundDependencyLoader(true)]
        private void load(EditorClipboard clipboard, Editor editor, IEditorChangeHandler changeHandler)
        {
            this.clipboard = clipboard.Content.GetBoundCopy();
            previewEditor = editor;
            previewChanges = changeHandler;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // May be null in the case of a ruleset that doesn't have editor support, see CreateMainContent().
            if (composer == null)
                return;

            EditorBeatmap.SelectedHitObjects.BindCollectionChanged((_, _) => updateClipboardActionAvailability());
            clipboard.BindValueChanged(_ => updateClipboardActionAvailability());
            composer.OnLoadComplete += _ =>
            {
                updateClipboardActionAvailability();
                if (previewEditor != null)
                    Scheduler.AddOnce(schedulePreview);
            };
            if (previewEditor != null)
            {
                previewEnabled = previewEditor.PreviewMods.GetBoundCopy();
                previewMods = previewEditor.Mods.GetBoundCopy();
                previewEnabled.BindValueChanged(_ => Scheduler.AddOnce(schedulePreview));
                previewMods.BindValueChanged(_ =>
                {
                    previewSettings?.Dispose();
                    previewSettings = new ModSettingChangeTracker(previewEditor.Mods.Value);
                    previewSettings.SettingChanged += _ => Scheduler.AddOnce(schedulePreview);
                    Scheduler.AddOnce(schedulePreview);
                }, true);
                if (previewChanges != null)
                    previewChanges.OnStateChange += schedulePreview;
            }
            updateClipboardActionAvailability();
        }

        private void schedulePreview()
        {
            if (!composer.IsLoaded || !previewContainer.IsLoaded)
                return;

            previewUpdate?.Cancel();
            previewCancellation?.Cancel();
            previewCancellation?.Dispose();
            previewCancellation = null;

            if (!previewEditor.PreviewMods.Value || previewEditor.Mods.Value.Count == 0)
            {
                previewContainer.Clear();
                composer.Show();
                return;
            }

            previewUpdate = Scheduler.AddDelayed(startPreview, 150);
        }

        private void startPreview()
        {
            var mods = previewEditor.Mods.Value.Select(mod => mod.DeepClone()).ToArray();
            var snapshot = new MemoryStream();
            using (var writer = new StreamWriter(snapshot, leaveOpen: true))
                new LegacyBeatmapEncoder(EditorBeatmap, EditorBeatmap.BeatmapSkin, EditorBeatmap.Storyboard).Encode(writer);

            snapshot.Position = 0;
            var rulesetInfo = EditorBeatmap.BeatmapInfo.Ruleset;
            _ = loadPreviewAsync(snapshot, mods, rulesetInfo, (previewCancellation = new CancellationTokenSource()).Token);
        }

        private async Task loadPreviewAsync(Stream snapshot, Mod[] mods, RulesetInfo rulesetInfo, CancellationToken token)
        {
            try
            {
                using (snapshot)
                {
                    var map = await Task.Run(() =>
                    {
                        using var reader = new LineBufferedReader(snapshot);
                        var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                        decoded.BeatmapInfo.Ruleset = rulesetInfo;
                        return new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(rulesetInfo, mods, token);
                    }, token).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        if (!token.IsCancellationRequested)
                            showPreview(map, mods, token);
                    });
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                Schedule(() => previewFailed(error, token));
            }
        }

        private void showPreview(IBeatmap map, Mod[] mods, CancellationToken token)
        {
            try
            {
                var preview = ruleset.CreateDrawableRulesetWith(map, mods);
                preview.Clock = clock;
                preview.ProcessCustomClock = false;
                preview.FrameStablePlayback = false;
                previewContainer.LoadPreview(new ModPreviewContent(preview), loaded =>
                {
                    if (loaded.LoadError != null)
                    {
                        previewFailed(loaded.LoadError, token);
                        loaded.Dispose();
                        return;
                    }

                    try
                    {
                        preview.Playfield.DisplayJudgements.Value = false;
                        var autoplay = ruleset.GetAutoplayMod();
                        if (autoplay != null)
                            preview.SetReplayScore(autoplay.CreateScoreFromReplayData(map, mods));
                        previewContainer.Child = loaded;
                        composer.Hide();
                    }
                    catch (Exception error)
                    {
                        loaded.Dispose();
                        previewFailed(error, token);
                    }
                }, token);
            }
            catch (Exception error)
            {
                previewFailed(error, token);
            }
        }

        private void previewFailed(Exception error, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            Logger.Error(error, "Could not load the editor mod preview.");
            previewContainer.Child = new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = "Mod preview unavailable"
            };
            composer.Show();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (previewChanges != null)
                previewChanges.OnStateChange -= schedulePreview;
            previewUpdate?.Cancel();
            previewCancellation?.Cancel();
            previewCancellation?.Dispose();
            previewSettings?.Dispose();
            previewEnabled?.UnbindAll();
            previewMods?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private partial class ModPreviewContainer : Container
        {
            public void LoadPreview(ModPreviewContent content, Action<ModPreviewContent> loaded, CancellationToken token)
                => LoadComponentAsync(content, loaded, token);

            public override bool PropagatePositionalInputSubTree => false;
            public override bool PropagateNonPositionalInputSubTree => false;
        }

        private partial class ModPreviewContent : CompositeDrawable
        {
            private readonly DrawableRuleset preview;

            public Exception LoadError { get; private set; }

            public ModPreviewContent(DrawableRuleset preview)
            {
                RelativeSizeAxes = Axes.Both;
                this.preview = preview;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                try
                {
                    LoadComponent(preview);
                    InternalChild = preview;
                }
                catch (Exception error)
                {
                    LoadError = error;
                    preview.Dispose();
                }
            }
        }

        #region Clipboard operations

        public override void Cut()
        {
            if (!CanCut.Value)
                return;

            Copy();
            EditorBeatmap.RemoveRange(EditorBeatmap.SelectedHitObjects.ToArray());
        }

        public override void Copy()
        {
            // on stable, pressing Ctrl-C would copy the current timestamp to system clipboard
            // regardless of whether anything was even selected at all.
            // UX-wise this is generally strange and unexpected, but make it work anyways to preserve muscle memory.
            // note that this means that `getTimestamp()` must handle no-selection case, too.
            // additionally, note we're intentionally not using `OsuGame.CopyToClipboard()`
            // because we do not want toasts to pop up on every Ctrl-C press - it'd be disruptive to mappers.
            hostClipboard.SetText(getTimestamp());

            if (CanCopy.Value)
                clipboard.Value = new ClipboardContent(EditorBeatmap).Serialize();
        }

        public override void Paste()
        {
            if (!CanPaste.Value)
                return;

            var objects = clipboard.Value.Deserialize<ClipboardContent>().HitObjects;

            Debug.Assert(objects.Any());

            double timeOffset = beatSnapProvider.SnapTime(clock.CurrentTime) - objects.Min(o => o.StartTime);

            foreach (var h in objects)
                h.StartTime += timeOffset;

            EditorBeatmap.BeginChange();

            EditorBeatmap.SelectedHitObjects.Clear();

            EditorBeatmap.AddRange(objects);
            EditorBeatmap.SelectedHitObjects.AddRange(objects);

            EditorBeatmap.EndChange();
        }

        private void updateClipboardActionAvailability()
        {
            CanCut.Value = CanCopy.Value = EditorBeatmap.SelectedHitObjects.Any();
            CanPaste.Value = composer.IsLoaded && !string.IsNullOrEmpty(clipboard.Value);
        }

        private string getTimestamp()
        {
            if (composer == null)
                return string.Empty;

            double displayTime = EditorBeatmap.SelectedHitObjects.MinBy(h => h.StartTime)?.StartTime ?? clock.CurrentTime;
            string selectionAsString = composer.ConvertSelectionToString();

            return !string.IsNullOrEmpty(selectionAsString)
                ? $"{displayTime.ToEditorFormattedString()} ({selectionAsString}) - "
                : $"{displayTime.ToEditorFormattedString()} - ";
        }

        #endregion

        // Combo colour normalisation should not be applied in the editor.
        // Note this doesn't affect editor test mode.
        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => new Bindable<float>();

        // Arguable.
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => globalGameplaySettings.PositionalHitsoundsLevel;
    }
}
