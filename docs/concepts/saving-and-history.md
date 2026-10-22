# Saving and history

Nothing you do in the editor touches the world until you say so. Your changes wait as **pending** changes, then go
into the world's files when you **Save to world**, or into the running game when you **Apply live**. The **History**
keeps every step, even after the editor closes, so you can take a change back later.

```text
  your edit ──► pending ──► Save to world ──► a new save in the world's folder   (offline)
                   │
                   └──────► Apply live ─────► the running game, which saves it    (live)
```

## Pending changes

Every stroke, click, paste or script run is one **step**, and it stays pending until it is saved or applied. The top
bar counts what is pending, in amber ("Unsaved: 3 zones, 1 deleted, 12 added, 1 reset", or "Not applied: …" live), and
reads "All saved" or "All applied" in grey when nothing waits:

| Count | What it is |
|---|---|
| **zones** | Zones whose ground (height or paint) really differs from the save. Raising then lowering back to the same height counts nothing. |
| **deleted** | Objects of the world that will be removed. |
| **added** | New objects: placed, pasted, copied or made by a tool. |
| **reset** | Zones marked to be made again by the game (see [Resetting zones](areas-and-zones.md#resetting-zones)). |

Once you have saved, the counts are what differs from that save, so undoing a saved step makes it pending again. In
the 3D view each new object that is not saved yet carries a green dot (**View → Unsaved marks** hides them).

**Pending changes belong to the world.** They stay when you move the area, go back to the map or open another place,
and are all saved together (see [The area](areas-and-zones.md#the-area)). They are lost only when you quit without
saving (the editor asks first), throw them away, or **Reload** a live world (it asks first too).

**Discard** throws away what is not saved or applied: it undoes the unsaved steps at the end of the history, newest
first, redoes saved or applied steps you undid since, and drops the rest of the redo list. Saved and applied steps
stay. Some pending changes have no step in the open area's
history (made in another area, or before the world was read again); those can only go by reading the whole world
again from its files or from the game, which drops every pending change, and Discard asks a second time before it
does that.

## Saving a world

The game keeps a world as a folder of **numbered saves**: each save is a set of files (`_main.<n>.chunks`, `.db2`,
`.fwl2`, and a small `.ok` marker) plus chunk files holding the objects and the ground. **Save to world** works the way
the game does, so a world it saved opens in the game like any other:

1. **It writes a new save** with the next number. Only the chunk files that changed are written again; untouched
   objects are copied byte for byte. The `.ok` marker comes last, because the game only trusts a save that has it, so
   a save cut short is never taken for a good one. Each file keeps the save format it was read in, so worlds from
   Valheim 1.0.20 and later and from older versions both save, and open in the game that made them.
2. **It reads the new save back** and compares it with what it meant to write: the ground of the changed zones, the
   ground of every other zone, the number of objects, the reset zones. If anything differs or fails, the new files are
   removed and the message ends with "Nothing changed". The old save has not been touched at that point.
3. **It tidies up.** The save you opened the world from is kept while the world is open, because each save is made
   from it. A save you made earlier in the same session is removed when you save again. When you leave the world,
   only the newest save stays.
4. **It writes the history** with the world (see [History](#history)).

**Close the game first.** A running game or server keeps the world in memory and writes over your save the next time
it saves. To change a world while it runs, use [live mode](live-editing.md).

**What cannot be saved** is listed with the reason: edits in a zone no player has been near (the world has no data
there yet: see [Zones](areas-and-zones.md#zones)), and new ground in a world where nobody ever dug or levelled in game
(the editor makes ground edits by copying one the game made, so dig once anywhere in game first).

### The game saved since you opened it

If the game saved the world after the editor read it (you played between two saves), saving would throw away what
happened in the game, so the editor saves nothing. It offers to **bring your changes over** onto the game's save
instead: it makes each of your pending changes again on that save, finding objects by their kind and place, since
their ids change with every save (see [What an object is](objects.md#what-an-object-is)).

Where the game changed the same thing, it asks once per kind, **Keep mine** or **Keep the game's**:

| What clashes | Keep mine |
|---|---|
| Zones whose ground you both changed | Your changed points and paint go over the game's; the game's ground stays everywhere else in those zones. |
| Objects the game changed (a chest used, a sign, a plant growing) that you deleted, moved or edited | They are deleted, moved or edited as you did. |
| Objects the game removed (taken, destroyed, or a creature or ship that moved away) that you moved or edited | They come back where you put them. One that had only moved is then there twice. |

Nothing is written yet: the world is now the game's save with your changes pending on it. Look them over, then save.
History steps that touched something you gave to the game's version can no longer be undone, nor can the steps
before them; the message says how many were dropped.

## Applying live

In [live mode](live-editing.md) the same button is **Apply live**. It sends the pending ground, objects and zone
resets to the running game, which shares them with every player there and saves them itself, like anything players
build. The steps sent are tagged **applied** in History; with **Auto** on, every step is applied as soon as you finish
it.

Applied steps can still be undone: undo (or **Remove** in History), then apply again.

- A **deleted** object comes back as a copy of what it was when the world was read, with its data (a chest with its
  contents as they were). To the game it is a new object.
- A **placed** object is removed again, even when another mod made it again under a new id meanwhile: the plugin
  finds it by its kind and place.
- A **moved** object moves back.
- A **zone reset** cannot be undone once applied.

## History

**History** (`L`) lists every step, newest first: its name ("Raise", "Paste ×3", "Removed: Place"), its time and what
it touched. Tags say a step is **applied** (saved or sent to the game), made by **Claude**, or from an **earlier
session**. It keeps every step by default, as long as they take less than 2 GB of memory (past that the oldest go
first); **Settings → History → Steps kept** keeps only that many instead.

![The History panel after five edits, the last one undone](../images/history.jpg)
*The last step was undone with **Back to here**: it is greyed out and offers **Redo to here**.*

There are two ways back:

| | What it does | Example |
|---|---|---|
| **Back to here** | Undoes every step after this one, like `Ctrl+Z` many times. The later steps stay greyed out, to redo. A new change drops them. | You tried three things and want the world as it was before them. |
| **Remove** | Takes out this step only and keeps everything done after it: its height change comes off the points it touched, its paint goes where nothing repainted since, its objects go and the ones it deleted come back, its resets are cancelled. The removal is a new step, so `Ctrl+Z` brings it back. | Remove a tree line planted an hour ago without losing the ground work done since. |

**Take back Claude's changes** does a **Remove** for every step Claude made in the open area's history, and keeps
yours. Claude's steps made in other areas are kept aside (below): open those areas to take them back too.

### The history belongs to the world

The history comes along when you move or resize the area, when Follow grows it, and through the map. A step made
wholly in another area is kept aside while this one is open ("N step(s) made in other areas are kept"); it comes
back, ready to undo, when you open an area that holds it. A step only partly in the open area cannot be undone from
here, nor can the steps before it. Saving or applying from any area marks the steps of every area as saved.

### Kept after the editor closes

After each **Save to world** or **Apply live**, the history is written to the editor's data folder (`history/`, one
file per world folder; live, one per connection and world). Open the world again, even days later, and the steps are
back (unless a save started the history over, [below](#when-the-history-starts-over)), tagged **earlier session**: undo puts the ground back, a deleted object back with its contents, a placed one
away. Changes you neither saved nor applied are not kept.

The world may have changed since (players built and dug, or the game saved it again), so the editor asks once before
the first step from an earlier session is undone or redone: doing it puts things back as they were, over what changed
since. A world only the editor saved since is not asked about.

### When the history starts over

Some saves change the ground under the area so much that the editor reads the world again from what was written:
saving or applying **zone resets**, and saving **No limit** ground (it becomes ground discs, see
[No limit](ground-limits.md#no-limit)). The history then starts over and its file is removed. Check the result before
you save those.

## Backups

The editor makes no backup folder of its own: it never touches the old save until the new one has read back
correctly, and the save you opened from stays until you leave the world. That protects you from a save going wrong,
not from an edit you regret after leaving. For that, keep your own copy of any world you care about (see
[Back up a world before editing](../guides/back-up-a-world.md)).

The game keeps its own backups next to the world folder (`<World>_backup_auto-<date>`), and editor versions from
before kept theirs as `<World>_backup_terraineditor-<date>`. The Area tool's **Restore from a backup** puts one
selection back as it was in such a copy, without rolling back the whole world (see
[Restore one area from a backup](../guides/restore-from-backup.md)). It matches objects this way:

- An object in the selection now and in the backup, of the same kind, at the same place (within 1 cm) and facing the
  same way, is left alone.
- Every other object of the chosen kinds in the selection now is removed, even when its kind is hidden in View.
- Every object of the backup without a match comes back, with all its data: a chest with its contents, a sign with
  its text.
- Runestones are never touched.

The ground comes back exactly as in the backup, No limit ground included. A copy of another world (another seed) is
refused.
