# Ritsu roadmap

## Shipped foundations

- **Local play.** Ritsu started as a portable Windows x64 Torii client with local play, editors, file imports, and a choice between local storage and linking an osu!lazer folder. Linking uses shared maps, scores, replays, skins, and collections in place. osu!stable detection and imports are local. Local play and editing work offline.
- **Optional beatmap catalogue.** Browse the Torii catalogue without an account, with search, covers, audio previews where available, and downloads through the existing importer.
- **Modosu and profiles.** Modosu mods use the shared osu! ruleset mod list and shared score and replay storage. Local profiles have stable internal IDs and start with scores played in Ritsu. Local PP estimates use the calculator version and mods of each score and support profile-specific settings. Ritsu includes JSON mod presets, reuse of mods from a score, comparison of preset settings, and a live star counter for the HUD. Cross-client compatibility of the integrated mods, scores, and replays still requires in-game checks in lazer and Torii.
- **Song Select and results.** In both Song Select layouts, clicking the search field opens local history and favourites beneath it, and the adjacent star saves or removes the current query. It also has PP and farmability sorting, PP filters and groups, and reverse ordering. Score rows show stars calculated with the score's difficulty-changing mods. Results already have a collection button, and osu!standard replays have an overlay for click markers and cursor paths.

Torii remains the primary upstream. Record the client, framework, and resources revisions and rerun the relevant checks on each upstream update.

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
- **osu!standard judgement variants.** Both mods are in Difficulty Increase. Decoy Circles offers adjustable spacing and uses the skin's circle in color or grayscale, with a white cross. Clicking a decoy applies a miss. Its local difficulty and PP bonus scales with the placed decoy count and appearance. Directional Flicks checks the cursor entry direction and allows another attempt within the hit window.

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
