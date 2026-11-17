<!-- source: 43410d17 -->
# Raccourcis

Chaque touche et action de souris de l'éditeur, groupées selon ce que vous faites. Le bouton **?** de la barre
du haut (ou `F3`) affiche les principales pendant que vous travaillez.

Les touches ne fonctionnent que dans l'éditeur 3D : pas pendant que vous tapez dans une case, ni sur la page d'accueil
ou la carte du monde (sauf `F1`, qui fonctionne partout). Sur
Mac, appuyez sur `Cmd` partout où cette page indique `Ctrl` (`Cmd+Z` annule, `Cmd`+clic ajoute à une sélection) et sur
`Option` pour `Alt`.

## Caméra

| Touche ou souris | Action |
|---|---|
| Clic droit glissé | Tourner la vue. En Marche et en Vol : regarder autour de soi. |
| Molette | Zoomer, de 3 m à 3 km du point que vous regardez. |
| Clic milieu glissé | Faire glisser la vue. |
| `Space` + clic gauche glissé, `Shift` + clic droit glissé | Faire glisser la vue, dans n'importe quel outil (pratique sans bouton du milieu). |
| Clic gauche glissé (outil **Vue**) | Faire glisser la vue. |
| `W` `A` `S` `D` ou les flèches | Se déplacer. Plus le zoom est éloigné, plus c'est rapide. Pas tant que `Ctrl` ou `Alt` est maintenu. |
| `F` | Marcher à hauteur des yeux, puis voler, puis revenir à la vue habituelle, dans tous les outils. |
| `Shift` (Marche) | Courir : 12 m/s au lieu de 6. |
| `Space` / `C` (Vol) | Monter / descendre, à travers le sol aussi (dans les tunnels et les caves d'un mod). `Shift` : 45 m/s au lieu de 15. |

En marchant ou en volant, la molette ne zoome pas et les glissés ne font pas glisser la vue. Voir
[Marche et Vol](../editor-basics.md#marche-et-vol).

## Panneaux et enregistrement

| Touche | Action |
|---|---|
| `V` | Ouvrir ou fermer le panneau **Vue**. |
| `L` | Ouvrir ou fermer l'**Historique**. |
| `?` (`Shift+/`) ou `F3` | Ouvrir ou fermer la fiche d'aide **Commandes**. |
| `F1` | La fenêtre d'aide, à la page de l'outil utilisé (voir [La fenêtre d'aide](#la-fenêtre-daide)). Le petit **?** en haut de chaque panneau ouvre la page de ce panneau. |
| `Ctrl+S` | **Enregistrer dans le monde** (un monde enregistré), **Appliquer en direct** (en direct), **Enregistrer le plan** (l'Atelier). |

## Choisir un outil

| Touche | Outil |
|---|---|
| `1` `2` `3` `4` `5` | Élever, Abaisser, Aplanir, Lisser, Restaurer |
| `0` | Renaturer |
| `O` (la lettre) | Éroder |
| `6` `7` `8` `9` | Le pinceau **Peinture** avec Terre, Cultiver, Pavé, Effacer la peinture |
| `B` | Région |
| `P` | Tracé |
| `T` | Placement (**Bâtir** dans l'Atelier) |
| `U` | Bâtir |
| `G` | Relief |
| `E` | Sélection |
| `M` | Mesure |
| `H` ou `Esc` | Vue (`Esc` seulement quand il n'a rien d'autre à annuler, voir [Comment fonctionne Esc](#comment-fonctionne-esc)) |

Les chiffres du pavé numérique fonctionnent aussi. **Donjon**, **Script** et **Plans** n'ont pas de
touche : cliquez dessus dans la barre d'outils. Dans l'Atelier, seuls `U` ou `T`, `E` et `H` ou `Esc` choisissent un
outil.

## Annuler, copier et coller

| Touche | Action |
|---|---|
| `Ctrl+Z` | Annuler la dernière modification. |
| `Ctrl+Y` ou `Ctrl+Shift+Z` | La rétablir. |
| `Ctrl+C` | Copier le cadre ou le polygone de Région, ou les objets sélectionnés (outils Région et Sélection). |
| `Ctrl+X` | Couper : la même copie, puis les objets quittent le monde en une étape (le sol reste). |
| `Ctrl+V` | Commencer à coller le presse-papiers, depuis n'importe quel outil. |
| `[` / `]` | Taille du pinceau 1 m plus petite / plus grande (de 1 à 30 m). |

## Sélectionner des objets

Ces raccourcis fonctionnent dans l'outil **Sélection** (`E`). Un clic dans l'outil **Vue** choisit les objets de la
même façon.

| Touche ou souris | Action |
|---|---|
| Clic | Sélectionner cet objet seul. Recliquez au même endroit : l'objet suivant, derrière lui. |
| `Ctrl`+clic | Ajouter l'objet, ou le retirer s'il était sélectionné. |
| `Shift`+clic | Retirer l'objet de la sélection. |
| `Ctrl+Shift`+clic | Ajouter chaque objet affiché d'une rangée, du dernier cliqué jusqu'à celui-ci. |
| Double-clic | Une pièce de construction : tout le bâtiment. Une partie d'un navire ou d'un autre groupe lié : tout le groupe. Tout autre objet (un coffre, un panneau, un arbre) : cet objet, avec son Inspecteur ouvert. Dans l'outil Vue, un double-clic sur un objet passe à Sélection. |
| Glisser sur un sol vide | Sélectionner ce qui est dans le rectangle (ou dans le contour tracé au sol, si **Un glissé trace** est réglé ainsi). `Ctrl` : ajouter à la sélection. `Shift` : retirer. |
| `Alt` + glisser | Pareil, en commençant aussi sur des objets (les objets ne sont pas touchés). |
| `Ctrl+A` / `Alt+A` | Sélectionner chaque objet affiché / vider la sélection. |
| `Ctrl+I` | Inverser la sélection. |
| `Ctrl+H` / `Alt+H` | Masquer les objets sélectionnés dans la vue / les réafficher. |
| `Alt+Z` | Activer ou désactiver **Sélectionner à travers** : un rectangle prend aussi ce qui est caché derrière d'autres objets. |
| `Shift+F` ou `.` du pavé numérique | Cadrer la sélection. |

## Par outil

### Pinceaux de sculpture et de peinture

| Touche ou souris | Action |
|---|---|
| Clic gauche glissé | Passer le pinceau sur le sol. |
| `Alt`+clic | Prendre la hauteur du sol à cet endroit comme hauteur d'Aplanir, et passer à **Aplanir**. |
| `Alt+Shift`+clic | Régler la plage de hauteur du [Masque](../masks.md) de 2 m au-dessous à 2 m au-dessus de ce point (avec l'outil Tracé aussi). |

### Région

| Touche ou souris | Action |
|---|---|
| Glisser (Cadre) | Tracer le cadre. |
| Clic (Polygone) | Ajouter un point. Double-clic : fermer le polygone. |
| `Enter` | Fermer le polygone en cours de tracé ; une fois la sélection faite, effectuer l'action choisie. |
| `Enter`, puis `Del` | Avec **Objets** choisi : sélectionner les objets à l'intérieur, puis les supprimer dans l'outil Sélection. |
| `Backspace` | Retirer le dernier point du polygone. |
| `Esc` | Effacer la sélection (le cadre ou le polygone). |

### Tracé

| Touche ou souris | Action |
|---|---|
| Clic | Ajouter un point. |
| Glisser la ligne | Ajouter un point à cet endroit et le déplacer. |
| Glisser un point | Le déplacer. |
| `Ctrl`+clic sur un point | Le retirer. |
| `Alt`+clic | Prendre la hauteur du sol à cet endroit comme hauteur du tracé (Rampe : son début). |
| `Ctrl+Alt`+clic | Rampe : prendre la hauteur comme sa fin. |
| `Alt+Shift`+clic | Régler la plage de hauteur du [Masque](../masks.md), comme avec un pinceau. |
| `Enter` | Appliquer le tracé. |
| `Backspace` | Retirer le dernier point. |
| `Esc` | Effacer la ligne. |

### Placement

| Touche ou souris | Action |
|---|---|
| Clic gauche glissé (Pinceau) | Placer des objets sous le pinceau. `Shift` + glisser : les retirer. |
| Clic (Ligne, Polygone) | Ajouter un point. Glisser le contour : ajouter un point à cet endroit. Glisser un point : le déplacer. `Ctrl`+clic sur un point : le retirer. |
| Double-clic sur le dernier point | Placer le long de la forme. |
| Glisser (Grille, formes en ligne) | Tracer le rectangle de la grille, ou la forme d'une extrémité à l'autre. |
| `Alt`+clic | Prendre la hauteur à cet endroit comme **Élévation**. |
| `Enter` | Placer le long de la ligne, du cercle ou du rectangle, ou dans la grille ou le polygone. |
| `Backspace` | Retirer le dernier point de la ligne ou du polygone. |
| `Esc` | Effacer la forme en cours de tracé, ou annuler **Prendre dans le monde**. |
| `N` | Une nouvelle disposition aléatoire. |
| `R` | Tourner l'aperçu, une grille tracée ou un polygone d'un quart de tour. |
| `,` / `.` ou `Alt` + molette | Tourner l'aperçu, une grille tracée ou un polygone de 15° (`Shift`, plus fin : 1°). |
| `PgUp` / `PgDn` ou `Ctrl` + molette | Monter ou descendre ce que vous placez de 0,5 m (`Shift`, plus fin : 0,1 m). |
| Clic sur un objet (**Prendre dans le monde**) | Prendre son type. `Shift`+clic l'ajoute aux types choisis. |

### Bâtir, Coller et déplacer une sélection : les pas de la barre de construction

[Bâtir](../build.md), Coller et Sélection tournent et soulèvent avec les mêmes touches, selon les pas réglés sur
[la barre de construction](../build.md#la-barre-de-construction) (**Pivoter de**, 22.5° au départ ; **Rehausse**, 0.5 m au
départ). `Shift` rend toujours le pas plus fin.

| Touche ou souris | Action |
|---|---|
| `,` / `.` ou `Alt` + molette | Tourner la pièce, le collage ou la sélection du pas **Pivoter de** (`Shift` : 1°). |
| `PgUp` / `PgDn` ou `Ctrl` + molette | Les monter ou descendre du pas **Rehausse** (`Shift` : un cinquième). |

### Bâtir (mondes, Atelier et construction dans un donjon)

| Touche ou souris | Action |
|---|---|
| Clic | Poser la pièce (**Pose** : **Une pièce**), ou ajouter un point (**Ligne**). |
| Glisser | Une rangée de la pièce (**Une pièce**) ; le cercle, le rectangle ou la grille (**Pose**). |
| Maintenir `Alt` | Placer la pièce au quart de mètre, sans aimantation. |
| `R` | Tourner la pièce d'un quart de tour. |
| `Q` / `E` | Avec **Aimantation** sur **Manuel** : le point d'aimantation précédent / suivant de la pièce. |
| `Shift` + clic droit | Retirer la pièce sous le pointeur. |
| Clic milieu | Bâtir avec le type de la pièce sous le pointeur. |
| `Enter` | Poser la ligne, le cercle, le rectangle ou la grille. |
| `Backspace` | Retirer le dernier point de la ligne. |
| `Esc` | Effacer la ligne, la figure ou la grille en cours de tracé. |

### Coller

| Touche ou souris | Action |
|---|---|
| Clic | Poser le collage. Recliquez pour plus de copies. |
| `R` | Tourner d'un quart de tour. |
| `X` | Mettre en miroir. |
| `Esc` | Arrêter le collage, retour à l'outil Région. |

### Déplacer la sélection (Sélection)

| Touche ou souris | Action |
|---|---|
| Glisser un objet sélectionné | Déplacer la sélection. |
| Glisser une flèche ou l'anneau | Déplacer le long de cet axe, ou tourner. |
| `R` | Tourner la sélection d'un quart de tour. |
| `End` | La poser sur le sol. |
| `Del` | Supprimer les objets sélectionnés. |
| `I` | Ouvrir l'Inspecteur de l'objet sélectionné (un seul objet sélectionné). |
| `Esc` | Annuler un déplacement en cours, ou un rectangle ou un contour en cours de tracé. |

### Autres outils

| Outil | Touche ou souris | Action |
|---|---|---|
| Relief | Clic | Poser le relief à cet endroit : la forme de la formule, la montagne ou l'image. |
| Mesure | Clic sur deux points | Mesurer entre eux. `Esc` efface la mesure. |
| Donjon | `R` | Tourner la salle à ajouter : elle se raccorde par son ouverture suivante. |
| | `Del` | Supprimer la salle sélectionnée. |
| Script | `Ctrl+Enter` | Exécuter le script (dans la zone de code). |
| Inspecteur | `Esc` | Le fermer. |
| L'outil d'un mod | Ses propres touches | Un outil sous **MODS** dans la barre d'outils prend en priorité les touches qu'il utilise ; les autres fonctionnent comme d'habitude. |

## Comment fonctionne Esc

`Esc` fait la première de ces actions qui s'applique, puis s'arrête :

1. Annule le choix d'un type avec **Prendre dans le monde**.
2. Arrête le collage (retour à l'outil Région).
3. Ferme l'Inspecteur.
4. Annule ce que fait l'outil : un déplacement dans Sélection, un rectangle ou un contour en cours de tracé, une forme
   de Placement ou une ligne posée avec Bâtir, un cadre ou un polygone de Région, les points du Tracé, une mesure.
5. Passe à l'outil Vue.

## La carte du monde et un nouveau monde

| Souris | Action |
|---|---|
| Glisser | Faire glisser la carte. |
| Molette | Zoomer vers la souris. |
| Clic | Choisir un endroit (la zone pour **Modifier en 3D**). |
| Double-clic (la carte d'un nouveau monde) | Afficher tout le monde. |
| `Enter` dans une zone de recherche | Chercher. |

## La fenêtre d'aide

| Touche | Action |
|---|---|
| `F1` | Ouvrir l'aide sur ce que vous utilisez : l'outil, l'Inspecteur, la carte, la page d'accueil… (n'importe quelle page). |
| `Ctrl+F` | Chercher dans l'aide. |
| `Alt+Left` / `Alt+Right` | Précédent / suivant. |
| `Esc` | Fermer une image agrandie, sinon vider la recherche. |
| `Enter` dans la zone de recherche | Ouvrir la première page trouvée. |

## Voir aussi

- [La fenêtre de l'éditeur](../editor-basics.md) : la barre du haut, la barre d'outils, la caméra et les panneaux.
- [Sélection](../select.md) : sélectionner et déplacer des objets.
- [Placement](../place.md), [Bâtir](../build.md), [Tracé](../path.md), [Région, copier et coller](../area.md) : ce
  que font les touches de chaque outil.
- [Glossaire](glossary.md) : les mots qu'utilise l'éditeur.
