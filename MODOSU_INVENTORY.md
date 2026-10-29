# Modosu integration inventory

The local copy is at `C:\Users\natha\OneDrive\Documents\ChatGPT\Mosu clone\source`. Its `codex/offline-practice` branch is at commit `ef807e8df043323014a781ca74942c547986f56e`, matching the remote branch of the same name and descending from `master` (`5224a4d2dddfecd2a5bcc73e8828e3cb4c829fbe`). It also contains uncommitted changes to performance export to tosu; those changes are not on GitHub and were not included in the initial port.

Sources reviewed: [Modosu, Azuuryy](https://github.com/Azuuryy/Modosu) and [MOsu, p-720](https://github.com/p-720/mosu), revision `2df91468c545dbc1dfe01951e6f9f8d8b991f45d`. Modosu and MOsu identify GPL-3.0 as their license. The license is preserved in `client/LICENCE-Modosu.txt` and in the package.

| MOsu feature | Status in Ritsu | Follow-up |
| --- | --- | --- |
| Spacing Adjust and Fun Spacing | Ported to the shared osu! mod list; targeted tests cover reapplication and empty maps | Verify visually and in gameplay, including scores and replays. |
| Circle Generation and No Sliding | Ported to the shared list; targeted tests cover generation and slider conversion | Verify in gameplay and with replays. |
| RandomV2 | Ported to the shared list with replay settings; targeted test uses a fixed seed | Verify live settings and replays in game. |
| Miss Challenge | Ported to the osu! ruleset, with a manual limit or one based on the active profile's best score | Manual validation is still required. |
| Alternate, Relax, Autopilot, Single Tap | Torii variants are retained as requested by the user | No adaptation planned. |
| Metronome | Standalone mod ported with five selectable rates | Verify audio manually. |
| Skip breaks | Existing Torii feature retained, with an option to skip directly to the end of a break | Manual validation is still required. |
| Local profiles and PP | Profiles and new-score associations for all four modes; configurable local PP estimates | Manual validation is still required; existing scores are not associated automatically. |
| JSON presets | Preset import and export, with mapping for legacy ruleset names | Manual validation is still required. |
| Collections and map suggestions | Removed from scope by the user | No port planned for this step. |
| Live stars | In-game counter added using cumulative difficulty | Manual validation is still required. |
| Sharing mods through chat | Feature exists in MOsu | Deferred because chat and online services are disabled in the current milestone. |

In Modosu, its own mods are registered in `ModosuRuleset.GetModsFor`: Miss Challenge, Circle Generation, RandomV2, Spacing Adjust, No Sliding, Metronome, and Fun Spacing Adjust. Alternate, Single Tap, Relax, and Autopilot also have their own classes. The code includes a `LocalProfile` model, a `LegacyPracticeDataMigration`, collection and preset import processors, and JSON exports in `exports`. The native tosu export found in the local copy uses temporary JSON files; a reference copy is kept in `references/modosu-tosu/` and is not integrated into Ritsu.

Next step: the user must manually validate the new features, especially scores and replays in linked clients. Existing Modosu scores are not imported or modified.

Product decision: integrate mods into the existing ruleset and keep a shared list. The user will later check whether other clients can read scores and replays; compatibility for new mods has not yet been verified.
