# Ritsu — démarrage rapide

1. Extraire tout le ZIP dans un nouveau dossier.
2. Ouvrir **Launch Ritsu.cmd**. Le programme reste nommé `torii.exe` et affiche l'identité Torii : c'est volontaire pour cette première version.
3. Choisir le dossier osu!lazer détecté ou le sélectionner manuellement pour partager ses données. Il est aussi possible de conserver le dossier local. Après confirmation, rouvrir le jeu ; aucun compte n'est nécessaire.
4. Glisser des maps `.osz` ou skins `.osk` dans la fenêtre. L'import est aussi disponible dans **Settings → Maintenance → Import files**.
5. Choisir **Play → Solo**. Les mods Modosu sont disponibles dans la liste commune des mods osu!.

Les scores et replays des parties normales sont locaux. Le comportement d'autoplay et des replays reste celui de Torii : regarder un replay ne crée pas un nouveau score. Un fichier `.osr` importé nécessite sa map locale correspondante.

Les archives d'origine sont conservées après import.

## Profils et entraînement

Ouvrir les profils avec le bouton portant le nom du joueur dans la barre du haut, ou avec **Ctrl+P**. Les pastilles sous le nom permettent de choisir un profil ; le bouton **+** en crée un. Un clic droit sur une pastille permet de renommer ou supprimer le profil et, pour le profil actif, de configurer l'inclusion de Relax et Autopilot dans les PP. La page affiche ses scores récents et meilleures performances pour le mode sélectionné dans la barre du jeu.

Le profil actif reçoit les nouveaux scores ; les scores déjà présents ne sont pas automatiquement associés. Dans la sélection d'une map, ouvrir **Ranking**, puis choisir **All profiles** dans le menu du classement pour afficher tous les scores locaux de cette map, y compris ceux sans association à un profil. **Current profile** filtre sur le profil actif. Dans la sélection classique, ce même menu se trouve au-dessus des scores.

Les PP sont des estimations locales. Le rang et son graphique comparent uniquement les profils locaux pour le mode sélectionné : ils ne représentent pas un classement officiel. L'historique est reconstruit avec les profils et réglages PP actuels. Supprimer un profil retire ses associations mais conserve les scores et replays dans le stockage partagé.

Les presets JSON s'importent et s'exportent dans **Settings → Maintenance → Mod Presets**. Dans l'éditeur de skin, ajouter **RitsuLiveStarCounter** aux composants du HUD puis le placer à l'endroit souhaité ; il affiche la difficulté cumulée pendant la partie. Le saut direct à la fin des pauses reste dans **Settings → Torii → Gameplay**.

## Données et mises à jour

Par défaut les données sont dans `%APPDATA%\Ritsu`. Si un dossier osu!lazer est choisi, Ritsu l'utilise directement : les changements apportés aux maps, scores, skins ou collections peuvent être visibles dans les deux clients. Éviter de faire fonctionner les deux clients simultanément sur les mêmes données. Le choix est également accessible dans **Settings → Torii → Data source**. Le fichier de configuration s'appelle encore `torii.ini`.

Pour mettre à jour cette version, fermer le jeu et extraire le nouveau paquet dans un autre dossier. Les mises à jour automatiques sont désactivées. Aucune association de fichiers du système n'est installée.

## Fonctionnement hors ligne

Connexion, boutique, classements en ligne, chat, téléchargements, présence, télémétrie, envoi de rapports et ouverture de liens externes sont indisponibles. Les intégrations Discord, serveur WebSocket et ancien service TCP local sont désactivés. Le générateur de maps qui installe des outils externes n'est pas proposé ; l'éditeur de maps classique reste disponible.

Les journaux locaux sont accessibles depuis **Settings → General → Open Torii folder**, puis `logs`. Ne pas utiliser les anciens outils Modosu de pare-feu ou d'installation de DLL avec ce client.
