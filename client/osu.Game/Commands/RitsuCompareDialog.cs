// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Commands
{
    internal partial class RitsuCompareDialog : PopupDialog
    {
        public RitsuCompareDialog(ScoreInfo[] scores, Action<ScoreInfo> open)
        {
            HeaderText = "Compare local scores";
            Buttons = new PopupDialogButton[] { new PopupDialogCancelButton { Text = "Close" } };
            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = 35, Vertical = 10 },
                Spacing = new Vector2(0, 5),
            };
            MainContent.Add(content);

            if (scores.Length < 2)
            {
                content.Add(new OsuSpriteText { Text = "Play this difficulty at least twice on this profile to compare scores." });
                return;
            }

            var items = scores.OrderByDescending(s => s.Date).ThenBy(s => s.ID).Select(s => new ScoreChoice(s)).ToArray();
            var left = new OsuDropdown<ScoreChoice> { RelativeSizeAxes = Axes.X, Items = items, Current = { Value = items[0] } };
            var right = new OsuDropdown<ScoreChoice>
            {
                RelativeSizeAxes = Axes.X,
                Items = items,
                Current = { Value = items.Skip(1).MaxBy(c => c.Score.TotalScore)! },
            };
            var details = new FillFlowContainer<OsuSpriteText>
            {
                RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 4),
            };
            content.AddRange(new Drawable[] { left, right, details });

            void refresh()
            {
                details.Clear();
                var a = left.Current.Value.Score;
                var b = right.Current.Value.Score;
                if (a.ID == b.ID)
                {
                    row("Choose two different scores.");
                    return;
                }

                row($"Score   {a.TotalScore:N0}   /   {b.TotalScore:N0}");
                row($"Accuracy   {a.Accuracy:P2}   /   {b.Accuracy:P2}");
                row($"Combo   {a.MaxCombo:N0}   /   {b.MaxCombo:N0}");
                row($"Mods   {formatMods(a)}   /   {formatMods(b)}");
                row($"PP   {a.PP?.ToString("0.##") ?? "—"}   /   {b.PP?.ToString("0.##") ?? "—"}");
            }

            void row(string text) => details.Add(new OsuSpriteText { Text = text });
            void openResult(ScoreInfo score)
            {
                Hide();
                open(score);
            }

            left.Current.BindValueChanged(_ => refresh());
            right.Current.BindValueChanged(_ => refresh(), true);
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "Open first score", HideDialogOnAction = false, Action = () => openResult(left.Current.Value.Score) },
                new PopupDialogOkButton { Text = "Open second score", HideDialogOnAction = false, Action = () => openResult(right.Current.Value.Score) },
                new PopupDialogCancelButton { Text = "Close" },
            };
        }

        private static string formatMods(ScoreInfo score) => score.APIMods.Length == 0 ? "NM" : string.Join(", ", score.APIMods.Select(m => m.Acronym));

        private sealed record ScoreChoice(ScoreInfo Score)
        {
            public override string ToString() => $"{Score.Date.ToLocalTime():g} · {Score.TotalScore:N0} · {formatMods(Score)}";
        }
    }
}
