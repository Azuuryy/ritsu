# Ritsu desktop UI guide

Use native osu!framework controls and the client’s existing visual vocabulary. Start with Song Select for browsing and filters, the mod picker for game configuration, and results for score hierarchy. Coloured emphasis should indicate the current action, a state change or the headline result. Pair colour with a readable label.

For a tool such as score simulation, show the map title and difficulty first. Group mode and mods under setup. Give judgement fields their own section with labels aligned before the controls. Give simulated PP a distinct placement and show rank, accuracy and combo as supporting values. Keep a single primary Calculate action and put Reset and Back nearby. This layout was reviewed and approved in game; see the approved score simulation section below.

Reuse the client’s standard text boxes, dropdowns, scroll containers and buttons. Test game scaling and a narrow window before adopting a fixed width. Prefer inspecting the normal entry point with a representative map, keyboard input and a loading or invalid state over a static mockup.

When a design is approved, record the concrete convention, why it helps and a link or screenshot from an approved screen. Avoid adding speculative rules.

## Approved: score simulation (`/visualize`, Song Select footer, F4)

Validated by Nathan in game. Reference implementation: `client/osu.Game/Commands/RitsuSimulationScreen.cs`. When a new tool shows score-like data, start from this screen rather than from a vertical form.

- **Follow Modern V2 results, adapted to the tool.** Reuse its geometry: slanted wedges (`OsuGame.SHEAR`), a title wedge and a darker body wedge, a large light headline value, statistic rows with slanted separators, and the original `AccuracyCircle` (V2 style) on the right. The headline is the tool's result (estimated PP), not the score. Do not repeat a value that is already shown (rank, combo, perfect PP appear once).
- **Design space.** Build inside a `DrawSizePreservingFillContainer` (1280×620, minimum strategy) below the toolbar so narrow windows scale instead of breaking.
- **Map header.** Status badge, title, artist, then the clock and metronome statistics (`BeatmapTitleWedge.Statistic`). Below, one rounded block: the top line holds the ruleset icon, the star rating, then the difficulty name and "mapped by" on a single line; the bottom part is a darker block with object counts on the left (Circles and Sliders, hidden when zero; spinners are not shown because they are irrelevant here) and CS/AR/OD/HP on the right, using `BeatmapTitleWedge.DifficultyStatisticsDisplay` at full size. There is no gap or width difference between the two parts.
- **Colours.** Text and the bars above statistics use `StarRatingDisplay.ForegroundTextColour`, not the raw difficulty colour, which becomes unreadable on dark panels. Hit-result labels use `OsuColour.ForHitResult`.
- **Inputs are parallelograms.** Only the plate is sheared (about 0.18, the same slant as the 10° separators); the text stays upright. Plates have fixed widths aligned to the left of their column (about 146 px for accuracy and combo, 84 px for judgements) so spacing stays regular. Their height follows the font. Set `Size = Vector2.One` on a relative-size `OsuTextBox`, because its default height is absolute.
- **Counts.** Main judgements read `672×`; secondary ones read `587/587` with a smaller maximum. Editable fields sit next to the read-only count of the same group.
- **Actions are footer modules, not buttons in the content.** Use `ScreenFooterContent.Argon` with `ScreenFooterButton`: Mods (lime) and Reset (blue), the same modules as Song Select. F1 opens the mods and F2 resets, only when pressed alone. The Song Select footer has a matching Simulate module (pink, the Screenshot accent). F4 alone opens it; Alt+F4, Ctrl+F4 and other combinations must never be captured, and F4 is ignored in gameplay, the editor and the simulation itself.
- **Background parallax.** Draw the beatmap background inside a `ParallaxContainer` so it follows the user's menu parallax setting.
- **Tests.** The footer is absent in `ScreenTestScene`; trigger actions with their keys (F2) in visual tests.
