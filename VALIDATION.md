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
