# World map

![The world map with a zone picked: the area to open outlined in light blue, Edit in 3D and the size list in the side panel](images/map.jpg)

*A zone is picked: the light blue square is the area **Edit in 3D** will open, at the size chosen next to the button.*

Opening a world shows a map of the whole world. It is drawn the way the game draws its own map, from the
world's own data, so it looks like the in-game map with everything revealed. From here you pick the place to
edit in 3D, search the whole world, reset zones anywhere at once, and save (or apply live) what is waiting.
The locations the game marks on its map show their icons as in the game: the start, and the traders once
their zone is made (with Expand World Data, the icons its location file gives).
A world made wider with Expand World Size is shown whole too, with the Expand World add-on installed and on (see
the [Expand World add-on](../addons/ExpandWorld/README.md)).

## How to use it

1. **Drag** to move the map, turn the **mouse wheel** to zoom (towards the pointer). The map remembers where
   you last looked, also the next time you start the editor.
2. **Click** a spot. The side panel shows its zone and biome ("Zone 12, -4 · Black Forest"), with **Edit in 3D**
   and the size list. The area that will open is outlined in light blue.
3. Choose the size, then press **Edit in 3D**. The [3D editor](editor-basics.md) opens around the zone you
   clicked.

At the bottom of the map, a box shows the point under the pointer: its coordinates, its zone and its biome
("x 812, z -260 · zone 13, -4 · Meadows"), then, when the point has them, its area's alt biome ("alt biome Dark
Meadows", shown once the world's alt biomes are worked out, a few seconds after the first point), and with Expand
World Data its territory and the status effects a player gets there ("territory frost · effects Freezing (night)").

The top of the side panel gives the world's name, seed, save number and how many objects it holds (live: the
connection instead of the save number). **Worlds** goes back to the start page.

## Controls

| Control | What it does |
|---|---|
| **Show buildings (N player-built pieces)** | Draws every player-built piece. Close up you see each piece's footprint (outlined when you are very close); further out each piece is a dot; zoomed far out nothing is drawn. The pieces of a ruin that players built onto (a patched tower) are drawn with the building, as in the 3D view. N is how many player-built pieces the world has. On at first. |
| **Show ruins (spoilers)** | Also draws the pieces of the world's ruins (stone towers, ruined houses, Mistlands and Ashlands ruins), fainter. Off at first, because it shows where they are. |
| **Outline edited zones** | Outlines the zones whose ground was changed: yellow for changes already in the world (made in game, or saved), orange for zones with changes not saved (or not applied) yet. On at first. |
| **Show painted ground (dirt, paved, fields) when zoomed in** | Close up, the map is drawn again at 1 m detail, with your pending ground changes in it; with this on, it also shows dirt, paved and cultivated ground as the game does. On at first. |
| **Zone grid** | Draws the lines between the 64 m zones (only when zoomed in enough). |
| **Clouds (as in game)** | Draws the drifting cloud shadows of the in-game map. |
| **Edit in 3D** | Opens the 3D editor on the area around the picked zone. |
| Size list (next to **Edit in 3D**) | How much ground the editor loads: "3 × 3 zones (192 m)", "5 × 5 zones (320 m)" (the default), "7 × 7 zones (448 m)", "9 × 9 zones (576 m)", "13 × 13 zones (832 m)" or "17 × 17 zones (1088 m)". Bigger areas take longer to load and draw. You can change it later in the editor's top bar. |
| **Save to world…** / **Apply live** | Writes every pending change into the world files (live: sends it to the running game), as in the editor. The line above says what is waiting, for example "Not saved yet: 3 zones of ground, 12 deleted, 40 added, 25 zone reset." |
| **Discard** | Throws away every change not saved or applied yet (you are asked first). |
| **Reload from the game** | Live only: reads the whole world again from the running game. The line above it lists the players online ("Live world · 2 player(s) online: Ragnar, Freya"), or says that the game does not answer right now. |

In live mode the players online are drawn as blue dots with their names, updated every 5 seconds. See
[Live mode](live-mode.md).

Changes stay pending across areas: you can edit one area, go back to the map, open another one, edit it, and
save everything at once.

## Search the world

![Search the world for Beech1: every beech pinned in orange on the map, the counts and the list of results in the side panel](images/map-search.jpg)

*Each orange pin is one object found; the one picked in the list is larger, with a gold ring.*

Find things anywhere in the world (like Amulet's find, or WorldEdit's `//count`). Type part of a name or a
text, choose what to look for, and press **Find** (or `Enter`). Letter case does not matter. The section folds
with its title, like the two below.

| Look for | Finds |
|---|---|
| **Objects (by kind)** | Every object whose kind contains the text: `portal`, `Beech`, `chest`, `guard_stone`… The game's own bookkeeping objects (names starting with `_`, such as `_ZoneCtrl`) are found only when your text starts with `_` too. |
| **Items in containers** | Every chest, cart, ship or other container holding an item whose name contains the text: `Wood`, `Draugr`, `Silver`… Each result says how many it holds ("Wood ×50"). |
| **Texts (signs, portals, wards…)** | Every object with a text that contains it: sign texts, portal tags, ward and tombstone names, and any other text an object keeps. Each result shows the text that matched. |

Above the results, a line counts what was found per kind (per item for **Items in containers**, all stacks
added up) and in all. The list shows the first 300 results; up to 2,000 are pinned on the map.

Click a result to go there: the map centres on it and picks its zone, and **Edit in 3D** then opens the editor
with that object selected. For items and texts its [data](select.md#inspecting-and-changing-an-objects-data)
opens too, so you can change the chest's contents or the sign's text at once.

The search reads the world as it is in the editor: objects you deleted but did not save yet are not found,
objects you placed but did not save yet are. Changing what to look for searches again at once.

## Reset zones across the world

![Reset zones across the world with Meadows chosen and Show matching pressed: the matching zones shaded blue on the map, their count below the buttons](images/map-reset.jpg)

*Blue zones match the filter; zones already marked for reset are red.*

Like Minecraft's MCA Selector: pick zones anywhere in the world by what they are, and have the game make
them again from scratch (new trees, rocks, ore, dungeons). Use it, for example, to get the new content of a
Valheim update in places nobody has built.

### Steps

1. Open **Reset zones across the world** (folded at first).
2. Set the filter (below). Press **Show matching**: the editor reads every zone of the world (a moment, the first
   time), shades the matching zones blue and says "N of M zones match (K objects in them)". From then on the
   map follows the filter as you change it.
3. Press **Mark for reset…**. After you agree, every matching zone is marked for reset (red).
4. **Save to world** (offline) or **Apply live** does the reset. Until then, **Unmark all** cancels it.

| Control | What it does |
|---|---|
| Biome buttons (**Meadows**, **Black Forest**, **Swamp**, **Mountain**, **Plains**, **Mistlands**, **Ashlands**, **Deep North**, **Ocean**) | Only zones whose middle is in one of the biomes switched on. None on: every biome. |
| **No buildings in the zone or within N zone(s)** | Leaves out zones close to player-built pieces: N zones in every direction, diagonals included (0 to 50; 2 at first). On at first. A zone that holds pieces is always left out, even with this off: the map never resets buildings. |
| **No ground edits** | Leaves out zones whose ground was changed (in game or in the editor). On at first. |
| **Only zones the game generated** | Only zones the game has already made (a player has been near them). Off: also zones that only hold objects or ground edits from the editor. On at first. |
| **From the centre … to … m** | Only zones at this distance from the middle of the world, in metres. Empty: no limit (the world's edge is at 10,500 m). Use it to reset only the outer rings, for example. |
| **Also undo their ground edits** | The reset also takes away the zones' ground changes, so the ground goes back to the seed's. Only matters when **No ground edits** is off. |
| **Show matching** | Shades the matching zones blue and counts them and the objects in them. |
| **Mark for reset…** | Marks every matching zone for reset (red), after asking. |
| **Unmark all** | Cancels every zone reset that is marked, also those marked with the Area tool in the 3D editor. |

### How it works

A reset does to a zone what the game does to a zone nobody has visited yet:

- Every object in the zone is removed, except **players' tombstones** and **player-built pieces** (walls,
  portals, carts, ships and the rest of what a hammer builds). Everything else goes: trees, rocks, ore,
  pickables, ruins, dungeon entrances, the zone's spawners, and also items lying on the ground and creatures
  standing there, tamed ones included.
- The zone is marked as not generated, and the location it holds (a ruin, a dungeon entrance, an altar) as not
  placed yet. The next time a player comes near, the game generates the zone again from the seed: new
  vegetation, and its location built again.
- The ground changes stay, unless you ticked **Also undo their ground edits**.
- Live, the game generates a reset zone again at once when a player is near it, otherwise the next time someone
  comes. After a live reset the editor reads the world again from the game, and the history starts over.

A reset cannot be undone once it is saved or applied. Until then, **Unmark all** (or undo the marking in the
editor) cancels it.

Only zones inside the world are listed. A dedicated server with nobody online also generates zones around a
point far outside the world: those are left out.

## Edited zones

The **Edited zones (N)** section (folded at first) lists every zone whose ground was changed, most changed
first. Each row gives the zone, how many height points (`h`) and paint points (`p`) were changed, the range of
the height changes in metres (for example `-2.4 … 6.0 m`), and "(not saved)" when some of its changes are
still pending. Click a row to go there: the map zooms in on the zone and picks it, ready for **Edit in 3D**.

## Good to know

- The map shows the world as it was read. In live mode, the 3D editor follows what players change in the open
  area, but the rest of the map only changes with **Reload from the game**.
- Zoomed out, the map comes from the world's own map data. Zoomed in closer than about 6 m a pixel, the part in
  view is drawn again at 1 m detail, with your pending ground changes in it.
- Before you save zone resets (or anything else) into a world offline, close the game or stop the server: a
  running game writes over the world files when it saves. To change a world while it runs, use live mode.
