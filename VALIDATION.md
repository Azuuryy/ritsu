# Validation du socle hors ligne

Validation effectuée le 26 octobre 2026. Les résultats et captures restent dans `.cache/verification-a8060692388f42d5bfe881ef0798c8ec`. Les rapports distinguent la compilation de référence, les contrôles ciblés, le parcours avec rendu et le redémarrage. L'utilisateur a confirmé le bon fonctionnement du démarrage.

## Vérifications

- Compilation Release de Torii sans modification avant adaptation et lancement dans un hôte de vérification distinct. L'installation Torii déjà ouverte n'a pas été pilotée.
- Contrôles ciblés : effacement du jeton, absence de connexion/authentification, rejet unique des requêtes, absence de transport HTTP créé, absence de connexion temps réel, blocage HTTP synchrone/asynchrone et conservation des identifiants des composants de skins Torii.
- Le détecteur de communications est vérifié dans un processus séparé par une requête témoin vers `127.0.0.1:9`. Cette requête appartient uniquement au contrôle du détecteur, pas au client livré.
- Parcours de jeu : import d'une map synthétique et d'un skin, sélection, partie Solo avec NF sans entrées artificielles, score et replay enregistrés, export/import OSR, lecture du replay, éditeur de maps et éditeur de skins.
- Vérification de la persistance du score et des données du replay dans un second processus. La fermeture de ce second scénario est passée par le parcours normal de sortie avant l'attestation automatisée de lecture ; la lecture après redémarrage reste à confirmer à l'usage.
- Captures du rendu réel inspectées séparément des assertions de fonctionnement.
- Le parcours complet a abouti en 48 secondes avec 11 contrôles réussis. La lecture du replay dans la même session est validée.
- Les adaptations ont été appliquées avec succès à une copie vierge du commit Torii épinglé ; aucun ancien workflow de publication n'y reste actif.

## Portée du contrôle réseau

`NetworkAudit` relève les événements .NET de début de requête HTTP, de résolution DNS et de connexion/acceptation de socket. Les rapports incluent l'identifiant du processus et le nombre d'événements observés. Ce contrôle n'est pas un relevé de tous les paquets du système ni une preuve générale sur tout code natif tiers.

Le parcours complet a relevé zéro événement réseau .NET. `tools/Measure-ClientNetwork.ps1` a également observé le processus et ses enfants : zéro socket TCP/UDP constaté (25 observations dans le premier parcours contrôlé, puis 5 lors du redémarrage ; seul enfant observé : `conhost.exe`). Les observations Windows peuvent manquer une connexion très brève entre deux relevés.

Le client bloque les requêtes du framework avant transport, n'instancie pas de fournisseur API authentifié et désactive les intégrations externes connues. Le contrôle ne dépend d'aucune règle de pare-feu. Le fonctionnement sans réseau concerne le client exécuté ; la compilation et la récupération des dépendances utilisent Internet.

`Verify.ps1` compile et lance les contrôles sans ouvrir le jeu. Les scénarios graphiques sont disponibles explicitement avec `-IncludeGameScenarios`. Aucun nouveau lancement graphique n'est nécessaire pour compiler ou produire le ZIP.

## Limites connues

- L'identité Torii reste visible et les exécutables conservent leurs noms.
- Les nouveaux outils Modosu, le tournoi et le nouvel éditeur visuel ne sont pas inclus.
- La partie automatisée valide la chaîne locale avec NF et des misses ; elle ne constitue pas un benchmark de latence ou un test d'habileté.
- La base épinglée émet des avertissements de compilation et de dépendances hérités. Ce jalon ne les transforme pas en certification générale de sécurité.
- Le pilotage interactif a été interrompu par Échap après le lancement de référence. Les vérifications suivantes utilisent des scénarios automatisés et des captures du moteur, sans contrôle des fenêtres.
