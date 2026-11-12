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

To move a Ritsu score to another profile, right-click its row in the map leaderboard and choose **Transfer score to profile**, then select the destination and confirm. Choose **All profiles** to access scores belonging to another profile. The score's PP inclusion setting follows it, and the destination profile's beatmap exclusions still apply.

New scores go to the active profile; existing scores are not associated automatically. Scores already associated with a Ritsu profile display that profile's current name without changing the shared score database. On a map's selection screen, open **Ranking** and choose **All profiles** from the leaderboard menu to show every local score for that map, including scores that are not associated with a profile. **Current profile** filters to the active profile. In the classic selection screen, the same menu is above the scores.

Estimated playtime is calculated from the recorded scores counted for the selected profile and ruleset. Choose **Map Length** or **Drain Length** in **Settings → Torii → Gameplay**. Map Length spans the first to last playable object, including breaks; Drain Length excludes breaks within that interval. Fixed speed mods, including custom rates, adjust the duration. The estimate uses the full map even for a saved failed score; attempts without a saved score, replay viewing and autoplay are not included. Missing or changed beatmaps and unsupported variable-speed mods are omitted and counted in the playtime tooltip. This is not a measure of wall-clock time.

PP values are local estimates. The rank and graph compare local profiles only for the selected mode; they are not official rankings. History is reconstructed using the current profiles and PP settings. Deleting a profile removes its score associations but keeps scores and replays in shared storage.

The beatmap catalogue in the top bar uses Torii's public API without an account. The native search and filters refresh automatically after a short pause. Cards are centred and sized to the available width. Click a card to open its detailed beatmap page; maps containing video offer separate downloads with and without video. Hover the artwork to play an audio preview, or hover a card to reveal its download button. Right-click a card to copy its Torii link or, for maps also hosted on osu!, its osu! link. Downloads use Torii and are imported by the existing beatmap importer into the currently selected library, including a linked osu!lazer library. A linked library is shared: importing here also changes that installation's data. Covers and audio depend on their host being available; Torii-exclusive maps without a preview URL do not offer an audio button. Favourites and other account-dependent filters remain unavailable. Imported maps can be played through **Play → Solo**.

Import and export JSON presets through **Settings → Maintenance → Mod Presets**. In the skin editor, add **RitsuLiveStarCounter** to the HUD components and place it where you want; it displays cumulative difficulty during play. The option to skip directly to the end of breaks is under **Settings → Torii → Gameplay**.

In **RandomV2** settings, enable **Custom angle** and check any of the 15 **Patterns**, including **Star**, **Square (90°)** and **Octagon (45°)**. RandomV2 uses the seed to choose among the selected patterns at phrase boundaries. Clear the selection to show **Angle without patterns** and edit the base angle. That angle also defines the base layout for transitions and for phrases where no selected pattern fits.

In the RandomV2 Autoplay preview, open **RandomV2 Settings** to see the numbered trajectory and choose **Use this seed**. In solo pause or fail, use **Retry same seed** or **New seed**. Quick retry keeps the current seed. **Separate pattern selections**, **Mirrored responses** and **Progressive spacing** are under **Custom angle**.

In osu!standard, **Streamline** is in **Difficulty Reduction**. It removes alternate interior notes from long streams. **One Eye** is in **Difficulty Increase**. It halves the approach rate after other difficulty adjustments, for example from 10 to 5 after Hard Rock. **No Sliding**, **Reversed Sliders**, **Directional Flicks** and **Decoy Circles** are also in **Difficulty Increase**. In **Decoy Circles** settings, choose a minimum interval from 2 to 32 beats and select **Real Fake** or **Grayscale**. Both use the skin's circle with a centered white cross. Ignored decoys fade without a miss animation. The interval defaults to eight beats at the tempo of the last decoy, and placement still needs an isolated circle with enough room. The local PP bonus is `0.10 × weight × count / (20 + count)`, where count is the number of placed decoys. The weight is 1 for Grayscale and 2 for Real Fake. Directional Flicks is marked **WIP** and cannot be activated from its card in the mod selector.

## Library and everyday play

- Click the Song Select search field to open history, favourites and pinned searches. Use **+** beside **Pinned searches** to name the current search and keep its filters, collection, sort and group. Right-click a pinned search to rename it or replace its settings. Press **Ctrl+Left Alt** at the search cursor for filter suggestions, then use **↑**, **↓** and **Tab** or click a suggestion.
- Right-click a sort, group, ranking scope or collection dropdown to arrange its entries. Star the ones you use often and choose **Manual**, **Alphabetical** or **Most used**. In manual mode, drag an entry or focus it and press **↑** or **↓**.
- The **Collections** menu on a difficulty, profile score or result can add the difficulty or its set to a native collection. In **Manage collections**, use the edit button to search and select multiple difficulties, create subgroups or remove entries. The undo button restores the last removal. A score marked for analysis appears in the active profile's score page.
- Right-click a pinned search and choose **Create smart collection**. Select it from the collection dropdown. For practice, try `length<2m pbmisses>=1 pbmisses<=3 playedthisweek=no`. The personal best follows the active profile's local score ranking, and the week starts on Monday in local time. The `length` filter uses Song Select's existing duration rounding.
- Open the command palette with **Ctrl+Shift+P** from the main menu, Song Select or results. Use `/recent` to open the latest score in the active mode, `/mods` to load a mod preset, and `/queue` to manage the session's play queue. **Select next queued difficulty** brings the map into Song Select. Use `/session save` in Song Select to capture the current mods, speed, skin, HUD settings and starting search.
- Use **Difficulty memo** in the collection menu to keep a private note for the active profile. You can link a local replay at a time in seconds or `m:ss`. Use **Set target BPM** in Song Select to preview and apply a fixed playback rate, choosing the reference section on maps with changing BPM.

## Data and updates

By default, data is stored in `%APPDATA%\Ritsu`. If you choose an osu!lazer folder, Ritsu uses it directly: changes to maps, scores, skins, or collections may be visible in both clients. Avoid running both clients against the same data at the same time. The setting is also available under **Settings → Torii → Data source**. The configuration file is still named `torii.ini`.

To update this version, close the game and extract the new package into a different folder. Automatic updates are disabled. No system file associations are installed.

## Offline behavior

Sign-in, the store, online leaderboards, chat, presence, telemetry, report submission, and external links are unavailable. Discord, WebSocket server, and legacy local TCP integrations are disabled. The map generator that installs external tools is not offered; the classic map editor remains available.

Local logs are available from **Settings → General → Open Torii folder**, then `logs`. Do not use legacy Modosu firewall or DLL installation tools with this client.
