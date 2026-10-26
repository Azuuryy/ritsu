# Ritsu — démarrage rapide

1. Extraire tout le ZIP dans un nouveau dossier.
2. Ouvrir **Launch Ritsu.cmd**. Le programme reste nommé `torii.exe` et affiche l'identité Torii : c'est volontaire pour cette première version.
3. Régler la langue et l'interface dans l'accueil local. Aucun compte n'est nécessaire.
4. Glisser des maps `.osz` ou skins `.osk` dans la fenêtre. L'import est aussi disponible dans **Settings → Maintenance → Import files**.
5. Choisir **Play → Solo**. Les modes intégrés, mods existants et éditeurs restent ceux de Torii.

Les scores et replays des parties normales sont locaux. Le comportement d'autoplay et des replays reste celui de Torii : regarder un replay ne crée pas un nouveau score. Un fichier `.osr` importé nécessite sa map locale correspondante.

Les archives d'origine sont conservées après import.

## Données et mises à jour

Les données sont dans `%APPDATA%\Ritsu`, indépendamment du dossier des exécutables. Le fichier de configuration s'appelle encore `torii.ini`. Les installations existantes ne sont ni liées ni migrées automatiquement.

Pour mettre à jour cette version, fermer le jeu et extraire le nouveau paquet dans un autre dossier. Les mises à jour automatiques sont désactivées. Aucune association de fichiers du système n'est installée.

## Fonctionnement hors ligne

Connexion, boutique, classements en ligne, chat, téléchargements, présence, télémétrie, envoi de rapports et ouverture de liens externes sont indisponibles. Les intégrations Discord, serveur WebSocket et ancien service TCP local sont désactivés. Le générateur de maps qui installe des outils externes n'est pas proposé ; l'éditeur de maps classique reste disponible.

Les journaux locaux sont accessibles depuis **Settings → General → Open Torii folder**, puis `logs`. Ne pas utiliser les anciens outils Modosu de pare-feu ou d'installation de DLL avec ce client.
