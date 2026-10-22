# Areas and zones

Valheim cuts its world into square **zones** of 64 m, and the editor opens a block of them at a time, the **area**.
This explains why some places cannot be saved yet, why the edge of the area never moves, and why a big area is slow.

<!-- picture: concept-area-zones.jpg: a diagram over a top-down 3D view of a 5 × 5 area: Zone borders on, the
outermost ring of ground points tinted to show the locked edge, one zone labelled "zone 64 m", the whole block labelled
"the area (5 × 5 zones, 320 m)", and the 12 m Follow band inside each side drawn as a dashed line -->

## Zones

A zone is a square of 64 × 64 m. The status bar names the zone under the mouse (`zone 20, -6`), **View → Zone
borders** draws their lines in 3D, and the map's **Zone grid** draws them on the map.

The game makes a zone the first time a player comes near it: its trees, rocks and ore, and the location it holds (a
village, a dungeon entrance). This is called **generating** the zone. Until then the world's files hold nothing
there, not even the ground. For you this means:

- **Edits in a zone nobody has visited cannot be saved.** The save message lists them. Walk past the place in game
  first, then edit it.
- **A new world is empty.** The editor can show a seed's land before anyone plays it, but there is nothing to edit
  until the game has made the zones (see [A new world](../new-world.md)).
- **The game decides by zone.** It lays out one dungeon per zone, resets whole zones (below), and the pending line
  counts the zones whose ground you changed.

Each zone holds 65 × 65 ground points, one per metre; neighbouring zones share the points along their common edge.
See [How the ground is stored](ground-limits.md#how-the-ground-is-stored).

## The area

The area is the block of zones open in the 3D editor: from 3 × 3 up to 17 × 17 zones around the spot you picked on
the [map](../map.md).

| Size | Across | Good for |
|---|---|---|
| 3 × 3 | 192 m | A house, a field, quick edits; the fastest to open. |
| 5 × 5 (the default) | 320 m | A base and its surroundings. |
| 7 × 7, 9 × 9 | 448 m, 576 m | A village, a long road, pasting a town. |
| 13 × 13, 17 × 17 | 832 m, 1088 m | A mountain range, a coast. Slow to open and to draw. |

The editor draws every object and ground point of the area, which is why it opens one block and not the whole world.
Everything you do happens inside it: a paste, a mountain or a script must fit in the open area, so open a bigger one
before big work.

**Your changes belong to the world, not to the area.** The editor keeps one set of pending changes for the whole
world. Move the area, resize it, or go back to the map and open another place: what you did elsewhere still waits and
is saved with the rest. The history comes along too (see [History](saving-and-history.md#history)).

The top bar's arrows move the area one zone (64 m) at a time, and the camera stays where it is in the world. They do
not wait: what you were drawing (a path, an Area box, a Place line, a measure) and the selection are dropped. The
size list opens the area again at another size around the same middle zone; like Follow (below), it waits while
something would be lost. Opening another area shows hidden objects again.

## The locked edge

The outermost line of ground points of the area never changes. No brush, path, Area action, paste or script moves
it, and the Place tool puts nothing on it.

The reason: those points are shared with the zones just outside the area, which the editor has not loaded. If the
editor moved them, the ground outside would meet a step or a cliff it cannot see. With the edge locked, the area
always joins the land around it without a seam. A script that writes there is not stopped: the change is simply
left out.

```text
 outside   │ locked │        what you can edit         │ locked │   outside
 ──────────┼────────┼──────────────────────────────────┼────────┼──────────
           │  1 m   │  everything else in the area     │  1 m   │
```

To work near an edge, move the area with the arrows, pick a bigger size, or turn on **Follow**.

## Follow

With **Follow** on (in the top bar), the area grows by itself as you work towards its edge. When the point you look
at comes within 12 m of the edge, a row of zones is added on that side, or on both sides near a corner. A 3 × 3 area
becomes 4 × 3, then 4 × 4, and so on.

Follow only adds zones, it never drops any, so nothing you are working on leaves the area. It stops at 17 zones each
way (about 1 km) and says so; then move the area with the arrows, or open another place from the map.

Follow waits, and the status bar says why, while growing would lose something: objects are selected, a brush stroke
is under way, a path is drawn and not applied, or changes are still being sent to a live game.

## Resetting zones

A reset makes the game generate a zone again, as if nobody had ever been there. Use it to get a Valheim update's new
content in places nobody built, or to bring back a wood or a dungeon. You mark zones with the [Area](../area.md) tool
or across the whole world from the [map](../map.md); nothing happens until you save or apply live.

When the reset is written:

- **Everything in the zone goes**: trees, rocks, ore, pickables, ruins, dungeon entrances and their rooms, spawners,
  items lying about and creatures, tamed animals included (move them out first).
- **Some things stay**: players' tombstones always; player-built pieces with **Keep my buildings** on (the map's
  reset always keeps them, and by default leaves out zones that hold any); the ground edits, unless you
  chose to undo them too.
- **The game makes it again** the next time a player comes near: new vegetation, and its location built again at its
  place, with fresh loot and creatures. Live, a zone with a player near it is made again at once.

A zone counts as selected when the selection reaches into it past the outer half of the soft edge, so a zone that the
selection only grazes is not reset. A reset cannot be undone once it is saved or applied; until then, cancel or undo
the marking. After a reset is written the editor reads the world again, so the history starts over (see
[History](saving-and-history.md#history)).
