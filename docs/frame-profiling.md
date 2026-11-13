# Profilage local Ritsu

Sous Windows, ouvrir la palette avec Ctrl+Maj+P et lancer `/profiler start`. Reproduire la séquence à observer. La capture se termine au bout de 30 secondes ou via `/profiler stop` depuis un écran qui accepte la palette. Ouvrir le `report.html` indiqué par la notification. Le fichier `trace.json` de la même capture contient les données brutes, version de schéma 1. Le rapport fonctionne depuis le disque et le client enregistre les résultats sous son stockage privé.

## Comparer le menu des mods ouvert et fermé

Démarrer la capture depuis Song Select. Attendre quelques secondes avec le menu fermé, ouvrir le menu des mods, attendre que l’animation se termine, puis le fermer. Dans le rapport, comparer les plages du thread Draw et du thread Update. Les opérations `Mods.Overlay.Open`, `Mods.Overlay.Update` et `Mods.Overlay.Close` permettent de repérer la transition et le travail propre à l’overlay. Le coût du rendu de ses enfants se lit dans les frames Draw et les compteurs du framework. La pause de Ctrl+F11 fige l’affichage de cet overlay. La trace est créée par les commandes du profileur.

## Lire les mesures

- La durée d’une frame suit l’horloge monotone du processus. Le budget dépend du plafond du thread. Les phases du moniteur couvrent le travail, le scheduler, les attentes et le swap selon le thread. L’attente et le pacing peuvent participer au dépassement d’un budget.
- Les index de frame appartiennent à chaque thread. Les frames update et draw qui partagent un identifiant produit se rapportent à la même publication. Les autres threads et workers se comparent par horodatage.
- Les événements d’opération ont un thread et un intervalle. Leurs durées peuvent se chevaucher. Le temps GC provient du compteur de pauses globales. Les allocations de frame sont managées et propres au thread courant. Le tas managé et le working set sont des échantillons du processus. Les courbes de cache suivent les sites instrumentés et leurs points de mesure.
- Une capture saturée indique les événements perdus. Réduire le scénario ou le plafond de frames pour isoler un pic. L’analyse physique de l’entrée à l’affichage nécessite un protocole et un matériel de mesure distincts.

## Référence reproductible

Exécuter `Verify.ps1 -IncludeGameScenarios -FrameBenchmark` depuis la racine, puis répéter avec `-LinkedLibrary` pour le parcours lié. Le premier scénario produit le replay de fixture. Deux processus frais ouvrent la bibliothèque et rejouent ce même fichier, après un chargement et une recherche Song Select avec caches froids, puis des accès à chaud. Comparer `frame-baseline.json` et `frame-instrumented.json`. La référence collecte les durées minimales de frame dans les deux runs. La version instrumentée ajoute les phases, allocations et événements. La différence de p95 estime la surcharge de cette instrumentation, avec le bruit du système.

Conserver la résolution, le renderer, le plafond de FPS et le mode d’exécution entre les runs. Les rapports relèvent la révision de build, le renderer, le plafond update, le CPU déclaré, les p50/p95/p99 et les dépassements par thread. Répéter la paire avant de retenir une différence. Pour isoler un calcul, réutiliser les benchmarks Release `BenchmarkBeatmapParsing`, `BenchmarkCarouselFilter`, `BenchmarkRealmReads` et `BenchmarkDifficultyCalculation` dans `osu.Game.Benchmarks`.

Pour relier une durée à une pile, collecter séparément une trace EventPipe avec [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace). Pour examiner les objets retenus, faire un autre run avec [dotnet-gcdump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump). L’export intégré ne déclenche pas de collecte du tas.
