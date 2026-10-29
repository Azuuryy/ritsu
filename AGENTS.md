# Ritsu project instructions

- Follow [ROADMAP.md](ROADMAP.md): the current milestone is a locally playable Torii client with no online services. Later steps remain planned and must not be described as implemented.
- Preserve Torii's local features, including osu!lazer folder detection and linking, and local imports. Linking uses the shared data directly; do not require copying or isolated storage. Local storage remains an option.
- Integrate Modosu mods into the existing shared ruleset list; do not create a separate ruleset to isolate them. Scores and replays use the current shared behavior. Compatibility with other clients will be checked by the user after integration and must not be described as verified before that check.
- Support multiple local profiles with configurable PP estimates and, later, coexistence with an account and leaderboards on a private server. Ritsu profiles start from scratch: do not import or automatically associate existing Modosu scores. Use a stable internal ID for new profiles.
- Operations on a linked folder can change data in the original installation. Preserve the user's explicit choice, explain the shared data, and avoid automatic migrations or deletions.
- Keep online services disabled for the offline milestone: authentication, downloads, updates, telemetry, server integrations, and external communications. Local file operations are not online services.
- Keep dependency revisions and patches in sync: every change to the bundled client must also be reflected in `patches/client-offline.patch`, then checked with `Bootstrap.ps1` and appropriate validations.
- Document behavior and limitations that have actually been verified; distinguish historical validation from new changes. Preserve credits and licenses from the original projects.
