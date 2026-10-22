# Objects

Everything in a Valheim world that is not ground is an **object**: a tree, a rock, a wall, a chest, a creature, an
invisible spawn point. This page explains what the game keeps of each one, who it counts as built by, how building
pieces snap and hold, the rules plants grow by, and what spawn points do.

## What an object is

In the save, each object is a short record:

| Part | Example |
|---|---|
| Its **kind**, the game's name for it (its *prefab*) | `Beech1`, `rock4_forest`, `piece_chest_wood`, `Spawner_Boar` |
| Its **place**, **turn**, tilt and size | x 1250, z −380, height 32.4 m, facing 90° |
| Its **data**: values the game keeps for it | A sign's text, a chest's contents, who built it, its health, when it was planted, a portal's tag |

The [Inspector](../select.md) shows all of it. The names of the values come from the game's code and from your mods'
code, which the editor reads from their files but never runs. Change a value only when you know what it does: the game
may reset or ignore values it does not expect.

**Ids change; kind and place do not.** Each object also has an id, but the game gives objects new ids when it saves,
and the editor writes a moved object as a copy at its new place. So wherever the editor has to find an object again
later, it looks for the same kind at the same place: a saved selection (within 5 cm across and 20 cm in height), your
changes brought over onto a newer save, a restore from a backup, an undo after applying live. An object that moved or
went since counts as missing.

**New objects start fresh.** A placed object is a clean copy of one of its kind in the world, with nothing unique kept
(no contents, no health), or a new object exactly as the game makes one. A replaced object is fresh too: a chest
replaced by another kind of chest is empty. A pasted copy or a blueprint, on the other hand, keeps everything its
objects held: the chest's contents, the sign's text. Kinds the world cannot make (from a mod that is not installed)
are left out, and the editor says which.

**Contents you set stay yours.** A chest the game has never opened fills itself with the game's loot the first time it
loads. When you change a chest's contents, the editor marks them as the chest's own, so the game leaves them. An item
put on a stand is saved whole, as new and at full durability for its quality.

**Not everything you see is an object.** Parts of the game's locations (a crypt entrance's rocks, the trader's camp, a
boss's altar) are made again from the location each time the zone loads, so they cannot be picked or changed (see
[Locations level the ground](ground-limits.md#locations-level-the-ground)). A runestone is the same: you can delete it,
which removes its location for good, but not move, copy or inspect it.

**Some mods hide objects far underground**, such as crafting stations 10 km under a house. Boxes, zones, **All** and
**Whole building** leave out what lies more than 10 m under the ground, so those are not taken with what stands above.

### Linked groups: ships and vehicles

Some mods build one thing out of many objects: a ship mod's vessel is a root object with pieces built on it. Each
piece is saved with the id of what it hangs on and its offset from it, and the mod puts it there when the group loads.
A mod tells the editor about this in a `worldeditor.json` file it ships; the editor knows no mod by itself. Then the
editor draws the group as the mod builds it, and moves, copies and deletes it whole, never one part alone. Each paste
gives the group new ids, because two groups sharing ids would share their parts in game.

### Creatures stand on pieces

A creature whose place lies inside a building piece (a dungeon's boss, a tamed boar in a pen) is stood on top of the
highest piece under it whenever the editor positions objects: opening or adding a blueprint, a move, a paste, a script,
Claude's tools. The ground is not a piece: a creature on it stays where it is placed.

## Player-built pieces

Everything a player can place with the hammer, the hoe, the cultivator or the serving tray is **player built** in the
game: it stores **who built it**, as a player id. That matters more than it seems. Without a builder, the game takes a
piece for part of a ruin:

| | Player built | No builder (a ruin) |
|---|---|---|
| Taking it down | Gives all its materials back | Gives a third of them back |
| A fire next to it | Makes a base: monsters do not spawn there | Is no base |
| Raids | Target it | Ignore it |
| A ward or a private chest | Answers to its builder | Answers to nobody |

So the editor writes a builder on every piece it places, pastes or builds: the player chosen in **View → Building →
Built by**. That list offers the players who built in this world (with their piece counts), the players named on
beds, wards and tombstones, and the characters on this computer. It starts with the player with the most pieces, else
your own character. **Nobody (not player built)** places pieces as parts of a ruin on purpose. With no player known at
all, pieces get **Unknown player**: they count as built, but wards and private chests answer to nobody, so choose your
own id when you can.

Some things keep or drop the builder on purpose:

- Moving or editing an object keeps the builder it has.
- A blueprint keeps no builder: whoever builds it in game becomes its builder. One saved from a ruin remembers that it
  is a ruin.
- A generated dungeon has no builder, like the game's own: raids ignore it and it gives a third of its materials back.
- **Make player built** (the [Select](../select.md) tool) gives a builder to pieces that have none: a ruin you want to
  repair and keep, or pieces placed by old editor versions.

A ruin you built onto (a stone tower patched with wood) counts as your building: its pieces that touch yours, and the
ones touching those, show with your buildings in View and on the map.

## Snapping

Building pieces join the way the game's hammer joins them. Each piece has **snap points**, at its corners and ends,
which the editor reads from your Valheim with each piece's real shape (its colliders) and its placement rules. When
two snap points come close, the piece moves so that they meet exactly: walls stand end to end, floors meet edge to
edge, a wall sits on a floor.

**How the hammer places a piece**, which the Workshop's Build panel and building in a dungeon follow step by step:

1. The cursor's line meets the nearest thing: the ground, or a piece's real shape (not its box).
2. The piece, turned as you set it, is moved so that the point of its shape nearest the hit touches it. Rugs and
   other ground pieces, and pieces the game lets sink into things, go by their middle instead.
3. Of every snap point of the piece and of the pieces within 10 m, the closest pair within half a metre meet. Point
   at a wall's top to put the next one on it, near its end to put one beside it.
4. A snap that would put the piece exactly where the same piece already stands is skipped.

A lift (`Ctrl` + wheel) is added before the snap, so a lifted piece snaps to points near its new height. A grid rounds
only points on the ground: a piece pointed at another piece snaps as usual.

The other tools snap by the same snap points, each in its own way:

| Where | How it snaps |
|---|---|
| [Place](../place.md), **Snap to pieces already there** | One piece at a time under the cursor. **Beside**: end to end with the piece next to the cursor, at its level, where a pair of snap points meets within 1 m. **On top**: its bottom on the top of the piece under the cursor. A snapped piece faces like the piece it joins. |
| [Place](../place.md), Line **End to end** | Each piece starts where the last one ends. A piece's two ends are the middles of its two short sides, found from its snap points (kinds without snap points use their model's box). A line that starts within 1 m of a piece continues it. |
| [Select](../select.md), **Snap to other pieces when moving** | While you move, the closest pair of snap points (one of a moved piece, one of a piece around) within 0.75 m locks together, and the whole selection shifts so they meet. |
| [Script](../scripting.md), `snap: true` | A placed piece meets the snap points of the pieces there within 0.5 m. |

Typed positions (the Select tool's **EXACT PLACE**) never snap: they are exact.

## The support check

In game every building piece needs **support**, which flows up from the ground through the pieces that touch. A piece
with too little breaks and falls as soon as a player comes near. The Workshop's **Support check** works it out the way
the game does, from data it reads in your Valheim (each piece's shape, material and centre of mass), and tints each
piece in the colours of the game's build mode:

| Colour | Meaning |
|---|---|
| Light blue | Full support: it stands on the ground. |
| Green | Strong: half its material's maximum support or more. |
| Yellow, orange | Weaker. |
| Red | About to break, or breaking: in game it falls as soon as a player comes near. |

**How support is worked out.**

- A piece whose shape, grown by 0.15 m on every side, reaches the ground has full support.
- Any other piece takes the best support of the pieces it touches, less a loss for the distance between their centres
  of mass: the "up" rate when held from straight below, the "sideways" rate when held from the side, a mix between.
  Held from two sides at least 100° apart (seen from above), it gets the average of the two when that is better.
- This is repeated until nothing changes. Pieces below their material's minimum break; then everything is worked out
  again without them, so a collapse spreads the way it does in game.

| Material | Maximum | Minimum | Lost per metre sideways | Lost per metre up |
|---|---|---|---|---|
| Wood | 100 | 10 | 20 % | 12.5 % |
| Core wood | 140 | 10 | 17 % | 10 % |
| Timber | 200 | 10 | 20 % | 8 % |
| Stone | 1000 | 100 | 100 % | 12.5 % |
| Ice | 1000 | 100 | 33 % | 12.5 % |
| Iron | 1500 | 20 | 8 % | 8 % |
| Black marble | 1500 | 100 | 50 % | 12.5 % |
| Grausten | 2000 | 100 | 33 % | 10 % |
| Ancient | 5000 | 100 | 25 % | 7 % |

<!-- picture: concept-support-stone-wood.jpg: the Workshop plot, Support check on, two builds side by side seen from
the side: left a stone wall three pieces high with one stone wall piece reaching 1 m out sideways from its top (that
piece red, falling count in the top bar), right a wood wall with wood beams reaching three or four metres out (green to
yellow to orange along the beams); top bar visible -->

That is why stone stands tall but cannot reach out (one metre sideways takes all its support), and why wood reaches
further out than stone but not as high.

**Exceptions, as in game.** Some pieces are never checked: black marble floor slabs, ward stones, ships and carts.
They stay up wherever they are, and a slab gives its material's full support to what rests on it; that is how a
dungeon of building pieces holds 5000 m up (see [Why it holds 5000 m up](dungeons.md#why-it-holds-5000-m-up)). The
parts that move on their own (a door's swinging leaf, a ship's hull) count for nothing: nothing rests on them. In
a game dungeon, the rooms' floors and walls count as ground, so pieces resting on them hold.

**What the check leaves out**: rocks and trees (the game counts them as full support; the Workshop's plot has none),
the exact shape of curved colliders (their box counts), and pieces the game gives no support at all (they are not
tinted). For a blueprint built on uneven ground, the check takes the ground to be where the building met it: at the
terrain contact points Homestead recorded, else under the lowest piece of each spot.

## Growing rules

Saplings, crops and wild plants follow the game's own rules, and the editor's [Place](../place.md) and Regrow tools
follow them too, so what you plant grows in game.

**Room to grow.** A sapling or a crop only grows when nothing stands within its **grow radius**, read from your game:
0.5 m for most crops, 2 to 3 m for tree saplings. With **Leave saplings and crops room to grow** on (the default):

- saplings and crops keep at least their grow radius from everything, each other included;
- grown crops (`Pickable_Carrot`) and trees that grow from saplings (`Beech1`, `Oak1`) keep the room their sapling
  needs, like a real field or orchard;
- bushes do not grow from anything in game, so only **Spacing** applies to them.

Off, you can plant as tightly as you like, but those plants may never grow.

**Crops need cultivated ground.** The game only lets crops grow on cultivated soil, so paint it first (the **Cultivate**
brush or the Area tool's Paint). The Place panel warns when a ticked crop needs it.

**Water.** An object placed on the ground is left out where the ground is more than 0.3 m under the water, except
kelp and seaweed.

**Clumping.** Real woods grow in groves with clearings between them. **Clumping** lays a noise pattern over the world,
its blobs **Patch size** across: where the pattern is low, spots are thinned out, and the higher the Clumping, the more
of the ground is cleared. The pattern is fixed to the world, not to the brush, so strokes side by side match.

### How Regrow decides what grows

The Area tool's **Regrow nature** puts back what the game itself would grow, by working out each zone as the game does
when it first makes it:

- **The game's own rules.** It reads the game's vegetation rules from your Valheim and uses the same random numbers,
  seeded by the world's seed, the zone and the kind. On ground still as generated, you get back the same trees at the
  same places the world first had.
- **The game's conditions**: the biome (and whether a kind grows at its edge or its middle), the height above the
  water, the slope, the forest factor (groves and clearings), how rough the ground is round the spot, the distance
  from the world's centre, how many groups per zone and how many objects per group.
- **The ground as it is now.** A hill you raised gets what grows at its new height and slope; a pad you levelled gets
  what grows on flat ground.
- **Not on top of things.** A spot is skipped when something stands within 1 m, an object of the same kind within 3 m
  (a tree still standing is not doubled), or a building piece within 4 m.
- **Alt biomes** (Dark Meadows, Rock Black Forest…) are where the game puts them, from the seed, with what they add and
  without what they keep out.
- **Locations** keep their ground clear, as when the game first makes a zone.

What it cannot know: the game's physics checks against objects already standing. It works on up to 16 zones at once.
The Mountain tool's **Grow the biome's trees and rocks on it** uses the same rules on the slopes it raised.

## Spawn points

The game puts creatures in its camps, villages and dungeons with **spawn points**: invisible objects
(`Spawner_Boar`, `Spawner_Draugr`…), each spawning its creature where it stands. Which creature, how many stars and
whether it comes back are fixed by the spawn point's kind, so to get another creature you place another kind.

| How it spawns | Which ones |
|---|---|
| **Spawns once**: the first time a player comes near, never again once it dies | Most of the game's spawn points. |
| **Back N min after it dies** | The `*_respawn_*` ones, `Spawner_Location_*`, `Spawner_imp_respawn`, `Spawner_Fish4`; often 60 min. |
| **By night only** or **by day only** | A few that wait. |
| **When an event wakes it** | The `TriggerSpawner_` ones. |

**Creature nests** are the visible kind: greydwarf nests, bone piles and the like. They keep spawning creatures round
themselves until destroyed ("Greydwarf, Greydwarf_Elite · up to 3 around, within 5 m").

![Boar and draugr spawn points by a hut, one selected and open in the Inspector](../images/spawn-points.jpg)
*Spawn points by a hut: orange boxes with a marker above each. The selected draugr spawn point is open in the
Inspector, whose line says what it spawns.*

The Place tool and the Inspector show this line for each kind, read from your game (`Spawn point: Boar · 0–2 ★ · spawns
once`). In 3D, spawn points show as orange boxes (red when their creature comes back) and nests as purple ones, each
with a marker seen through anything in front. Both are hidden at first, because they give away where things are; turn
them on in the View panel.

## How a paste clears its site

A copy (from the Area tool or a blueprint) holds:

- **The objects**, with everything they hold: a pasted chest is full like the original.
- **The ground under the buildings only**: under each building piece (by its real shape) and 1 m round it, fading into
  the ground where it lands over 3 more metres. The rest of the selection's ground stays behind, so a pasted town sits
  in the land where it lands, instead of carrying a square of foreign ground with it. Blueprints hold no ground.
- **Heights** relative to the copy's average ground height, which goes to the height of the ground where you click.

The Select tool's copy differs: trees, rocks and the like each land on the ground where they go, and building pieces
keep the building's shape with the ground under them.

<!-- picture: concept-paste-clear-site.jpg: two copies of the same small hut pasted side by side into the slope of a
mound, Game look, seen from the side and a little above: left pasted with Clear the site off (the hill runs through
the walls and floor), right with it on (the ground dug out round the hut down to its floor, trees in the footprint
gone); Paste panel visible with the Clear the site switch -->

With **Clear the site** on, each paste also makes room for itself, in the same undo step:

- Where a pasted building piece stands (by its real shape, and 1 m round it), the ground in its way is **dug down** to
  the lowest piece's bottom, past ±8 m if needed. A building pasted into a hill is not buried in it. Lower ground is
  never raised.
- Where the copied ground went in fully, nothing is dug: the building stands on it as it stood on the original.
- Pieces more than 100 m below the ground are ignored (some mods keep crafting stations there), and nothing is dug
  under a linked group such as a ship.
- **Trees, rocks, ore, bushes and pickables** in that footprint are taken away. Buildings, creatures and tamed animals
  stay.

Pasted objects are new objects made from the copy, so a paste works in another world too. Live, a **Cut** then pasted
moves the objects in the game under their own ids when it can (see
[Moving an object keeps it the same object](live-editing.md#moving-an-object-keeps-it-the-same-object)).

## Blueprint files

A blueprint is a building kept as a file, in the format of
[Homestead](https://thunderstore.io/c/valheim/p/sighsorry/Homestead/), the in-game building mod: the same file shows
in Homestead's hammer tab, ready to build in game, and pastes into any world from the editor. Sharing a building means sharing its `.blueprint` file and the `.png` picture beside it.

**What it keeps.** Building pieces only: trees, rocks and items left lying are not kept, and neither is the ground.
Each piece is a line with its kind, its place in metres from the **anchor** (the middle of the building at ground
level), its turn and its size, lowest pieces first:

```text
#Name:Small hut
#Creator:Valheim World Editor
#Description:A hut with a fire pit
#Tags:house, wood
#Pieces
woodwall;Building;-3;1;-2;0;0;0;1;"";1;1;1
```

The editor adds lines of its own that Homestead and PlanBuild skip, so the same file still builds in game: the tags,
what objects hold (a chest's contents, a sign's text, a boss's stars), whether the building is a ruin, and which
pieces only the editor can place (statues, ships, creatures: Homestead's hammer skips them, and the cost shown leaves
them out). No builder is ever written: whoever builds it in game becomes its builder.

**What comes back.** Pasted from the editor, a blueprint's objects come back holding what they held. Built in game
with Homestead it is only the pieces: the game makes chests empty and places no creatures.

**Its height.** Opened in the Workshop, a blueprint is moved as one so that the bottom of its lowest building piece is
on the plot (rocks and other things the hoe places do not count). The editor remembers that lift and takes it off
again when it saves, so the anchor keeps its height and the building stands as before when Homestead builds it.

**Other files.** The editor also reads PlanBuild `.blueprint` files and `.vbuild` files (BuildShare and older tools,
one piece per line), and can write `.vbuild`. They measure from a corner, so they are centred on the plot. Their
ground marks are not kept, pieces of mods you do not have are left out, and the message says how many.
