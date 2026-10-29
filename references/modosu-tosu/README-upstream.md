# Native Modosu performance for tosu

The Modosu ruleset exports PP and difficulty values calculated by osu! itself. The matching tosu fork reads this local export for custom rulesets instead of simulating their mods with a vanilla calculator.

## Build and load

1. Build `osu.Game.Rulesets.Modosu/osu.Game.Rulesets.Modosu.csproj` with the .NET 10 SDK.
2. Close lazer and install the built `osu.Game.Rulesets.Modosu.dll` in its rulesets directory, keeping a backup of the previous DLL.
3. Start the modified tosu fork and restart lazer. Start a new Modosu play or replay.

The running game must load the new ruleset DLL. Editing the source or restarting only tosu does not update an already loaded ruleset.

## Values

- Native live PP, including its osu! aim/speed/accuracy/flashlight components.
- Native live and full-map star difficulty and maximum combo.
- Final PP from the same value Modosu saves on the results path.
- Full-map stars and perfect-score (SS) PP from lazer's difficulty cache as soon as a map is selected, including edits to existing mod settings.
- Native FC and best-remaining-play PP projections during gameplay and on results.
- The actual post-mod gameplay beatmap and concrete Modosu mod types are used. FSA, SA and similar transforms are not replaced with a vanilla acronym.

The SS maximum is fixed for a map/ruleset/mod configuration. The best-remaining-play projection keeps past judgements and predicts perfect remaining judgements, using the largest combo still reachable. The FC projection also replaces misses with greats, repairs ticks/tails and uses full combo, while retaining existing OK/Meh judgements. Both hypothetical scores are evaluated by the native Modosu calculator. A full judgement history is required to distinguish judged slider tails from pending tails; projections remain unavailable after replay header seeks that omit this history. The SS maximum remains available. Arbitrary historical results that were not played in the current session are not exported.

## Local protocol

Modosu publishes a versioned JSON frame to `%TEMP%/modosu-tosu/<game-pid>.json` (`Path.GetTempPath()` on other systems). A single background writer coalesces updates and atomically replaces the file. No socket or additional port is used. Difficulty is calculated asynchronously; live snapshots are published at most every 100 ms.

An independent background writer publishes `<game-pid>.selection.json` with a 250 ms heartbeat. Its values come from `BeatmapDifficultyCache`, like the game's own selection display. Cancellation and a configuration key prevent an old calculation from replacing a newer selection. Switching away from Modosu invalidates the selection export. Tosu requires the exact process lifetime, MD5 and full mods/settings JSON and a heartbeat no older than two seconds. The maximum is exposed through `performance.accuracy[100]` (v2) and `menu.pp[100]` (v1); stars use the existing beatmap stats fields. Gameplay frames also include their own native SS maximum, so it is retained throughout a play even if the selection changes elsewhere.

During selection, the native export also supplies the mods/settings: Tosu's memory menu mapping can omit custom mods such as FSA. This synchronisation happens before metadata selection and still requires a fresh frame matching the process lifetime and map. During gameplay and results, full mods/settings continue to be checked against the score read from memory.

## Overlay compatibility

A fresh native selection or gameplay frame also identifies Modosu's osu-style judgements for presentation. The raw ruleset stays -1 internally and in v2. Grade calculation, timing-error routing and legacy hit conversion use the compatible osu mode; the v1 and StreamCompanion mode fields expose 0 so older counters can interpret them. This presentation mode expires with its map-specific native identity and never chooses the PP/difficulty calculator.

The installed reComfortaa 1.003 overlay had an independent subscription bug: its `applyFilters` request omitted `100`, `50` and `0` while its renderer read those fields. The local script includes these keys now. `packages/tosu/patches/recomfortaa-hits.patch` in the Tosu fork preserves this fix for reapplication if the overlay is updated. Reload an already open reComfortaa page or OBS source to load the changed script. Visual verification of Simplistic, reComfortaa and BTMC confirmed matching live hit counts; the first two also displayed the restored grade. Static analysis introduced no new TypeScript diagnostics.

Schema 1 includes the process ID and start time, session ID, map MD5, full mods JSON, retries, play time, basic hit counts, score date, PP and stars. A null PP value means calculation is not ready; a numeric zero is a valid result.

Tosu rejects mismatched process lifetimes, maps, mods/settings, retries, future hit counts, stale live frames and different results. Live frames expire after two seconds without a heartbeat. Results require the exact score date and hit counts, so the results screen can remain open without an active player heartbeat. Starting a new producer session prevents older asynchronous result tasks from replacing its frame.

Lazer prepares a cloned score for results; the gameplay score retains its initial date. The result hook reads the completed `Player.prepareScoreForDisplayTask` immediately in `OnShowingResults`, before that same prepared score is displayed. This private field is version-dependent and should be checked when updating lazer. If it becomes unavailable, the receiver still rejects a mismatched date.

## Validation

- `ModosuPerformanceExportTest` compares exported PP to the game's native calculator and verifies FSA changes full-map difficulty.
- `TestSceneNativePerformanceExport` exercises the producer and actual atomic file without a HUD counter.
- `TestSceneNativePerformancePlayer` exercises the normal player hook with FSA.
- `TestSceneNativePerformanceResults` checks the actual results hook against the prepared score's date, while the gameplay score still has its initial date.
- `TestSceneNativePerformanceSelection` checks SS PP and stars before gameplay and recalculation after editing the same FSA mod instance.
- The tosu fork has `packages/tosu/tests/modosuPerformance.test.ts` for protocol matching, expiry, retries, null/zero values and corrupt files.

Live validation on 2026-10-16 with NF + FSA confirmed score 240376, maximum combo 237, full difficulty 7.194230470194993 and final PP 129.25152630723764. Tosu's results PP matched the native export exactly; its rounded live fields showed 129.25 PP and 7.19 stars. Map metadata also remained available (CS 3.8, AR 9.6, OD 9.3, HP 5.2, BPM 110).

Selection and projection validation on the same date confirmed both v1 and v2 output: quaver with FSA showed 5.998516964854275 stars and 272.5996851222463 SS PP; Iza, Mairimasu showed 5.805907055135776 stars and 249.7805107278162 SS PP. During Immortal Flame with NF + FSA, SS PP stayed at 284.3114472498911 while the native FC and remaining-play projections diverged after a miss (272.8429904528989 versus 242.1502696237283). Targeted tests: five native tests, fourteen Tosu protocol tests, four visual scenes, plus the actual Tosu state-mapping harness. TypeScript diagnostics remain at the pre-existing baseline (171 errors, no new errors).
