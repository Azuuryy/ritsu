# Ritsu roadmap

## 0 — Torii offline foundation

Current deliverable: a portable Windows x64 client with an isolated instance, optional linking to an osu!lazer folder or local storage, local file imports, local play and editors, and no connected services. Linking uses the shared data directly; osu!stable detection and imports remain local. Torii's names and appearance are retained. Torii remains the primary upstream; every update must identify the three dependency revisions and pass the checks again.

## 1 — Integrate Modosu

Integrate the current project's mods, profiles, presets, and statistics into the client. Ritsu profiles start from scratch: existing Modosu scores are neither imported nor migrated. Keep local PP estimates identified by calculator version and mods used. Preserve credits to Modosu and MOsu. Collection imports/exports and map suggestions are outside this step's scope.

Use one shared mod list in the existing ruleset and preserve shared score and replay storage. The user will check behavior in lazer and Torii after integration; new mods have not yet been verified there. Plan for multiple local profiles with configurable PP estimates and stable internal IDs. Local profiles should eventually coexist with accounts and leaderboards on a private server.

Keep a separate copy of the local Modosu → tosu integration in `references/modosu-tosu/`; do not integrate it into the client yet. Plan a full adaptation to Ritsu later, covering client and mod identity, PP and difficulty calculations, selection, gameplay, results, counter compatibility, and local data exchange. Define scenarios and test the protocol before enabling it.

## 2 — Measured performance

Establish baseline frame times, latency, memory use, and resource consumption on identical maps and skins. Review EMPYREAN's ideas without treating its claims as proven results. Keep only changes measured to have no gameplay or rendering regressions.

## 3 — Tournament

Define a LAN scenario before development: organizer, workstation preparation, synchronized launch, controls, incident recovery, and result capture. Study inso as a reference. A LAN tournament does not necessarily require a permanent community server.

## 4 — Full visual editor

Write a separate specification before development. The goal is to compose menus, selection, results, and gameplay from components, layers, animations, and event responses. Define limits, rendering budgets, formats, compatibility, and creation scenarios. Scripting and map-specific gameplay changes are not implied by this phase.

## 5 — Ritsu identity

Decide which parts of Torii's visual identity remain and which become Ritsu. Before internal renaming, map names persisted in skins, profiles, settings, and replays; plan how to read and convert them. Keep notices and credits.

## 6 — Optional services

Evaluate updates, sharing creations, osu! OAuth, and a community server independently. Also study map search without player sign-in and decide separately whether downloading maps should be offered. Compare the official API and its OAuth requirements with public mirrors, their features, and availability before integration. OAuth can link an identity and authorized data; it does not validate scores for official leaderboards. Local play must remain usable without these services.

Size any future hosting from observed concurrent connections, calculations, storage, and traffic. No purchase or deployment is included in the current milestone.
