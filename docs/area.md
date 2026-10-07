# Area, copy and paste (`B`)

![An area selection and its panel](images/area.jpg)

The Area tool works on everything inside a box or polygon at once: the ground, the objects, or the
zones themselves. The [Mask](masks.md) applies to the ground actions.

## Selecting

| Control | What it does |
|---|---|
| **Box** | Drag a rectangle on the ground. |
| **Polygon** | Click corners; double-click or `Enter` closes the shape, `Backspace` removes the last corner. |
| `Esc` | Clears the selection. Clicking outside starts a new one. |

The panel shows the selected surface and how many shown objects are inside.

## Ground

| Control | What it does |
|---|---|
| **Soft edge** | Ground actions fade out over this many metres inside the edge of the selection, so the result blends in. |
| **Flatten** | Levels the ground inside to **Height**. |
| **Raise** / **Lower** | Lifts / digs the ground inside by **Amount**. |
| **Smooth** | Evens out bumps inside. |
| **Naturalize** | Natural-looking bumps inside (Bumps and Size of the Naturalize tool). |
| **Restore** | Puts the ground inside back to how the world generated it, and removes paint. |
| **Height** | Height (m) used by Flatten. **avg** sets it to the average ground height inside. |
| **Amount** | Metres used by Raise and Lower. |
| **Paint** + **apply** | Paints the ground inside with dirt, cultivated, paved, or clears the paint. |

Each button is one undo step.

## Objects inside

| Control | What it does |
|---|---|
| **Kind chips** | Which kinds the buttons act on, with how many are inside. "hidden" means the kind is switched off in View. |
| **Remove** | Removes the objects of the ticked kinds inside the selection. |
| **Select** | Selects them, to move, turn or copy them with the [Select tool](select.md). |
| **Replace** … **with** … | Replaces every object of the first kind inside by the second kind, at the same place and facing. The first list only offers kinds that are inside; the second offers every kind the game has. |

## Copy and paste

![Pasting a copy, turned](images/paste.jpg)

| Control | What it does |
|---|---|
| **Copy** (`Ctrl+C`) | Copies the ground shape, the paint and the shown objects inside the selection. The copy is kept in the browser, so you can paste it in another area or after a reload. |
| **Paste** (`Ctrl+V`) | Switches to pasting: the outline and the objects (orange dots) follow the cursor; click to place. You can paste several copies; `Esc` stops. |

While pasting:

| Control | What it does |
|---|---|
| **Ground shape and paint** | Paste the copied ground. The shape is kept relative to the point you click. |
| **Objects** | Paste the copied objects. They are new, independent objects: a pasted chest is an empty chest. |
| **Height** | Moves the pasted ground and objects up or down (m). |
| **Turn 90°** (`R`) | A quarter turn. `,` and `.` or Alt + wheel turn by 1° (Shift: 15°), to any angle. |
| **Mirror** (`F`) | Mirrors the paste. |
| **Done** (`Esc`) | Stops pasting. |
| **Copies** | How many copies one click places (like WorldEdit's `//stack`). The extra copies are outlined too. All of them are one undo step. |
| **Along** | Which way the copies follow each other: along the copy's **width** or **depth** (these turn and mirror with the paste), or **upwards** (stacked floors; only the first copy shapes the ground). |
| **Gap** | Space between two copies (m). Each copy is moved by its own size plus the gap; a negative gap makes them overlap. Copies side by side each sit on the ground where they land. |

## Blueprints

A blueprint is a copy kept as a file, like a WorldEdit schematic: it stays when the editor closes
and can be pasted into any world.

| Control | What it does |
|---|---|
| **Save blueprint…** | Saves the clipboard (what the Area or Select tool copied last) under a name you type. A blueprint with the same name is replaced, after asking. |
| **Blueprints…** | Opens the list of saved blueprints, each with a picture seen from above (ground shaded by height, objects as dots), its size, its number of objects and the world it came from. **Paste** puts it on the clipboard and starts pasting; **Delete** removes its file. |

### Other mods' blueprints

| Control | What it does |
|---|---|
| **Import file…** | Reads a blueprint of the [PlanBuild](https://github.com/sirskunkalot/PlanBuild) mod (`.blueprint`) or a `.vbuild` file (BuildShare and older tools) and keeps it as a blueprint here. PlanBuild's terrain marks become copied ground: levelled to their height and painted. Pieces the game does not know (from other mods) are left out, and the editor says which. |
| **.blueprint** / **.vbuild** | Writes the blueprint as a PlanBuild `.blueprint` or a `.vbuild` file into `blueprints/export` (in a browser it is downloaded too). For PlanBuild, copy it into `BepInEx/config/PlanBuild/blueprints`. Only the objects are written: those formats cannot hold free-form ground. |

Sign texts and items on item stands in PlanBuild files are not carried over: pasted objects are always
fresh ones.

Blueprints are files in the `blueprints` folder of the editor's data folder
(`~/.local/share/ValheimWorldEditor/blueprints` on Linux, `%LOCALAPPDATA%\ValheimWorldEditor\blueprints`
on Windows), one `.json` file each, so they can be copied to another computer or shared. Pasted into
another world, the objects are made like the ones you plant: copies of an object of the same kind
in that world, or new objects for kinds it has none of. Kinds the game does not know (from mods)
are left out, and the editor says which.

## Restore from a backup

Like WorldEdit's `//restore`: put the selection back as it was in a backup of this world, to undo
griefing, a bad raid or an edit you regret, without rolling back the whole world.

| Control | What it does |
|---|---|
| **Backup** | The backups next to the world folder, newest first: the editor's (made before every save, `<World>_backup_terraineditor-<date>`) and the game's own (`<World>_backup_auto-<date>`). **Another folder…** picks any other copy of the same world, for example a server backup copied to this computer (in live mode the list is empty, so that is the way). A copy of another world (another seed) is refused. |
| **Ground** | The height and paint inside the selection come back exactly as in the backup; within the **Soft edge** they blend into the ground around. The Mask applies. |
| **Objects** | The objects of the kinds ticked under **Objects inside** (tick **Buildings** for buildings) come back as they were, with all their data: a restored chest has its contents, a sign its text. Objects that are in the selection now but were not in the backup are removed. Objects unchanged since the backup are left as they are. |
| **Restore the selection** | Does it: one step in History. Save or Apply live writes it. |

## Reset zones

| Control | What it does |
|---|---|
| **Keep my buildings** | Player-built pieces in the zones are kept. |
| **Reset ground edits too** | Also undoes the ground edits in the zones (height and paint). |
| **Reset zones…** | Marks every zone under the selection (red outline). On save, those zones lose their trees, rocks, ruins and dungeon entrances, and the game generates them again the next time a player goes there: new trees, new ore, new dungeons. You are asked to confirm first. |
| **Cancel reset** | Cancels the reset of the zones under the selection. |

Resetting is applied when you save (offline) or apply live (with a WorldEditorBridge newer than 0.4.1).
Players' tombstones are never removed. To reset many zones across the whole world at once (by biome,
away from buildings...), use the [zone filter on the world map](map.md#reset-zones-across-the-world).
