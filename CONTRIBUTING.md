# Contributing to Ritsu

Ritsu is an osu!lazer client built on Torii for local play. The current milestone keeps all online services disabled: no authentication, downloads, updates, telemetry or external communications. Changes must preserve offline play.

Read [ROADMAP.md](ROADMAP.md) before starting and [VALIDATION.md](VALIDATION.md) for what has actually been verified. Features planned for later milestones must not be described as implemented.

## Ground rules

- Keep online services disabled. Do not add network requests, background telemetry, automatic updates or server integrations.
- Keep user data safe. Linking an osu!lazer folder uses the shared data directly; never migrate, overwrite or delete that data automatically.
- New Ritsu profiles start from scratch. Do not import or associate existing Modosu scores automatically.
- Preserve credits and licences from Torii, osu!, Modosu and MOsu in files you touch.
- Document only behaviour that has been verified, and distinguish historical validation from new work.

## Requirements

Windows x64, Git, the .NET 10 SDK, and Python 3 for packaging and the optional graphical scenarios. Downloading dependencies requires Internet access on the first run; the built client runs offline.

## Set up and build

```powershell
git clone git@github.com:Azuuryy/ritsu.git
cd ritsu
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bootstrap.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

`Bootstrap.ps1` verifies the pinned client source and restores the framework and resources. `Build.ps1` produces the Windows package in `artifacts/`; `Package.ps1` produces every supported platform. See [artifacts/README.md](artifacts/README.md).

Developer checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify.ps1
```

Optional graphical scenarios run only with `-IncludeGameScenarios`; they open a window and use a separate test profile.

## Repository layout

- `client/`: bundled Torii source, pinned by `dependencies.json`. It is not a submodule.
- `patches/`: the offline adaptations applied to the pinned client and framework revisions.
- `toriirefresh-framework/`, `toriirefresh-resources/`: framework and resources restored by `Bootstrap.ps1`.
- `tools/`: validation tooling and the artifact packager.
- `artifacts/`: generated release archives, ignored except for its README.

## Changing the client

Every change to `client/` must also be reflected in [`patches/client-offline.patch`](patches/client-offline.patch), then checked with `Bootstrap.ps1`. The patch is applied to the pinned upstream revision, so it must stay applicable in both directions:

```powershell
git apply --reverse --check --directory=client -- patches/client-offline.patch
```

Keep `dependencies.json` and `ritsu-source-bundle.json` consistent with the bundled revision. Do not leave a client change out of the patch.

Add tests where practical. Visual tests live in `client/osu.Game.Tests`; the `Ritsu.Validation` project covers offline checks.

## Commits and pull requests

- Use conventional prefixes: `feat:`, `fix:`, `docs:`, `build:`, `ci:`, `refactor:`, `chore:`.
- Work on a branch and open a pull request against `main`, which is protected.
- Describe what was validated and how. State what was not verified instead of implying it was.
- Update [CHANGELOG.md](CHANGELOG.md) for user-visible changes and bump [VERSION](VERSION) only when preparing a release. Release steps are documented in [artifacts/README.md](artifacts/README.md).

## Licensing

- [`LICENCE`](LICENCE): MIT licence of the client, inherited from osu! and Torii.
- [`LICENCE-Modosu.txt`](LICENCE-Modosu.txt): GPL-3.0 licence covering the Modosu/MOsu-derived mods. Code adapted from those projects stays under GPL-3.0, and the licence is shipped in every package.

Keep the upstream copyright headers and notices, and add new credits only when they are accurate.

## Reporting issues

Include the Ritsu version, Windows version, whether the data source is local storage or a linked osu!lazer folder, reproduction steps, and `torii-crash.txt` from **Settings → General → Open Torii folder** when the client does not start.
