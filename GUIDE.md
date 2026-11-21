# Ritsu — quick start

1. Extract the entire ZIP into a new folder.
2. Open **Launch Ritsu.cmd**. The program is still named `torii.exe` and retains the Torii identity; this is intentional for this first release.
3. Choose the detected osu!lazer folder or select one manually to share its data. You can also keep local storage. Reopen the game after confirming; no account is required.
4. Drag `.osz` maps or `.osk` skins into the window. Import is also available through **Settings → Maintenance → Import files**.
5. Choose **Play → Solo**. Modosu mods are available in the shared osu! mod list.

Scores and replays from regular plays are local. Autoplay and replay behavior remain as in Torii: watching a replay does not create a new score. An imported `.osr` file requires its corresponding map to be available locally.

Original archives are kept after import.

## Profiles and training

Open profiles with the player-name button in the top bar, or press **Ctrl+P**. Use the profile chips below the name to switch profiles; select **+** to create one. Right-click a chip to rename or delete that profile and, for the active profile, choose whether Relax and Autopilot count toward PP. Right-click an individual score in the profile or beatmap ranking to include or exclude it from that profile's PP while keeping its score and replay. In song select, the beatmap and beatmap set menus also let you include or exclude all of that profile's existing scores for the difficulty or set; the action appears below **Edit** when available. The page shows recent scores and best performances for the mode selected in the game bar.

In the local library, maps reported as Unranked, unknown or Blacklisted by their source display as **Approved** when included in the active profile’s PP. **Unranked** marks a difficulty excluded from that profile’s PP. A set shows Unranked when all its difficulties are excluded. Ritsu evaluates this when displaying installed maps, including imports and downloads, and retains their source metadata.

To move a Ritsu score to another profile, right-click its row in the map leaderboard and choose **Transfer score to profile**, then select the destination and confirm. Choose **All profiles** to access scores belonging to another profile. The score's PP inclusion setting follows it, and the destination profile's beatmap exclusions still apply. The transfer changes Ritsu's profile association without modifying the shared score or replay. The standard replay export writes the associated profile's current display name into the exported copy. The copy retains the original online identifiers and all other replay data, so other clients may still associate it with the original account. To preserve the recorded name and bytes, right-click the score in the profile or map leaderboard and choose **Export original replay**.

To explicitly import a replay, right-click a profile chip and choose **Import replay to this profile**, or run `/import replay` from the command palette. Choose a destination profile and an `.osr` file, a detected MOsu/Modosu score in the linked library, or all detected scores. The corresponding beatmap must be installed. Original files and linked scores are kept; importing creates or associates a local score and therefore writes to the selected storage. Unsupported mods and settings are reported instead of silently discarded. Some map-transforming mods may play differently, so check imported replays in game. Actual MOsu replay compatibility across clients has not yet been verified.

New scores go to the active profile; existing scores are not associated automatically. Scores already associated with a Ritsu profile display that profile's current name without changing the shared score database. On a map's selection screen, open **Ranking** and choose **All profiles** from the leaderboard menu to show every local score for that map, including scores that are not associated with a profile. **Current profile** filters to the active profile. In the classic selection screen, the same menu is above the scores.

Estimated playtime is calculated from the recorded scores counted for the selected profile and ruleset. Choose **Map Length** or **Drain Length** in **Settings → Torii → Gameplay**. Map Length spans the first to last playable object, including breaks; Drain Length excludes breaks within that interval. Fixed speed mods, including custom rates, adjust the duration. The estimate uses the full map even for a saved failed score; attempts without a saved score, replay viewing and autoplay are not included. Missing or changed beatmaps and unsupported variable-speed mods are omitted and counted in the playtime tooltip. This is not a measure of wall-clock time.

PP values are local estimates. The rank and graph compare local profiles only for the selected mode; they are not official rankings. History is reconstructed using the current profiles and PP settings. Deleting a profile removes its score associations but keeps scores and replays in shared storage.

The Torii beatmap catalogue supports browsing and downloading maps without an account. You can also import files from your computer or link an existing osu!lazer library. Imports into a linked library also change that installation's shared data.

Import and export JSON presets through **Settings → Maintenance → Mod Presets**. In the skin editor, add **RitsuLiveStarCounter** to the HUD components and place it where you want; it displays cumulative difficulty during play. The option to skip directly to the end of breaks is under **Settings → Torii → Gameplay**.

In **RandomV2** settings, enable **Custom angle** and check any of the 15 **Patterns**, including **Star**, **Square (90°)** and **Octagon (45°)**. RandomV2 uses the seed to choose among the selected patterns at phrase boundaries. Clear the selection to show **Angle without patterns** and edit the base angle. That angle also defines the base layout for transitions and for phrases where no selected pattern fits.

In the RandomV2 Autoplay preview, open **RandomV2 Settings** to see the numbered trajectory and choose **Use this seed**. In solo pause or fail, use **Retry same seed** or **New seed**. Quick retry keeps the current seed. **Separate pattern selections**, **Mirrored responses** and **Progressive spacing** are under **Custom angle**.

In osu!standard, **Streamline** is in **Difficulty Reduction**. It removes alternate interior notes from long streams. **One Eye** is in **Difficulty Increase**. It halves the approach rate after other difficulty adjustments, for example from 10 to 5 after Hard Rock. **No Sliding**, **Reversed Sliders**, **Directional Flicks** and **Decoy Circles** are also in **Difficulty Increase**. In **Decoy Circles** settings, choose a minimum interval from 2 to 32 beats and select **Real Fake** or **Grayscale**. Both use the skin's circle with a centered white cross or a configurable number. Ignored decoys fade without a miss animation. The interval defaults to eight beats at the tempo of the last decoy. Minimum note spacing defaults to half a beat; set it to a quarter beat to allow decoys in denser patterns where there is enough room. The local PP bonus is `0.10 × weight × count / (20 + count)`, where count is the number of placed decoys. The weight is 1 for Grayscale and 2 for Real Fake. Directional Flicks is marked **WIP** and cannot be activated from its card in the mod selector.

## Library and everyday play

- Click the Song Select search field to open history, favourites and pinned searches. Use **+** beside **Pinned searches** to name the current search and keep its filters, collection, sort and group. Right-click a pinned search to rename it or replace its settings. Press **Ctrl+Left Alt** at the search cursor for filter suggestions, then use **↑**, **↓** and **Tab** or click a suggestion.
- Right-click a sort, group, ranking scope or collection dropdown to arrange its entries. Star the ones you use often and choose **Manual**, **Alphabetical** or **Most used**. In manual mode, drag an entry or focus it and press **↑** or **↓**.
- The **Collections** menu on a difficulty, profile score or result can add the difficulty or its set to a native collection. In **Manage collections**, use the edit button to search and select multiple difficulties, create subgroups or remove entries. The undo button restores the last removal. A score marked for analysis appears in the active profile's score page.
- Right-click a pinned search and choose **Create smart collection**. Select it from the collection dropdown. For practice, try `length<2m pbmisses>=1 pbmisses<=3 playedthisweek=no`. The personal best follows the active profile's local score ranking, and the week starts on Monday in local time. The `length` filter uses Song Select's existing duration rounding.
- Open the command palette with **Ctrl+Shift+P** from the main menu, Song Select or results. Use `/recent` to open the latest score in the active mode, `/mods` to load a mod preset, and `/queue` to manage the session's play queue. **Select next queued difficulty** brings the map into Song Select. Use `/session save` in Song Select to capture the current mods, speed, skin, HUD settings and starting search.
- Use **Difficulty memo** in the collection menu to keep a private note for the active profile. You can link a local replay at a time in seconds or `m:ss`. Use **Set target BPM** in Song Select to preview and apply a fixed playback rate, choosing the reference section on maps with changing BPM.

Decoy Circles can display a fixed number from 0 to 99 instead of the cross. Set **Decoy number** to -1 to keep the cross.

## Data and updates

By default, data is stored in `%APPDATA%\Ritsu`. If you choose an osu!lazer folder, Ritsu uses it directly: changes to maps, scores, skins, or collections may be visible in both clients. Avoid running both clients against the same data at the same time. The setting is also available under **Settings → Torii → Data source**. The configuration file is still named `torii.ini`. Development launches and published builds using the standard Ritsu data folder share this configuration and the selected library. Close one before launching the other. Existing `torii.dev.ini` files are left in place.

To update this version, close the game and extract the new package into a different folder. Automatic updates are disabled. No system file associations are installed.

## Offline behavior

Sign-in, the store, online leaderboards, chat, presence, telemetry, report submission, and external links are unavailable. Discord, WebSocket server, and legacy local TCP integrations are disabled. The map generator that installs external tools is not offered; the classic map editor remains available.

Local logs are available from **Settings → General → Open Torii folder**, then `logs`. Do not use legacy Modosu firewall or DLL installation tools with this client.
