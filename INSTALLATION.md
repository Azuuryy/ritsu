# Installation

## Download and play

Download the Windows x64 package from [GitHub Releases](https://github.com/Azuuryy/ritsu/releases), extract it into a new folder and open **Launch Ritsu.cmd**. Do not put the package inside an existing Torii or osu!lazer installation.

On first launch, choose the detected osu!lazer folder, select one manually, or keep the default local storage in `%APPDATA%\Ritsu`. Linking uses the shared data directly: maps, scores, skins and collections may be visible in both clients, and Ritsu never migrates or deletes that data automatically. Avoid running both clients against the same data at the same time.

No account, service or network access is required to play. Online services remain disabled; see [GUIDE.md](GUIDE.md) for the complete offline behaviour.

## Import content

Drag `.osz` maps, `.osk` skins and `.osr` replays into the window, or use **Settings → Maintenance → Import files**. A replay needs its beatmap to be available locally. Original archives are kept after import.

## Build from source

Requirements: Windows x64, Git and the .NET 10 SDK (validated with 10.0.103; the packaged runtime is 10.0.3). Downloading source and dependencies requires Internet access on the first run; the resulting client works offline.

Run the full pipeline without opening the game:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Pipeline.ps1
```

To prepare and build separately:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bootstrap.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Dependency revisions are pinned in `dependencies.json`. The repository contains the pinned Torii source and its offline adaptations. `Bootstrap.ps1` verifies the bundled source and the patches, restores the framework and resources from their pinned revisions, and does not overwrite local changes. `Build.ps1` creates a self-contained Windows distribution with native libraries and a SHA-256 checksum in `artifacts/`. Caches and logs stay in `.cache/`.

Developer checks can be run on their own:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify.ps1
```

This builds and runs offline checks without opening the game. Optional graphical scenarios run only with `-IncludeGameScenarios`; they require Python 3, open a window and use a separate test profile. `Build.ps1` and `Pipeline.ps1` never launch the game.

## Package every platform

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Package.ps1
```

This publishes Windows, Linux and macOS builds and writes the archives and their `.sha256` sidecars to `artifacts/`. Only the Windows package is launched and validated on Windows; the Linux and macOS packages are cross-published previews, and the macOS package is unsigned. See [artifacts/README.md](artifacts/README.md) for the outputs, options and release publishing steps.

## Update and remove

Close the game and extract a new package into a different folder; automatic updates are disabled and no system file associations are installed. Removing the client folder does not delete your data. Local storage is in `%APPDATA%\Ritsu`; a linked osu!lazer folder is left untouched.
