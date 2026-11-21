# Ritsu roadmap

## Shipped foundations

- **Local play.** Ritsu started as a portable Windows x64 Torii client with local play, editors, file imports, and a choice between local storage and linking an osu!lazer folder. Linking uses shared maps, scores, replays, skins, and collections in place. osu!stable detection and imports are local. Local play and editing work offline.
- **Torii catalogue.** Unauthenticated Torii catalogue searches, media and beatmap downloads are allowed. Scores remain local; imports from local files and linked libraries remain available. Catalogue pagination requires investigation.
- **Modosu and profiles.** Modosu mods use the shared osu! ruleset mod list and shared score and replay storage. Local profiles have stable internal IDs and start with scores played in Ritsu. Local PP estimates use the calculator version and mods of each score and support profile-specific settings. Ritsu includes JSON mod presets, reuse of mods from a score, comparison of preset settings, and a live star counter for the HUD. Cross-client compatibility of the integrated mods, scores, and replays still requires in-game checks in lazer and Torii.
- **Song Select and results.** In both Song Select layouts, clicking the search field opens local history and favourites beneath it, and the adjacent star saves or removes the current query. It also has PP and farmability sorting, PP filters and groups, and reverse ordering. Score rows show stars calculated with the score's difficulty-changing mods. Results already have a collection button, and osu!standard replays have an overlay for click markers and cursor paths.

Torii remains the primary upstream. Record the client, framework, and resources revisions and rerun the relevant checks on each upstream update.

## Feedback implementation steps

1. **Targeted corrections.** The Realm mapset regression is covered by a passing test. Local profile scope persists across sessions and both Song Select layouts; profile command accuracy is displayed as a percentage without scaling it twice. Headless navigation tests pass.
2. **Profile loading and stability.** The local score expansion button now exits its loading state; a headless navigation test covers opening a score and reopening the profile. The Linux native crash remains under investigation: the supplied crash report identifies the audio thread, but not the underlying cause. The log reports PP calculations disabled when Song Select resumed; the local PP cache serializes writes under a lock. An isolated Windows test performed 100 score saves, repeated profile/rank reads and forced garbage collections without reproducing a failure. These observations do not identify the Linux crash or rule out the renderer, audio or another native component.
3. **Local status and PP.** Unranked is reserved for difficulties excluded from the active profile’s PP. The local library displays Unranked, unknown and blacklisted source statuses as Approved whenever the difficulty is included. The same rule applies to existing maps, imports and downloads. A set is locally Unranked when all its difficulties are excluded. Shared metadata retains its source status. Filtering, grouping and profile switching are covered by targeted tests; in-game confirmation remains open.
4. **Score import and transfer.** Explicitly import `.osr` files or detected linked MOsu/Modosu scores into a chosen profile, reporting missing maps and unsupported mods or settings. Isolated integration tests cover import, source preservation and reimport; synthetic FSA, SA, RandomV2, No Sliding and Circle Generation replays generated with Ritsu transformations retain their settings and play to completion with maximum combo. Playback of an actual MOsu replay and cross-client compatibility remain to be checked. Transfers change Ritsu profile associations without modifying shared scores. Standard replay export writes the associated profile name into a copy while retaining online identifiers and other replay bytes; an explicit option exports the original unchanged. Cross-client identity handling remains unverified.
5. **Decoy Circles.** Choose a fixed number from 0 to 99 instead of the cross; the default remains the cross for existing replays. Conversion and headless gameplay tests cover the setting; the Argon and classic drawable trees show the configured number or the cross, including two-digit numbers. Minimum note spacing defaults to half a beat for existing replays and can be reduced to a quarter beat for denser patterns. Headless gameplay tests pass for both spacings at 300 and 400 BPM; the reported problematic maps still need in-game checks. The current PP multiplier increases with decoy count but saturates below 20% for Real Fake and 10% for Grayscale, so additional decoys have diminishing impact. On the reported ODDS & ENDS difficulty, a perfect-play estimate rises from 545.325 PP at 8 beats and 99 decoys to 554.010 PP at 2 beats and 246 decoys; the diminishing increase matches the report. The comparison tool accepts a local `.osu` file. These measurements do not establish human difficulty or justify a specific rebalance.
6. **Torii update evaluation.** The upstream comparison on 2026-11-17 found the public client master 10 commits ahead of the pinned revision, the framework master identical, and the resources master 16 commits behind the pinned revision. The newer Torii client build pins resources revision `052c77623424f3f1a571d8bcbac0d608d458f8f8`, which is 13 commits ahead of Ritsu’s resource pin and changes 47 icon and sound files; the resource repository’s default branch is not that build pin. Its README still describes most resources as CC-BY-NC, so the provenance and licence of each replacement must be established before treating the assets as commercially usable. The Torii client master documents Satori 10; the `nova` branch has diverged and contains Satori 11 build work. Do not replace the pinned runtime without platform builds, replay checks, licence review and measurements. Preserve the existing Classic song select and results, evaluate missing Classic features individually, and test Aspire maps before claiming support. The upstream editor protection for selected circles and sliders whose drawable is temporarily absent is integrated and covered by headless selection tests. Retain free local profiles and defer account, coin, web and server integration; local mod policies remain a possible later addition. Audit each replacement asset and its licence individually before adopting it. Keep the redesigned upstream mod visuals as a candidate pending the resource licence audit; defer the newly playable ScoreV2 mod until its scoring and replay compatibility are assessed. Do not merge the upstream branch wholesale.

   Sources: [Torii client comparison](https://github.com/ShikkesoraSIM/torii/compare/27291c03874c611155073440b5e2539548681fe6...master), [editor fix](https://github.com/ShikkesoraSIM/torii/commit/db7bd1d5b), [Satori 10 build notes](https://github.com/ShikkesoraSIM/torii/blob/master/satori-runtime/README.md), [Nova comparison](https://github.com/ShikkesoraSIM/torii/compare/27291c03874c611155073440b5e2539548681fe6...nova), [Torii resource pin](https://github.com/ShikkesoraSIM/torii/commit/508942f5b6753cbce4e3d855bf725c34a17de09f), [resource comparison](https://github.com/ShikkesoraSIM/torii-resources/compare/2a516e45ba416dbd2cf8b821661909109f738b34...052c77623424f3f1a571d8bcbac0d608d458f8f8), [resource licence overview](https://github.com/ShikkesoraSIM/torii-resources/blob/052c77623424f3f1a571d8bcbac0d608d458f8f8/README.md).

   The follow-up audit of [Nova v2026.1117.1](https://github.com/ShikkesoraSIM/torii/releases/tag/v2026.1117.1-nova) found that the public tag `a2628b3` still targets .NET 10. The published binaries were built from a separately identified private revision. The public [Nova branch snapshot](https://github.com/ShikkesoraSIM/torii/tree/e6f9f48032b68888d4db198310e538db50eec8a4) provides the migration reference: desktop and Android entry points target .NET 11, while the shared game, rulesets, tests and iOS still target .NET 10. The desktop entry point runs its managed dependencies on the same .NET 11 runtime. Nova's framework and resource pins match Ritsu's.

   The desktop entry point and Ritsu validation tool now target .NET 11 with the stable Microsoft SDK. The Windows x64 self-contained build includes CoreCLR 11.0.0, and targeted profile, replay-mod and catalogue tests pass. Keep shared library targets until startup, gameplay, replay playback and the other desktop platforms are validated. Evaluate Satori 11 as a separate build using a pinned runtime, the upstream GC/async/pinning probe and per-platform checks. Nova specifies macOS 14 and x86-64-v2 requirements. Android is outside the Ritsu migration scope. Leave Score V2, mod presentation and the new Aspire/2B rendering out of the runtime change. The Ritsu client patch does not apply to clean public source even at the current pin, so port the changes onto the bundled Ritsu source and regenerate the affected patch hunks instead of replacing the bundle.

Each client change must be included in the offline patch and validated with Bootstrap and focused checks. Cross-client compatibility and unresolved reports require in-game confirmation.

## Library and everyday play

Start with pinned searches, custom menu ordering, and quick access to collections from results.

- **Search.** Add autocomplete for filter names and values. Let players name and pin a search that restores its query, filters, sort, and group together.
- **Menu ordering.** Let players pin and manually reorder sorts, scopes, groups, and collections using drag and drop or keyboard actions. Offer alphabetical and most-used ordering as selectable modes.
- **Collection management.** Use a common menu in Song Select, profile scores, and results. Pin frequently used collections and let players add a difficulty or the whole set, or mark a specific score for analysis. In the manager, support search, multi-selection, undo for removal, and user-created subgroups.
- **Smart collections.** Let players turn a saved search into a collection that refreshes as the library or the active profile's play history changes. For example, surface short maps with one to three misses on the personal best that the player has yet to revisit this week.
- **Local command palette.** Provide a shortcut to searchable, clickable actions such as opening the last score, loading a mod preset, or adding a map to the queue. Add short GuraBot-style aliases such as `/recent` and `/compare` as the underlying actions become available.
- **Small improvements.** Add a private memo per difficulty, visible in Song Select and results, with replay links for timestamps when a replay is available. Let players enter a target BPM to set the playback rate, using a reference section on maps with variable BPM. Add session presets that combine mods, rate, skin, HUD, and a starting search.

## Replay analysis and practice

Build the judgement timeline first, then add bookmarks, practice from a selected passage, and comparison between attempts.

- **Judgement timeline.** Mark misses, slider breaks, and other judgements and jump to the selected event from results. Extend the osu!standard replay overlay with a slow-motion view of the cursor path, input and hit window. Separate observed events, such as an early click, from likely explanations. Then adapt the analysis to other rulesets, for example by showing errors per column in mania.
- **Practice from a passage.** Let players bookmark a passage and launch a loop from a result or replay, with adjustable start and end points, lead-in, and playback rate. Offer a gradual increase in rate after a series of clean repetitions and record segment results in a dedicated practice history.
- **Compare attempts.** Align two attempts on the same difficulty and settings to compare accuracy, consistency, and combo breaks by passage.
- **Small improvements.** Show the number of retries and where attempts ended during the current session. Give Hit Sounder distinct sounds for K1 and K2. Add a metronome count-in on resume and a Miss Challenge HUD indicator for the remaining miss budget. Show the score used when Miss Challenge takes its limit from the active profile.

## Mods and beatmap variants

### Available on the integration branch

- **RandomV2.** Custom angle offers 15 optional patterns, with separate lists for jumps, streams and bursts, mirrored responses and progressive spacing. Solo pause and fail menus offer **Retry same seed** and **New seed**. The Autoplay preview can show the numbered trajectory and keep its seed for solo play.
- **No Sliding and Spacing Adjust.** Select short, long or repeating sliders and choose streams, head-only taps, or head-and-tail taps. Set a maximum jump distance while preserving rapid sequences where possible.
- **osu!standard variants.** Streamline (formerly Half-Stream) is a difficulty reduction. No Sliding and Reversed Sliders are difficulty increases. One Eye halves the approach rate after other difficulty adjustments. Echo, Swing and Phrase Mirror were retired from the selection and kept for existing replays.
- **osu!standard judgement variants.** Both mods are in Difficulty Increase. Decoy Circles offers adjustable spacing and uses the skin's circle in color or grayscale, with a white cross or a configurable number. Clicking a decoy applies a miss. Its local difficulty and PP bonus scales with the placed decoy count and appearance. Directional Flicks checks the cursor entry direction and allows another attempt within the hit window.

Scores and Ritsu OSR exports store variant settings, the transformation version and the seed where applicable. Before promotion to `main`, review each mod on real maps and verify compatibility with the relevant linked-library clients.

### Later prototypes

- **osu!mania and osu!taiko.** Try seeded note-level shuffle with jack limits and preserved chord sizes, selected taps converted to short holds, and taiko note colours derived from rhythmic accents.

## Measured performance and compatibility

- **Performance baselines.** Measure frame times, latency, memory use, and resource consumption on identical maps and skins. Measure the delay from typing to results on a large library. Evaluate EMPYREAN ideas against these baselines and adopt changes backed by measurements and gameplay and rendering checks.
- **Local diagnostics.** Show progress for PP calculations used by the current view. Mark frame-time spikes and compare two runs on the same map with different skins or settings.
- **tosu adaptation.** Use the separate Modosu integration reference in `references/modosu-tosu/`. Define a Ritsu-specific adaptation covering client and mod identity, PP and difficulty, selection, gameplay, results, counter compatibility, and local data exchange. Validate the scenarios and protocol before enabling it.

## Local community and LAN

- **Coaching packs.** Share a local file containing practice passages, notes, speeds, and targets. On import, match entries to the local library and list missing maps.
- **LAN tournaments.** Define the organiser workflow, workstation setup, synchronised launch, controls, incident recovery, and result capture before development. Study inso as a reference and design for a local event.

## Full visual editor

Write a separate specification for composing menus, Song Select, results, and gameplay from components, layers, animations, and event responses. Define limits, rendering budgets, formats, compatibility, and creation scenarios. Keep scripting and map-specific gameplay changes as separate design topics.

## Ritsu identity

Decide which parts of Torii's visual identity carry over to Ritsu. Before renaming internal identifiers, inventory names stored in skins, profiles, settings, and replays and plan how to read or convert them. Retain the original notices and credits.

## Optional services

Evaluate client updates, online sharing of creations, direct osu! access and OAuth, and a community server independently. Keep each service optional for local play. Design any private accounts and leaderboards to coexist with local profiles. Treat official leaderboard eligibility as a separate concern from sign-in. Size any future hosting from observed concurrency, calculations, storage, and traffic, and base the decision on funding. Hosting purchase and deployment belong to a separate milestone.

Release announcements are handled on the repository side by `.github/workflows/github-releases-to-discord.yml` and require a configured webhook. See the [release announcement guide](artifacts/README.md#announce-on-discord).

### GitHub release updates

Plan opt-in update checks and downloads, with manual downloads from the release page and offline startup available. Define the supported platforms and package formats first. Use Torii's desktop Velopack implementation in `client/osu.Desktop/Updater/VelopackUpdateManager.cs` and the release stream and pagination logic in `ToriiUpdateSource.cs` as references. Torii also has a separate macOS install path and a mobile update notifier.

- Publish versioned, platform-specific artifacts on GitHub Releases, with consistent metadata or tags for stable and prerelease channels.
- Check the selected channel, compare versions, and require an explicit choice to change channels or downgrade.
- Check and download updates outside gameplay. Handle GitHub unavailability gracefully and let players review release notes and choose installation now or on restart.
- Verify platform, version, and integrity before installing. Specify signing or trusted checksums and recovery for interrupted downloads or failed installs.
- Restrict updates to application files. Keep Ritsu settings and data, including any linked osu!lazer folder, outside the installer's write scope.
- Test upgrades, channel changes, interruptions, rollback and recovery, and offline startup on every supported platform before enabling automatic updates.
