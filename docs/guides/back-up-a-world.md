# Back up a world before editing

Make a copy of a world's folder that you can always go back to, whatever happens to the world afterwards.

**You need:** the world's name, and Valheim closed (or the server that runs the world stopped).

1. Close Valheim, or stop the server. A running game keeps writing the world, so a copy made while it runs may be
   half old, half new.
2. Find the world's folder: on the editor's start page, click **A saved world** and hover the world's card. The
   tooltip ends with the folder, for example `~/.config/unity3d/IronGate/Valheim/worlds_local/Midgard`.
3. Open the folder that holds it (`worlds_local`) in your file manager. The usual places are:
   - Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local`
   - Linux: `~/.config/unity3d/IronGate/Valheim/worlds_local` (Valheim through Proton:
     `<Steam library>/steamapps/compatdata/892970/pfx/drive_c/users/steamuser/AppData/LocalLow/IronGate/Valheim/worlds_local`)
   - macOS: `~/Library/Application Support/IronGate/Valheim/worlds_local`
   - A dedicated server: `<savedir>/worlds_local` on the server.
4. Copy the world's folder (`Midgard`) and paste the copy in the same `worlds_local` folder.
5. Rename the copy `<World>_backup_<date>`, for example `Midgard_backup_2026-10-22`. Keep the world's name exactly
   as it is, then `_backup_`.

> [!TIP]
> Named this way, the copy sits next to the world, and the editor lists it as a backup in the Area tool's
> **Restore from a backup**, so you can bring back one area from it later: see
> [Restore one area from a backup](restore-from-backup.md).

## Check it worked

- The copy holds the same files as the world: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` and the `.chunk` files.
- On the start page, **A saved world** lists the copy too, under its own name. Open the world itself to edit, not
  the copy.
- In a world's 3D view, the [Area](../area.md) tool's **Restore from a backup** lists the copy as **Editor · \<date\>**.

## Undo it

The copy changes nothing in the world: delete the copy's folder when you no longer need it.

To put the whole world back as it was in the copy:

1. Close Valheim, or stop the server.
2. Move the world's folder (`Midgard`) out of `worlds_local`, to your Desktop for example (delete it later when you
   are sure).
3. Rename the copy (`Midgard_backup_2026-10-22`) to the world's name (`Midgard`).
4. Start the game: it loads the world as it was when you copied it.

The game keeps its own backups next to the world too (`<World>_backup_auto-<date>`); the editor lists them as
**Game · \<date\>**.

## Related

- [Your first edit](../start/first-edit.md)
- [Restore one area from a backup](restore-from-backup.md)
- [Undo a step, take back one change, or discard everything](undo-changes.md)
- [Backups](../concepts/saving-and-history.md#backups)
