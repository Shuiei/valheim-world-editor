# Workshop

Build on a blank plot with every piece the game has, see what would hold and what it costs, and keep it as a
blueprint to build in game or paste into any world.

**Where:** start page, **The Workshop** → **Open the Workshop**; in a world, **Blueprints** (tool rail, CREATE section)
→ **New in Workshop** · **Key:** none · **Works in:** the Workshop's own plot (the
**Blueprints** panel works in saved worlds and live)

![A tall stone house on the plot with Support check on](images/workshop.jpg)
*A tall stone and wood house seen from a distance with **Support check** on: the base light blue and green, the upper
walls yellow then orange, one overhang red. The top bar shows the piece count and how many would fall.*

A blueprint is a file of [Homestead](https://thunderstore.io/c/valheim/p/sighsorry/Homestead/), the in-game building
mod: the same file shows in Homestead's hammer tab, ready to build in game, and can be pasted into a world from the
editor.

## Use it

To build a hut and keep it:

1. On the start page, click **The Workshop**, then **Open the Workshop**. (From a world: **Blueprints** on the tool
   rail, then **New in Workshop**; a world with unsaved changes asks first.)
2. In the **Build** panel, pick a piece on the **Pieces** tab.
3. Click on the plot to put it down. It snaps to the pieces already there, like the hammer.
4. Turn the piece with `,` and `.`; lift it with `Ctrl` + wheel. To move or delete pieces, press `E` (Select).
5. Watch the **Support check** colours and the "would fall" count in the top bar.
6. Click **Save blueprint** (`Ctrl+S`), and give it a name, a description and tags.
7. Click **Start** to go back to the start page.

To change a saved blueprint, click it under **Your blueprints** on the start page (or on **Home**), or click **Edit**
beside it in a world's Blueprints panel.

![The Workshop on the start page](images/start-workshop.jpg)
*The start page's **The Workshop**: **Open the Workshop**, **Get Homestead** (it is not installed here), and three
blueprints under **Your blueprints**, newest first.*

## The plot

The plot is not part of any world: 3 × 3 zones (192 m across) of flat meadow at one height. Its middle, at ground
level, is the blueprint's **anchor**, the point Homestead measures every piece from.

- The tool rail has only **Build** (`T`), **Select** (`E`) and **View**. The ground tools are for worlds.
- There is no Mask, no View panel and no zone borders, and buildings are drawn solid.
- The top bar has **Start**, the **Support check** switch, the **Cut** slider and **Save blueprint**. It counts the
  pieces, says how many would fall and whether the building is saved
  (`12 piece(s) · 2 would fall · not saved as a blueprint`).
- Under the title: what the building costs in game. Point at it for every material, the crafting stations needed
  nearby and the pieces with no known recipe.

There is no **Discard**: undo, or open the blueprint again. Leaving with a building not saved as a blueprint
(**Start**, opening another, quitting) asks first.

<!-- picture: workshop-overview.jpg: the Workshop with a small wood hut in the middle of the plot, Build panel on the Pieces tab with the Building tab open, Support check on, Cut off; the whole window visible so the rail (Build, Select, View), the top bar (Start, Support check, Cut, piece count, Save blueprint) and the cost under the title are all readable -->

## Settings

### Pieces tab

The **Build** panel has two tabs at its top: **Pieces** and **Library**. **Pieces** lists everything players build,
each with a small picture from the game's model:

| Tab | What it holds |
|---|---|
| **Building**, **Heavy building**, **Furniture**, **Crafting**, **Misc**, **More** | The hammer's own tabs, in the game's order. |
| **Plants** | The cultivator's pieces: crops, saplings and flowers. |
| **Feasts** | The serving tray's pieces. |
| **Mods** | The pieces your mods add: a row of buttons picks one mod (with its number of pieces), and the headings are the mod's own groups. Shown once the editor has learned them from a live game (see [Live mode](live-mode.md)). |

In each tab the pieces are grouped by the crafting station they need, in the order players get them (none, workbench,
stonecutter, forge, artisan table, black forge, galdr table), then by name.

| Setting | What it does |
|---|---|
| **Search pieces** | Finds pieces in every tab by name or prefab (`stone floor`, `woodwall`). Every word must match. |
| A piece's tile | Picks it. Its name and cost show under the list (`Costs Wood 2 (needs Workbench)`). Point at a tile for its prefab name. |
| **Snap to pieces** | On: the piece snaps to the pieces already there, like the hammer. Off: it stays where it touches what you point at (like holding `Alt` in game). See [Snapping](concepts/objects.md#snapping). |
| **Grid** | **Off** (as in game), **0.5 m**, **1 m** or **2 m**: a point on the ground is rounded to this grid, counted from the plot's middle. A piece pointed at another piece snaps as usual. |
| **Turn by** | How far `,` and `.` (or `Alt` + wheel) turn the piece: **1°**, **15°**, **22.5°** (the game's step, the default), **45°** or **90°**. |
| **Lift** | `Ctrl` + wheel lifts or lowers the piece by 0.5 m a notch before it snaps (`Shift`: 0.1 m). The line shows the lift (`+1.5 m`); **Reset** takes it back to none. |

Each click puts down one piece; dragging does not paint more. With no piece picked, the Workshop picks the wooden wall.

### Library tab

![The Library tab with two blueprints and a search](images/workshop-library.jpg)
*The **Library** tab searching "hut": **Import file…** and **Generate dungeon…** at the top, then two blueprints, each
with its own picture, pieces, cost, **Open** and **Add**.*

| Setting | What it does |
|---|---|
| **Search blueprints** | Keeps the blueprints with every word you type in their name, description or tags. |
| **Open** | The blueprint alone on the plot, to change it (asks first when the plot is not saved). **Save blueprint** writes it back. |
| **Add** | Adds the blueprint to the plot, in its middle. One undo step. |
| Drag a row onto the plot | Adds the blueprint where you drop it. |
| Drop files on the plot | `.blueprint` and `.vbuild` files from your file manager are imported, then added where they land. Dropped on a world, they are only imported. |
| **Import file…** | Imports a `.blueprint` (Homestead, PlanBuild) or `.vbuild` file into the library. |
| **Generate dungeon…** | Shows the dungeon generator (building pieces only; see [Dungeon](dungeons.md)). **Open on the plot** puts the dungeon there alone, **Add to the plot** adds it in the middle; either way its blueprint is kept in the library. **← Library** goes back. |

An opened or added blueprint keeps its exact form, moved as one so its lowest building piece sits on the plot. A
Homestead blueprint keeps its anchor at the plot's middle; other files are centred. Saving it again keeps the anchor's
height, so the building stands as before when Homestead builds it. Pieces from mods you do not have are left out, and
the message lists them. A blueprint opened from outside Homestead's folder asks before saving over a library blueprint
of the same name.

### Support check

**Support check** (on by default) tints each building piece in the colours the game's build mode gives its support:

| Colour | Meaning |
|---|---|
| **Light blue** | Full support: it stands on the ground. |
| **Green** | Strong. |
| **Yellow**, **orange** | Weaker. |
| **Red** | About to break: in game it falls as soon as a player comes near. |

The top bar says how many pieces would fall, and **Save blueprint** asks first when some would. Stone stands tall but
cannot reach out; wood reaches further out but not as high. See [The support check](concepts/objects.md#the-support-check).

### Cut

![The same house with Cut, seen from above](images/workshop-cut.jpg)
*The same house with **Cut at 3 m**, seen from above: everything above 3 m is hidden, so you see into the house.*

The **Cut** slider hides everything above a height, 0 to 39 m above the plot, and the cursor goes through what is
hidden, so you can build inside. Its right end is off (`Cut: off`).

## Save a blueprint

**Save blueprint** (`Ctrl+S`) asks for:

| Field | What it is |
|---|---|
| **Name** | Homestead lists it under this name; the file is named after it. |
| **Description** | A few words, shown in the library and to people you share it with. Line breaks become spaces. |
| **Tags (separated by commas)** | Words to find it by: `house, viking, stone`. Up to 12, duplicates dropped. |
| **A dungeon: pasted into a world, it goes 5000 m up behind an entrance on the ground** | Makes it a dungeon blueprint: see [Dungeon](dungeons.md). |

The dialog shows the **Cost in game**. Saving the blueprint you opened writes over it; another name makes a new one,
and replaces one of that name only after asking. A picture is saved beside the file (a `.png`, the building seen from
above at an angle, on a transparent background), which Homestead uses as its icon. It is drawn with the game's models
when the editor can read them, else each piece's shape in its material's colour. A blueprint made elsewhere without a
picture gets one drawn in the library, in memory only.

<!-- picture: workshop-save-dialog.jpg: the Save blueprint dialog over the Workshop with a small wood hut on the plot, Name "Small hut", Description "A hut with a fire pit", Tags "house, wood", A dungeon off, the Cost in game line visible -->

What is kept:

- **Building pieces only.** Trees, rocks and items left on the plot are not.
- **What objects hold**: a chest's contents, a sign's text, an item stand's item, a creature's stars. Select an object
  and press `I` to see or change it: see [Inspector](select.md).
- **Whether it is a ruin**: pieces with no builder, as the game's ruins and generated dungeons have. No builder is
  written for the rest: whoever builds it in game becomes its builder.
- **Editor-only pieces**: statues, props, ships and creatures. Homestead's hammer skips them; pasting from the editor
  places them. The library counts them (`2 of 40 piece(s) only the editor can place`), and the cost leaves them out.

## Blueprints panel

![The Blueprints panel with a saved hut](images/blueprints.jpg)
*The **Blueprints** panel in a world: the Homestead banner, **Search blueprints** and **Import file…**, then a saved
hut with its picture, pieces, cost and its **Paste**, **Edit**, **Details**, **.vbuild** and **Delete** buttons.*

In a world, **Blueprints** on the tool rail (or **Blueprints…** in the Area panel) opens the library beside the rail;
**✕** goes back to View. A banner says whether Homestead is installed, with its version, or offers **Get Homestead**.
Each blueprint shows its picture, pieces, maker, the world it came from, its cost, description and tags.
**Search blueprints** matches name, maker, description and tags.

| Button | What it does |
|---|---|
| **Paste** | Starts pasting it into the open area (see [Area](area.md)). Kinds this world cannot make are left out and listed. A dungeon blueprint goes up behind an entrance instead. |
| **Edit** | Opens it in the Workshop. |
| **Details** | Changes its name, description, tags and the dungeon switch. A new name renames the file and picture when that name is free. |
| **.vbuild** | Writes its objects (not the ground) as a `.vbuild` file (BuildShare and older tools) into the `export` folder of the editor's blueprints folder, and says where. |
| **Delete** | Removes its file and picture, after asking. Homestead loses it too. |
| **Import file…** | Imports a `.blueprint` or `.vbuild` file. |
| **New in Workshop** | Opens the Workshop on a blank plot. |

Blueprints from editor versions before Homestead are under **Older editor blueprints**, with **Paste**, **To
Homestead** (keeps it as a Homestead blueprint, numbered when the name is taken), **.vbuild** and **Delete**. Homestead blueprints hold no ground: a
copy's ground shape is not kept, and the message says so.

**To save a blueprint from a world**, copy something with the Area or Select tool (`Ctrl+C`), then click **Save
blueprint…** in the Area panel. See [Paste a blueprint into a world](guides/paste-a-blueprint.md) for the other way.

## Blueprint files

Homestead builds the editor's blueprints in game, with a chest for the materials. The start page and the Blueprints
panel look for `Homestead.dll` in every `BepInEx/plugins` folder the editor knows: your Valheim's, the mod manager
profile the editor runs from, the folders added in Settings, and the r2modman and Thunderstore Mod Manager profiles.
Blueprints are saved where Homestead looks for them even when it is not installed, so they show in game once you
install it.

Blueprints live in Homestead's folder in Valheim's save folder (the one that has it already, else the save folder of
your worlds):

- Linux: `~/.config/unity3d/IronGate/Valheim/Homestead/Blueprints`
- Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\Homestead\Blueprints`

To share a building, share its `.blueprint` file and the `.png` beside it.

**Import** reads `.blueprint` files of Homestead and PlanBuild (their ground marks are not kept) and `.vbuild` files.
The file becomes a Homestead blueprint under its own name, numbered (`House (2)`) when the name is taken. The message
says how many pieces were unknown and how many lines could not be read.

A Homestead blueprint is a PlanBuild `.blueprint` file with header lines (`#Name:`, `#Creator:`, `#Description:`,
`#Category:`, `#HomesteadVersion:`...) and one line per piece after `#Pieces`:
`prefab;Building;x;y;z;qx;qy;qz;qw;"";1;1;1`, its place in metres from the anchor, its turn and scale. The editor adds
its own lines (`#Tags:`, `#EditorData:` for what objects hold, `#EditorRuin:1`), which Homestead and PlanBuild skip, so
the file still builds in game. Pieces are written lowest first, to the millimetre. The file name is the blueprint's
name, with characters Windows or Linux refuse turned into `_`, at most 64 characters.

The cost comes from the recipes in your own Valheim: each piece's materials times how many, most first, with the
stations that must be near (`Wood 120 · Stone 64 · Core wood 20 · … (needs Stonecutter, Workbench)`). Pieces with no
known recipe (from mods) are listed apart; with none known, it reads `Cost unknown (pieces from mods?)`.

## Shortcuts

| Key or mouse | What it does |
|---|---|
| `T` | Build. |
| `E` | Select: move, turn, delete pieces. |
| `Esc` | View. |
| Click | Put the piece down. |
| `,` `.` or `Alt` + wheel | Turn the piece by the **Turn by** step (`Shift`: 1°). |
| `Ctrl` + wheel | Lift or lower the piece 0.5 m (`Shift`: 0.1 m). |
| `Ctrl+S` | Save blueprint. |
| `Ctrl+Z` | Undo. |

All the editor's keys are in [Shortcuts](help/shortcuts.md).

## Tips and limits

- **Recover:** `Ctrl+Z` works on the plot. A saved blueprint stays in the library: **Open** (or **Edit**) brings it
  back.
- A tab shows at most 300 pieces: search to find the others.
- **What comes back.** Pasted from the editor, a blueprint's objects hold what they held (chest contents, signs, the
  boss). Built in game with Homestead, it is only the pieces: chests are empty and no creatures come.
- Copy and paste are for worlds: on the plot, use **Add** or drag a blueprint in.
- A blueprint made with mods you do not have opens without their pieces; saving it then writes it without them.
- Creatures inside a building piece are stood on top of the highest piece under them (the message says how many).
  The top bar counts the ones still inside a piece.
- The support check leaves out rocks and trees (the plot has none) and counts mesh shapes by their box.
- Homestead checks versions with the server: a server that runs it disconnects players who do not have it (console
  players cannot have it). Players only need it in their own game to build blueprints.
- To build inside a dungeon with the same Build panel, see [Dungeon](dungeons.md).
- **Claude** can build on the plot and save blueprints: see [Claude](claude.md).

## Related

- [Paste a blueprint into a world](guides/paste-a-blueprint.md)
- [Your first build](start/first-build.md)
- [The support check](concepts/objects.md#the-support-check) · [Snapping](concepts/objects.md#snapping)
- [Dungeon](dungeons.md) · [Select](select.md)
