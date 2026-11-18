# Commandes Ritsu

Ouvrir la palette avec **Ctrl+Maj+P** depuis le menu principal, le Song Select solo ou les résultats. Saisir une commande ou rechercher son libellé, puis valider au clavier ou à la souris. Une commande à choix affiche les entrées disponibles.

| Commande | Action |
|---|---|
| `/map` | Difficulté, BPM, combo et PP d’un score parfait avec les mods sélectionnés |
| `/simulate`, `/visualize` | Écran de simulation lazer pour standard, taiko, catch et mania |
| `/map find <titre, ID ou lien>` | Choisir une difficulté importée et la sélectionner |
| `/simulate map <titre, ID ou lien>` | Simuler une difficulté importée |
| `/attributes` | Afficher les attributs avec les mods sélectionnés |
| `/strains` | Afficher une série de points de difficulté au fil de la map |
| `/favorite` | Ajouter ou retirer la difficulté de la collection locale Ritsu favourites |
| `/scores`, `/history` | Parcourir les scores sur la difficulté ou l’historique du profil actif |
| `/mapper top <mapper>` | Parcourir les scores du profil actif filtrés par auteur |
| `/mostplayed`, `/recommend` | Afficher les difficultés les plus jouées ou des suggestions locales |
| `/pinned` | Parcourir les scores marqués du profil actif |
| `/compare score`, `/compare top` | Comparer les scores de la difficulté ou les meilleurs scores communs à deux profils |
| `/compare profile` | Choisir un deuxième profil local et comparer les statistiques |
| `/nochoke` | Recalculer les PP d’une projection full combo d’un score local compatible |
| `/whatif <PP>`, `/reach <PP de profil>` | Projeter le PP pondéré du profil à partir de la difficulté sélectionnée |
| `/profile` | Statistiques du profil local actif dans le mode sélectionné |
| `/top` | Parcourir jusqu’à 50 meilleurs scores du profil local actif et ouvrir leur résultat |
| `/recent` | Dernier score du profil actif dans le mode actif |
| `/compare` | Comparer deux scores locaux sur la difficulté sélectionnée |
| `/memo` | Mémo et repères de replay de la difficulté |
| `/mods` | Choisir un preset de mods du mode actif |
| `/session save` | Enregistrer le mode, les mods, le skin, les options HUD et la recherche |
| `/session load` | Choisir un preset de session à restaurer |
| `/session delete` | Choisir un preset de session et confirmer la suppression |
| `/queue add` | Ajouter la difficulté à la file en mémoire |
| `/queue next` | Sélectionner la prochaine difficulté disponible |
| `/queue remove` | Choisir une difficulté à retirer |
| `/queue up` | Choisir une difficulté à remonter |
| `/queue down` | Choisir une difficulté à descendre |
| `/bpm [cible] [--keep-ar]` | Ouvrir le dialogue BPM. La compensation DA est disponible en standard et en catch |
| `/profiler start` | Démarrer une capture locale Windows de 30 secondes |
| `/profiler stop` | Terminer la capture et exporter le rapport |

Dans le Song Select solo, **Tab** ouvre et ferme le panneau de file à droite. Si les suggestions de recherche sont ouvertes, Tab valide d’abord la suggestion. Ouvrir le simulateur depuis un clic droit sur une difficulté, depuis le menu des mods ou depuis la palette. Il utilise les calculateurs de PP du client. Les réglages de mods sont propres à la simulation. En mode précision cible, l’écran affiche la répartition calculée et la précision obtenue.

Le profilage enregistre `trace.json` et `report.html` sous le stockage privé Ritsu, dans `ritsu/profiles/frame/`.

## Correspondance avec les bots

Le statut « candidat » indique une piste de transposition. Les options dépendantes de données externes sont détaillées dans les inventaires épinglés.

### GuraBot

| Commande | Surface | Ritsu |
|---|---|---|
| [`amifarmer`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/amifarmer/AmIFarmer.command.ts#L19) | slash et préfixe | Candidat à étudier |
| [`avatar`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/avatar/Avatar.command.ts#L15) | slash et préfixe | Candidat local selon les données disponibles |
| [`badge`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/badge/Badge.command.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`badge info`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/badge/BadgeInfo.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`badge list`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/badge/BadgeList.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`badgeinfo`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/badge/BadgeInfo.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`badgelist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/badge/BadgeList.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`bws`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/bws/Bws.command.ts#L24) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`catch`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/profile/Profile.command.ts#L30) | préfixe | Candidat local selon les données disponibles |
| [`catchcard`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L107) | préfixe | Candidat local selon les données disponibles |
| [`catchcommon`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareTop.subcommand.ts#L39) | préfixe | Candidat local selon les données disponibles |
| [`catchcompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareScore.subcommand.ts#L47) | préfixe | Candidat local selon les données disponibles |
| [`catchcompareprofile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareProfile.subcommand.ts#L47) | préfixe | Candidat local selon les données disponibles |
| [`catchfarmmaps`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmMaps.subcommand.ts#L39) | préfixe | Candidat local selon les données disponibles |
| [`catchgraphosutrackpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackPp.command.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchgraphosutrackrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackRank.command.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchgraphosutrackscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackScores.command.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchgraphrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphRank.command.ts#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchgraphskillscurve`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphSkillsCurve.command.ts#L41) | préfixe | Candidat local selon les données disponibles |
| [`catchgraphtophours`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopHours.command.ts#L42) | préfixe | Candidat local selon les données disponibles |
| [`catchmapperpinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperPinned.subcommand.ts#L32) | préfixe | Candidat local selon les données disponibles |
| [`catchmappertop`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperTop.subcommand.ts#L32) | préfixe | Candidat local selon les données disponibles |
| [`catchnochoke`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/nochoke/NoChoke.command.ts#L31) | préfixe | Candidat local selon les données disponibles |
| [`catchosustatsbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsBest.subcommand.ts#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosustatscompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCompare.subcommand.ts#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosustatscount`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCount.subcommand.ts#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosustatsplayers`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsPlayers.subcommand.ts#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosustatsscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsScores.subcommand.ts#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosutrackdecaydays`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayDays.subcommand.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosutrackdecayrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayRank.subcommand.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosutrackreachpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachPp.subcommand.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchosutrackreachrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachRank.subcommand.ts#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchpinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/pinned/Pinned.command.ts#L31) | préfixe | Candidat local selon les données disponibles |
| [`catchpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Pp.command.ts#L30) | préfixe | Candidat local selon les données disponibles |
| [`catchrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Rank.command.ts#L30) | préfixe | Service externe ou fonctionnalité Discord |
| [`catchreach`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Reach.command.ts#L30) | préfixe | Candidat local selon les données disponibles |
| [`catchrecent`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L104) | préfixe | Candidat local selon les données disponibles |
| [`catchrecentbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentBest.command.ts#L46) | préfixe | Candidat local selon les données disponibles |
| [`catchrecentlist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L75) | préfixe | Candidat local selon les données disponibles |
| [`catchrecentpass`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L116) | préfixe | Candidat local selon les données disponibles |
| [`catchrecentpasslist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L87) | préfixe | Candidat local selon les données disponibles |
| [`catchrecommend`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmRecommend.subcommand.ts#L39) | préfixe | Candidat local selon les données disponibles |
| [`catchskills`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L64) | préfixe | Candidat local selon les données disponibles |
| [`catchtop`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/top/Top.command.ts#L31) | préfixe | Candidat local selon les données disponibles |
| [`catchtopif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/topif/TopIf.command.ts#L31) | préfixe | Candidat local selon les données disponibles |
| [`catchwhatif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/whatif/WhatIf.command.ts#L31) | préfixe | Candidat local selon les données disponibles |
| [`checkname`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/checkname/Checkname.command.ts#L29) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`commands`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/Commands.command.ts#L20) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`common`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareTop.subcommand.ts#L19) | préfixe | Candidat à étudier |
| [`compare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/Compare.command.ts#L12) | slash | Partiel · /compare ou /recent sur le profil local |
| [`compare profile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareProfile.subcommand.ts#L11) | slash | Adapté · /compare profile pour deux profils locaux |
| [`compare score`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareScore.subcommand.ts#L11) | slash | Adapté · /compare pour les scores locaux |
| [`compare top`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareTop.subcommand.ts#L11) | slash | Candidat local selon les données disponibles |
| [`compareprofile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareProfile.subcommand.ts#L19) | préfixe | Candidat local selon les données disponibles |
| [`comparescore`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareScore.subcommand.ts#L27) | préfixe | Partiel · /compare ou /recent sur le profil local |
| [`config`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/config/Config.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`config guild`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/config/ConfigGuild.subcommand.ts#L22) | slash | Service externe ou fonctionnalité Discord |
| [`config render`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/config/ConfigRender.subcommand.ts#L17) | slash | Service externe ou fonctionnalité Discord |
| [`config user`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/config/ConfigUser.subcommand.ts#L18) | slash | Service externe ou fonctionnalité Discord |
| [`cs`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareScore.subcommand.ts#L18) | slash | Partiel · /compare ou /recent sur le profil local |
| [`donate`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/Donate.command.ts#L13) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`farm`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/Farm.command.ts#L12) | slash | Candidat local selon les données disponibles |
| [`farm maps`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmMaps.subcommand.ts#L11) | slash | Candidat local selon les données disponibles |
| [`farm recommend`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmRecommend.subcommand.ts#L11) | slash | Adapté · /recommend à partir de l’historique local |
| [`farmmaps`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmMaps.subcommand.ts#L19) | préfixe | Candidat local selon les données disponibles |
| [`farmrecommend`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmRecommend.subcommand.ts#L19) | préfixe | Candidat local selon les données disponibles |
| [`fix`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/fix/Fix.command.ts#L31) | slash et préfixe | Candidat local selon les données disponibles |
| [`graph`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/Graph.command.ts#L12) | slash | Candidat à étudier |
| [`graph achievements`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphAchievements.command.ts#L9) | slash | Candidat à étudier |
| [`graph history`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphHistory.command.ts#L9) | slash | Candidat à étudier |
| [`graph ladder`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphLadder.group.ts#L8) | groupe | Candidat à étudier |
| [`graph ladder decay`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackLadderDecay.command.ts#L15) | slash | Candidat à étudier |
| [`graph ladder density`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackLadderDensity.command.ts#L15) | slash | Candidat à étudier |
| [`graph ladder pp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackLadderPp.command.ts#L15) | slash | Candidat à étudier |
| [`graph osutrack`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrack.group.ts#L8) | groupe | Service externe ou fonctionnalité Discord |
| [`graph osutrack accuracy`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackAccuracy.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`graph osutrack grades`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackGrades.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`graph osutrack hits`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackHits.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`graph osutrack playcount`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackPlaycount.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`graph osutrack pp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackPp.command.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`graph osutrack rank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackRank.command.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`graph osutrack scores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackScores.command.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`graph rank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphRank.command.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`graph replays`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphReplays.command.ts#L9) | slash | Candidat à étudier |
| [`graph skills`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphSkills.group.ts#L8) | groupe | Candidat à étudier |
| [`graph skills curve`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphSkillsCurve.command.ts#L12) | slash | Candidat à étudier |
| [`graph top`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTop.group.ts#L8) | groupe | Candidat à étudier |
| [`graph top age`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopAge.command.ts#L10) | slash | Candidat à étudier |
| [`graph top curve`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopCurve.command.ts#L10) | slash | Candidat à étudier |
| [`graph top hours`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopHours.command.ts#L12) | slash | Candidat à étudier |
| [`graph top pp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopPp.command.ts#L10) | slash | Candidat à étudier |
| [`graph top rankeddate`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopRankedDate.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`graphachievements`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphAchievements.command.ts#L17) | préfixe | Candidat à étudier |
| [`graphhistory`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphHistory.command.ts#L17) | préfixe | Candidat à étudier |
| [`graphosutrackpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackPp.command.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphosutrackrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackRank.command.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphosutrackscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackScores.command.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphRank.command.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphskillscurve`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphSkillsCurve.command.ts#L21) | préfixe | Candidat à étudier |
| [`graphtophours`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopHours.command.ts#L20) | préfixe | Candidat à étudier |
| [`help`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/Help.command.ts#L20) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`invite`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/Invite.command.ts#L13) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`leaderboard`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/leaderboard/Leaderboard.command.ts#L48) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`link`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/link/Link.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`link osu`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/link/LinkOsu.subcommand.ts#L20) | slash | Service externe ou fonctionnalité Discord |
| [`link twitch`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/link/LinkTwitch.subcommand.ts#L16) | slash | Service externe ou fonctionnalité Discord |
| [`mania`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/profile/Profile.command.ts#L40) | préfixe | Candidat local selon les données disponibles |
| [`maniacard`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L118) | préfixe | Candidat local selon les données disponibles |
| [`maniacommon`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareTop.subcommand.ts#L50) | préfixe | Candidat local selon les données disponibles |
| [`maniacompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareScore.subcommand.ts#L58) | préfixe | Candidat local selon les données disponibles |
| [`maniacompareprofile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareProfile.subcommand.ts#L58) | préfixe | Candidat local selon les données disponibles |
| [`maniafarmmaps`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmMaps.subcommand.ts#L50) | préfixe | Candidat local selon les données disponibles |
| [`maniagraphosutrackpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackPp.command.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniagraphosutrackrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackRank.command.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniagraphosutrackscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackScores.command.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniagraphrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphRank.command.ts#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniagraphskillscurve`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphSkillsCurve.command.ts#L52) | préfixe | Candidat local selon les données disponibles |
| [`maniagraphtophours`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopHours.command.ts#L54) | préfixe | Candidat local selon les données disponibles |
| [`maniamapperpinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperPinned.subcommand.ts#L43) | préfixe | Candidat local selon les données disponibles |
| [`maniamappertop`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperTop.subcommand.ts#L43) | préfixe | Candidat local selon les données disponibles |
| [`manianochoke`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/nochoke/NoChoke.command.ts#L42) | préfixe | Candidat local selon les données disponibles |
| [`maniaosustatsbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsBest.subcommand.ts#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosustatscompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCompare.subcommand.ts#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosustatscount`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCount.subcommand.ts#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosustatsplayers`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsPlayers.subcommand.ts#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosustatsscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsScores.subcommand.ts#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosutrackdecaydays`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayDays.subcommand.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosutrackdecayrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayRank.subcommand.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosutrackreachpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachPp.subcommand.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniaosutrackreachrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachRank.subcommand.ts#L51) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniapinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/pinned/Pinned.command.ts#L42) | préfixe | Candidat local selon les données disponibles |
| [`maniapp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Pp.command.ts#L41) | préfixe | Candidat local selon les données disponibles |
| [`maniarank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Rank.command.ts#L41) | préfixe | Service externe ou fonctionnalité Discord |
| [`maniareach`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Reach.command.ts#L41) | préfixe | Candidat local selon les données disponibles |
| [`maniarecent`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L128) | préfixe | Candidat local selon les données disponibles |
| [`maniarecentbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentBest.command.ts#L58) | préfixe | Candidat local selon les données disponibles |
| [`maniarecentlist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L99) | préfixe | Candidat local selon les données disponibles |
| [`maniarecentpass`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L140) | préfixe | Candidat local selon les données disponibles |
| [`maniarecentpasslist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L111) | préfixe | Candidat local selon les données disponibles |
| [`maniarecommend`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmRecommend.subcommand.ts#L50) | préfixe | Candidat local selon les données disponibles |
| [`maniaskills`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L75) | préfixe | Candidat local selon les données disponibles |
| [`maniatop`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/top/Top.command.ts#L42) | préfixe | Candidat local selon les données disponibles |
| [`maniatopif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/topif/TopIf.command.ts#L42) | préfixe | Candidat local selon les données disponibles |
| [`maniawhatif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/whatif/WhatIf.command.ts#L42) | préfixe | Candidat local selon les données disponibles |
| [`map`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/map/Map.command.ts#L48) | Partiel · /map pour la difficulté sélectionnée |
| [`mapper`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/Mapper.command.ts#L11) | slash et préfixe | Candidat local selon les données disponibles |
| [`mapper pinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperPinned.subcommand.ts#L12) | slash | Candidat local selon les données disponibles |
| [`mapper top`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperTop.subcommand.ts#L12) | slash | Adapté · /mapper top pour les scores locaux |
| [`matchcost`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/matchcost/MatchCost.command.ts#L49) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`medal`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/Medal.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`medal info`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalInfo.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`medal list`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalList.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`medal missing`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalMissing.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`medal recent`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalRecent.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`medal stats`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalStats.subcommand.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`medalinfo`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalInfo.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`medallist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalList.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalmissing`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalMissing.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalrecent`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalRecent.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalstats`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/medal/MedalStats.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`mostplayed`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mostplayed/MostPlayed.command.ts#L17) | slash et préfixe | Adapté · /mostplayed pour le profil local |
| [`nochoke`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/nochoke/NoChoke.command.ts#L11) | slash et préfixe | Adapté · /nochoke pour un score local compatible |
| [`osekai`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L14) | slash | Service externe ou fonctionnalité Discord |
| [`osekai accuracy`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L67) | slash | Service externe ou fonctionnalité Discord |
| [`osekai badges`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L115) | slash | Service externe ou fonctionnalité Discord |
| [`osekai level`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L57) | slash | Service externe ou fonctionnalité Discord |
| [`osekai mapsets`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L88) | slash | Service externe ou fonctionnalité Discord |
| [`osekai medals`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L25) | slash | Service externe ou fonctionnalité Discord |
| [`osekai playtime`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L126) | slash | Service externe ou fonctionnalité Discord |
| [`osekai pp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L47) | slash | Service externe ou fonctionnalité Discord |
| [`osekai rarity`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L36) | slash | Service externe ou fonctionnalité Discord |
| [`osekai replays`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L77) | slash | Service externe ou fonctionnalité Discord |
| [`osekai subscribers`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osekai/Osekai.command.ts#L104) | slash | Service externe ou fonctionnalité Discord |
| [`osustats`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStats.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`osustats best`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsBest.subcommand.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`osustats compare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCompare.subcommand.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`osustats count`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCount.subcommand.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`osustats players`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsPlayers.subcommand.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`osustats scores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsScores.subcommand.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`osustatsbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsBest.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatscompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCompare.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatscount`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCount.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatsplayers`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsPlayers.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatsscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsScores.subcommand.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`osutrack`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrack.command.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`osutrack decay`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecay.group.ts#L8) | groupe | Service externe ou fonctionnalité Discord |
| [`osutrack decay days`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayDays.subcommand.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`osutrack decay rank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayRank.subcommand.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`osutrack reach`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReach.group.ts#L8) | groupe | Service externe ou fonctionnalité Discord |
| [`osutrack reach pp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachPp.subcommand.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`osutrack reach rank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachRank.subcommand.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`osutrackdecaydays`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayDays.subcommand.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`osutrackdecayrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayRank.subcommand.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`osutrackreachpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachPp.subcommand.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`osutrackreachrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachRank.subcommand.ts#L20) | préfixe | Service externe ou fonctionnalité Discord |
| [`otr`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/Otr.command.ts#L10) | slash | Candidat à étudier |
| [`otr compare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/OtrCompare.subcommand.ts#L17) | slash | Candidat à étudier |
| [`otr history`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/OtrHistory.subcommand.ts#L16) | slash | Candidat à étudier |
| [`otr leaderboard`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/OtrLeaderboard.subcommand.ts#L17) | slash | Service externe ou fonctionnalité Discord |
| [`otr map`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/OtrMap.subcommand.ts#L18) | slash | Candidat à étudier |
| [`otr profile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/OtrProfile.subcommand.ts#L17) | slash | Candidat à étudier |
| [`otr scout`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/otr/OtrScout.subcommand.ts#L17) | slash | Candidat à étudier |
| [`ping`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/Ping.command.ts#L12) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`pinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/pinned/Pinned.command.ts#L11) | slash et préfixe | Candidat local selon les données disponibles |
| [`pp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Pp.command.ts#L10) | slash et préfixe | Candidat local selon les données disponibles |
| [`profile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/profile/Profile.command.ts#L11) | Partiel · /profile pour le profil local actif |
| [`rank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Rank.command.ts#L10) | slash et préfixe | Service externe ou fonctionnalité Discord |
| [`reach`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Reach.command.ts#L10) | slash et préfixe | Adapté · /reach pour un objectif de PP de profil |
| [`recent`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L13) | slash | Partiel · /compare ou /recent sur le profil local |
| [`recent best`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentBest.command.ts#L13) | slash | Candidat local selon les données disponibles |
| [`recent list`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L11) | slash | Candidat local selon les données disponibles |
| [`recent pass`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L40) | slash | Candidat local selon les données disponibles |
| [`recent passlist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L20) | slash | Candidat local selon les données disponibles |
| [`recent score`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L24) | slash | Partiel · /compare ou /recent sur le profil local |
| [`recentbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentBest.command.ts#L23) | préfixe | Candidat local selon les données disponibles |
| [`recentlist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L30) | préfixe | Candidat local selon les données disponibles |
| [`recentpass`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L69) | préfixe | Candidat local selon les données disponibles |
| [`recentpasslist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L40) | préfixe | Candidat local selon les données disponibles |
| [`recentscore`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L59) | préfixe | Candidat local selon les données disponibles |
| [`render`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/render/Render.command.ts#L25) | slash et préfixe | Candidat local selon les données disponibles |
| [`rp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L49) | slash | Candidat à étudier |
| [`rs`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L33) | slash | Partiel · /compare ou /recent sur le profil local |
| [`scorepost`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/scorepost/Scorepost.command.ts#L41) | slash et préfixe | Candidat local selon les données disponibles |
| [`search`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/search/Search.command.ts#L61) | slash et préfixe | Candidat à étudier |
| [`simulate`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/simulate/Simulate.command.ts#L79) | Partiel · /simulate pour osu!standard |
| [`skill`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L14) | slash | Candidat local selon les données disponibles |
| [`skill card`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L34) | slash | Candidat local selon les données disponibles |
| [`skill stats`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L27) | slash | Candidat local selon les données disponibles |
| [`skillcard`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L87) | préfixe | Candidat local selon les données disponibles |
| [`skills`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L44) | préfixe | Candidat local selon les données disponibles |
| [`snipe`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/Snipe.command.ts#L12) | slash | Service externe ou fonctionnalité Discord |
| [`snipe country`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipeCountry.group.ts#L8) | groupe | Service externe ou fonctionnalité Discord |
| [`snipe country list`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipeCountryList.subcommand.ts#L11) | slash | Service externe ou fonctionnalité Discord |
| [`snipe country stats`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipeCountryStats.subcommand.ts#L15) | slash | Service externe ou fonctionnalité Discord |
| [`snipe global`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipeGlobal.subcommand.ts#L9) | slash | Service externe ou fonctionnalité Discord |
| [`snipe player`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipePlayer.group.ts#L8) | groupe | Service externe ou fonctionnalité Discord |
| [`snipe player gain`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipePlayerGain.subcommand.ts#L18) | slash | Service externe ou fonctionnalité Discord |
| [`snipe player list`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipePlayerList.subcommand.ts#L19) | slash | Service externe ou fonctionnalité Discord |
| [`snipe player loss`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipePlayerLoss.subcommand.ts#L18) | slash | Service externe ou fonctionnalité Discord |
| [`snipe player stats`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/snipe/SnipePlayerStats.subcommand.ts#L15) | slash | Service externe ou fonctionnalité Discord |
| [`taiko`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/profile/Profile.command.ts#L19) | préfixe | Candidat local selon les données disponibles |
| [`taikocard`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L96) | préfixe | Candidat local selon les données disponibles |
| [`taikocommon`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareTop.subcommand.ts#L28) | préfixe | Candidat local selon les données disponibles |
| [`taikocompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareScore.subcommand.ts#L36) | préfixe | Candidat local selon les données disponibles |
| [`taikocompareprofile`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/compare/CompareProfile.subcommand.ts#L28) | préfixe | Candidat local selon les données disponibles |
| [`taikofarmmaps`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmMaps.subcommand.ts#L28) | préfixe | Candidat local selon les données disponibles |
| [`taikographosutrackpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackPp.command.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikographosutrackrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackRank.command.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikographosutrackscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphOsuTrackScores.command.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikographrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphRank.command.ts#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikographskillscurve`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphSkillsCurve.command.ts#L30) | préfixe | Candidat local selon les données disponibles |
| [`taikographtophours`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/graph/GraphTopHours.command.ts#L30) | préfixe | Candidat local selon les données disponibles |
| [`taikomapperpinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperPinned.subcommand.ts#L21) | préfixe | Candidat local selon les données disponibles |
| [`taikomappertop`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/mapper/MapperTop.subcommand.ts#L21) | préfixe | Candidat local selon les données disponibles |
| [`taikonochoke`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/nochoke/NoChoke.command.ts#L20) | préfixe | Candidat local selon les données disponibles |
| [`taikoosustatsbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsBest.subcommand.ts#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosustatscompare`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCompare.subcommand.ts#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosustatscount`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsCount.subcommand.ts#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosustatsplayers`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsPlayers.subcommand.ts#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosustatsscores`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osustats/OsuStatsScores.subcommand.ts#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosutrackdecaydays`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayDays.subcommand.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosutrackdecayrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackDecayRank.subcommand.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosutrackreachpp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachPp.subcommand.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoosutrackreachrank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/osutrack/OsuTrackReachRank.subcommand.ts#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikopinned`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/pinned/Pinned.command.ts#L20) | préfixe | Candidat local selon les données disponibles |
| [`taikopp`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Pp.command.ts#L19) | préfixe | Candidat local selon les données disponibles |
| [`taikorank`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Rank.command.ts#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`taikoreach`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/reach/Reach.command.ts#L19) | préfixe | Candidat local selon les données disponibles |
| [`taikorecent`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L80) | préfixe | Candidat local selon les données disponibles |
| [`taikorecentbest`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentBest.command.ts#L34) | préfixe | Candidat local selon les données disponibles |
| [`taikorecentlist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L51) | préfixe | Candidat local selon les données disponibles |
| [`taikorecentpass`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/Recent.command.ts#L92) | préfixe | Candidat local selon les données disponibles |
| [`taikorecentpasslist`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/recent/RecentList.command.ts#L63) | préfixe | Candidat local selon les données disponibles |
| [`taikorecommend`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/farm/FarmRecommend.subcommand.ts#L28) | préfixe | Candidat local selon les données disponibles |
| [`taikoskills`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/skill/Skill.command.ts#L53) | préfixe | Candidat local selon les données disponibles |
| [`taikotop`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/top/Top.command.ts#L20) | préfixe | Candidat local selon les données disponibles |
| [`taikotopif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/topif/TopIf.command.ts#L20) | préfixe | Candidat local selon les données disponibles |
| [`taikowhatif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/whatif/WhatIf.command.ts#L20) | préfixe | Candidat local selon les données disponibles |
| [`top`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/top/Top.command.ts#L11) | Partiel · /top pour le profil local actif |
| [`topif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/topif/TopIf.command.ts#L11) | slash et préfixe | Candidat local selon les données disponibles |
| [`unlink`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/unlink/Unlink.command.ts#L10) | slash | Service externe ou fonctionnalité Discord |
| [`unlink osu`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/unlink/UnlinkOsu.subcommand.ts#L15) | slash | Service externe ou fonctionnalité Discord |
| [`unlink twitch`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/general/unlink/UnlinkTwitch.subcommand.ts#L16) | slash | Service externe ou fonctionnalité Discord |
| [`whatif`](https://github.com/hitorisu39/gurabot/blob/4516a2db049ecf0d6ed1881f0e319e33ac61c186/src/commands/osu/whatif/WhatIf.command.ts#L11) | slash et préfixe | Adapté · /whatif pour le PP pondéré du profil actif |

### Bathbot

| Commande | Surface | Ritsu |
|---|---|---|
| [`/attributes`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L18) | slash | Adapté · /attributes pour la difficulté locale |
| [`/attributes ar`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L79) | slash | Candidat local selon les données disponibles |
| [`/attributes cs`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L103) | slash | Candidat local selon les données disponibles |
| [`/attributes hp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L127) | slash | Candidat local selon les données disponibles |
| [`/attributes od`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L151) | slash | Candidat local selon les données disponibles |
| [`/avatar`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/avatar.rs#L26) | slash | Candidat local selon les données disponibles |
| [`/badges`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/badges/mod.rs#L20) | slash | Service externe ou fonctionnalité Discord |
| [`/badges query`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/badges/mod.rs#L40) | slash | Service externe ou fonctionnalité Discord |
| [`/badges user`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/badges/mod.rs#L59) | slash | Service externe ou fonctionnalité Discord |
| [`/bg`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/fun/bg_game/mod.rs#L95) | slash | Candidat à étudier |
| [`/bookmarks`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/bookmarks/slash.rs#L31) | slash | Adapté · /favorite via une collection locale |
| [`/builder`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/embed_builder.rs#L36) | slash | Service externe ou fonctionnalité Discord |
| [`/builder copy`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/embed_builder.rs#L52) | slash | Service externe ou fonctionnalité Discord |
| [`/bws`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/bws.rs#L28) | slash | Service externe ou fonctionnalité Discord |
| [`/card`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/cards.rs#L79) | slash | Candidat à étudier |
| [`/claimname`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/claim_name.rs#L42) | slash | Candidat à étudier |
| [`/compare`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/mod.rs#L34) | slash | Partiel · /compare ou /recent sur le profil local |
| [`/compare mostplayed`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/mod.rs#L208) | slash | Candidat local selon les données disponibles |
| [`/compare profile`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/mod.rs#L155) | slash | Adapté · /compare profile pour deux profils locaux |
| [`/compare score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/mod.rs#L60) | slash | Adapté · /compare pour les scores locaux |
| [`/compare top`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/mod.rs#L183) | slash | Candidat local selon les données disponibles |
| [`/config`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/config.rs#L101) | slash | Service externe ou fonctionnalité Discord |
| [`/cs`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/score.rs#L62) | slash | Partiel · /compare ou /recent sur le profil local |
| [`/ct`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/common.rs#L28) | slash | Candidat à étudier |
| [`/dailychallenge`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/daily_challenge/mod.rs#L17) | slash | Candidat à étudier |
| [`/dailychallenge user`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/daily_challenge/mod.rs#L28) | slash | Candidat à étudier |
| [`/fix`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/fix.rs#L40) | slash | Candidat local selon les données disponibles |
| [`/graph`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L79) | slash | Candidat à étudier |
| [`/graph bpm`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L106) | slash | Candidat à étudier |
| [`/graph medals`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L144) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L153) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack accuracy`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L226) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack grades`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L237) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack hit_ratios`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L198) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack playcount`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L212) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack pp_rank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L170) | slash | Service externe ou fonctionnalité Discord |
| [`/graph osutrack score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L184) | slash | Service externe ou fonctionnalité Discord |
| [`/graph playcount_replays`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L250) | slash | Candidat à étudier |
| [`/graph rank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L267) | slash | Service externe ou fonctionnalité Discord |
| [`/graph score_rank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L284) | slash | Service externe ou fonctionnalité Discord |
| [`/graph snipe_count`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L314) | slash | Service externe ou fonctionnalité Discord |
| [`/graph sniped`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L301) | slash | Service externe ou fonctionnalité Discord |
| [`/graph strains`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L123) | slash | Candidat à étudier |
| [`/graph top`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/mod.rs#L325) | slash | Candidat à étudier |
| [`/help`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/help/interaction.rs#L29) | slash | Candidat à étudier |
| [`/higherlower`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/fun/higherlower_game.rs#L25) | slash | Candidat à étudier |
| [`/higherlower pp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/fun/higherlower_game.rs#L34) | slash | Candidat à étudier |
| [`/leaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/leaderboard.rs#L38) | slash | Service externe ou fonctionnalité Discord |
| [`/map`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/map.rs#L27) | Partiel · /map pour la difficulté sélectionnée |
| [`/mapper`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/mapper.rs#L37) | slash | Candidat local selon les données disponibles |
| [`/matchcompare`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_compare.rs#L17) | slash | Candidat à étudier |
| [`/matchcost`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_costs.rs#L27) | slash | Service externe ou fonctionnalité Discord |
| [`/matchlive`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_live.rs#L25) | slash | Service externe ou fonctionnalité Discord |
| [`/matchlive track`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_live.rs#L40) | slash | Service externe ou fonctionnalité Discord |
| [`/matchlive untrack`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_live.rs#L49) | slash | Service externe ou fonctionnalité Discord |
| [`/medal`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L26) | slash | Service externe ou fonctionnalité Discord |
| [`/medal common`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L65) | slash | Service externe ou fonctionnalité Discord |
| [`/medal info`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L130) | slash | Service externe ou fonctionnalité Discord |
| [`/medal list`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L156) | slash | Service externe ou fonctionnalité Discord |
| [`/medal missing`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L186) | slash | Service externe ou fonctionnalité Discord |
| [`/medal recent`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L217) | slash | Service externe ou fonctionnalité Discord |
| [`/medal stats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/mod.rs#L235) | slash | Service externe ou fonctionnalité Discord |
| [`/minesweeper`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/fun/minesweeper.rs#L14) | slash | Candidat à étudier |
| [`/mostplayed`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/most_played.rs#L23) | slash | Adapté · /mostplayed pour le profil local |
| [`/nochoke`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/nochoke.rs#L32) | slash | Adapté · /nochoke pour un score local compatible |
| [`/osc`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/counts.rs#L25) | slash | Candidat à étudier |
| [`/osekai`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L20) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai badges`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L48) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai loved_mapsets`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L55) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai medal_count`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L62) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai ranked_mapsets`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L69) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai replays`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L80) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai standard_deviation`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L87) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai subscribers`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L99) | slash | Service externe ou fonctionnalité Discord |
| [`/osekai total_pp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osekai/mod.rs#L106) | slash | Service externe ou fonctionnalité Discord |
| [`/osustats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/mod.rs#L21) | slash | Service externe ou fonctionnalité Discord |
| [`/osustats best`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/mod.rs#L124) | slash | Service externe ou fonctionnalité Discord |
| [`/osustats count`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/mod.rs#L40) | slash | Service externe ou fonctionnalité Discord |
| [`/osustats players`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/mod.rs#L54) | slash | Service externe ou fonctionnalité Discord |
| [`/osustats scores`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/mod.rs#L78) | slash | Service externe ou fonctionnalité Discord |
| [`/owner`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/owner/mod.rs#L20) | slash | Service externe ou fonctionnalité Discord |
| [`/owner add_bg`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/owner/mod.rs#L37) | slash | Service externe ou fonctionnalité Discord |
| [`/owner requestmembers`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/owner/mod.rs#L50) | slash | Service externe ou fonctionnalité Discord |
| [`/owner tracking`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/owner/mod.rs#L64) | slash | Service externe ou fonctionnalité Discord |
| [`/pc`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/profile.rs#L39) | slash | Candidat à étudier |
| [`/pinned`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pinned.rs#L47) | slash | Candidat local selon les données disponibles |
| [`/pp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pp.rs#L21) | slash | Candidat local selon les données disponibles |
| [`/profile`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/profile.rs#L25) | Partiel · /profile pour le profil local actif |
| [`/rank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/mod.rs#L18) | slash | Service externe ou fonctionnalité Discord |
| [`/rank pp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/mod.rs#L27) | slash | Service externe ou fonctionnalité Discord |
| [`/rank score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/mod.rs#L60) | slash | Service externe ou fonctionnalité Discord |
| [`/ranked`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranked.rs#L42) | slash | Service externe ou fonctionnalité Discord |
| [`/ranking`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/mod.rs#L14) | slash | Service externe ou fonctionnalité Discord |
| [`/ranking country`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/mod.rs#L63) | slash | Service externe ou fonctionnalité Discord |
| [`/ranking pp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/mod.rs#L28) | slash | Service externe ou fonctionnalité Discord |
| [`/ranking score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/mod.rs#L41) | slash | Service externe ou fonctionnalité Discord |
| [`/ratios`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ratios.rs#L26) | slash | Candidat à étudier |
| [`/rb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L301) | slash | Candidat à étudier |
| [`/recent`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L28) | slash | Partiel · /compare ou /recent sur le profil local |
| [`/recent best`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L78) | slash | Candidat local selon les données disponibles |
| [`/recent fix`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L254) | slash | Candidat local selon les données disponibles |
| [`/recent leaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L169) | slash | Service externe ou fonctionnalité Discord |
| [`/recent list`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L205) | slash | Candidat local selon les données disponibles |
| [`/recent score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/mod.rs#L48) | slash | Partiel · /compare ou /recent sur le profil local |
| [`/relax`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/relax/mod.rs#L24) | slash | Candidat à étudier |
| [`/relax profile`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/relax/mod.rs#L41) | slash | Candidat à étudier |
| [`/relax top`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/relax/mod.rs#L54) | slash | Candidat à étudier |
| [`/render`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/render.rs#L69) | slash | Candidat local selon les données disponibles |
| [`/render replay`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/render.rs#L88) | slash | Candidat local selon les données disponibles |
| [`/render score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/render.rs#L95) | slash | Candidat local selon les données disponibles |
| [`/render settings`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/render.rs#L102) | slash | Candidat local selon les données disponibles |
| [`/render settings copy`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/render.rs#L130) | slash | Candidat local selon les données disponibles |
| [`/roll`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/roll.rs#L15) | slash | Service externe ou fonctionnalité Discord |
| [`/rs`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L786) | slash | Partiel · /compare ou /recent sur le profil local |
| [`/score`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/score.rs#L116) | slash | Candidat local selon les données disponibles |
| [`/search`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/map_search.rs#L20) | slash | Candidat à étudier |
| [`/serverconfig`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/server_config.rs#L20) | slash | Service externe ou fonctionnalité Discord |
| [`/serverconfig authorities`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/server_config.rs#L34) | slash | Service externe ou fonctionnalité Discord |
| [`/serverconfig authorities add`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/server_config.rs#L67) | slash | Service externe ou fonctionnalité Discord |
| [`/serverconfig authorities remove`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/server_config.rs#L79) | slash | Service externe ou fonctionnalité Discord |
| [`/serverconfig edit`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/server_config.rs#L104) | slash | Service externe ou fonctionnalité Discord |
| [`/serverleaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/serverleaderboard.rs#L17) | slash | Service externe ou fonctionnalité Discord |
| [`/serverleaderboard all_modes`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/serverleaderboard.rs#L60) | slash | Service externe ou fonctionnalité Discord |
| [`/serverleaderboard ctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/serverleaderboard.rs#L102) | slash | Service externe ou fonctionnalité Discord |
| [`/serverleaderboard mania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/serverleaderboard.rs#L114) | slash | Service externe ou fonctionnalité Discord |
| [`/serverleaderboard osu`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/serverleaderboard.rs#L78) | slash | Service externe ou fonctionnalité Discord |
| [`/serverleaderboard taiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/serverleaderboard.rs#L90) | slash | Service externe ou fonctionnalité Discord |
| [`/simulate`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/simulate/mod.rs#L41) | Partiel · /simulate pour osu!standard |
| [`/skin`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/skin.rs#L20) | slash | Service externe ou fonctionnalité Discord |
| [`/skin check`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/skin.rs#L42) | slash | Service externe ou fonctionnalité Discord |
| [`/skin set`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/skin.rs#L150) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L27) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe country`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L43) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe country list`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L84) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe country stats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L105) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe player`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L114) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe player gain`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L129) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe player list`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L144) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe player loss`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L165) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe player sniped`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L191) | slash | Service externe ou fonctionnalité Discord |
| [`/snipe player stats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L180) | slash | Service externe ou fonctionnalité Discord |
| [`/sniped`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/mod.rs#L191) | slash | Service externe ou fonctionnalité Discord |
| [`/song`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/mod.rs#L106) | slash | Service externe ou fonctionnalité Discord |
| [`/top`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L49) | Partiel · /top pour le profil local actif |
| [`/topif`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/if_.rs#L37) | slash | Candidat local selon les données disponibles |
| [`/topold`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L43) | slash | Candidat local selon les données disponibles |
| [`/topold ctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L271) | slash | Candidat local selon les données disponibles |
| [`/topold mania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L344) | slash | Candidat local selon les données disponibles |
| [`/topold osu`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L60) | slash | Candidat local selon les données disponibles |
| [`/topold taiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L184) | slash | Candidat local selon les données disponibles |
| [`/track`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/mod.rs#L23) | slash | Service externe ou fonctionnalité Discord |
| [`/track add`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/mod.rs#L35) | slash | Service externe ou fonctionnalité Discord |
| [`/track remove`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/mod.rs#L85) | slash | Service externe ou fonctionnalité Discord |
| [`/track remove all`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/mod.rs#L107) | slash | Service externe ou fonctionnalité Discord |
| [`/track remove user`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/mod.rs#L98) | slash | Service externe ou fonctionnalité Discord |
| [`/trackstream`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/twitch/mod.rs#L12) | slash | Service externe ou fonctionnalité Discord |
| [`/trackstream add`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/twitch/mod.rs#L29) | slash | Service externe ou fonctionnalité Discord |
| [`/trackstream remove`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/twitch/mod.rs#L41) | slash | Service externe ou fonctionnalité Discord |
| [`/whatif`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/whatif.rs#L52) | slash | Adapté · /whatif pour le PP pondéré du profil actif |
| [`Bookmark map`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/bookmarks/message.rs#L16) | menu contextuel | Candidat local selon les données disponibles |
| [`addstream`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/twitch/addstream.rs#L10) | préfixe | Service externe ou fonctionnalité Discord |
| [`ar`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L177) | préfixe | Candidat local selon les données disponibles |
| [`authorities`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/authorities.rs#L21) | préfixe | Service externe ou fonctionnalité Discord |
| [`avatar`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/avatar.rs#L41) | préfixe | Candidat local selon les données disponibles |
| [`backgroundgame`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/fun/bg_game/mod.rs#L37) | préfixe | Candidat à étudier |
| [`badges`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/badges/query.rs#L24) | préfixe | Service externe ou fonctionnalité Discord |
| [`badgesuser`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/badges/user.rs#L48) | préfixe | Service externe ou fonctionnalité Discord |
| [`bombsaway`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/bombsaway.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`bookmarks`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/bookmarks/slash.rs#L91) | préfixe | Adapté · /favorite via une collection locale |
| [`bws`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/bws.rs#L126) | préfixe | Service externe ou fonctionnalité Discord |
| [`card`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/cards.rs#L111) | préfixe | Candidat à étudier |
| [`cardctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/cards.rs#L136) | préfixe | Candidat à étudier |
| [`cardmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/cards.rs#L149) | préfixe | Candidat à étudier |
| [`cardtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/cards.rs#L123) | préfixe | Candidat à étudier |
| [`catchit`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/catchit.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`changelog`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/changelog.rs#L23) | préfixe | Service externe ou fonctionnalité Discord |
| [`claimname`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/claim_name.rs#L59) | préfixe | Candidat à étudier |
| [`commands`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/commands.rs#L84) | préfixe | Service externe ou fonctionnalité Discord |
| [`common`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/common.rs#L59) | préfixe | Candidat à étudier |
| [`commonctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/common.rs#L110) | préfixe | Candidat à étudier |
| [`commonmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/common.rs#L76) | préfixe | Candidat à étudier |
| [`commontaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/common.rs#L93) | préfixe | Candidat à étudier |
| [`compare`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/score.rs#L171) | préfixe | Partiel · /compare ou /recent sur le profil local |
| [`comparectb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/score.rs#L334) | préfixe | Candidat local selon les données disponibles |
| [`comparemania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/score.rs#L373) | préfixe | Candidat local selon les données disponibles |
| [`comparetaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/score.rs#L304) | préfixe | Candidat local selon les données disponibles |
| [`countryranking`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/countries.rs#L16) | préfixe | Service externe ou fonctionnalité Discord |
| [`countryrankingctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/countries.rs#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`countryrankingmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/countries.rs#L24) | préfixe | Service externe ou fonctionnalité Discord |
| [`countryrankingtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/countries.rs#L32) | préfixe | Service externe ou fonctionnalité Discord |
| [`countrysnipelist`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/country_snipe_list.rs#L23) | préfixe | Service externe ou fonctionnalité Discord |
| [`countrysnipelistctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/country_snipe_list.rs#L53) | préfixe | Service externe ou fonctionnalité Discord |
| [`countrysnipelistmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/country_snipe_list.rs#L89) | préfixe | Service externe ou fonctionnalité Discord |
| [`countrysnipestats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/country_snipe_stats.rs#L26) | préfixe | Service externe ou fonctionnalité Discord |
| [`countrysnipestatsctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/country_snipe_stats.rs#L52) | préfixe | Service externe ou fonctionnalité Discord |
| [`countrysnipestatsmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/country_snipe_stats.rs#L78) | préfixe | Service externe ou fonctionnalité Discord |
| [`cs`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L194) | préfixe | Partiel · /compare ou /recent sur le profil local |
| [`ctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/profile.rs#L134) | préfixe | Candidat à étudier |
| [`dailychallenge`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/daily_challenge/today.rs#L27) | préfixe | Candidat à étudier |
| [`dailychallengeuser`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/daily_challenge/user.rs#L39) | préfixe | Candidat à étudier |
| [`ding`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/ding.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`fireandflames`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/fireandflames.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`fireflies`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/fireflies.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`fix`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/fix.rs#L151) | préfixe | Candidat local selon les données disponibles |
| [`flamingo`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/flamingo.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphbpm`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/bpm.rs#L70) | préfixe | Candidat à étudier |
| [`graphmedals`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/medals.rs#L39) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphplaycount`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/playcount_replays.rs#L72) | préfixe | Candidat à étudier |
| [`graphrank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/rank.rs#L54) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphrankctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/rank.rs#L82) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphrankmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/rank.rs#L99) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphranktaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/rank.rs#L66) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphscorerank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/score_rank.rs#L55) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphscorerankctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/score_rank.rs#L87) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphscorerankmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/score_rank.rs#L104) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphscoreranktaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/score_rank.rs#L71) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphsnipecount`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/snipe_count.rs#L40) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphsnipecountctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/snipe_count.rs#L56) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphsnipecountmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/snipe_count.rs#L73) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphsniped`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/sniped.rs#L38) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphsnipedctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/sniped.rs#L54) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphsnipedmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/sniped.rs#L71) | préfixe | Service externe ou fonctionnalité Discord |
| [`graphstrains`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/map_strains.rs#L69) | préfixe | Candidat à étudier |
| [`graphstrainsctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/map_strains.rs#L115) | préfixe | Candidat à étudier |
| [`graphstrainsmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/map_strains.rs#L138) | préfixe | Candidat à étudier |
| [`graphstrainstaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/graphs/map_strains.rs#L92) | préfixe | Candidat à étudier |
| [`help`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/help/message.rs#L27) | préfixe | Candidat à étudier |
| [`hp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L211) | préfixe | Candidat local selon les données disponibles |
| [`invite`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/invite.rs#L16) | préfixe | Service externe ou fonctionnalité Discord |
| [`leaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/leaderboard.rs#L214) | préfixe | Service externe ou fonctionnalité Discord |
| [`leaderboardctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/leaderboard.rs#L268) | préfixe | Service externe ou fonctionnalité Discord |
| [`leaderboardmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/leaderboard.rs#L295) | préfixe | Service externe ou fonctionnalité Discord |
| [`leaderboardtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/leaderboard.rs#L241) | préfixe | Service externe ou fonctionnalité Discord |
| [`link`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/link.rs#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`mania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/profile.rs#L100) | préfixe | Candidat local selon les données disponibles |
| [`map`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/map.rs#L218) | Partiel · /map pour la difficulté sélectionnée |
| [`mapper`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/mapper.rs#L108) | préfixe | Candidat local selon les données disponibles |
| [`mapperctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/mapper.rs#L170) | préfixe | Candidat local selon les données disponibles |
| [`mappermania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/mapper.rs#L128) | préfixe | Candidat local selon les données disponibles |
| [`mappertaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/mapper.rs#L149) | préfixe | Candidat local selon les données disponibles |
| [`matchcosts`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_costs.rs#L102) | préfixe | Service externe ou fonctionnalité Discord |
| [`matchlive`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_live.rs#L63) | préfixe | Service externe ou fonctionnalité Discord |
| [`matchliveremove`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/match_live.rs#L96) | préfixe | Service externe ou fonctionnalité Discord |
| [`medal`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/medal.rs#L34) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalrecent`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/recent.rs#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalscommon`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/common.rs#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalslist`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/list.rs#L45) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalsmissing`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/missing.rs#L19) | préfixe | Service externe ou fonctionnalité Discord |
| [`medalstats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/medals/stats.rs#L33) | préfixe | Service externe ou fonctionnalité Discord |
| [`minesweeper`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/fun/minesweeper.rs#L45) | préfixe | Candidat à étudier |
| [`mostplayed`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/most_played.rs#L38) | préfixe | Adapté · /mostplayed pour le profil local |
| [`mostplayedcommon`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/most_played.rs#L21) | préfixe | Candidat local selon les données disponibles |
| [`nochokes`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/nochoke.rs#L140) | préfixe | Candidat local selon les données disponibles |
| [`nochokesctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/nochoke.rs#L174) | préfixe | Candidat local selon les données disponibles |
| [`nochokestaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/nochoke.rs#L156) | préfixe | Candidat local selon les données disponibles |
| [`od`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/attributes.rs#L228) | préfixe | Candidat local selon les données disponibles |
| [`osu`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/profile.rs#L83) | préfixe | Candidat local selon les données disponibles |
| [`osustatscount`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/counts.rs#L72) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatscountctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/counts.rs#L126) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatscountmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/counts.rs#L90) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatscounttaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/counts.rs#L108) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatsglobals`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/globals.rs#L34) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatsglobalsctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/globals.rs#L127) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatsglobalsmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/globals.rs#L65) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatsglobalstaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/globals.rs#L96) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatslist`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/list.rs#L230) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatslistctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/list.rs#L311) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatslistmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/list.rs#L257) | préfixe | Service externe ou fonctionnalité Discord |
| [`osustatslisttaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/osustats/list.rs#L284) | préfixe | Service externe ou fonctionnalité Discord |
| [`ping`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/ping.rs#L26) | préfixe | Service externe ou fonctionnalité Discord |
| [`pinned`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pinned.rs#L122) | préfixe | Candidat local selon les données disponibles |
| [`pinnedctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pinned.rs#L146) | préfixe | Candidat local selon les données disponibles |
| [`pinnedmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pinned.rs#L166) | préfixe | Candidat local selon les données disponibles |
| [`pinnedtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pinned.rs#L134) | préfixe | Candidat local selon les données disponibles |
| [`playersnipelist`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/player_snipe_list.rs#L24) | préfixe | Service externe ou fonctionnalité Discord |
| [`playersnipelistctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/player_snipe_list.rs#L54) | préfixe | Service externe ou fonctionnalité Discord |
| [`playersnipelistmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/player_snipe_list.rs#L83) | préfixe | Service externe ou fonctionnalité Discord |
| [`playersnipestats`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/player_snipe_stats.rs#L28) | préfixe | Service externe ou fonctionnalité Discord |
| [`playersnipestatsctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/player_snipe_stats.rs#L49) | préfixe | Service externe ou fonctionnalité Discord |
| [`playersnipestatsmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/player_snipe_stats.rs#L70) | préfixe | Service externe ou fonctionnalité Discord |
| [`pp`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pp.rs#L88) | préfixe | Candidat local selon les données disponibles |
| [`ppctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pp.rs#L153) | préfixe | Candidat local selon les données disponibles |
| [`ppmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pp.rs#L109) | préfixe | Candidat local selon les données disponibles |
| [`ppranking`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L334) | préfixe | Service externe ou fonctionnalité Discord |
| [`pprankingctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L424) | préfixe | Service externe ou fonctionnalité Discord |
| [`pprankingmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L364) | préfixe | Service externe ou fonctionnalité Discord |
| [`pprankingtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L394) | préfixe | Service externe ou fonctionnalité Discord |
| [`pptaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/pp.rs#L131) | préfixe | Candidat local selon les données disponibles |
| [`prefix`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/prefix.rs#L12) | préfixe | Service externe ou fonctionnalité Discord |
| [`pretender`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/pretender.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`profilecompare`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/profile.rs#L281) | préfixe | Candidat local selon les données disponibles |
| [`profilecomparectb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/profile.rs#L347) | préfixe | Candidat local selon les données disponibles |
| [`profilecomparemania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/profile.rs#L303) | préfixe | Candidat local selon les données disponibles |
| [`profilecomparetaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/compare/profile.rs#L325) | préfixe | Candidat local selon les données disponibles |
| [`rank`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/pp.rs#L260) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/pp.rs#L329) | préfixe | Service externe ou fonctionnalité Discord |
| [`ranked`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranked.rs#L59) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankedscoreranking`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L454) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankedscorerankingctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L478) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankedscorerankingmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L462) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankedscorerankingtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ranking/players.rs#L470) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/pp.rs#L283) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankrankedscore`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/score.rs#L24) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankrankedscorectb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/score.rs#L90) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankrankedscoremania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/score.rs#L46) | préfixe | Service externe ou fonctionnalité Discord |
| [`rankrankedscoretaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/score.rs#L68) | préfixe | Service externe ou fonctionnalité Discord |
| [`ranktaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/rank/pp.rs#L306) | préfixe | Service externe ou fonctionnalité Discord |
| [`ratios`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/ratios.rs#L46) | préfixe | Candidat à étudier |
| [`recent`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L38) | préfixe | Partiel · /compare ou /recent sur le profil local |
| [`recentbest`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L303) | préfixe | Candidat local selon les données disponibles |
| [`recentbestctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L424) | préfixe | Candidat local selon les données disponibles |
| [`recentbestmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L348) | préfixe | Candidat local selon les données disponibles |
| [`recentbesttaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L386) | préfixe | Candidat local selon les données disponibles |
| [`recentctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L119) | préfixe | Candidat local selon les données disponibles |
| [`recentctbleaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/leaderboard.rs#L83) | préfixe | Service externe ou fonctionnalité Discord |
| [`recentleaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/leaderboard.rs#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`recentlist`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L41) | préfixe | Candidat local selon les données disponibles |
| [`recentlistctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L110) | préfixe | Candidat local selon les données disponibles |
| [`recentlistmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L64) | préfixe | Candidat local selon les données disponibles |
| [`recentlistpass`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L133) | préfixe | Candidat local selon les données disponibles |
| [`recentlistpassctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L212) | préfixe | Candidat local selon les données disponibles |
| [`recentlistpassmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L159) | préfixe | Candidat local selon les données disponibles |
| [`recentlistpasstaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L185) | préfixe | Candidat local selon les données disponibles |
| [`recentlisttaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/list.rs#L87) | préfixe | Candidat local selon les données disponibles |
| [`recentmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L65) | préfixe | Candidat local selon les données disponibles |
| [`recentmanialeaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/leaderboard.rs#L47) | préfixe | Service externe ou fonctionnalité Discord |
| [`recentpass`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L145) | préfixe | Candidat local selon les données disponibles |
| [`recentpassctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L235) | préfixe | Candidat local selon les données disponibles |
| [`recentpassmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L175) | préfixe | Candidat local selon les données disponibles |
| [`recentpasstaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L205) | préfixe | Candidat local selon les données disponibles |
| [`recenttaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/score.rs#L92) | préfixe | Candidat local selon les données disponibles |
| [`recenttaikoleaderboard`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/recent/leaderboard.rs#L65) | préfixe | Service externe ou fonctionnalité Discord |
| [`relaxprofile`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/relax/profile.rs#L65) | préfixe | Candidat à étudier |
| [`relaxtop`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/relax/top.rs#L50) | préfixe | Candidat à étudier |
| [`removestream`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/twitch/removestream.rs#L10) | préfixe | Service externe ou fonctionnalité Discord |
| [`rockefeller`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/rockefeller.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`roll`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/roll.rs#L35) | préfixe | Service externe ou fonctionnalité Discord |
| [`saygoodbye`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/saygoodbye.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`search`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/map_search.rs#L579) | préfixe | Candidat à étudier |
| [`serverconfig`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/utility/server_config.rs#L175) | préfixe | Service externe ou fonctionnalité Discord |
| [`simulate`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/simulate/mod.rs#L192) | Partiel · /simulate pour osu!standard |
| [`simulatectb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/simulate/mod.rs#L280) | préfixe | Candidat local selon les données disponibles |
| [`simulatemania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/simulate/mod.rs#L322) | préfixe | Candidat local selon les données disponibles |
| [`simulatetaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/simulate/mod.rs#L240) | préfixe | Candidat local selon les données disponibles |
| [`sniped`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped.rs#L29) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped.rs#L50) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedgain`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped_difference.rs#L17) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedgainctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped_difference.rs#L34) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedgainmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped_difference.rs#L58) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedloss`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped_difference.rs#L75) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedlossctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped_difference.rs#L99) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedlossmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped_difference.rs#L128) | préfixe | Service externe ou fonctionnalité Discord |
| [`snipedmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/snipe/sniped.rs#L71) | préfixe | Service externe ou fonctionnalité Discord |
| [`sotarks`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/mapper.rs#L191) | préfixe | Candidat à étudier |
| [`startagain`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/startagain.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`taiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/profile.rs#L117) | préfixe | Candidat local selon les données disponibles |
| [`tijdmachine`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/songs/tijdmachine.rs#L4) | préfixe | Service externe ou fonctionnalité Discord |
| [`top`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L160) | Partiel · /top pour le profil local actif |
| [`topctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L265) | préfixe | Candidat local selon les données disponibles |
| [`topif`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/if_.rs#L131) | préfixe | Candidat local selon les données disponibles |
| [`topifctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/if_.rs#L179) | préfixe | Candidat local selon les données disponibles |
| [`topiftaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/if_.rs#L155) | préfixe | Candidat local selon les données disponibles |
| [`topmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L195) | préfixe | Candidat local selon les données disponibles |
| [`topold`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L423) | préfixe | Candidat local selon les données disponibles |
| [`topoldctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L516) | préfixe | Candidat local selon les données disponibles |
| [`topoldmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L460) | préfixe | Candidat local selon les données disponibles |
| [`topoldtaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/old.rs#L487) | préfixe | Candidat local selon les données disponibles |
| [`toptaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/top/mod.rs#L230) | préfixe | Candidat local selon les données disponibles |
| [`track`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/track.rs#L149) | préfixe | Service externe ou fonctionnalité Discord |
| [`trackctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/track.rs#L245) | préfixe | Service externe ou fonctionnalité Discord |
| [`trackedstreams`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/twitch/tracked.rs#L9) | préfixe | Service externe ou fonctionnalité Discord |
| [`tracklist`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/track_list.rs#L24) | préfixe | Service externe ou fonctionnalité Discord |
| [`trackmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/track.rs#L181) | préfixe | Service externe ou fonctionnalité Discord |
| [`tracktaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/track.rs#L213) | préfixe | Service externe ou fonctionnalité Discord |
| [`untrack`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/untrack.rs#L14) | préfixe | Service externe ou fonctionnalité Discord |
| [`untrackall`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/tracking/untrack_all.rs#L8) | préfixe | Service externe ou fonctionnalité Discord |
| [`whatif`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/whatif.rs#L100) | préfixe | Adapté · /whatif pour le PP pondéré du profil actif |
| [`whatifctb`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/whatif.rs#L163) | préfixe | Candidat local selon les données disponibles |
| [`whatifmania`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/whatif.rs#L121) | préfixe | Candidat local selon les données disponibles |
| [`whatiftaiko`](https://github.com/MaxOhn/Bathbot/blob/d490f1fc4a1f11eab4bc28b326edefc9b73b4062/bathbot/src/commands/osu/whatif.rs#L142) | préfixe | Candidat local selon les données disponibles |
