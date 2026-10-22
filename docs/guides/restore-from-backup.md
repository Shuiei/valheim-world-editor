# Restore one area from a backup

Put one place back as it was in a backup, after griefing, a bad raid or an edit you regret, without rolling back the
rest of the world.

**You need:** a backup of the world from before the damage: the game's own backups next to the world folder
(`<World>_backup_auto-<date>`), or a server backup copied to this computer. The game closed, or
[live mode](edit-a-server-live.md).

1. Open the world and find the place on the [map](../map.md). Press **Edit in 3D**.
2. Press `B` ([Area](../area.md)), choose **Polygon** and click round the damaged place, a little wider than it.
3. In **Action**, choose **Restore from a backup**.
4. In **Backup**, choose a date before the damage ("Game · date"). For a server backup or another copy, choose
   **Another folder…** and pick it (live, this is the only way). The line under it says which save it is.
5. Choose the kind chips to bring back. For a building, turn on **Your buildings**.
6. Leave **Ground (height and paint)** and **Objects of the kinds chosen above** ticked, or untick the one you do not
   want back.
7. Press **Restore the selection (Enter)**.
8. Look at the result. If it is right, **Save to world** (or **Apply live**).

<!-- picture: area-backup.jpg: a polygon round a griefed hut (walls missing), Restore from a backup chosen, Backup list open showing "Game · date" rows, Your buildings chip on; second half: the same view after Restore the selection with the walls back -->

## Check it worked

- The status bar says "Restored from the backup: the ground, N object(s) brought back, M removed."
- "Nothing to restore: the selection is as it was in the backup" means the backup has the same objects and ground
  there: choose an older backup.
- A restored chest has its contents again: click it with [Select](../select.md) (`E`) and press `I` to look inside.

## Undo it

`Ctrl+Z` takes the restore back before you save. Objects of the chosen kinds that are not in the backup are removed,
so restore again from a newer backup if you lost something you built later.

## Related

- [Area: Restore from a backup](../area.md#restore-from-a-backup)
- [Back up a world before editing](back-up-a-world.md)
- [Backups](../concepts/saving-and-history.md#backups)
