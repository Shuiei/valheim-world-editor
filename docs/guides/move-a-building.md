# Move or copy a building

Move a house, with everything in it, a few metres or across the world, or put a copy of it somewhere else.

**You need:** a world open in the editor, with the area round the building open (a backup first: see
[Back up a world](back-up-a-world.md)).

## Steps

1. Press `E` to open [Select](../select.md).
2. Double-click a wall of the building: every connected piece is selected, chests and furniture on its floors too.
3. Optional: to add loose objects inside (a table standing apart), `Ctrl` + click them.
4. For a short move, drag a selected piece, or drag an arrow to move along one axis; drag the orange ring to turn it.
   Keep **Put each object on the ground when moving** on, so the building sits on the ground where it lands.
5. For a long move, press `Ctrl+X` (or `Ctrl+C` for a copy). The building is now on the clipboard.
6. Open the area at the new place (from the [Map](../map.md)), or stay in this one.
7. Press `Ctrl+V`: the building follows the cursor, see-through.
8. Turn it with `R` (a quarter turn) or `,` `.`, then click where it should stand.
9. Press `Esc` to stop pasting.
10. Click **Save to world** (or **Apply live**).

Copies and moved pieces keep everything they hold: chest contents, sign texts, who built them. A building pasted
with `Ctrl+V` takes the ground under it along.

## Check it worked

The status bar says how many objects moved or were pasted. Look round the building from several sides: nothing
should float or be left behind. After saving, open the world in game and walk in.

## Undo it

- Before saving: `Ctrl+Z` takes back the move, the cut or the paste (a cut and its paste are separate steps).
- After saving: see [Undo a step, take back one change, or discard everything](undo-changes.md), or
  [Restore one area from a backup](restore-from-backup.md).

## Tips

- A paste must fit inside the open area: if it says *Not pasted*, open a bigger area round the place.
- The clipboard stays: you can paste in another area or another world.
- To move by exact metres, use **EXACT PLACE** → **Move by** in the Select panel.
- For a ship or other mod vehicle, select any part: the whole group moves.
- In live mode, a building moved while no player is near it stays the same object in the game, so mods that track
  it keep track: see
  [Moving an object keeps it the same object](../concepts/live-editing.md#moving-an-object-keeps-it-the-same-object).

## Related

- [Select](../select.md) · [Area](../area.md)
- [Snapping](../concepts/objects.md#snapping)
- [Paste a blueprint into a world](paste-a-blueprint.md)
