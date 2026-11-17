<!-- source: 9e216b4e -->
# Déplacer ou copier un bâtiment

Déplacez une maison, avec tout ce qu'elle contient, de quelques mètres ou à l'autre bout du monde, ou placez-en une
copie ailleurs.

**Il vous faut :** un monde ouvert dans l'éditeur, avec le secteur autour du bâtiment ouvert (une sauvegarde d'abord :
voir [Sauvegarder un monde](back-up-a-world.md)).

## Étapes

1. Appuyez sur `E` pour ouvrir [Sélection](../select.md).
2. Double-cliquez sur un mur du bâtiment : toutes les pièces reliées sont sélectionnées, coffres et meubles de ses
   étages compris.
3. Facultatif : pour ajouter des objets isolés à l'intérieur (une table posée à l'écart), `Ctrl` + cliquez dessus.
4. Pour un petit déplacement, faites glisser une pièce sélectionnée, ou faites glisser une flèche pour déplacer le
   long d'un axe ; faites glisser l'anneau orange pour le tourner. Laissez
   **Poser chaque objet sur le sol pendant le déplacement** activé, pour que le bâtiment repose sur le sol là où il
   arrive.
5. Pour un grand déplacement, appuyez sur `Ctrl+X` (ou `Ctrl+C` pour une copie). Le bâtiment est maintenant dans le
   presse-papiers.
6. Ouvrez le secteur au nouvel endroit (depuis la [Carte](../map.md)), ou restez dans celui-ci.
7. Appuyez sur `Ctrl+V` : le bâtiment suit le curseur, en transparence.
8. Tournez-le avec `R` (un quart de tour) ou `,` `.` (de la valeur de **Pivoter de** dans la barre de construction),
   puis cliquez là où il doit se dresser.
9. Appuyez sur `Esc` pour arrêter de coller.
10. Cliquez sur **Enregistrer dans le monde** (ou **Appliquer en direct**).

Les copies et les pièces déplacées gardent tout ce qu'elles contiennent : le contenu des coffres, le texte des
panneaux, qui les a construites. Un bâtiment collé avec `Ctrl+V` emporte le sol qui se trouve dessous.

## Vérifier le résultat

La barre d'état dit combien d'objets ont été déplacés ou collés. Faites le tour du bâtiment sous plusieurs angles :
rien ne doit flotter ni rester en arrière. Après l'enregistrement, ouvrez le monde en jeu et entrez.

## Annuler

- Avant l'enregistrement : `Ctrl+Z` annule le déplacement, la coupe ou le collage (une coupe et son collage sont des
  étapes distinctes).
- Après l'enregistrement : voir [Annuler une étape, retirer une modification ou tout abandonner](undo-changes.md), ou
  [Restaurer un secteur depuis une sauvegarde](restore-from-backup.md).

## Astuces

- Un collage doit tenir dans le secteur ouvert : s'il indique *Non collé*, ouvrez un secteur plus grand autour de
  l'endroit.
- Le presse-papiers reste : vous pouvez coller dans un autre secteur ou un autre monde.
- Pour déplacer d'un nombre exact de mètres, utilisez **POSITION EXACTE** → **Déplacer de** dans le panneau Sélection.
- Un bâtiment qu'on fait glisser s'accroche aux pièces à moins de 0,75 m tant que **Aimantation** est sur **Auto**
  dans [la barre de construction](../build.md#la-barre-de-construction). Choisissez **Libre** pour le laisser exactement là où
  vous le faites glisser.
- Pour un bateau ou un autre véhicule de mod, sélectionnez n'importe quelle partie : tout le groupe se déplace.
- En mode direct, un bâtiment déplacé alors qu'aucun joueur n'est à proximité reste le même objet dans le jeu, donc
  les mods qui le suivent ne le perdent pas : voir
  [Déplacer un objet le garde comme le même objet](../concepts/live-editing.md#déplacer-un-objet-le-garde-comme-le-même-objet).

## Voir aussi

- [Sélection](../select.md) · [Région](../area.md)
- [Aimantation](../concepts/objects.md#aimantation)
- [Coller un plan dans un monde](paste-a-blueprint.md)
