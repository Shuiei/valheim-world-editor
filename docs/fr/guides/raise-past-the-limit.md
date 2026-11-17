<!-- source: c7695b6c -->
# Élever ou creuser au-delà de la limite de 8 m du jeu

Élevez une falaise ou une flèche, ou creusez un canyon, bien au-delà des 8 m que permet le jeu, d'une façon que le jeu
de chaque joueur charge sans aucun mod.

**Il vous faut :** un monde ouvert en 3D ([une copie ou une sauvegarde de celui-ci](back-up-a-world.md)), avec le jeu
fermé, ou le [mode direct](edit-your-game-live.md).

1. Sauvegardez le monde. Enregistrer ce genre de sol efface l'Historique du secteur.
2. Dans la barre d'outils, cliquez sur **Élever** (`1`) ou **Abaisser** (`2`).
3. Sur la fiche **Masque** à côté des réglages du pinceau, cochez **Sans limite : au-delà des ±8 m du jeu**. La barre
   d'état indique que les outils de sol dépassent maintenant les ±8 m du jeu.
4. Réglez **Taille** et **Force**, puis maintenez le bouton gauche sur le sol et déplacez-vous. Le sol ne s'arrête plus
   à 8 m et ne devient plus rouge.
5. Facultatif : façonnez le résultat avec **Lisser** (`4`) ou **Renaturer** (`0`), qui continuent d'agir au-delà de la
   limite tant que Sans limite est activé.
6. Regardez la forme sous plusieurs angles. `Ctrl+Z` annule un coup de pinceau ; **Restaurer** (`5`) en annule une
   partie.
7. Cliquez sur **Enregistrer dans le monde** (ou **Appliquer en direct**). Le message d'enregistrement indique combien
   de disques de sol ont été créés et combien d'arbres et de rochers ont été retirés, par exemple « Sol Sans limite :
   312 disque(s) de sol, 40 arbre(s)/rocher(s) retiré(s) ».

Pour toute une montagne en un clic, utilisez plutôt [Relief](../landform.md#montagne) avec son type **Montagne** : il
dépasse toujours la limite.

## Vérifier le résultat

- Passez le curseur sur le nouveau sol : la hauteur « d'origine » de la barre d'état est maintenant celle du nouveau
  sol.
- Ouvrez le monde en jeu et allez-y. La forme apparaît à mesure que vous approchez, comme le sol aplani d'un village.

## Annuler

- **Avant l'enregistrement :** `Ctrl+Z`, le pinceau **Restaurer**, ou **Abandonner** pour tout ce qui n'est pas
  enregistré.
- **Après l'enregistrement :** l'Historique repart du monde enregistré. Le pinceau **Restaurer** ramène toujours
  l'endroit au sol généré par le monde (tout comme le **Restaurer** de l'outil Tracé et le **Revenir au sol généré**
  de Région) : enregistrez à nouveau, et les disques de sol dont il n'a plus besoin disparaissent. Ou remettez
  l'endroit comme avant avec [Restaurer un secteur depuis une sauvegarde](restore-from-backup.md).

## Voir aussi

- [Sans limite](../concepts/ground-limits.md#sans-limite) : comment le sol est enregistré au-delà de 8 m
- [Sculpter et peindre](../terrain.md#sans-limite)
- [Relief](../landform.md)
