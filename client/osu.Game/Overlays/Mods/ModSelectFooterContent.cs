// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.LocalPriority;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Offline;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Overlays.Mods
{
    public partial class ModSelectFooterContent : VisibilityContainer
    {
        private readonly ModSelectOverlay overlay;

        [Resolved(CanBeNull = true)]
        private RitsuProfileManager? profiles { get; set; }

        private RankingInformationDisplay? rankingInformationDisplay;
        private BeatmapAttributesDisplay? beatmapAttributesDisplay;
        private FillFlowContainer<ShearedButton> buttonFlow = null!;
        private FillFlowContainer contentFlow = null!;

        public DeselectAllModsButton? DeselectAllModsButton { get; set; }

        public readonly IBindable<WorkingBeatmap?> Beatmap = new Bindable<WorkingBeatmap?>();
        public readonly IBindable<IReadOnlyList<Mod>> ActiveMods = new Bindable<IReadOnlyList<Mod>>();
        public readonly IBindable<RulesetInfo?> Ruleset = new Bindable<RulesetInfo?>();

        /// <summary>
        /// Whether the effects (on score multiplier, on or beatmap difficulty) of the current selected set of mods should be shown.
        /// </summary>
        protected virtual bool ShowModEffects => true;

        /// <summary>
        /// Additional vertical space used when the statistics are above the buttons.
        /// </summary>
        public float ExtraHeight { get; private set; }

        private Bindable<bool> showAttributes = null!;

        public ModSelectFooterContent(ModSelectOverlay overlay)
        {
            this.overlay = overlay;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.Both;
            showAttributes = config.GetBindable<bool>(OsuSetting.ShowModDifficultyAttributes);

            InternalChild = buttonFlow = new FillFlowContainer<ShearedButton>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Padding = new MarginPadding { Horizontal = 20 },
                Spacing = new Vector2(10),
                ChildrenEnumerable = CreateButtons(),
            };

            if (ShowModEffects)
            {
                AddInternal(contentFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(30, 10),
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Horizontal = 20 },
                    Children = new Drawable[]
                    {
                        rankingInformationDisplay = new RankingInformationDisplay
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight
                        },
                        beatmapAttributesDisplay = new BeatmapAttributesDisplay
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            BeatmapInfo = { Value = Beatmap.Value?.BeatmapInfo },
                        },
                    }
                });
            }
        }

        private ModSettingChangeTracker? modSettingChangeTracker;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (profiles != null) profiles.Changed += onProfileChanged;

            if (beatmapAttributesDisplay is { } attributes)
            {
                showAttributes.BindValueChanged(change => attributes.Collapsed.Value = !change.NewValue, true);
                attributes.Collapsed.BindValueChanged(change => showAttributes.Value = !change.NewValue);
            }

            Beatmap.BindValueChanged(b =>
            {
                beatmapAttributesDisplay?.BeatmapInfo.Value = b.NewValue?.BeatmapInfo;

                updateInformation();
            }, true);

            Ruleset.BindValueChanged(_ => updateInformation());
            ActiveMods.BindValueChanged(_ =>
            {
                updateInformation();

                modSettingChangeTracker?.Dispose();

                // Importantly, use ActiveMods.Value here (and not the ValueChanged NewValue) as the latter can
                // potentially be stale, due to complexities in the way change trackers work.
                //
                // See https://github.com/ppy/osu/pull/23284#issuecomment-1529056988
                modSettingChangeTracker = new ModSettingChangeTracker(ActiveMods.Value);
                modSettingChangeTracker.SettingChanged += _ => updateInformation();
            }, true);
        }

        private void updateInformation()
        {
            WorkingBeatmap? workingBeatmap = Beatmap.Value;

            if (rankingInformationDisplay != null && workingBeatmap != null)
            {
                var scoreMultiplierCalculator = Ruleset.Value?.CreateInstance().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(workingBeatmap.BeatmapInfo.Difficulty));
                double multiplier = scoreMultiplierCalculator?.CalculateFor(ActiveMods.Value) ?? 1;

                rankingInformationDisplay.ModMultiplier.Value = multiplier;
                // osu!local: the active local profile may count every play (or Relax / Autopilot) as ranked.
                rankingInformationDisplay.Ranked.Value = LocalPriorityPolicy.CountAllAsRanked
                                                         || (RitsuOffline.Enabled && profiles != null
                                                             ? RitsuLocalBeatmapStatus.ForBeatmap(workingBeatmap.BeatmapInfo, profiles.GetActiveProfile()) != BeatmapOnlineStatus.Unranked
                                                               && ActiveMods.Value.All(m => m.Acronym != "AT" && m.Acronym != "CN"
                                                                                           && (m.Acronym != "RX" && m.Acronym != "AP" || profiles.IncludeRelaxAutopilot || LocalPriorityPolicy.CountRelaxAutopilot))
                                                             : ActiveMods.Value.All(LocalPriorityPolicy.IsModRanked));
            }

            beatmapAttributesDisplay?.Mods.Value = ActiveMods.Value;
        }

        private void onProfileChanged() => Schedule(updateInformation);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && profiles != null) profiles.Changed -= onProfileChanged;
            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            if (beatmapAttributesDisplay != null)
            {
                float rightEdgeOfLastButton = buttonFlow[^1].ScreenSpaceDrawQuad.TopRight.X;

                float estimatedWidth = showAttributes.Value ? 640 : 410;
                float projectedLeftEdge = buttonFlow.ToScreenSpace(new Vector2(buttonFlow.DrawWidth - estimatedWidth, 0)).X;
                bool moveAboveButtons = rightEdgeOfLastButton > projectedLeftEdge;

                contentFlow.Direction = DrawWidth < estimatedWidth + 40 ? FillDirection.Vertical : FillDirection.Horizontal;
                contentFlow.Margin = new MarginPadding { Horizontal = 20, Bottom = moveAboveButtons ? buttonFlow.DrawHeight + 10 : 0 };
                ExtraHeight = moveAboveButtons ? contentFlow.DrawHeight + 10 : 0;
            }
        }

        protected virtual IEnumerable<ShearedButton> CreateButtons()
        {
            yield return DeselectAllModsButton = new DeselectAllModsButton(overlay);
        }

        protected override void PopIn()
        {
            this.MoveToY(0, 400, Easing.OutQuint)
                .FadeIn(400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.MoveToY(-20f, 200, Easing.OutQuint)
                .FadeOut(200, Easing.OutQuint);
        }
    }
}
