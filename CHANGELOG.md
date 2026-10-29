# Changelog

Les changements notables de Ritsu sont consignés ici. Les dates suivent le calendrier ISO ; le détail des fonctionnalités prévues reste dans [ROADMAP.md](ROADMAP.md).

## Unreleased

## [0.2.3] - 2026-10-29

### Added

- Série complète des mods d'entraînement Modosu dans la liste commune du ruleset osu! : Metronome, Spacing Adjust, Fun Spacing (Spacing Overtime), Circle Generation, No Sliding, RandomV2 et Miss Challenge.
- Présentation unifiée des mods Modosu avec style dédié, réglages et info-bulles adaptés ; réglages de replay pour RandomV2 et Spacing Adjust.
- Import et export JSON des presets de mods, compteur d'étoiles en direct ajoutable au HUD depuis l'éditeur de skin et réglage de progression des replays.

### Changed

- Intégration des mods au ruleset osu! existant, sans créer de ruleset séparé ; la liste commune et les scores/replays partagés sont conservés.
- Harmonisation visuelle avec les mods exclusifs Torii, avec une bordure dédiée aux mods Modosu et un style réutilisable pour les mods Ritsu.

## [0.2.2] - 2026-10-29

### Added

- Gestion de plusieurs profils locaux : création, sélection, renommage et suppression, avec un identifiant stable pour chaque profil.
- Page de profil avec scores récents et meilleures performances par mode, estimation locale des PP et option par profil pour inclure Relax et Autopilot.
- Classements locaux filtrables par profil actif ou sur l'ensemble des profils, y compris les scores historiques non associés.

### Changed

- Association des nouveaux scores au profil actif via des métadonnées locales séparées ; les anciens scores restent dans le stockage partagé et ne sont associés à aucun profil automatiquement.

### Fixed

- Chargement des statistiques de profil avec une requête Realm compatible.
- Affichage et navigation de l'interface de profils locaux.

Sur un build précédent, l'utilisateur a indiqué que les quatre modes, la séparation des scores par profil et le compteur d'étoiles fonctionnaient. La nouvelle interface et les corrections de chargement restent à vérifier manuellement. La compatibilité des mods, scores et replays avec les autres clients n'est pas encore validée.

## [0.2.1] - 2026-10-29

### Added

- Profils locaux pour les quatre modes, avec association des nouveaux scores, filtres de classement local et estimation de PP configurable.
- Mod Miss Challenge avec limite manuelle ou basée sur le meilleur score local.
- Import et export JSON de presets de mods, compteur d'étoiles en jeu et réglage de progression des replays.
- Bordure propre aux mods Modosu et style préparé pour les futurs mods Ritsu.
- Option de saut directement à la fin des pauses.

### Changed

- Accès aux profils depuis la barre supérieure, avec une page de statistiques, de scores, d'historique de PP et de classement local.
- Ajout du compteur d'étoiles dans l'éditeur de skin pour le placer librement sur le HUD.
- Présentation des mods Modosu rapprochée des exclusivités Torii et filtre de tous les profils rendu plus visible.

Sur un build précédent, l'utilisateur a indiqué que les quatre modes, la séparation des scores par profil et le compteur d'étoiles fonctionnaient. Le déplacement des profils et du compteur, les changements visuels et la compatibilité avec les autres clients restent à vérifier manuellement.

## [0.2.0] - 2026-10-28

### Added

- Liaison facultative au dossier osu!lazer existant, avec détection locale et accès direct aux données partagées ; le stockage local Ritsu reste disponible.
- Première série de mods Modosu intégrés à la liste commune du ruleset osu! : Metronome, Spacing Adjust, Fun Spacing, Circle Generation, No Sliding et RandomV2.
- Réglages de replay pour RandomV2 et Spacing Adjust.
- Conservation de la licence Modosu dans le paquet distribué.

### Changed

- Le menu du métronome propose les taux 0,25×, 0,5×, 1×, 2× et 3×.
- Le calcul des subdivisions du métronome évite le ralentissement des ticks aux taux élevés.
- Le contrôle des données liées et le premier démarrage permettent de choisir explicitement le stockage osu!lazer partagé.

### Fixed

- Restauration de la détection et de l'import local depuis les installations osu!stable.
- Recherche des fixtures de validation dans leur dossier source `tools/fixtures`.