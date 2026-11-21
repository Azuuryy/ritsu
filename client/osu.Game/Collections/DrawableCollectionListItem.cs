// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Collections
{
    /// <summary>
    /// Visualises a <see cref="BeatmapCollection"/> inside a <see cref="DrawableCollectionList"/>.
    /// </summary>
    public partial class DrawableCollectionListItem : OsuRearrangeableListItem<Live<BeatmapCollection>>, IFilterable
    {
        private const float item_height = 45;
        private const float button_width = item_height * 0.75f;
        private const float action_button_height = 36;
        private const float membership_button_width = 34;
        private const float action_spacing = 4;
        private const float membership_actions_width = 4 * membership_button_width + 3 * action_spacing;

        protected TextBox TextBox => content.TextBox;

        internal ManageCollectionsDialog? MembershipManager { get; init; }

        private ItemContent content = null!;

        /// <summary>
        /// Creates a new <see cref="DrawableCollectionListItem"/>.
        /// </summary>
        /// <param name="item">The <see cref="BeatmapCollection"/>.</param>
        /// <param name="isCreated">Whether <paramref name="item"/> currently exists inside realm.</param>
        public DrawableCollectionListItem(Live<BeatmapCollection> item, bool isCreated)
            : base(item)
        {
            // For now we don't support rearranging and always use alphabetical sort.
            // Change this to:
            //
            // ShowDragHandle.Value = item.IsManaged;
            //
            // if we want to support user sorting (but changes will need to be made to realm to persist).
            ShowDragHandle.Value = false;

            Masking = true;

            // This doesn't match the latest design spec (should be 5) but is an in-between that feels right to the eye
            // until we move everything over to Form controls.
            CornerRadius = 10;
            CornerExponent = 2.5f;
        }

        protected override Drawable CreateContent() => content = new ItemContent(Model, MembershipManager);

        /// <summary>
        /// The main content of the <see cref="DrawableCollectionListItem"/>.
        /// </summary>
        private partial class ItemContent : CompositeDrawable
        {
            private readonly Live<BeatmapCollection> collection;

            public ItemTextBox TextBox { get; private set; } = null!;

            private readonly ManageCollectionsDialog? manager;
            private readonly IBindable<ManageCollectionsDialog.MembershipTarget?>? target;
            private Container nameArea = null!;
            private DeleteButton? delete;
            private FillFlowContainer<OsuAnimatedButton>? membershipActions;
            private OsuAnimatedButton addSet = null!;
            private OsuAnimatedButton addDifficulty = null!;
            private OsuAnimatedButton removeDifficulty = null!;
            private OsuAnimatedButton removeSet = null!;

            public ItemContent(Live<BeatmapCollection> collection, ManageCollectionsDialog? manager)
            {
                this.collection = collection;
                this.manager = manager;
                target = manager?.Target.GetBoundCopy();

                RelativeSizeAxes = Axes.X;
                Height = item_height;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, TextureStore textures)
            {
                InternalChildren = new[]
                {
                    collection.IsManaged
                        ? delete = new DeleteButton(collection)
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            IsTextBoxHovered = v => TextBox.ReceivePositionalInputAt(v)
                        }
                        : Empty(),
                    nameArea = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Right = collection.IsManaged ? button_width : 0 },
                        Children = new Drawable[]
                        {
                            TextBox = new ItemTextBox(collection)
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = item_height,
                                CommitOnFocusLost = true,
                            },
                        }
                    },
                };

                if (manager == null)
                    return;

                var buttonBackground = colours.GreySeaFoamDarker.Darken(0.5f);
                AddInternal(membershipActions = new FillFlowContainer<OsuAnimatedButton>
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(action_spacing, 0),
                    Children = new[]
                    {
                        addSet = membershipButton(new Sprite { Name = "Beatmap set icon", Texture = textures.Get("Ritsu/collection-set-add") },
                            colours.GreenDark, buttonBackground, "Add the beatmap set", true, true),
                        addDifficulty = membershipButton(new SpriteIcon { Icon = FontAwesome.Solid.Plus },
                            colours.Green3, buttonBackground, "Add this difficulty", true, false),
                        removeDifficulty = membershipButton(new SpriteIcon { Icon = FontAwesome.Solid.Minus },
                            colours.Yellow, buttonBackground, "Remove this difficulty", false, false),
                        removeSet = membershipButton(new Sprite { Name = "Beatmap set icon", Texture = textures.Get("Ritsu/collection-set-remove") },
                            colours.YellowDark, buttonBackground, "Remove the beatmap set", false, true),
                    },
                });
            }

            private OsuAnimatedButton membershipButton(Drawable icon, Colour4 colour, Colour4 backgroundColour, string description, bool add, bool wholeSet)
            {
                icon.Anchor = Anchor.Centre;
                icon.Origin = Anchor.Centre;
                icon.Colour = colour;
                icon.Size = new Vector2(wholeSet ? 19 : 13);

                return new OsuAnimatedButton
                {
                    Name = description,
                    TooltipText = description,
                    Size = new Vector2(membership_button_width, action_button_height),
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = backgroundColour, Depth = 1 },
                        icon,
                    },
                    Action = () =>
                    {
                        if (target?.Value is not { } selection)
                            return;

                        string[] hashes = wholeSet ? selection.SetHashes : selection.DifficultyHash == null ? Array.Empty<string>() : new[] { selection.DifficultyHash };
                        manager!.ChangeMembership(collection.ID, hashes, add);
                        updateMembership();
                    },
                };
            }

            private void updateMembership()
            {
                if (membershipActions == null || target == null)
                    return;

                var selection = target.Value;
                membershipActions.Alpha = selection == null ? 0 : 1;
                float actionWidth = selection == null ? 0 : membership_actions_width;
                nameArea.Padding = new MarginPadding { Right = button_width + actionWidth };
                delete!.X = -actionWidth;
                if (selection == null)
                {
                    TextBox.SetMembership(false);
                    return;
                }

                collection.PerformRead(c =>
                {
                    var members = c.IsValid ? c.BeatmapMD5Hashes.ToHashSet() : new HashSet<string>();
                    bool hasDifficulty = selection.DifficultyHash != null && members.Contains(selection.DifficultyHash);
                    addSet.Enabled.Value = c.IsValid && selection.SetHashes.Any(hash => !members.Contains(hash));
                    addDifficulty.Enabled.Value = c.IsValid && selection.DifficultyHash != null && !hasDifficulty;
                    removeDifficulty.Enabled.Value = hasDifficulty;
                    bool hasSetMember = selection.SetHashes.Any(members.Contains);
                    removeSet.Enabled.Value = hasSetMember;
                    TextBox.SetMembership(selection.DifficultyHash != null ? hasDifficulty : hasSetMember);
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // Bind late, as the collection name may change externally while still loading.
                TextBox.Current.Value = collection.PerformRead(c => c.IsValid ? c.Name : string.Empty);
                TextBox.OnCommit += onCommit;
                target?.BindValueChanged(_ => updateMembership(), true);
            }

            private void onCommit(TextBox sender, bool newText)
            {
                if (collection.IsManaged && collection.Value.Name != TextBox.Current.Value)
                    collection.PerformWrite(c => c.Name = TextBox.Current.Value);
            }
        }

        private partial class ItemTextBox : OsuTextBox
        {
            protected override float LeftRightPadding => item_height / 2;

            private const float count_text_size = 12;

            private readonly Live<BeatmapCollection> collection;

            private OsuSpriteText countText = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public void SetMembership(bool containsBeatmap)
            {
                BackgroundUnfocused = containsBeatmap ? colours.Green3.Darken(0.73f) : colours.GreySeaFoamDarker.Darken(0.5f);
                BackgroundFocused = containsBeatmap ? colours.Green3.Darken(0.56f) : colours.GreySeaFoam;
            }

            public ItemTextBox(Live<BeatmapCollection> collection)
            {
                this.collection = collection;

                CornerRadius = 10;
                CornerExponent = 2.5f;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                SetMembership(false);

                if (collection.IsManaged)
                {
                    TextContainer.Height *= (Height - count_text_size) / Height;
                    TextContainer.Margin = new MarginPadding { Bottom = count_text_size };

                    TextContainer.Add(countText = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.TopLeft,
                        Depth = float.MinValue,
                        Font = OsuFont.Default.With(size: count_text_size, weight: FontWeight.SemiBold),
                        Margin = new MarginPadding { Top = 2, Left = 2 },
                        Colour = colours.Yellow
                    });

                    // interestingly, it is not required to subscribe to change notifications on this collection at all for this to work correctly.
                    // the reasoning for this is that `DrawableCollectionList` already takes out a subscription on the set of all `BeatmapCollection`s -
                    // but that subscription does not only cover *changes to the set of collections* (i.e. addition/removal/rearrangement of collections),
                    // but also covers *changes to the properties of collections*, which `BeatmapMD5Hashes` is one.
                    // when a collection item changes due to `BeatmapMD5Hashes` changing, the list item is deleted and re-inserted, thus guaranteeing this to work correctly.
                    countText.Text = CommonStrings.ItemsCount(collection.PerformRead(c => c.BeatmapMD5Hashes.Count));
                }
                else
                {
                    PlaceholderText = CollectionsStrings.CreateNew;
                }
            }
        }

        public partial class DeleteButton : OsuClickableContainer
        {
            public Func<Vector2, bool> IsTextBoxHovered = null!;

            [Resolved]
            private IDialogOverlay? dialogOverlay { get; set; }

            private readonly Live<BeatmapCollection> collection;

            private Drawable background = null!;
            private Colour4 idleColour;
            private Colour4 hoverColour;

            public DeleteButton(Live<BeatmapCollection> collection)
            {
                this.collection = collection;
                RelativeSizeAxes = Axes.Y;

                Width = button_width + item_height / 2;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                idleColour = colours.Red.Darken(0.78f);
                hoverColour = colours.Red.Darken(0.25f);

                Child = new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -action_spacing,
                    Size = new Vector2(button_width - action_spacing, action_button_height),
                    Masking = true,
                    CornerRadius = 10,
                    CornerExponent = 2.5f,
                    Children = new[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = idleColour
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(12),
                            Icon = FontAwesome.Solid.Trash
                        }
                    }
                };

                Action = () =>
                {
                    if (collection.PerformRead(c => c.BeatmapMD5Hashes.Count) == 0)
                        deleteCollection();
                    else
                        dialogOverlay?.Push(new DeleteCollectionDialog(collection, deleteCollection));
                };
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => base.ReceivePositionalInputAt(screenSpacePos) && !IsTextBoxHovered(screenSpacePos);

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(hoverColour, 100, Easing.Out);
                return false;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(idleColour, 100);
            }

            protected override bool OnClick(ClickEvent e)
            {
                background.FlashColour(Color4.White, 150);

                return base.OnClick(e);
            }

            [Resolved(canBeNull: true)]
            private ManageCollectionsDialog? manager { get; set; }

            private void deleteCollection()
            {
                if (manager != null)
                    manager.DeleteCollection(collection.ID);
                else
                    collection.PerformWrite(c => c.Realm!.Remove(c));
            }
        }

        public IEnumerable<LocalisableString> FilterTerms => Model.PerformRead(m => m.IsValid ? new[] { (LocalisableString)m.Name } : []);

        private bool matchingFilter = true;

        public bool MatchingFilter
        {
            get => matchingFilter;
            set
            {
                matchingFilter = value;

                if (matchingFilter)
                    this.FadeIn(200);
                else
                    Hide();
            }
        }

        public bool FilteringActive { get; set; }
    }
}
