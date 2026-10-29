# Changelog

Notable Ritsu changes are listed here. Dates use the ISO calendar; planned features are described in the [roadmap](ROADMAP.md).

## Unreleased

Local replay video rendering is planned for 0.2.5.

## [0.2.4] - 2026-10-29

### Added

- Restored the beatmap catalogue button in the top bar, with an offline placeholder. Online search, downloads, and sign-in remain unavailable.
- Local profiles show an estimated global rank for osu!standard, interpolated from the bundled September 2026 PP sample.
- Local profile owners can choose a profile picture from an image file on their device.

### Changed

- The local rank is displayed as a standalone profile statistic, without a redundant rank history graph.

### Fixed

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
