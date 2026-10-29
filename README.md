# Ritsu — Torii offline foundation

The first Windows x64 milestone is a standalone Torii client for local play. Torii's names and appearance are intentionally retained; renaming is deferred.

## Play

On Windows x64, run `Pipeline.ps1` to build the client and create `Ritsu-<version>-win-x64.zip` in `artifacts/`. Extract it and open **Launch Ritsu.cmd**. The current version is listed in [`VERSION`](VERSION), with release history in [`CHANGELOG.md`](CHANGELOG.md). Choose an existing osu!lazer folder to share its data, or keep the default local storage in `%APPDATA%\Ritsu`. No account, service, or network access is required to play. Do not put the package inside an existing Torii or lazer installation.

Import `.osz`, `.osk`, and `.osr` files by dragging them into the window or through **Settings → Maintenance → Import files**. Maps must be available locally to play their replays. An osu!lazer folder can be detected or selected manually and used directly, without copying. Map downloads are not available.

See [GUIDE.md](GUIDE.md), [ROADMAP.md](ROADMAP.md), and [VALIDATION.md](VALIDATION.md).

## Build

Requirements: Windows x64, Git, and the .NET 10 SDK (validated with 10.0.103; the packaged runtime is 10.0.3). Downloading source and dependencies requires Internet access; the resulting client works offline.

Run the full pipeline without opening the game:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Pipeline.ps1
```

To prepare and build separately:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bootstrap.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Dependency revisions are pinned in `dependencies.json`. The repository already contains the pinned Torii source and its offline adaptations. `Bootstrap.ps1` verifies it and restores the required framework and resources from their pinned repositories. It rejects unexpected revisions and does not overwrite local changes. `Build.ps1` creates a self-contained distribution with native libraries and a SHA-256 checksum. Caches and logs stay in `.cache/`.

Developer checks can also be run on their own:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify.ps1
```

This command builds and runs offline checks without opening the game. Optional graphical scenarios run only with `-IncludeGameScenarios`; they require Python 3, open a window, and use a separate test profile. `Build.ps1` and `Pipeline.ps1` never launch the game.

The client source retains Torii's history, with this milestone's changes on `codex/ritsu-offline`. Legacy workflows are kept with the `.disabled` extension and do not run on GitHub.

## Origin and credits

- [**Torii**](https://github.com/ShikkesoraSIM/torii-osu), by ShikkesoraSIM and its contributors: the foundation for this client, its flow, and its interfaces.
- [**osu!**](https://github.com/ppy/osu) / [**osu!framework**](https://github.com/ppy/osu-framework), by ppy and their contributors: the engine, gameplay, and original tools.
- [**Modosu**](https://github.com/Azuuryy/Modosu), maintained by the user.
- [**MOsu**](https://github.com/p-720/mosu), by p-720 and its contributors: the origin of the Modosu work.

Contributions from these projects remain credited to their original authors. Client, framework, and resource notices are preserved. The Metronome, Spacing Adjust, Fun Spacing, Circle Generation, No Sliding, RandomV2, and Miss Challenge mods are adapted from Modosu/MOsu; their GPL-3.0 license is kept in `client/LICENCE-Modosu.txt` and in the package. New features still need in-game validation.
