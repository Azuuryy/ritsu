// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu.Objects;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI
{
    internal partial class RandomV2Trajectory : CompositeDrawable
    {
        private readonly IReadOnlyList<OsuHitObject> objects;
        private readonly Marker[] markers = new Marker[12];
        public readonly BindableBool Visible = new BindableBool(true);

        public RandomV2Trajectory(IReadOnlyList<OsuHitObject> objects)
        {
            this.objects = objects;
            RelativeSizeAxes = Axes.Both;
            Visible.BindValueChanged(change => Alpha = change.NewValue ? 1 : 0, true);
            for (int i = 0; i < markers.Length; i++)
                AddInternal(markers[i] = new Marker());
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible.Value)
                return;

            int left = 0;
            int right = objects.Count;
            while (left < right)
            {
                int middle = (left + right) / 2;
                if (objects[middle].StartTime < Time.Current)
                    left = middle + 1;
                else
                    right = middle;
            }

            int start = Math.Max(0, left - 3);
            for (int i = 0; i < markers.Length; i++)
            {
                int index = start + i;
                Marker marker = markers[i];
                marker.Alpha = index < objects.Count ? 1 : 0;
                if (index >= objects.Count)
                    continue;

                var position = objects[index].StackedPosition;
                marker.Number.Position = position;
                marker.Number.Text = (index + 1).ToString();
                marker.Number.Colour = index < left ? Color4.Silver : Color4.LightYellow;
                marker.Line.Alpha = i > 0 ? 0.65f : 0;
                if (i == 0)
                    continue;

                var previous = objects[index - 1].StackedEndPosition;
                var direction = position - previous;
                marker.Line.Position = previous;
                marker.Line.Width = direction.Length;
                marker.Line.Rotation = MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI;
            }
        }

        private partial class Marker : CompositeDrawable
        {
            public readonly Box Line;
            public readonly OsuSpriteText Number;

            public Marker()
            {
                RelativeSizeAxes = Axes.Both;
                InternalChildren = new Drawable[]
                {
                    Line = new Box { Origin = Anchor.CentreLeft, Height = 1.5f, Colour = Color4.LightYellow },
                    Number = new OsuSpriteText { Origin = Anchor.Centre, Font = OsuFont.Default.With(size: 17), Shadow = true },
                };
            }
        }
    }
}
