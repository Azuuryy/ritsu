# Changelog

Notable Ritsu changes are listed here. Dates use the ISO calendar; planned features are described in the [roadmap](ROADMAP.md).

## Unreleased

### Added

- A Spotify-style queue panel opens with Tab in solo Song Select, with local covers, queue controls, and shared session queue actions.
- The command palette and local library are split into dedicated modules. Local map, score, profile, collection, and session actions are available through the command registry.
- A dedicated score simulator uses lazer performance calculators for osu!standard, taiko, catch, and mania, with native mod selection and configurable judgements.
- Local frame profiling can export bounded frame and operation traces, with benchmark scenarios for baseline and instrumented runs.
- Documentation inventories GuraBot and Bathbot commands, maps local equivalents and service dependencies, and records the client/framework audit and Ronnia feasibility study.

### Changed

- `/bpm` can preserve the map's unmodded AR while changing playback speed, and `/compare`, `/simulate`, and `/visualize` expose local score and map workflows.
- Beatmap queue and simulator entry points are shared between the command palette, Song Select, and mod selection.


## [0.5.0] - 2026-11-12

### Added

- Song Select search history, favourites and named pinned searches are available from the search bar, with a slanted popover aligned to the bar and a clear action beside recent searches. Pinned searches restore their filters, collection, sort and group; right-click one to rename it, replace its settings or create a smart collection.
- Filter suggestions appear at the search cursor with **Ctrl+Left Alt** and can be chosen with the arrow keys, Tab or the mouse.
- Pin entries and reorder the sort, group, ranking scope and collection menus, using manual, alphabetical or most-used order.
- A shared collection menu in Song Select, profile scores and results supports search, multi-selection, user-created subgroups and undo for removal. Scores can be marked for analysis and appear in the active profile's score page.
- Smart collections refresh from a saved search. The `pbmisses` and `playedthisweek` filters use the active profile's personal bests and play history, with weeks starting on Monday in local time.
- A command palette (**Ctrl+Shift+P**) offers `/recent`, `/mods`, `/queue`, `/session` and `/bpm` actions, alongside a session play queue and session presets that restore mods, speed, skin, HUD settings and the starting search.
- Per-difficulty private memos can link a local replay at a timestamp. A target BPM dialog applies a playback rate from a chosen timing section on maps with changing tempo.
- Transfer a local score to another profile from either leaderboard; PP inclusion follows the score and the destination profile's beatmap exclusions still apply.
- osu!standard beatmap variants: Streamline (formerly Half-Stream), No Sliding, Reversed Sliders, One Eye, Decoy Circles and the work-in-progress Directional Flicks. Echo, Swing and Phrase Mirror are no longer selectable but remain readable for existing replays.
- RandomV2 custom angle offers 15 optional patterns, separate jump, stream and burst selections, mirrored responses and progressive spacing. A numbered trajectory is available in the Autoplay preview, and pause or fail offers **Retry same seed** and **New seed**.
- Variant scores and OSR exports store their settings, transformation version and seed. Unsupported versions are rejected with a notification.
- Decoy Circles uses the skin's circle with a centered white cross, and its local difficulty and PP bonus scale with the number of placed decoys.
- No Sliding and Spacing Adjust can target short, long or repeating sliders, including streams, head-only taps, head-and-tail taps and a maximum jump distance.

### Changed

- A beatmap set's PP inclusion state is resolved from its local difficulties.

### Fixed

- A replay opened from a difficulty memo starts at the linked timestamp, and an unavailable replay or beatmap is reported in a notification.

The library, variant and profile changes still require the manual in-game checks listed in [VALIDATION.md](VALIDATION.md). Compatibility of the variant mods, scores and replays with other clients has not been verified.

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
