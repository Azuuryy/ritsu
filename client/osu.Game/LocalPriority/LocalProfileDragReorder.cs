// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Drag-and-drop reordering of profile items (chips on the profile page, rows in the switcher) inside a fill flow.
    /// Items that can be reordered implement <see cref="IReorderableProfileItem"/>; other items (online account, "+" buttons) keep their place.
    /// </summary>
    public static class LocalProfileDragReorder
    {
        /// <summary>
        /// Moves <paramref name="dragged"/> to the place of the reorderable item under the cursor, live while dragging.
        /// </summary>
        public static void DragTo(FillFlowContainer flow, Drawable dragged, Vector2 screenSpaceMouse)
        {
            var items = reorderable(flow).ToList();

            int from = items.IndexOf(dragged);
            if (from < 0)
                return;

            var target = items.FirstOrDefault(i => i != dragged && i.ScreenSpaceDrawQuad.Contains(screenSpaceMouse));
            if (target == null)
                return;

            int to = items.IndexOf(target);

            // Items of different widths: only move once the cursor is past the middle of the target (no flickering back and forth).
            var quad = target.ScreenSpaceDrawQuad;
            bool vertical = flow.Direction == FillDirection.Vertical;
            float middle = vertical ? quad.Centre.Y : quad.Centre.X;
            float cursor = vertical ? screenSpaceMouse.Y : screenSpaceMouse.X;

            if ((to > from && cursor < middle) || (to < from && cursor > middle))
                return;

            items.RemoveAt(from);
            items.Insert(to, dragged);

            // Keep the reorderable items in the same layout slots as before, just in the new order.
            var slots = items.Select(flow.GetLayoutPosition).OrderBy(p => p).ToArray();

            for (int i = 0; i < items.Count; i++)
                flow.SetLayoutPosition(items[i], slots[i]);
        }

        /// <summary>
        /// The profile ids in the flow, in their current (dragged) order.
        /// </summary>
        public static IReadOnlyList<Guid> CurrentOrder(FillFlowContainer flow) =>
            reorderable(flow).Select(d => ((IReorderableProfileItem)d).ProfileId!.Value).ToArray();

        private static IEnumerable<Drawable> reorderable(FillFlowContainer flow) =>
            flow.FlowingChildren.Where(d => d is IReorderableProfileItem { ProfileId: not null });
    }

    /// <summary>
    /// A profile chip / row that can be dragged to reorder profiles.
    /// </summary>
    public interface IReorderableProfileItem
    {
        Guid? ProfileId { get; }
    }
}
