# Area, copy and paste

Change everything inside a box or a polygon at once: level or paint its ground, remove, replace or regrow its
objects, copy and paste it, or put it back as it was in a backup.

**Where:** tool rail, **EDIT** section · **Key:** `B` · **Works in:** saved worlds, live

![A box dragged round a dense clump of trees, Remove objects chosen, the kind chips counting what is inside](images/area.jpg)
*A box dragged round a clump of trees with **Remove objects** chosen: the kind chips count the trees, rocks and
bushes inside. Nothing has changed yet: the button at the bottom does it.*

## Use it

1. In the tool rail, click **Area** (`B`).
2. At the top of the panel, choose **Box** or **Polygon** and draw the selection on the ground.
3. In the **Action** list, choose what to do. The panel shows only that action's settings and a line saying what it
   does.
4. Press the button at the bottom, or `Enter`. Its label names the action: **Flatten (Enter)**, **Remove the objects
   (Enter)**, **Regrow nature (Enter)**…
5. Not right? `Ctrl+Z` takes it back: each action is one step in History. **Save** (or **Apply live**) keeps it.

The chosen action is remembered. The [Mask](masks.md), on its own card next to the panel, limits the ground actions
and Remove, Select, Regrow and Restore from a backup to the ground it lets through.

## Settings

| Setting | What it does |
|---|---|
| **Box** | Drag a rectangle on the ground. A box smaller than a metre is dropped. Dragging somewhere else starts a new box. |
| **Polygon** | Click corners. A double-click or `Enter` closes the shape (three corners at least); `Backspace` removes the last corner. Clicking after it is closed starts a new one. |
| **Clear (Esc)** | Clears the selection, to start a new one. |
| **Soft edge** | 0 to 20 m (3 m at first). The ground actions fade in over this many metres inside the outline, so the result blends into the land around. At **0** the edge is sharp and follows a slanted outline straight. |
| **Action** | What to do with the selection: the [ground actions](#ground-actions), the [object actions](#object-actions), [Copy and paste](#copy-and-paste), [Heightmap](#heightmap), [Restore from a backup](#restore-from-a-backup) or [Reset zones](#reset-zones). |

The line under the buttons gives the surface and how many shown objects are inside ("1250 m² selected · 84 shown
object(s) inside"). The selection is drawn only in the Area tool and while pasting; it is kept when you use another
tool.

![A polygon drawn round part of a wood](images/area-polygon.jpg)
*A closed polygon round part of a wood, with **Remove objects** chosen.*

![The same wood after Remove objects](images/area-polygon-done.jpg)
*After **Remove the objects (Enter)**: the trees inside the polygon are gone, the ones outside stand.*

## Ground actions

Each point is faded by the **Soft edge** and by the Mask.

| Action | What it does |
|---|---|
| **Flatten** | Levels the ground inside to **Height** (m). **avg** sets Height to the average ground height inside. |
| **Raise** / **Lower** | Lifts / digs the ground inside by **Amount** (m). |
| **Smooth** | Evens out bumps: each point moves towards the average of the ground within 3 m. |
| **Naturalize** | The ground is smoothed, then natural bumps are added, with the [Naturalize](terrain.md#naturalize) brush's **Bumps** and **Bump size**. |
| **Restore the ground** | Back to how the world generated it, paint removed, ground raised or dug with No limit included. |
| **Erode** | Weathers the ground in one go: slopes settle, rain cuts gullies and leaves fans, slopes settle again. Uses the [Erode](terrain.md#erode) brush's rest angle. |
| **Paint** | Paints the ground inside with **Dirt**, **Cultivated**, **Paved** or **Clear paint** (the list under it). |

Under **Flatten**, **Raise** and **Lower**:

| Setting | What it does |
|---|---|
| **No limit: past the game's ±8 m** | The ground may go up to 400 m past the generated ground. The same switch as in the brush settings: it stays on for every tool until you switch it off. See [No limit](terrain.md#no-limit). |
| Cut and fill line | How much ground the action would move ("Flatten to 35 m: dig 120 m³, fill 340 m³"), and what is out of reach ("(40 m³ out of reach: ±8 m limit)"). The second line, "Ground inside: … raised, … dug since generated", compares with the world as generated. Both follow every change. |

<!-- picture: area-cut-fill.jpg: a box over a hillside, Flatten chosen, Height 35, No limit off; panel cropped in frame showing the cut and fill line with "out of reach: ±8 m limit" and the "Ground inside" line; the box outline visible on the slope behind -->

## Object actions

For **Remove objects**, **Select objects**, **Replace objects**, **Regrow nature** and **Restore from a backup**, a row
of **kind chips** chooses which kinds the action works on. Each chip shows how many objects of its kind are inside
("Trees & logs 42"). At first **Trees & logs**, **Rocks**, **Bushes & shrubs** and **Pickables** are on; choose **Your
buildings** for player-built pieces, **Ore & deposits** for copper, tin, silver and the like.

A chip that says "hidden" is a kind switched off in the [View panel](editor-basics.md): its objects are not counted
and the action leaves them alone, except **Restore from a backup**.

| Action | What it does |
|---|---|
| **Remove objects** | Removes the shown objects of the chosen kinds inside, where the Mask allows. A linked group (a mod's ship) goes with its parts. `Ctrl+Z` brings them back. |
| **Select objects** | Selects them and switches to the [Select](select.md) tool, to move, turn, copy or delete them. |
| **Replace objects**: **Replace** … **with** … | Replaces every object of the first kind inside by the second, at the same place and facing. The first list offers the kinds inside, the most common first ("Beech1 (42)"); it does not follow the chips. The second offers every kind the world can make; **pick** fills it by clicking an object (`Esc` cancels). The new objects are fresh: a replaced chest is empty. New building pieces are built by the player chosen in View → **BUILDING** → **Built by**. |
| **Regrow nature** | Puts back what the game grows here (below). |

### Regrow nature

![A bare zone and the same zone after Regrow nature, same camera](images/area-regrow.jpg)
*Left: a zone stripped bare. Right: the same zone after **Regrow nature**: the game's own trees, bushes and rocks for
the biome are back, and the status bar says what grew.*

Regrow nature puts back the trees, rocks, bushes, pickables and ore that the game grows here, for the chosen kinds,
inside the selection and where the Mask allows. Use it to undo the clearing of a wood, or to dress ground you
reshaped.

- **It follows the game's own rules** and the world's seed: on ground still as generated, you get back the same trees
  at the same places. Alt biomes (Dark Meadows, Rock Black Forest…) get what they add.
- **It uses the ground as it is now**: a hill you raised gets what grows at its new height and slope.
- **It does not double what stands**: nothing grows within 1 m of an object, 3 m of one of the same kind or 4 m of a
  building piece, nor where a location (a village, a dungeon entrance) keeps its ground clear.
- **Up to 16 zones at once** (256 × 256 m): choose a smaller selection for more.
- If a kind it grew is hidden in View, it switches it on, so what grew stays visible.

The rules in full: [Growing rules](concepts/objects.md#growing-rules).

## Copy and paste

![Pasting a copied wood: the see-through copy follows the cursor, turned, with the Paste panel](images/paste.jpg)
*A copied wood of a few dozen trees and rocks following the cursor, see-through and turned, with the Paste panel. A
click puts it down.*

The **Copy and paste** action shows these buttons. `Ctrl+C` and `Ctrl+X` work with any action of the Area tool (and in
the Select tool); `Ctrl+V` works in any tool.

| Setting | What it does |
|---|---|
| **Copy (Ctrl+C)** | Copies every shown object inside (whatever the kind chips say), and the ground their buildings stand on. Kinds the world cannot make are left out. The copy is remembered: paste it in another area, another world, or after restarting the editor. |
| **Cut (Ctrl+X)** | The same copy, then the copied objects leave the world (`Ctrl+Z` brings them back), so a paste moves them. The ground stays. A selection with no objects copies its ground only and removes nothing. |
| **Paste (Ctrl+V)** | Starts pasting: the copy follows the cursor; click to put it down. Paste as many as you like; `Esc` stops. |
| **Save blueprint…** / **Blueprints…** | See [Blueprints](#blueprints). |

A copy holds:

- **The objects, with all their data**: a chest's contents, a sign's text, their health, who built them.
- **The ground under the buildings only**: under each building piece plus 1 m, fading out over 3 more metres. The
  rest of the selection's ground is not copied, so a pasted town sits in the land it lands on.
- **Heights relative to the copy's average ground**, which goes to the height of the ground where you click.

The [Select](select.md) tool's copy differs: trees and rocks each land on the ground, while building pieces keep the
building's shape.

### Pasting

![A hut pasted three times upwards: the see-through preview beside the original shows three floors](images/paste-stack.jpg)
*A hut with **Copies** 3 and **Along** upwards, seen from the ground: the see-through preview beside the original is
three floors high, before the click.*

The paste must fit in the open area: its outline turns red when it does not, and a click there says so. Open a bigger
area first (the area size list in the top bar, or the [map](map.md)).

| Setting | What it does |
|---|---|
| **Ground shape and paint** | Pastes the ground under the buildings, keeping its shape relative to the point you click, past the game's ±8 m when needed. A building stands on the ground it was built on. |
| **Objects** | Pastes the copied objects. |
| **Clear the site (dig the ground in its way, take away trees and rocks)** | On at first. Digs ground in the way of pasted building pieces down to the lowest piece (never raises ground), and takes away trees, rocks, ore, bushes and pickables in their footprint. Buildings and creatures stay. |
| **Height** | Moves the paste up or down (m). Lower a building into a hill and Clear the site digs it out. |
| **Turn 90° (R)** | A quarter turn. `,` and `.` turn by 1° (`Shift`: 15°). |
| **Mirror (F)** | Mirrors the paste east to west, before turning. |
| **Done (Esc)** | Stops pasting, back to the Area tool. |

Under **REPEAT (STACK)**:

| Setting | What it does |
|---|---|
| **Copies** | How many copies one click places, 1 to 50, all one undo step. |
| **Along** | Which way they follow each other: **its width** or **its depth** (these turn and mirror with the paste), or **upwards** (stacked floors). |
| **Gap** | Space between two copies (m); negative overlaps them. Upwards, a copy's size is its height plus a metre. |

Copies side by side each sit on the ground where they land; stacked upwards, only the first shapes the ground. The
line at the top of the panel says what is on the clipboard: name, size, objects, copies, turn, mirrored. The status
bar says what the paste did: "Pasted with the ground, 54 object(s), 7 tree(s), rock(s) and the like cleared from the
site."

<!-- picture: paste-clear-site.jpg: a stone hut copy pasted into the side of a grassy mound, Clear the site on; left half before the click (see-through preview sunk into the mound), right half after (the ground dug away round the hut's walls), same camera, Paste panel visible -->

## Blueprints

A blueprint is a building saved as a file, to paste into any world or build in game with
[Homestead](https://thunderstore.io/c/valheim/p/sighsorry/Homestead/). The library, the Workshop and the support
check are in [The Workshop](workshop.md).

| Setting | What it does |
|---|---|
| **Save blueprint…** | Saves the clipboard's objects (what Area or Select copied last) as a Homestead blueprint, with a name, description and tags, and whether it is a dungeon (see [Dungeons](dungeons.md)). Ground and builders are not kept. A blueprint with the same name is replaced, after asking. |
| **Blueprints…** | Opens the library (also **Blueprints** on the rail). **Paste** puts a blueprint on the clipboard and starts pasting. |

Pasted pieces keep what the blueprint holds (sign texts, items on stands, chest contents) and are built by the player
chosen in View → **BUILDING** → **Built by** (pieces nobody built stay so). Kinds the game does not know (mods not
installed) are left out, and the editor says which. A dungeon blueprint goes 5000 m up, behind an entrance on the
ground where you click, as the game keeps its dungeons.

## Heightmap

Export the ground as a grayscale picture, change it in an image editor (GIMP, Krita…) or a terrain tool (Gaea, World
Machine…), and bring it back.

| Setting | What it does |
|---|---|
| **Export the area** | Writes the ground of the whole open area as a 16-bit grayscale PNG: black is its lowest point, white its highest, north at the top, one pixel per metre (a 3 × 3 area is 193 × 193 pixels). It goes to the `heightmaps` folder of the editor's data folder; the status bar says where. |
| **Import…** | Reads a PNG (grayscale or colour, 8 or 16 bits) and fits it to the selection's box, or to the whole area when nothing is selected. |
| **Lowest** / **Highest** | The heights for black and white. A picture the editor exported brings its own, so an unchanged picture puts the ground back exactly. |
| **Put it into the ground** | Sets the ground to the picture's heights, one undo step. Inside a selection, **Soft edge** blends it in. The Mask and the ±8 m limit apply (unless No limit is on): points that cannot reach their height turn red, and the status bar counts them. |
| **Cancel** | Forgets the picture without changing the ground. |

Use 16 bits when you can: an 8-bit picture has only 256 steps between Lowest and Highest (20 cm steps over 50 m).

<!-- picture: area-heightmap.jpg: Heightmap action after Import… of an exported 16-bit PNG: panel shows Export the area / Import…, the picture line "Picture 321 × 321 fitted to …", Lowest and Highest boxes, Put it into the ground and Cancel; 3D view behind showing the area from above -->

## Restore from a backup

Put the selection back as it was in a backup of this world, to undo griefing, a bad raid or an edit you regret,
without rolling back the whole world. Step by step: [Restore one area from a backup](guides/restore-from-backup.md).

| Setting | What it does |
|---|---|
| **Backup** | The backups next to the world folder, newest first: the game's own ("Game · date") and the editor's from older versions ("Editor · date"). **Another folder…** picks any other copy of the same world, such as a server backup copied to this computer; live, the list is empty, so that is the way. A copy of another world (another seed) is refused. The line under it says which save it is and how many objects it holds. |
| **Ground (height and paint)** | The ground inside comes back exactly as in the backup, No limit ground included; within **Soft edge** it blends in. The Mask applies. |
| **Objects of the kinds chosen above** | Objects of the kinds chosen with the chips come back with all their data: a restored chest has its contents. Choose **Your buildings** to bring buildings back. |
| **Restore the selection (Enter)** | Does it: one step in History. |

An object that is the same in both (same kind, place and facing) is left alone. Every other object of the chosen kinds
inside is removed, even when hidden in View, and every unmatched object of the backup comes back. Runestones are
never touched. When nothing differs, the status bar says "Nothing to restore: the selection is as it was in the
backup."

<!-- picture: area-backup.jpg: a polygon round a griefed hut (walls missing), Restore from a backup chosen, Backup list open showing "Game · date" rows, Your buildings chip on, the backup info line visible; second half: the same view after Restore the selection with the walls back -->

## Reset zones

Mark the zones under the selection so that the game generates them again: new trees, rocks, ore and locations. To pick
zones across the whole world by biome or distance from buildings, use the
[world map](map.md#reset-zones-across-the-world); step by step: [Generate zones again](guides/reset-zones.md).

| Setting | What it does |
|---|---|
| **Keep my buildings** | Player-built objects in the zones are kept. On at first. |
| **Reset ground edits too** | Also undoes the zones' ground edits: height (No limit ground too) and paint. On at first. |
| **Reset zones… (Enter)** | Marks every zone under the selection for reset (red outline), after asking. |
| **Cancel reset** | Cancels the reset of the zones under the selection. |

A zone counts when the outline takes in at least one of its ground points, whatever the soft edge: a zone the
selection stays outside of is not reset. The reset happens when you save (offline) or apply live (WorldEditorBridge 0.10.0 or newer).

> [!CAUTION]
> **Once saved or applied, a reset cannot be undone.** Every tree, rock, ruin, dungeon entrance, creature and dropped
> item in the zones goes; players' tombstones stay. Tamed animals have no builder, so they go too: move them out
> first.

<!-- picture: area-reset.jpg: a box over two zones away from buildings, Reset zones chosen, Keep my buildings and Reset ground edits too ticked, after Reset zones… : the two zones outlined red, Zone borders on -->

## Shortcuts

| Key or mouse | What it does |
|---|---|
| `B` | Choose Area. |
| Drag (Box) / click corners (Polygon) | Draw the selection. |
| Double-click or `Enter` | Close the polygon. |
| `Backspace` | Remove the polygon's last corner. |
| `Enter` | Do the chosen action. |
| `Esc` | Clear the selection; a second `Esc` leaves the tool. |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | Copy / cut the selection / paste. |
| While pasting: `R`, `F` | Turn 90°, mirror. |
| While pasting: `,` `.` or `Alt`+wheel | Turn by 1° (`Shift`: 15°). |
| While pasting: `Ctrl`+wheel | Height by 0.5 m (`Shift`: 0.1 m). |
| While pasting: `Esc` | Done. |

## Tips and limits

- **Pasted trees and rocks can float or sink** on uneven ground: an Area copy keeps heights relative to the copy, not
  to the ground under each object. To move only trees and rocks, copy them with the [Select](select.md) tool, where
  each lands on the ground.
- **Draw the selection a few metres wider than the buildings**: the copied ground fades within the **Soft edge**, so a
  building close to the outline takes less of its ground.
- **A big town needs a big area**: choose 9 × 9 or more in the area size list before pasting.
- **Pasted objects are new objects** with all the copied data, so a paste works in another world too. Linked groups
  (a mod's ship) get new ids of their own. Live, a **Cut** then pasted moves the objects in the running game when it
  can (see [Live mode](live-mode.md)).
- **No action touches the area's outermost line of points** (see
  [The locked edge](concepts/areas-and-zones.md#the-locked-edge)).
- **To take an action back**, `Ctrl+Z` before you save; after saving, [Restore one area from a
  backup](guides/restore-from-backup.md).

## Related

- [Clear trees and rocks, level a building site](guides/clear-an-area.md)
- [Restore one area from a backup](guides/restore-from-backup.md)
- [Move or copy a building](guides/move-a-building.md)
- [Generate zones again](guides/reset-zones.md)
- [Objects](concepts/objects.md), [Backups](concepts/saving-and-history.md#backups)
- [How a paste clears its site](concepts/objects.md#how-a-paste-clears-its-site), [how Regrow decides what grows](concepts/objects.md#how-regrow-decides-what-grows)
