# Roadmap Ritsu

## 0 — socle Torii hors ligne

Livrable courant : Windows x64 portable, instance propre, liaison facultative au dossier osu!lazer ou stockage local, imports de fichiers, jeu et éditeurs locaux, aucun service connecté. La liaison utilise directement les données partagées ; la détection et l'import osu!stable restent locaux. Les noms et l'apparence Torii sont conservés. Torii reste l'amont principal ; toute mise à jour doit préciser les trois révisions de dépendances et repasser les contrôles.

## 1 — intégrer Modosu

Intégrer les mods, profils, presets et statistiques du projet actuel dans le client. Les profils Ritsu partent de zéro : les anciens scores Modosu ne sont ni repris ni migrés. Garder les PP locaux identifiés avec la version du calculateur et les mods utilisés. Conserver les crédits Modosu et MOsu. Les imports et exports de collections et les suggestions de maps sont hors du périmètre de cette étape.

Utiliser une seule liste de mods dans le ruleset existant et conserver le stockage partagé des scores et replays. Le comportement dans lazer et Torii sera vérifié par l'utilisateur après intégration ; il n'est pas encore validé pour les nouveaux mods. Prévoir plusieurs profils locaux avec estimation de PP configurable et identifiant interne stable. Les profils locaux doivent pouvoir cohabiter à terme avec les comptes et classements d'un serveur privé.

Conserver séparément une copie de l'intégration locale Modosu → tosu dans `references/modosu-tosu/`, sans l'intégrer au client à ce stade. Prévoir ultérieurement sa réadaptation complète à Ritsu : identité du client et des mods, calculs de PP et difficultés, sélection, gameplay, résultats, compatibilité des compteurs et vérification des échanges locaux. Définir les scénarios et tester le protocole avant activation.

## 2 — performances mesurées

Établir les temps d'image, latences, mémoire et consommation de référence sur des maps et skins identiques. Examiner les idées d'EMPYREAN sans reprendre ses promesses comme des résultats démontrés. Conserver uniquement les changements mesurés sans régression de gameplay ni de rendu.

## 3 — tournoi

Définir un scénario LAN avant développement : organisateur, préparation des postes, lancement synchronisé, contrôles, reprise après incident et capture des résultats. Étudier inso comme référence. Un tournoi LAN n'implique pas nécessairement de serveur communautaire permanent.

## 4 — éditeur visuel complet

Écrire un cahier des charges distinct avant développement. Ambition : composer menus, sélection, résultats et gameplay avec composants, calques, animations et réactions aux événements. Définir les limites, budgets de rendu, formats, compatibilité et scénarios de création. Les scripts et les changements de mécaniques propres aux maps ne sont pas implicites dans cette phase.

## 5 — identité Ritsu

Décider ce qui restera visuellement Torii et ce qui deviendra Ritsu. Avant tout renommage interne, cartographier les noms persistés dans les skins, profils, configurations et replays ; prévoir leur lecture et conversion. Les notices et crédits restent conservés.

## 6 — services facultatifs

Étudier indépendamment mises à jour, partage de créations, OAuth osu! et serveur communautaire. Étudier également une recherche de maps en ligne sans connexion du joueur, et décider séparément si leur téléchargement sera proposé. Comparer l'API officielle et ses exigences OAuth avec les miroirs publics, leurs fonctionnalités et leur disponibilité avant toute intégration. OAuth peut relier une identité et les données autorisées ; il ne valide pas nos scores pour les classements officiels. Le mode local doit rester utilisable sans ces services.

Un futur hébergement sera dimensionné sur les connexions simultanées, calculs, stockage et trafic observés. Aucun achat ni déploiement n'est inclus dans le jalon courant.
