<!-- source: 4a8a11bc -->
# Sauvegarder un monde avant de le modifier

Faites une copie du dossier d'un monde à laquelle vous pourrez toujours revenir, quoi qu'il arrive ensuite au monde.

**Il vous faut :** le nom du monde, et Valheim fermé (ou le serveur qui fait tourner le monde arrêté).

1. Fermez Valheim, ou arrêtez le serveur. Un jeu en cours continue d'écrire le monde : une copie faite pendant qu'il
   tourne risque d'être à moitié ancienne, à moitié nouvelle.
2. Trouvez le dossier du monde : sur la page d'accueil de l'éditeur, cliquez sur **Un monde enregistré** et survolez
   la carte du monde. L'info-bulle se termine par le dossier, par exemple
   `~/.config/unity3d/IronGate/Valheim/worlds_local/Midgard`.
3. Ouvrez le dossier qui le contient (`worlds_local`) dans votre gestionnaire de fichiers. Les emplacements habituels
   sont :
   - Windows : `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local`
   - Linux : `~/.config/unity3d/IronGate/Valheim/worlds_local` (Valheim via Proton :
     `<Steam library>/steamapps/compatdata/892970/pfx/drive_c/users/steamuser/AppData/LocalLow/IronGate/Valheim/worlds_local`)
   - macOS : `~/Library/Application Support/IronGate/Valheim/worlds_local`
   - Un serveur dédié : `<savedir>/worlds_local` sur le serveur.
4. Copiez le dossier du monde (`Midgard`) et collez la copie dans le même dossier `worlds_local`.
5. Renommez la copie `<World>_backup_<date>`, par exemple `Midgard_backup_2026-10-22`. Gardez le nom du monde
   exactement tel quel, suivi de `_backup_`.

> [!TIP]
> Nommée ainsi, la copie se trouve à côté du monde, et l'éditeur la liste comme sauvegarde dans **Revenir à une
> sauvegarde** de l'outil Région : vous pourrez donc plus tard en ramener un secteur. Voir
> [Restaurer un secteur depuis une sauvegarde](restore-from-backup.md).

## Vérifier le résultat

- La copie contient les mêmes fichiers que le monde : `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` et les fichiers
  `.chunk`.
- Sur la page d'accueil, **Un monde enregistré** liste aussi la copie, sous son propre nom. Ouvrez le monde lui-même
  pour le modifier, pas la copie.
- Dans la vue 3D d'un monde, **Revenir à une sauvegarde** de l'outil [Région](../area.md) liste la copie sous la forme
  **Éditeur · \<date\>**.

## Annuler

La copie ne change rien au monde : supprimez le dossier de la copie quand vous n'en avez plus besoin.

Pour remettre tout le monde tel qu'il était dans la copie :

1. Fermez Valheim, ou arrêtez le serveur.
2. Déplacez le dossier du monde (`Midgard`) hors de `worlds_local`, sur votre Bureau par exemple (supprimez-le plus
   tard quand vous en êtes sûr).
3. Renommez la copie (`Midgard_backup_2026-10-22`) avec le nom du monde (`Midgard`).
4. Lancez le jeu : il charge le monde tel qu'il était quand vous l'avez copié.

Le jeu garde aussi ses propres sauvegardes à côté du monde (`<World>_backup_auto-<date>`) ; l'éditeur les liste sous
la forme **Jeu · \<date\>**.

## Voir aussi

- [Votre première modification](../start/first-edit.md)
- [Restaurer un secteur depuis une sauvegarde](restore-from-backup.md)
- [Annuler une étape, retirer une modification ou tout abandonner](undo-changes.md)
- [Sauvegardes](../concepts/saving-and-history.md#sauvegardes)
