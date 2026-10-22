# Raise or dig past the game's 8 m limit

Raise a cliff or a spire, or dig a canyon, far past the 8 m the game allows, in a way every player's game loads with
no mod.

**You need:** a world open in 3D ([a copy or a backup of it](back-up-a-world.md)), with the game closed, or
[live mode](edit-your-game-live.md).

1. Back up the world. Saving this kind of ground clears the area's History.
2. In the tool rail, click **Raise** (`1`) or **Lower** (`2`).
3. At the bottom of the brush settings, tick **No limit: past the game's ±8 m**. The status bar says the ground
   tools now go past the game's ±8 m.
4. Set **Size** and **Strength**, then hold the left button on the ground and move. The ground no longer stops at 8 m
   or turns red.
5. Optional: shape the result with **Smooth** (`4`) or **Naturalize** (`0`), which keep working past the limit
   while No limit is on.
6. Look at the shape from several sides. `Ctrl+Z` takes back a stroke; **Restore** (`5`) takes back part of one.
7. **Save** (or **Apply live**). The save message says how many ground discs were made and how many trees and rocks
   were taken away, for example "No limit ground: 312 ground disc(s), 40 tree(s)/rock(s) taken away".

For a whole mountain in one click, use [Mountain](../mountain.md) instead: it always goes past the limit.

## Check it worked

- Move the cursor over the new ground: the status bar's "original" height is now the new ground.
- Open the world in game and go there. The shape appears as you come near, as a village's levelled ground does.

## Undo it

- **Before saving:** `Ctrl+Z`, the **Restore** brush, or **Discard** for everything not saved.
- **After saving:** History starts again from the saved world. Work the shape down again with No limit on, or put
  the place back with [Restore one area from a backup](restore-from-backup.md).

## Related

- [No limit](../concepts/ground-limits.md#no-limit): how the ground is saved past 8 m
- [Sculpt and paint](../terrain.md#no-limit)
- [Mountain](../mountain.md)
