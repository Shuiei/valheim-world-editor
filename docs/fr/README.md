<!-- source: d6008f75 -->
# Aide de Valheim World Editor

Valheim World Editor ouvre un monde Valheim, enregistré ou en cours de partie, dans une fenêtre 3D. Façonnez le sol,
construisez, placez et déplacez n'importe quoi, puis enregistrez le résultat ou regardez-le changer en jeu pendant que
vous travaillez.

**Nouveau ici ?** [Installer l'éditeur](start/install.md), puis suivez [votre première modification](start/first-edit.md) :
dix minutes sur une copie d'un monde, rien à perdre. Vous cherchez quelque chose ? Cherchez dans l'aide (`Ctrl+F` dans la
fenêtre d'aide), ou partez d'une tâche ci-dessous. Dans l'éditeur, `F1` ou le petit **?** en haut d'un panneau ouvre
l'aide sur cet outil.

> [!IMPORTANT]
> Gardez une copie d'un monde avant de le modifier : voir [Sauvegarder un monde avant de le modifier](guides/back-up-a-world.md).
> L'éditeur vérifie que chaque enregistrement se relit correctement avant de lâcher l'ancien, mais il ne garde aucune
> copie de son côté.

## Premiers pas

- [Installer l'éditeur](start/install.md) : téléchargement, premier lancement sous Windows, Linux ou macOS, et le plugin
  pour la modification en direct.
- [Votre première modification](start/first-edit.md) : ouvrir une copie d'un monde, élever une colline, planter des
  arbres, enregistrer, et voir le résultat en jeu.
- [Votre première construction](start/first-build.md) : construire une cabane dans l'Atelier, l'enregistrer comme plan et
  la coller dans un monde.

## Modifier des mondes

- [Sauvegarder un monde avant de le modifier](guides/back-up-a-world.md)
- [Ouvrir un monde qui n'est pas dans la liste](guides/open-a-world.md)
- [Modifier le monde dans lequel vous jouez](guides/edit-your-game-live.md)
- [Modifier un serveur dédié pendant que d'autres jouent](guides/edit-a-server-live.md)
- [Modifier un serveur loué ou hébergé par un panneau](guides/edit-a-rented-server.md)
- [Annuler une étape, retirer une modification ou tout abandonner](guides/undo-changes.md)
- [Restaurer un secteur depuis une sauvegarde](guides/restore-from-backup.md)
- [Générer à nouveau des zones](guides/reset-zones.md)
- [Trouver un objet, un objet dans un coffre ou un panneau](guides/find-things.md)
- [Modifier un monde créé avec Expand World, Riverheim ou Tunnelheim](guides/use-mods-worlds.md)

## Façonner le terrain

- [Élever ou creuser au-delà de la limite de 8 m du jeu](guides/raise-past-the-limit.md)
- [Faire une route ou une rampe qui monte une colline](guides/make-a-road.md)
- [Creuser une rivière ou un étang](guides/make-a-river.md)
- [Dégager arbres et rochers, aplanir un terrain à bâtir](guides/clear-an-area.md)

## Construire et remplir

- [Planter une forêt, un champ ou une haie](guides/plant-a-forest.md)
- [Déplacer ou copier un bâtiment](guides/move-a-building.md)
- [Changer le contenu d'un coffre ou le texte d'un panneau](guides/fill-a-chest.md)
- [Coller un plan dans un monde](guides/paste-a-blueprint.md)
- [Générer un donjon et le placer dans le monde](guides/make-a-dungeon.md)
- [Trouver une graine avec ce que vous voulez près du départ](guides/find-a-seed.md)
- [Laisser Claude façonner ou construire](guides/connect-claude.md)

## Outils

- [La fenêtre de l'éditeur](editor-basics.md) : la page d'accueil, la barre du haut, la barre d'outils, la caméra, le
  secteur, l'enregistrement, l'Historique, le panneau Vue et la Coupe.
- [La carte du monde](map.md) : trouver un endroit, l'ouvrir en 3D, chercher dans tout le monde.
- [Sculpter et peindre](terrain.md) : les pinceaux de sol, les tampons et Sans limite.
- [Relief](landform.md) : tout un relief en un clic, à partir de votre propre formule (collines, cratères, plateaux,
  douves), du générateur de montagnes (pics, crêtes et volcans au-delà de la limite du jeu) ou d'une image.
- [Tracé](path.md) : routes, rampes et rivières le long d'une ligne.
- [Masque](masks.md) : limiter n'importe quel outil à un biome, une hauteur, une pente ou une peinture ; sa fiche porte
  aussi l'interrupteur Sans limite.
- [Mesure et superpositions](measure.md) : distances, pentes, couleurs de pente et courbes de niveau.
- [Région, copier et coller](area.md) : aplanir ou peindre un cadre ou un polygone, choisir ses objets, le copier et le
  coller, le faire repousser ou le restaurer depuis une sauvegarde.
- [Placement](place.md) : arbres, rochers, cultures et points d'apparition.
- [Bâtir](build.md) : les pièces que construisent les joueurs, une à une ou le long de lignes, avec la barre de
  construction.
- [Sélection](select.md) : choisir, déplacer, tourner, copier, remplacer et supprimer des objets, et regarder à
  l'intérieur.
- [L'Atelier](workshop.md) : construire sur une parcelle vierge et garder le résultat comme plan.
- [Donjons](dungeons.md) : générer un donjon, ou modifier une à une les salles du jeu.
- [Un nouveau monde](new-world.md) : voir le monde d'une graine avant qu'il existe, et le créer.
- [Mode direct](live-mode.md) : Appliquer en direct, Auto, Recharger et les joueurs, pendant que le jeu tourne.
- [Script](scripting.md) : façonner, peindre et remplir un secteur avec des scripts C#.
- [Claude](claude.md) : ce que Claude peut faire dans l'éditeur, et ses limites.

## Comment ça marche

- [Secteurs et zones](concepts/areas-and-zones.md) : pourquoi l'éditeur ouvre le monde un bloc à la fois.
- [Enregistrement et historique](concepts/saving-and-history.md) : modifications en attente, enregistrement,
  application en direct, annulation et sauvegardes.
- [Limites du sol](concepts/ground-limits.md) : la limite de 8 m du jeu et comment l'éditeur la dépasse.
- [Modification en direct](concepts/live-editing.md) : comment l'éditeur communique avec un jeu en cours.
- [Objets](concepts/objects.md) : ce qu'est un objet, l'aimantation, la vérification du soutien, les règles de
  croissance, les points d'apparition.
- [Les donjons dans le jeu](concepts/dungeons.md) : comment le jeu conserve les donjons, et comment le générateur en
  construit un.
- [Sécurité](concepts/safety.md) : ce que l'éditeur écrit, et pourquoi rien ne quitte votre ordinateur.

## Aide

- [Dépannage](help/troubleshooting.md) : que faire quand quelque chose ne fonctionne pas.
- [Questions](help/faq.md) : des réponses courtes aux questions les plus fréquentes.
- [Raccourcis](help/shortcuts.md) : chaque touche et action de la souris.
- [Glossaire](help/glossary.md) : les mots qu'utilise l'éditeur.

## Pour les traducteurs et les auteurs de mods

- [Traduire](translating.md) : les catalogues de langues, comment en créer un, et comment traduire cette documentation.
- [Pour les auteurs de mods](modding.md) : ce que l'éditeur apprend des mods, `worldeditor.json`, les extensions et les
  modules complémentaires.
