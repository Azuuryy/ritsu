# Inventaire préparatoire à l'intégration Modosu

La copie locale se trouve dans `C:\Users\natha\OneDrive\Documents\ChatGPT\Mosu clone\source`. Sa branche `codex/offline-practice` est au commit `ef807e8df043323014a781ca74942c547986f56e`, identique à la branche distante homonyme et descendant de `master` (`5224a4d2dddfecd2a5bcc73e8828e3cb4c829fbe`). Elle contient aussi des modifications non commitées sur l'export de performance vers tosu : elles ne sont pas présentes sur GitHub et ne sont pas incluses dans le premier port.

Sources examinées : [Modosu, Azuuryy](https://github.com/Azuuryy/Modosu) et [MOsu, p-720](https://github.com/p-720/mosu), révision `2df91468c545dbc1dfe01951e6f9f8d8b991f45d`. Modosu et MOsu indiquent une licence GPL-3.0. La licence est conservée dans `client/LICENCE-Modosu.txt` et dans le paquet.

| Fonction dans MOsu | État observé dans Ritsu | Suite |
| --- | --- | --- |
| Spacing Adjust et Fun Spacing | Portés dans la liste commune de mods osu! ; tests ciblés de réapplication et de map vide | Valider visuellement et en partie, ainsi que les scores et replays. |
| Circle Generation et No Sliding | Portés dans la liste commune ; tests ciblés de génération et de conversion d'un slider | Valider en partie et avec les replays. |
| RandomV2 | Porté dans la liste commune, avec panneau de réglages en replay ; test ciblé avec graine fixe | Valider les réglages en direct et les replays en jeu. |
| Miss Challenge | Porté dans le ruleset osu!, limite manuelle ou fondée sur le meilleur score du profil actif | Validation manuelle à réaliser. |
| Alternate, Relax, Autopilot, Single Tap | Les variantes Torii sont conservées selon la décision de l'utilisateur | Aucune adaptation prévue. |
| Métronome | Mod autonome porté avec cinq taux sélectionnables | Validation sonore à réaliser. |
| Saut des pauses | Fonction Torii conservée ; option de saut direct à la fin de la pause | Validation manuelle à réaliser. |
| Profils et PP locaux | Profils et associations de nouveaux scores pour les quatre modes ; estimation de PP locale configurable | Validation manuelle à réaliser ; aucun ancien score n'est associé automatiquement. |
| Presets JSON | Import et export des presets, avec correspondance des anciens noms de ruleset | Validation manuelle à réaliser. |
| Collections et suggestions de maps | Retirées du périmètre par l'utilisateur | Aucun port prévu dans cette étape. |
| Étoiles en direct | Compteur en jeu ajouté à partir de la difficulté cumulée | Validation manuelle à réaliser. |
| Partage de mods par chat | Fonction présente dans MOsu | Reportée : le chat et les services en ligne sont désactivés dans le jalon actuel. |

Dans Modosu, les mods propres sont enregistrés dans `ModosuRuleset.GetModsFor` : Miss Challenge, Circle Generation, RandomV2, Spacing Adjust, No Sliding, Metronome et Fun Spacing Adjust. Les variantes Alternate, Single Tap, Relax et Autopilot ont également leurs propres classes. Le code contient un modèle `LocalProfile`, une migration `LegacyPracticeDataMigration`, des processeurs d'import de collections et de presets, ainsi que des exports JSON dans `exports`. L'export natif vers tosu trouvé dans la copie locale utilise des fichiers JSON temporaires ; une copie de référence est conservée dans `references/modosu-tosu/`, sans intégration à Ritsu.

Prochaine étape : validation manuelle des nouvelles fonctions par l'utilisateur, notamment les scores et replays dans les clients liés. Les anciens scores Modosu ne sont pas repris ni modifiés.

Décision produit : intégrer les mods dans le ruleset existant et garder une liste commune. L'utilisateur testera ultérieurement la lecture des scores et replays dans les autres clients ; leur compatibilité avec les nouveaux mods n'est pas encore vérifiée.
