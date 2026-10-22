# Undo a step, take back one change, or discard everything

Take back what you did in the editor: the last steps, one change from the middle while keeping the rest, or
everything not saved yet.

**You need:** a world open in 3D (saved or live) or the Workshop, with changes in **History**.

1. To undo the last change, press `Ctrl+Z` (or the curved arrow in the top bar). Press it again for the step
   before. The arrow's hover text names the step it undoes.
2. To redo what you just undid, press `Ctrl+Y` or `Ctrl+Shift+Z`.
3. To go back several steps at once, open **History** (`L`) and click **Back to here** on the last change to keep.
   The later ones turn grey; **Redo to here** on one of them brings them back.
4. To take back one change and keep everything done after it, click **Remove** on that change in **History**. It is
   struck through, and the removal is a step of its own: `Ctrl+Z` brings the change back.
5. To take back everything Claude did and keep your own changes, click **Take back Claude's changes** in **History**
   (shown only when Claude made changes).
6. To drop everything not saved or applied yet, click **Discard** in the top bar, then confirm. Saved and applied
   changes stay.
7. To take back a change you already saved, undo it (or **Remove** it), then **Save to world** again. Live, undo it
   and **Apply live** again (or let **Auto** send it).

![The History panel after five edits, the last one undone](../images/history.jpg)
*History: the last change undone with **Back to here**, greyed out with **Redo to here**.*

> [!WARNING]
> A new change made after an undo drops the greyed-out steps: they cannot be redone any more. Closing the editor
> drops every change you neither saved nor applied.

## Check it worked

- The view shows the ground and objects as they were, and the top bar's pending line counts less (or says "All
  saved").
- In **History**, the outlined row is the step you went back to; undone steps are grey, removed ones struck through.

## Undo it

Every one of these is reversible until you make a new change: `Ctrl+Y` redoes an undo, **Redo to here** brings back
greyed-out steps, and `Ctrl+Z` brings back a removed change. **Discard** is not undone: it asks first for that reason.

## Tips

- History is kept after each save or apply: open the world again, even after closing the editor, and the steps are
  back, tagged **earlier session**. If the world changed since, the editor asks before undoing one. See
  [History](../concepts/saving-and-history.md#history).
- A step made in another area waits until you open an area that holds it: the panel says how many.
- To go back further than the editor's History, use a copy of the world: see
  [Back up a world before editing](back-up-a-world.md) or [Restore one area from a backup](restore-from-backup.md).

## Related

- [Restore one area from a backup](restore-from-backup.md)
- [Back up a world before editing](back-up-a-world.md)
- [Saving and history](../concepts/saving-and-history.md)
- [The editor window](../editor-basics.md#history)
