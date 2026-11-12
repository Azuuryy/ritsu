# Offline foundation validation

## User feedback on the previous build

The user reported that all four modes, score separation by profile, and the star counter worked. This feedback predates moving profiles to the top bar and the counter into the skin editor; those changes still need manual verification. Compatibility of new mods, scores, and replays with other clients also remains to be checked.

On October 29, 2026, the user reported an incorrect profile interface and a failure to load statistics in build 0.2.1. Logs identified an unsupported Realm query; the fix and new interface still need in-game verification.

## Historical foundation validation

Validation was performed on October 26, 2026, against the then-isolated foundation; it does not cover the later restoration of osu!lazer folder linking or osu!stable detection. Results and captures remain in `.cache/verification-a8060692388f42d5bfe881ef0798c8ec`. Reports distinguish the reference build, targeted checks, rendered gameplay, and restart. The user confirmed that startup worked.

## Checks

- Release build of Torii before modification, followed by launch in a separate verification host. The already-open Torii installation was not controlled.
- Targeted checks: token clearing, no connection or authentication, single request rejection, no HTTP transport creation, no real-time connection, synchronous/asynchronous HTTP blocking, and preservation of Torii skin component identifiers.
- The communication detector is checked in a separate process with a probe request to `127.0.0.1:9`. This request belongs only to the detector check, not to the shipped client.
- Gameplay flow: import a synthetic map and skin, select a map, play Solo with NF and no artificial inputs, save a score and replay, export/import OSR, watch the replay, and open the map and skin editors.
- Score and replay data persistence was checked in a second process. That second scenario closed through the normal exit flow before automated playback attestation; playback after restart still needs confirmation in use.
- Actual rendered captures were inspected separately from functional assertions.
- The full flow completed in 48 seconds with 11 checks passing. Watching the replay in the same session is verified.
- Adaptations were successfully applied to a clean copy of the pinned Torii commit; no legacy publishing workflow remained active.

## Network check scope

`NetworkAudit` records .NET HTTP request start, DNS resolution, and socket connect/accept events. Reports include the process ID and event counts. This is not a capture of every system packet or general proof about all third-party native code.

The full flow recorded zero .NET network events. `tools/Measure-ClientNetwork.ps1` also observed the process and its children: no TCP/UDP sockets were seen (25 observations in the first controlled flow, then 5 during restart; the only observed child was `conhost.exe`). Windows polling may miss a brief connection between samples.

The client blocks framework requests before transport, does not instantiate an authenticated API provider, and disables known external integrations. The check does not rely on firewall rules. Offline operation describes the running client; compiling and retrieving dependencies use the Internet.

`Verify.ps1` builds and runs checks without opening the game. Graphical scenarios are available explicitly with `-IncludeGameScenarios`. No new graphical launch is required to build or create the ZIP.

## Known limitations

- Torii identity remains visible and executables keep their Torii names.
- This historical validation does not cover Modosu tools added later. Tournaments and the new visual editor are not included.
- The automated flow validates the local pipeline with NF and misses; it is not a latency benchmark or skill test.
- The pinned base emits inherited build and dependency warnings. This milestone is not a general security certification.
- Interactive control was stopped with Escape after the reference launch. Later checks use automated scenarios and engine captures without window control.


## Optional Torii catalogue — 2026-11-04

Debug compilation, Bootstrap, catalogue transport checks and existing offline checks passed. An isolated game session displayed live catalogue covers, loaded and played a native audio preview, and downloaded and imported Kenji Ninuma - DISCO★PRINCE through Torii into a separate test library. This does not attest playback of every provider's preview or compatibility with every archive. Torii-exclusive maps without a preview URL have no audio-preview button. The catalogue intentionally uses the network; the offline checks concern the other disabled online services and local-play path.


## Catalogue layout and details — 2026-11-04

An isolated game session verified centred and enlarged cards, context-menu entries for osu! and Torii links, loading of the native detail page from Torii, selection of a valid ruleset and difficulty, and both native download variants on a beatmap with video. This check verified the two choices in the UI; it did not download both variants of that video map. Existing download/import validation is recorded above.

## Library and everyday play — 2026-11-08

- The Windows desktop client built successfully and Bootstrap checked the bundled client and pinned framework and resources revisions.
- 50 targeted tests passed. Coverage includes keyboard completion in both Song Select layouts, restoring a pinned search in one update, native collection removal and undo, smart collections based on the active profile's local week, private data persistence, queue selection, session preset restoration, replay seeking, and applying target BPM using a selected timing section.
- The graphical smoke and restart scenarios passed in portable local storage and in a separate linked test directory. Each run imported the test map and skin, recorded a solo score, exported and re-imported its OSR, and opened the replay. Replay playback was verified again after restarting each instance. The audit recorded no HTTP, DNS or socket events in the game scenarios. The linked scenario uses a test directory through the shared storage path.
- The broader filter tests reported one failure on the existing status range assertion for `status>=r`. The assertion expects four statuses and receives five, including Torii's extra ranked status.

Run the local graphical scenario with `.\Verify.ps1 -IncludeGameScenarios`. Add `-LinkedLibrary` to use the linked test directory.

## Beatmap variants — 2026-11-08

- Targeted `ModosuSpacingTest` checks passed for selective No Sliding, settings round-trip and legacy defaults, as well as a capped jump followed by a rapid sequence. A rapid sequence is detected as at least three consecutive circles at intervals no longer than half a beat; this heuristic and edge clamping need gameplay validation on real charts.
- These automated checks do not validate replay playback, compatibility with other clients, or behavior in a linked library for the new variant settings.
- RandomV2 tests checked seeded pattern selection and settings round-trip, stable timings and phrase endpoints, and bounded exit distances at two note densities. The visual test checked that Custom angle reveals the multi-select control, two selections can be toggled together, and resetting clears the mask. Gameplay and replay playback of patterned maps remain to be tested.
- The RandomV2 angle and pattern controls were checked together. The UI test selected Octagon and Ladder, verified that the base angle was hidden, and restored the previous 90-degree setting by clearing the selection. Targeted generation tests confirmed the octagon turns and settings round-trip.

## RandomV2 and osu!standard variants — 2026-11-08

- Bootstrap checked the synchronized client patch and the Windows desktop client compiled. The 35 targeted tests passed. Coverage includes seed retry and isolation from Song Select, preview-to-solo position equality, the RandomV2 pattern settings UI, and OSR round trips with no transformation version and with versions 1 and 2. An unsupported version is rejected. A native chart was checked for mutations while applying Swing to its playable copy.
- The five conversion mods passed targeted generation checks. Gameplay test scenes checked Decoy Circles with Autoplay and a clicked decoy, and Directional Flicks with a recovered entry and an expired hit window.
- `Verify.ps1 -IncludeGameScenarios` passed with isolated local storage and a separate linked test library. The scenario saved a RandomV2 v2 score, exported and re-imported its OSR, and reconstructed the same playable positions from the saved replay both before and after restarting. The scenario used a fixture with No Fail and no synthetic gameplay input. The audit recorded no HTTP, DNS or socket attempts in the game scenarios.
- Playability on real charts and cross-client compatibility with Torii and osu!lazer remain checks for promotion to `main`.
- 2026-11-09: 14 variant tests and the mod selection UI test passed. A variant with only a hidden setting keeps the customisation panel closed. Swing and Reversed Sliders produced the same chart in both orders. Bootstrap and the desktop build passed after synchronising the client patch.
- On 2026-11-09, 17 variant tests and the mod settings UI test passed. Coverage includes decoy interval and appearance settings, both styles in judgement scenes, and the hidden Phrase Mirror identifier in legacy mod settings. Bootstrap and the desktop build passed.
- On 2026-11-09, 35 osu!standard tests and the mod selection UI test passed after recategorisation. Half Read changed AR 9 to 4.5 on the playable chart and preserved OD. Tests checked the retired mod identifiers, new categories, origin badges and icon coverage for selectable osu!standard mods. Bootstrap and the desktop build passed with the bundled icon assets.
- On 2026-11-09, 16 variant tests and the mod selection UI test passed with One Eye. Hard Rock and Difficulty Adjust produced the same final AR in both mod orders, in Song Select and on the playable chart. The previous HFR identifier remains readable. Bootstrap and the desktop build passed.
- On 2026-11-09, 10 targeted tests passed. Decoy Circles loaded with the Classic skin and applied one miss on a decoy hit. An untouched decoy remained neutral. Directional Flicks remained visible in search and could not be activated by click, Enter or Select All. Bootstrap and the Windows desktop build passed.
- On 2026-11-09, 27 targeted variant tests passed. Coverage includes both decoy appearances across Argon and Classic, hidden judgement feedback on ignored decoys, and star and PP calculations with zero, five or twenty decoys. Bootstrap and the Windows desktop build passed. The custom skin in the screenshot was not tested in a manual run.
