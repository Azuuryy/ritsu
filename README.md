# Ritsu — Torii offline foundation

Premier jalon Windows x64 : un client Torii autonome pour le jeu local. Les noms et l'apparence Torii sont volontairement conservés ; le renommage est reporté.

## Jouer

Sous Windows x64, lance `Pipeline.ps1` pour compiler le client et créer l'archive `Ritsu-<version>-win-x64.zip` dans `artifacts/`, puis extrais-la et ouvre **Launch Ritsu.cmd**. La version courante est indiquée dans [`VERSION`](VERSION) et son historique dans [`CHANGELOG.md`](CHANGELOG.md). Choisis le dossier osu!lazer existant pour partager ses données ou conserve le stockage local par défaut dans `%APPDATA%\Ritsu`. Aucun compte, service ou accès réseau n'est nécessaire pour jouer. Ne place pas le paquet dans l'installation Torii ou lazer existante.

Importer les fichiers `.osz`, `.osk` et `.osr` par glisser-déposer ou via **Settings → Maintenance → Import files**. Les maps doivent être présentes localement pour lire leurs replays. Le dossier osu!lazer peut être détecté ou choisi manuellement et utilisé directement, sans copie. Aucun téléchargement de maps n'est proposé.

Voir [GUIDE.md](GUIDE.md), [ROADMAP.md](ROADMAP.md) et [VALIDATION.md](VALIDATION.md).

## Compiler

Prérequis : Windows x64, Git et SDK .NET 10 (validation réalisée avec 10.0.103, runtime livré 10.0.3). Le téléchargement des sources et des dépendances nécessite Internet ; le client produit fonctionne hors ligne.

Pipeline complète, sans ouverture du jeu :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Pipeline.ps1
```

Pour exécuter séparément la préparation et la compilation :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Bootstrap.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Les révisions sont fixées dans `dependencies.json`. Le dépôt contient déjà la source Torii épinglée et ses adaptations hors ligne. `Bootstrap.ps1` la vérifie et restaure le framework et les ressources nécessaires depuis leurs dépôts épinglés. Il refuse une révision différente et n'écrase pas les modifications locales. `Build.ps1` produit une distribution autonome avec les bibliothèques natives et un SHA-256. Les caches et journaux restent dans `.cache/`.

Les contrôles développeur peuvent aussi être exécutés seuls :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify.ps1
```

Cette commande compile et exécute les contrôles hors ligne sans ouvrir le jeu. Les scénarios graphiques sont facultatifs et se lancent uniquement avec `-IncludeGameScenarios` ; ils nécessitent Python 3, ouvrent une fenêtre et utilisent un profil de test séparé. Le jeu n'est jamais lancé par `Build.ps1` ou `Pipeline.ps1`.

La source cliente conserve l'historique Torii, avec les modifications de ce jalon sur `codex/ritsu-offline`. Les anciens workflows sont conservés avec l'extension `.disabled` ; ils ne s'exécutent pas sur GitHub.

## Origine et crédits

- **Torii**, par ShikkesoraSIM et ses contributeurs : base de ce client, de son flow et de ses interfaces. [Dépôt](https://github.com/ShikkesoraSIM/torii-osu).
- **osu! / osu!framework**, par ppy et leurs contributeurs : moteur, gameplay et outils d'origine.
- **Modosu**, le projet maintenu par l'utilisateur : source des fonctions d'entraînement. [Dépôt](https://github.com/Azuuryy/Modosu).
- **MOsu**, par p-720 et ses contributeurs : origine du travail Modosu. [Dépôt](https://github.com/p-720/mosu).

Les contributions de ces projets ne sont pas réattribuées à Ritsu. Les notices du client, du framework et des ressources sont conservées. Les mods métronome, Spacing Adjust, Fun Spacing, Circle Generation, No Sliding, RandomV2 et Miss Challenge sont adaptés de Modosu/MOsu ; leur licence GPL-3.0 est conservée dans `client/LICENCE-Modosu.txt` et dans le paquet. Les nouvelles fonctions attendent leur validation en jeu.
