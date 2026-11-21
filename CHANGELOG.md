# Changelog

Notable Ritsu changes are listed here. Dates use the ISO calendar; planned features are described in the [roadmap](ROADMAP.md).

## Unreleased

## [0.5.1] - 2026-11-21

### Added

- Open the same collection manager from the orange **Collections** footer button, **Options**, the collection dropdown or a beatmap’s context menu. Add or remove a difficulty or its entire set. A dark-green background marks collections that already contain the selected difficulty.
- Modern V2 results now include a **Play progression** graph in the score details.
- Show or hide difficulty statistics in the mod picker from Gameplay settings or by right-clicking the statistics.

### Changed

- Score simulation now uses the Modern V2 results layout, with editable accuracy, combo and judgement counts beside estimated PP. Open it from **Simulate** in Song Select, with **F4** by itself, or with the existing commands.
- In the local library, **Unranked** marks maps excluded from the active profile’s PP. Maps with an unknown, Unranked or Blacklisted source status display as **Approved** when included. The original status is preserved.
- Difficulty Adjust supports wider settings in extended mode and retains historical values in saved scores. Target BPM can enable extended limits to preserve the original approach rate.

### Fixed

- Song Select ranking badges and Modern V2 results now use the inspected score’s mods and settings for star difficulty, including Spacing Overtime and Random. The ranking badge appears when the displayed star rating changes.
- PP calculations in Song Select report progress more frequently and continue after an individual calculation is cancelled.
- Windows development builds open the same Ritsu database as packaged builds when using the same storage.

## [0.5.0] - 2026-11-18

### Added

- Song Select now has search history, favourites, named pinned searches and smart collections. Pinned searches preserve filters, collection, sort and group. Use **Ctrl+Left Alt** for inline filter suggestions. The sort, group, ranking scope and collection menus support pinning and reordering.
- The shared collection menu supports search, multi-selection, subgroups and undo. Mark scores for analysis from Song Select, your profile or results. The `pbmisses` and `playedthisweek` filters use the active profile.
- Open the local command palette with **Ctrl+Shift+P** and the play queue with **Tab**. Save session presets, simulate scores across all four rulesets, set a target BPM and add private difficulty memos with replay timestamps.
- Assign a score from **All profiles** to an existing or new profile, transfer assigned scores between profiles and explicitly import `.osr` files or linked MOsu/Modosu replays. Standard replay exports use the destination profile name and the original replay can still be exported separately.
- Added osu!standard variants Streamline, No Sliding, Reversed Sliders, One Eye and Decoy Circles. Directional Flicks, Echo, Swing and Phrase Mirror remain readable for existing replays. Variant replays preserve settings, transformation version and seed.
- RandomV2 custom angles now support 15 optional patterns, mirrored responses and progressive spacing. Autoplay can show the numbered trajectory, and retries can reuse or replace the seed. No Sliding and Spacing Adjust offer additional targeting options.
- Local frame diagnostics capture timings and allocations for performance investigation.

### Changed

- Desktop packages bundle **.NET 11**. Standard development and published launches share the selected library and `torii.ini`. Close one Ritsu instance before opening another against the same library.
- Profile PP exclusions persist for future scores. Excluded maps display **Unranked** locally. Reincluding them restores their previous status, or **Approved** if already Unranked. The mod selector also reflects Autoplay, Cinema and the profile's Relax/Autopilot setting.
- Decoy Circles can display a number from **0 to 99** instead of the cross. The minimum gap can be reduced to a quarter beat for dense patterns.
- Shared difficulty calculations, background PP cache writes, lighter profile queries and a catalogue that loads only nearby cards reduce redundant work.

### Fixed

- Fixed the Realm crash when opening a beatmap set's PP menu, the profile score list getting stuck loading, profile scope resetting at restart and incorrect accuracy in `/profile`.
- The Torii catalogue skips pages without new results while a new cursor is available. It remains accessible without signing in.
- Fixed selecting circles and sliders in the editor when their drawable is temporarily unavailable.
- Difficulty memos open replays at the saved timestamp and show an error when the map or replay is unavailable.

### Security

- Updated MessagePack and SignalR to address the advisories reported in the previous build.

Playback of genuine MOsu replays and identity handling in other clients still need confirmation. The Linux crash reported after long sessions remains under investigation. See [VALIDATION.md](VALIDATION.md) for validation scope.

## [0.4.0] - 2026-11-05

### Added

- Browse the Torii beatmap catalogue with search, covers, audio previews and beatmap details, then download and import beatmaps through the existing importer.
- Local profiles show estimated playtime, with a setting to calculate it from full map length or drain length.

### Changed

- Returning from a score opened in a local profile restores song selection on that score's beatmap and difficulty.
- The temporary view of all difficulties in a beatmap set retains the latest selected difficulty when leaving the view; changing sort keeps the view open, while changing group or collection closes it.

## [0.3.1] - 2026-11-03

### Added

- Exclude individual local scores from profile PP in the profile or beatmap ranking, or exclude all existing scores for a beatmap or beatmap set from song select, while retaining their scores and replays.

### Changed

- Rename the Spacing Overtime mod acronym from SO to SOT.

### Fixed

- Load saved skin layouts when their component types refer to different client assembly versions.

## [0.3.0] - 2026-11-03

### Added

- Local result rankings show profile best plays, map rankings and PP gains after a score.
- Sort beatmaps by perfect-play PP or farmability, group them by perfect-play or profile-earned PP, filter by PP, and configure grouping intervals. Perfect-play PP calculations use a persistent cache and can be disabled.
- Sort local scores by PP and reverse sorting with the middle mouse button.
- Local profiles support individual avatars and country or custom image flags.
- View Ritsu release notes offline from the update notification or the settings footer.
- Scores display the star rating calculated with difficulty-changing mods.

### Changed

- Map PP calculations load progressively and account for selected difficulty-changing mods.

### Removed

- Local replay video recording and the FFmpeg experiment. The original Torii o!rdr integration, replay sharing and score screenshots are retained.

## [0.2.4] - 2026-10-29

### Added

- Restored the beatmap catalogue button in the top bar, with an offline placeholder. Online search, downloads, and sign-in remain unavailable.
- Local profiles show an estimated global rank for osu!standard, interpolated from the bundled September 2026 PP sample.
- Local profile owners can choose a profile picture from an image file on their device.

### Changed

- The local rank is displayed as a standalone profile statistic, without a redundant rank history graph.

### Fixed

- Fixed a startup crash caused by profile avatar initialization.
- New local installations use `Guest` for the initial profile name.
- New local scores use the active profile's name. Scores already associated with a Ritsu profile display its current name without changing shared score data.

The profile changes in this release still require manual in-game verification. Compatibility of scores and replays with other clients has not been verified.

## [0.2.3] - 2026-10-29

### Added

- The complete set of Modosu training mods in the shared osu! ruleset list: Metronome, Spacing Adjust, Fun Spacing (Spacing Overtime), Circle Generation, No Sliding, RandomV2, and Miss Challenge.
- A consistent presentation for Modosu mods, with dedicated styling, settings, and tooltips; replay settings for RandomV2 and Spacing Adjust.
- JSON mod preset import and export, a live star counter that can be added to the HUD in the skin editor, and a replay progress setting.

### Changed

- Mods are integrated into the existing osu! ruleset rather than a separate ruleset; the shared mod list and shared score/replay behavior are preserved.
- Modosu mods now use a unified visual style alongside Torii-exclusive mods, with a dedicated border and a reusable style for future Ritsu mods.

## [0.2.2] - 2026-10-29

### Added

- Multiple local profiles can be created, selected, renamed, and deleted; each profile has a stable ID.
- A profile page with recent scores and best performances by mode, local PP estimates, and a per-profile option to include Relax and Autopilot in PP.
- Local leaderboards can be filtered to the active profile or show all profiles, including historical scores without a profile association.

### Changed

- New scores are associated with the active profile through separate local metadata. Existing scores remain in shared storage and are not associated with any profile automatically.

### Fixed

- Profile statistics now load with a Realm-compatible query.
- Local profile interface display and navigation.

On an earlier build, the user reported that all four modes, profile-based score separation, and the star counter worked. The revised interface and loading fixes still need manual verification. Compatibility of mods, scores, and replays with other clients has not been verified.

## [0.2.1] - 2026-10-29

### Added

- Local profiles for all four modes, with new-score association, local leaderboard filters, and configurable PP estimates.
- Miss Challenge mod with a manual limit or a limit based on the best local score.
- JSON mod preset import and export, an in-game star counter, and a replay progress setting.
- A dedicated border for Modosu mods and a style prepared for future Ritsu mods.
- An option to skip directly to the end of breaks.

### Changed

- Access profiles from the top bar, with a page for statistics, scores, PP history, and local rankings.
- The star counter can be added and positioned freely in the skin editor.
- Modosu mods are styled more like Torii-exclusive mods, and the all-profiles filter is easier to find.

On an earlier build, the user reported that all four modes, profile-based score separation, and the star counter worked. Moving profiles and the counter, visual changes, and compatibility with other clients still need manual verification.

## [0.2.0] - 2026-10-28

### Added

- Optional linking to an existing osu!lazer folder, with local detection and direct access to shared data; local Ritsu storage remains available.
- The first set of Modosu mods in the shared osu! ruleset list: Metronome, Spacing Adjust, Fun Spacing, Circle Generation, No Sliding, and RandomV2.
- Replay settings for RandomV2 and Spacing Adjust.
- The Modosu license is included in the distributed package.

### Changed

- The metronome menu offers rates of 0.25×, 0.5×, 1×, 2×, and 3×.
- Metronome subdivision calculations avoid slowing down ticks at higher rates.
- Linked-data controls and first-run setup let users explicitly choose shared osu!lazer storage.

### Fixed

- Restored local detection and import from osu!stable installations.
- Validation fixtures are found in their source folder, `tools/fixtures`.
