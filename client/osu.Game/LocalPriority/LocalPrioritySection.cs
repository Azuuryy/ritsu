// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Settings → "Ritsu" (its own tab, right under Online). Local profiles themselves are managed on their profile page
    /// and in the switcher (right-click your name in the top bar), not here.
    /// </summary>
    public partial class LocalPrioritySection : SettingsSection
    {
        public override LocalisableString Header => LocalPriorityPolicy.GAME_NAME;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { @"local", @"offline", @"profile" });

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = FontAwesome.Solid.UserShield,
        };

        public LocalPrioritySection()
        {
            Children = new Drawable[]
            {
                new LocalProfileBehaviourSettings(),
                // Ritsu never signs in to an online account, so the online lookup settings are left out.
            };
        }
    }

    /// <summary>
    /// How local profiles are created and deleted.
    /// </summary>
    public partial class LocalProfileBehaviourSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Local profiles";

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[]
        {
            @"delete", @"confirm", @"sync", @"mods", @"account", @"create", @"switch", @"accuracy", @"ranked", @"unranked", @"pp",
        });

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        private readonly IBindable<LocalProfile?> activeProfile = new Bindable<LocalProfile?>();

        private SettingsButtonV2 importReplaysButton = null!;
        private SettingsButtonV2 exportTopReplaysButton = null!;
        private SettingsButtonV2 exportAllReplaysButton = null!;
        private SettingsButtonV2 rebuildStatisticsButton = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            activeProfile.BindTo(manager.ActiveProfile);
            activeProfile.BindValueChanged(p =>
            {
                importReplaysButton.Enabled.Value = p.NewValue != null;
                exportTopReplaysButton.Enabled.Value = p.NewValue != null;
                exportAllReplaysButton.Enabled.Value = p.NewValue != null;
                exportTopReplaysButton.Text = $@"Export top {LocalProfileManager.TOP_REPLAYS_EXPORT_COUNT} replays of {(p.NewValue != null ? $@"""{p.NewValue.Name}""" : @"the active profile")} to a folder…";
                exportAllReplaysButton.Text = $@"Export all replays of {(p.NewValue != null ? $@"""{p.NewValue.Name}""" : @"the active profile")} to a folder…";
                importReplaysButton.Text = p.NewValue != null
                    ? $@"Import replays (.osr) from a folder as ""{p.NewValue.Name}""…"
                    : @"Import replays (.osr) from a folder (switch to a local profile first)";
            }, true);
        }

        [Resolved]
        private IDialogOverlay? dialogs { get; set; }

        private void requestRemoveOldPlays()
        {
            var (scores, plays) = manager.CountPlaysBefore2000();

            if (scores == 0 && plays == 0)
            {
                dialogs?.Push(new LocalPriorityInfoDialog(@"Nothing to remove", @"No local profile has plays dated before the year 2000."));
                return;
            }

            dialogs?.Push(new RemoveOldPlaysDialog(scores, plays, manager.RemovePlaysBefore2000));
        }

        private void rebuildStatistics()
        {
            rebuildStatisticsButton.Enabled.Value = false;

            manager.RebuildStatisticsFromScoresAsync().ContinueWith(t => Schedule(() =>
            {
                rebuildStatisticsButton.Enabled.Value = true;

                if (t.IsFaulted)
                {
                    Logger.Error(t.Exception?.InnerException ?? t.Exception, @"Could not rebuild play statistics from saved scores");
                    return;
                }

                var (profiles, scores) = t.GetResultSafely();

                dialogs?.Push(new LocalPriorityInfoDialog(@"Play statistics rebuilt",
                    $@"Counted {scores:N0} saved score(s) of local profiles again; {profiles:N0} profile(s) got higher statistics. "
                    + @"Values only go up, so attempts recorded without a score (failed, quit or restarted) are kept."));
            }));
        }

        private partial class RemoveOldPlaysDialog : DangerousActionDialog
        {
            public RemoveOldPlaysDialog(int scores, long plays, Action remove)
            {
                HeaderText = @"Remove every play dated before 2000?";
                BodyText = $@"{scores:N0} score(s) of local profiles will be deleted and {plays:N0} recorded play(s) on such days forgotten. "
                           + @"Their dates are broken (usually 1 January 0001), which stretches the play history back to year 1.";
                DangerousAction = remove;
            }
        }

        private partial class LocalPriorityInfoDialog : PopupDialog
        {
            public LocalPriorityInfoDialog(string header, string body)
            {
                HeaderText = header;
                BodyText = body;
                Icon = FontAwesome.Solid.InfoCircle;
                Buttons = new PopupDialogButton[] { new PopupDialogOkButton { Text = @"OK" } };
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                importReplaysButton = new SettingsButtonV2
                {
                    Keywords = new[] { @"osr", @"replay", @"import", @"folder" },
                    Action = () =>
                    {
                        if (activeProfile.Value != null)
                            manager.BeginReplayImport(activeProfile.Value.Id);
                    },
                },
                exportTopReplaysButton = new SettingsButtonV2
                {
                    Keywords = new[] { @"osr", @"replay", @"export", @"folder", @"top", @"best" },
                    Action = () =>
                    {
                        if (activeProfile.Value != null)
                            manager.BeginReplayExport(activeProfile.Value.Id, true);
                    },
                },
                exportAllReplaysButton = new SettingsButtonV2
                {
                    Keywords = new[] { @"osr", @"replay", @"export", @"folder", @"all" },
                    Action = () =>
                    {
                        if (activeProfile.Value != null)
                            manager.BeginReplayExport(activeProfile.Value.Id, false);
                    },
                },
                rebuildStatisticsButton = new SettingsButtonV2
                {
                    Text = @"Rebuild play statistics from saved scores (every profile)",
                    TooltipText = @"Counts every saved score (replay) of every local profile again: play count, play time, hits, most played beatmaps "
                                  + @"and play history. Values only go up, so attempts recorded without a score are kept.",
                    Keywords = new[] { @"statistics", @"stats", @"rebuild", @"recalculate", @"repair", @"replay", @"play count", @"history" },
                    Action = rebuildStatistics,
                },
                new DangerousSettingsButtonV2
                {
                    Text = @"Remove scores whose beatmap difficulty can't be calculated",
                    Keywords = new[] { @"difficulty", @"broken", @"error", @"calculate", @"pp" },
                    Action = manager.RequestRemoveUncalculableScores,
                },
                new DangerousSettingsButtonV2
                {
                    Text = @"Remove every play dated before the year 2000",
                    Keywords = new[] { @"date", @"0001", @"history", @"broken", @"emergency" },
                    Action = requestRemoveOldPlays,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Delete profiles without asking (keeping their scores)",
                    Current = manager.DeleteKeepingScoresWithoutAsking,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Delete profiles without asking (together with their scores)",
                    Current = manager.DeleteWithScoresWithoutAsking,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Suggest syncing plays saved under the same name",
                    HintText = @"After creating or renaming a profile, offer to count plays already saved under its name "
                               + @"(online account, guest plays, older profiles) as its own. Always available from the profile's right-click menu.",
                    Current = manager.SuggestSyncingSameNamePlays,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Create profiles with mod rules",
                    HintText = @"Adds a second ""new profile"" button to the profile page and the switcher. It opens mod select first: "
                               + @"pick mods, then choose under ""Profile rules"" whether the profile blocks them, only allows them "
                               + @"(optionally only with the chosen settings) and/or selects them whenever you switch to it.",
                    Current = manager.ProfilesWithModRules,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Notify when switching profiles automatically",
                    Current = manager.AutoSwitchNotifications,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Switch profiles on beatmap start if rules are true",
                    Current = manager.SwitchOnBeatmapStart,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Calculate and show total hit accuracy",
                    Current = manager.ShowTotalAccuracy,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Count everything as ranked",
                    Current = manager.CountEverythingAsRanked,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Do not hide unranked scores when not counting them",
                    Current = manager.ShowNonCountingScores,
                }),
            };
        }
    }

    /// <summary>
    /// Whether local profiles may use the signed-in online account for read-only lookups (beatmaps, tags, leaderboards).
    /// </summary>
    public partial class LocalOnlineLookupSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Online lookups on local profiles";

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[]
        {
            @"leaderboard", @"beatmap", @"tags", @"download", @"online", @"account", @"ban",
        });

        [Resolved]
        private LocalProfileManager manager { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();
        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private readonly IBindable<LocalProfile?> activeProfile = new Bindable<LocalProfile?>();

        private SettingsItemV2 toggleItem = null!;
        private FormTextBox accountTextBox = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                toggleItem = new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Use the online account for lookups on local profiles",
                    Current = manager.LocalProfilesUseOnlineAccount,
                }),
                new SettingsItemV2(accountTextBox = new FormTextBox
                {
                    Caption = @"Online account used for lookups",
                    ReadOnly = true,
                })
                {
                    ShowRevertToDefaultButton = false,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            apiState.BindTo(api.State);
            localUser.BindTo(api.LocalUser);
            activeProfile.BindTo(manager.ActiveProfile);

            apiState.BindValueChanged(_ => Scheduler.AddOnce(updateStatus));
            localUser.BindValueChanged(_ => Scheduler.AddOnce(updateStatus));
            activeProfile.BindValueChanged(_ => Scheduler.AddOnce(updateStatus));
            manager.LocalProfilesUseOnlineAccount.BindValueChanged(_ => Scheduler.AddOnce(updateStatus), true);
        }

        private void updateStatus()
        {
            bool enabled = manager.LocalProfilesUseOnlineAccount.Value;

            toggleItem.Note.Value = new SettingsNote.Data(
                @"Warning: Ritsu is a modified, unofficial client. Sending requests to osu! servers with your account from it "
                + @"might get your account restricted or banned. Use at your own risk.",
                enabled ? SettingsNote.Type.Critical : SettingsNote.Type.Warning);

            // While a local profile is shown as the player, the signed-in online account is kept separately.
            var account = manager.IsLocalProfileActive ? LocalPriorityPolicy.OnlineAccountUser : localUser.Value;
            bool signedIn = account != null && account.Id > 1 && !LocalPriorityPolicy.IsOfflineProfileUser(account);

            switch (apiState.Value)
            {
                case APIState.Online when signedIn:
                    accountTextBox.Current.Value = $@"Signed in as {account!.Username}";
                    break;

                case APIState.Connecting:
                case APIState.Failing:
                    accountTextBox.Current.Value = @"Connecting…";
                    break;

                case APIState.RequiresSecondFactorAuth:
                    accountTextBox.Current.Value = @"Verification required: switch to the online account to finish signing in";
                    break;

                default:
                    accountTextBox.Current.Value = manager.IsLocalProfileActive && !enabled
                        ? @"Not used (local profile is fully offline)"
                        : @"Not signed in: switch to the online account and sign in first";
                    break;
            }
        }
    }
}
