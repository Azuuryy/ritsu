// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Library
{
    internal partial class RitsuMemoDialog : PopupDialog
    {
        public RitsuMemoDialog(string difficulty, RitsuMemo? memo, IReadOnlyList<ScoreInfo> scores, Func<Guid, double?> replayLength,
                               Action<RitsuMemo> save, Action<Guid, double> open)
        {
            HeaderText = $"Difficulty memo · {difficulty}";
            BodyText = "Optional replay timestamp in seconds or m:ss.";
            Icon = FontAwesome.Solid.Edit;

            var noReplay = new ReplayEntry(null, "No replay link");
            var entries = new List<ReplayEntry> { noReplay };
            entries.AddRange(scores.Select(s => new ReplayEntry(s.ID, $"{s.Date.LocalDateTime:g} · {s.TotalScore:N0}")));
            if (memo?.ReplayId is Guid previousReplay && entries.All(e => e.Id != previousReplay))
                entries.Add(new ReplayEntry(previousReplay, "Saved replay · unavailable"));

            var selector = new OsuDropdown<ReplayEntry>
            {
                RelativeSizeAxes = Axes.X,
                Items = entries,
                Current = { Value = entries.FirstOrDefault(e => e.Id == memo?.ReplayId) ?? noReplay },
            };
            var input = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Text = memo?.Text ?? string.Empty,
                PlaceholderText = "Private memo",
                ReleaseFocusOnCommit = false,
            };
            var timestamp = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Text = memo?.Timestamp is double time ? (time / 1000).ToString("R", CultureInfo.InvariantCulture) : string.Empty,
                PlaceholderText = "Timestamp",
                ReleaseFocusOnCommit = false,
            };

            var link = new RoundedButton
            {
                RelativeSizeAxes = Axes.X,
                Height = 29,
                Text = "Watch linked replay",
                Action = () =>
                {
                    if (selector.Current.Value.Id is not Guid replayId || !TryParseTimestamp(timestamp.Text, out double milliseconds))
                        return;
                    double? duration = replayLength(replayId);
                    if (duration == null || milliseconds > duration)
                    {
                        BodyText = "The replay is unavailable or does not reach this timestamp.";
                        return;
                    }
                    Hide();
                    open(replayId, milliseconds);
                },
            };
            void refresh()
            {
                bool hasLink = selector.Current.Value.Id != null;
                timestamp.Current.Disabled = !hasLink;
                link.Enabled.Value = hasLink && TryParseTimestamp(timestamp.Text, out _);
            }
            selector.Current.ValueChanged += _ => refresh();
            timestamp.Current.ValueChanged += _ => refresh();

            void confirm()
            {
                Guid? id = selector.Current.Value.Id;
                double? time = null;
                if (id != null)
                {
                    if (!TryParseTimestamp(timestamp.Text, out double parsed))
                    {
                        BodyText = "Enter a valid replay timestamp.";
                        return;
                    }
                    if (id != memo?.ReplayId || parsed != memo.Timestamp)
                    {
                        double? duration = replayLength(id.Value);
                        if (duration == null || parsed > duration)
                        {
                            BodyText = "The replay is unavailable or does not reach this timestamp.";
                            return;
                        }
                    }
                    time = parsed;
                }

                save(new RitsuMemo(input.Text, id, time));
                Hide();
            }

            MainContent.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = 35, Vertical = 8 },
                Spacing = new Vector2(0, 6),
                Children = new Drawable[] { input, selector, timestamp, link },
            });
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton { Text = "Save", Action = confirm, HideDialogOnAction = false },
                new PopupDialogCancelButton { Text = "Cancel" },
            };
            refresh();
        }

        internal static bool TryParseTimestamp(string input, out double milliseconds)
        {
            milliseconds = 0;
            var parts = input.Trim().Split(':');
            if (parts.Length > 3)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                bool last = i == parts.Length - 1;
                var style = last ? NumberStyles.AllowDecimalPoint : NumberStyles.None;
                if (!double.TryParse(parts[i], style, CultureInfo.InvariantCulture, out double value)
                    || !double.IsFinite(value) || value < 0 || (i > 0 && value >= 60))
                    return false;

                milliseconds = milliseconds * 60 + value;
            }
            milliseconds *= 1000;
            return double.IsFinite(milliseconds);
        }

        private sealed record ReplayEntry(Guid? Id, string Label)
        {
            public override string ToString() => Label;
        }
    }
}
