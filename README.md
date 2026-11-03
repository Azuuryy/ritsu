# Ritsu

[![Discord](https://img.shields.io/badge/Discord-Join%20the%20server-5865F2?style=flat-square&logo=discord&logoColor=white)](https://discord.gg/QESZQbgzBv)

Ritsu is a standalone osu!lazer client for local play, built on Torii. It keeps osu!'s gameplay and interface, adds local profiles and the Modosu training mods, and runs without an account or an Internet connection.

> **Current milestone: offline foundation.** Authentication, leaderboards, downloads, updates, chat, presence, telemetry and other online services are intentionally disabled. Later milestones are described in [ROADMAP.md](ROADMAP.md) and are not implemented yet.

<!-- Banner and screenshots can be added here. -->

## Features

### Local play

- All four osu! rulesets, played and scored locally.
- Local scores and replays, with `.osz`, `.osk` and `.osr` imports by drag and drop or from **Settings → Maintenance → Import files**.
- Link an existing osu!lazer folder and use its maps, scores, skins and collections directly, without copying; or keep local storage in `%APPDATA%\Ritsu`.
- osu!stable detection and local import.
- Local play and the editors work without a connection.

### Profiles and progress

- Multiple local profiles with stable IDs, switchable from the top bar.
- Local PP estimates per profile, with an option to include Relax and Autopilot.
- Profile pictures, country flags and custom flag images.
- Local result rankings after a score: position among the profile's best plays, rank on the map, and PP gained.
- Release notes readable offline from the update notification and the settings footer.

### Beatmap browsing

- Sort beatmaps by perfect-play PP or farmability, group them by perfect-play or profile-earned PP, and filter by PP range.
- Perfect-play PP values use a persistent cache and can be disabled to reuse cached values only.
- Local leaderboards sortable by PP, with reversed sorting on the middle mouse button.
- Search history and favourites in the song select search box.
- Modded star rating shown on score rows when difficulty-changing mods are used.

### Mods

- Modosu training mods integrated into the shared osu! mod list: Metronome, Spacing Adjust, Fun Spacing, Circle Generation, No Sliding, RandomV2 and Miss Challenge.
- JSON mod presets, a live star counter for the skin editor, and replay settings for RandomV2 and Spacing Adjust.

### Data and safety

- A linked osu!lazer folder is used in place; Ritsu does not copy, migrate or delete it automatically.
- Packages ship the client, framework, resources and Modosu licences and notices.

## Status

The current version is in [`VERSION`](VERSION) and release history in [`CHANGELOG.md`](CHANGELOG.md). What has actually been verified, and what still needs in-game checks, is documented in [VALIDATION.md](VALIDATION.md). Compatibility of new mods, scores and replays with other clients has not been verified.

## Getting started

See [INSTALLATION.md](INSTALLATION.md) for download, build and packaging instructions, and [GUIDE.md](GUIDE.md) for first steps in the client.

## Documentation

- [INSTALLATION.md](INSTALLATION.md) — install, build, verify and package.
- [GUIDE.md](GUIDE.md) — quick start, profiles, training and offline behaviour.
- [ROADMAP.md](ROADMAP.md) — milestones and planned work.
- [VALIDATION.md](VALIDATION.md) — what has been validated and known limitations.
- [CONTRIBUTING.md](CONTRIBUTING.md) — setup, patch rules, commits and pull requests.
- [CHANGELOG.md](CHANGELOG.md) — release notes.
- [artifacts/README.md](artifacts/README.md) — release assets, publishing steps and Discord announcements.

## Origin and credits

- [**Torii**](https://github.com/ShikkesoraSIM/torii-osu), by ShikkesoraSIM and its contributors: the foundation for this client, its flow, and its interfaces.
- [**osu!**](https://github.com/ppy/osu) / [**osu!framework**](https://github.com/ppy/osu-framework), by ppy and their contributors: the engine, gameplay, and original tools.
- [**Modosu**](https://github.com/Azuuryy/Modosu), maintained by the user.
- [**MOsu**](https://github.com/p-720/mosu), by p-720 and its contributors: the origin of the Modosu work.

Contributions from these projects remain credited to their original authors. Client, framework, and resource notices are preserved. The Metronome, Spacing Adjust, Fun Spacing, Circle Generation, No Sliding, RandomV2, and Miss Challenge mods are adapted from Modosu/MOsu; their GPL-3.0 license is kept in [`LICENCE-Modosu.txt`](LICENCE-Modosu.txt) and in the package. New features still need in-game validation.
