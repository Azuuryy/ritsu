---
name: ritsu-ui
description: Design and implement Ritsu desktop screens, overlays and controls using the existing osu!framework components and Ritsu visual conventions. Use for UI changes in the game client.
---

- Read [the Ritsu UI guide](references/ui-guide.md) and inspect the closest working screen before editing.
- Design around the player’s next action. Keep map identity, selected mode and mods, and the important result visible in that order when they are relevant.
- Preserve keyboard focus, Escape behavior and existing command entry points. Verify narrow windows and loading, error and empty states.
- Review the result in game where possible. Treat new visual conventions as proposals until Nathan validates them. After his validation, add the reusable decision and its example to the guide.
- Approved reference: the score simulation screen (see "Approved: score simulation" in the guide). Reuse its Modern V2 structure, parallelogram inputs, footer modules and shortcut rules before inventing a new layout.
