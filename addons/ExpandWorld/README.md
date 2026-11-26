# Expand World add-on

An add-on for the Valheim World Editor that generates, draws and predicts a world the way JereKuusela's **Expand
World** mods make it:
**Expand World Size** (a wider or stretched world, another altitude, the Ashlands and Deep North gaps),
**Expand World Rivers** (the water level, lakes, rivers and streams) and **Expand World Data** (the biome
file, the world file that lays the biomes out, territories, locations, vegetation and alt biomes).

Not made by the mods' author: the add-on reads the mods' settings and data files, and this repository holds none of
their code. Without the add-on (or with its switch off), every world is generated as the game itself does, whatever
it was made with. With it, each mod counts only where it is installed and on
(its plugin under `BepInEx/plugins`; a mod a mod manager turned off does not count, nor do the settings and data
files a removed mod leaves behind): a world is generated as the game generates it without the mods the folder
lacks, even one made with Expand World Size (the game without the mod passes over the settings its world file
keeps).

## Install

1. The Valheim World Editor 1.126.2 or later.
2. From the editor's [Releases](https://github.com/Shuiei/valheim-world-editor/releases) page,
   `ValheimWorldEditor-addon-ExpandWorld-<version>.zip`: unpack it into the `addons` folder next to the editor (on
   macOS, next to `ValheimWorldEditor.app`), so that you have `addons/ExpandWorld/worldeditor.json`.
3. The mods are read from the first BepInEx folder the editor knows that has any of them installed (the Valheim
   folder, the editor's own profile, the folders added in Settings, r2modman's and Thunderstore Mod Manager's
   profiles). For a server world opened from its files, add a copy of the server's BepInEx folder in Settings.
4. **Settings → Add-ons** lists it, with its switch: off, every world is generated as the game itself does. It is
   taken when a world is opened or made; the log says what was found and read.

## What is read from where

| What | Where it comes from |
|---|---|
| Expand World Size's settings (radius, edge, stretch, altitude, water depth, forest, offsets, height seed, the Ashlands' limits, the gaps, the save grid) | With the mod installed: the world's own file keeps every setting that was changed when the world was made (the mod writes them on the first save); a world without any uses `BepInEx/config/expand_world_size.cfg`. |
| Expand World Rivers' settings (the water level, lakes, rivers, streams) | `BepInEx/config/expand_world_rivers.cfg`. |
| Expand World Data's switches (legacy generation, wiggles, zone spawners, random locations, scaled altitudes, the Ashlands' limits and gaps, the blueprint folder) | `BepInEx/config/expand_world_data.cfg`. |
| The biomes, the world rules, the territories, the locations, the vegetation, the alt biomes, the rooms, the dungeons | `BepInEx/config/expand_world/expand_<kind>*.yaml`, each kind only when its switch in the settings file is on, every file of a kind at any depth, in the order the mod reads them. A kind whose switch is on without a file is the mod's default (the game's own, except the world rules, which the mod writes as its own list). |

The files are read through the **YamlDotNet** library the mod installs under `BepInEx/plugins` (the
`ValheimModding-YamlDotNet` package); the editor ships no copy of its own. Without it the data files are
not read, and the log says so.

A world made with Expand World Size shows "Expand World" on its card on the start page.

## What follows the mods

- **The ground** of zones the game has not made yet, the overview, the map, the water and the 3D view:
  the radius, the stretch, the altitude and water depth, the water level, the lakes, rivers and streams,
  the biome layout (the world rules, in the mod's current or legacy evaluation), every biome's ground
  shape, colour, lava and height transform, the territories. The map draws the sea at the world's real water level
  (the game's own map keeps its 29.5 m whatever Expand World Rivers sets).
- **The map's pointer box**: the territory a player at the point is in and the status effects the mod puts on
  them there (the territory's when it gives any, else the biome's), as the mod finds them for the player.
- **The water's colours**: with the mod's "Custom water color" on, the 3D view's water takes the colours the
  biome under the camera gives (when the biome file gives it other settings, as the mod needs), the territory's
  over them, as the game colours all its water by the player's place. The editor's water is its own drawing, so
  they are laid on it as close as it goes: the top colour is the shallows', the bottom colour the deep water's,
  the shallow colour tints the water near the shore and the surface colour tints the sky's reflection. The
  colours change at once where the game fades them over ten seconds.
- **The biomes**: a biome the file adds is drawn on the map and on the New world page's seed map with the
  map colour the file gives it (white without one, as the game draws a biome it does not know), named as
  the file names it, and its ground drawn as the ground it borrows. A territory is drawn in its own map
  colour on both.
- **The predictions** (the New world page, Regrow nature, a new world's generated zones): the location
  kinds of the location file (clones, blueprints, groups, "away from" and "close to" rules, a location
  wider than a zone at its zone's middle, the start temple forced into the middle zone), the vegetation
  file's rules (several prefabs, a scale by axis, a centre of the rule's own, the world's global keys, the
  ground an object keeps clear and the rule's wider clear areas, the check of the vegetation round a spot, the
  "data" and "drops" its objects get, a blueprint grown in place of a prefab with its parts made by chance and
  the blueprints it nests), the location file's clear areas, the clear
  ground of a wide location in the next zone, Expand World Size's locations multiplier, no zone spawner when the
  mod says so.
- **The Dungeon tool** (its generator from the game's rooms, the rooms it adds, the rooms drawn): the room file's
  rooms in place of the game's (a game room the file leaves out is still drawn in the dungeons that have it, but
  no longer added), each with the file's themes (a new theme name taking the next flag, as the mod gives it), its
  entrance, end cap and divider, end cap priority, weight, size, enabled, its place order ("minPlaceOrder": how
  many rooms from the entrance it joins at the fewest, as the game's own rooms say too), and its openings (type, place and turn,
  way in, doors, "exitOnly"); a copy of a game room ("name:suffix") as a room of its own, built as its game room
  with the objects the file adds (each by its chance, with its data) and saved as its game room, as the mod saves it.
  A blueprint room (a room whose name is no game room's but a blueprint's) is built of its blueprint as the mod
  builds it: its openings at the blueprint's snap points (each connection's place, or a piece the connection names,
  over the file's own), its size the blueprint's when the file gives none, its objects the blueprint's parts.
  The room and dungeon files' "objectSwap" and "objectData" change the rooms' objects (the dungeon's first, then
  the room's), drawing as the mod draws. The dungeon
  file's generators: themes, box ("bounds"), the fewest and most rooms (the Dungeon tool's size within them), the
  required and excluded rooms, the room limits (the most, and how many it must have), doors and their chance, and
  the rooms' rolls ("interiorTransform", "addBaseSeedToRandomSpawn"). The dungeons of a world the game made with the
  mod need nothing more: the mod saves copies as their game rooms and a blueprint room's parts as objects.
- **The alt biomes**: the alt biome file's (every field that says where an alt biome goes, a field left out
  being 0 as in the mod), the areas the world file's rules force alt biomes onto, Expand World Size's stretch
  of their distances, and the biome map Expand World Size sizes to the world. Locations and vegetation of an
  alt biome stand in its areas only, and what an alt biome keeps out stays out of them. An alt biome's
  "terrainTextureOverride" gives its areas that biome's ground look in the 3D view, as the game draws it.
- **The save grid**: a world wider than 16 km lies on a bigger grid of chunk files; the editor reads and
  writes it as it lies, with the add-on or without it.
- **A new world** (the New world page): the seeds looked at, the search and the world made follow the
  folder's rules; the page says so above Create. A location whose contents the location file changes is built
  in the generated zones as the mod spawns it: objects added ("objects", with their chance, scale, place on the
  ground and data), its own objects swapped, removed or given data ("objectSwap", "objectData" and their
  "location..." forms), the location's own "data", "scaleMin"/"scaleMax", "offset"/"groundOffset",
  "randomCardinal" and "randomDamage" (its building pieces worn as the game wears them, "all" over their data too;
  its objects the game drops onto the ground as it spawns them, its SnapToGround ones, on the ground first),
  and the ground of its zone levelled and painted round it ("levelArea", "levelRadius", "levelBorder", "paint",
  "paintRadius", "paintBorder"), written as the mod writes it. A blueprint location is built of its blueprint's
  pieces (made by chance, swapped and given data as the file says, the blueprints it holds in their places) on
  ground it levels unless the file says not to; a blueprint with a terrain snapshot ("#TerrainHeight",
  "#TerrainPaint") gives its ground the snapshot's heights and paint instead, in every zone the snapshot reaches,
  as the mod writes them. A location with `pregenerate` needs nothing more: the zones New world does not generate
  are generated by the game when the server starts, as the mod asks. Data comes from the data entries (`BepInEx/config/data/*.yaml` and
  `expand_world/expand_data*.yaml`), by name, as the "type, key, value" shorthand or written in place. The world file gets no settings of the mod's: the game
  writes them on the first save.

## What does not

- **Locations the editor cannot build ahead of the game** are placed where the mod places them, but their
  zones are left to the game by the New world page's generated zones, as dungeons are: "commands", "randomSeed", another "dungeon", and data the mod works out as it spawns an object (a choice of values, a range, a function,
  an inventory, a component's field) or a name it gives that is no prefab (a value group, a blueprint). With
  **Random locations** on, every location is. A location's random turn ("randomRotation", the game's own
  included) is drawn from whatever the game drew before it, so no tool can know it; New world draws one.
- **Vegetation the editor cannot write ahead of the game**: data the mod works out as it spawns an object, and a
  blueprint's part that is no prefab of the game, are left out of what Regrow nature and New world grow; the log
  says which rule. A prefab the game does not have (a mod's) is taken as drawing nothing as it wakes, so the draws
  after one can differ from the game's (the log names it).
- **The map's location icons** follow the location file's "iconAlways" and "iconPlaced"
  (an icon's size included), for icons the game's minimap has (the start, the traders); an icon named after a pin
  type, an item or a status effect is not drawn, and one the game makes pulse is drawn still.
- **Status effects** of biomes and territories: the map names those a player gets at the point under the pointer
  (by day, by night), but not the world and player keys that can turn one off, its duration or its damage. As in
  the mod, they count towards whether a biome's or a territory's settings are in use, as a territory's water
  colours do; a biome's water colours do not.
- **The Dungeon tool and the room and dungeon files' rest**: a room that is neither a game room nor a blueprint is
  left out (the log says which); a swap to a blueprint is not built; "faceCenter" and the camps' settings are not
  followed, as the Dungeon tool does not lay out camps; "randomSeed" has nothing to change, the Dungeon tool's seed
  being the one it is given. The generator joins rooms its own way, not the game's draw for draw, so a blueprint
  room's parts are drawn from the room's place. A room limit's
  "min" counts as rooms the dungeon must have: the Dungeon tool joins the rooms a dungeon needs first, so it has no
  tries to count ("maxRetries").
- **The zone system's water**: the game's own vegetation and location checks use the sea at 30 m whatever
  Expand World Rivers puts the water at, and neither mod changes that, so the editor keeps it too.
- **The mods' quirks** are mirrored as they are (a few are listed in the architecture notes), so that
  what the editor predicts is what the mod does.

Followed: Expand World Size 1.45, Expand World Rivers 1.11, Expand World Data 1.83. A later version that
adds a setting or a field is read with its defaults until the editor follows it. Data 1.81 lets a blueprint's
chance name a group (`0.5_a`: one of the group is made, by their chances as weights), and 1.82 lets a location's
`clearArea` be a radius in metres instead of its exterior radius; both are followed. Data 1.80 adds persistent events
(`expand_persistent_events.yaml`, with its "Persistent event data" switch), which the game runs while it is played and
which change no ground, location or vegetation: the world's rules do not read them, the
[Persistent event tool](#the-persistent-event-tool) does. Data 1.83 adds the game's own alt biomes an alt biome file
leaves out back to that file when a server starts with "Automatic data migration" on; the editor reads the file as it
is, so it sees them once the server has written them.

## The Persistent event tool

Expand World Data's persistent events are long lasting events over a part of the world (the game's own Jotun invasion,
and those the file adds): inside one the game forces an environment, lets the spawns that need the event run, and
shows it on the map. The game starts them by itself, somewhere its rules allow. The tool starts one **where you want
it**:

1. Open a saved world's area in the 3D editor and choose **Persistent event** on the rail, under **Mods**.
2. Choose the **Event** (the file's, in its order) and its **Size**, from the event's smallest radius (0 %) to its
   largest (100 %). The card says what the event comes to; the ring under the pointer is where it will reach.
3. Click the ground. The event is started there, and with **Place its objects** on, the objects it makes when it
   starts are placed round it.
4. Save the world. The game then finds the event active, as if it had started it there.

To stop one, set **A click** to "stops the event there" and click inside its ring: the event leaves the world's save,
its objects stay. Active events are ringed in blue, those started and not saved yet in green. Every start and stop is
one step of the history, undone and redone like any other.

How it works, and what it leaves out:

- **The save holds the event, the editor places its objects.** The game makes an event's objects only when it starts
  the event itself, so the tool places them as the file says: for each group a number between "minCount" and
  "maxCount", each of a prefab picked among "prefabs", within "maxDistance" of the middle (0: the event's radius),
  no nearer the event's other objects than "minDistanceFromOtherObjects", no steeper than "maxSlope", in or out of
  the water as "water" says (those that must be in the water float at its surface), turned at random unless
  "randomRotation" is off. A few places are tried for each, a hundred for an "important" group. Only inside the area
  open: open an area wide enough for the event, or the status bar says part of it reached past.
- **A saved world only.** In live mode the running game keeps the list of active events and writes it over the
  file's: start the event in the game there (`pevents start`). The Workshop has no world.
- **"Persistent event data" must be on** in Expand World Data's settings, and the file must be there (the game writes
  it the first time it runs with the switch on). The world's save names an event by its place in the file, as the mod
  warns: do not remove or reorder the file's entries once a world has active events.
- **Left out:** the event's "location", an object group's "data", and a prefab that is no object of the game (a
  blueprint, a location) are not placed (the status bar names them); "startMessage", "startCommands" and their end
  counterparts do not run; "enabled", "maxConcurrent", the biome, distance, altitude, slope and global key
  conditions are the game's rules for starting an event by itself, and do not hold the tool back.
- **The file is read when the tool is chosen**, but the card's list is made once: after adding or renaming events,
  restart the editor.
- An event with a duration starts counting when the world is saved, and lasts a time drawn between "minDuration" and
  "maxDuration"; without one it stays until stopped.

## The Custom object tool

The Expand World mods have no prefabs of their own: a "custom object" is an object of the game with a **data entry**,
a named set of values (a name, a health, a size, a value of the rule's own) that Expand World Data gives to a
location's objects and Expand World Prefabs' rules set, spawn with, or look for. An object the editor places is not
"created" in the game, so no rule gives it its data: the tool does.

```yaml
# expand_prefabs.yaml: a player saying "check" near such a block gets an effect.
- prefab: Player
  type: say, check
  objects:
  - prefab: blackmarble_1x1
    filter: int, sandichchuyen, 1
    maxDistance: 10
  spawn:
  - prefab: vfx_spawn
# The block: renamed, unbreakable, flattened.
- name: dichchuyen
  ints:
  - sandichchuyen, 1
  strings:
  - Piece.m_name, Dịch Chuyển
  floats:
  - WearNTear.m_health, -1
  - health, -1
  vecs:
  - scale, 0.01,1,1
```

1. Open an area in the 3D editor and choose **Custom object** on the rail, under **Mods**.
2. Choose the **Data** entry. The card says how many values it sets and which objects the rules use it on.
3. With **A click** on "places a new object", choose the **New object** and click the ground: here a
   `blackmarble_1x1` with `dichchuyen`. The list holds the objects the rules use an entry on: the rule's own objects
   with the "data" it sets or the "filter" it asks, those it spawns or swaps in with data, and the nearby "objects"
   it looks for (as above, by a value the entry sets).
4. For any other object, place it with Place or Build first, then, with **A click** on "changes the object clicked",
   click it. An object that already has data keeps its other values.

How it works, and what it leaves out:

- **The entries are read** from `BepInEx/config/data/*.yaml`, `expand_world/expand_data*.yaml` and the entries
  written among the rules of `expand_world/expand_prefabs*.yaml`, in the first BepInEx folder with Expand World Data
  or Expand World Prefabs installed; a later entry of a name wins.
- **Values are written as the game reads them**: "ints", "floats", "strings", "longs", "bools", "hashes", "vecs"
  (x, z, y), "quats" (y, x, z) and "bytes". A component's field ("Piece.m_name", "WearNTear.m_health") also marks the
  object as having fields, and fields of that component, as the game asks. A "scale" is the object's size on each
  axis; the editor draws the object unscaled when the axes differ.
- **Left out**, and named on the card: a value with a parameter or a function (`<level>`), a list or a range the mod
  draws from, a key that is a number or an animation's, and what is no plain value ("items", "containerSize",
  "position", "rotation"…).
- **The rules themselves do not run in the editor**: a "create" rule with a chance, a swap or a spawn is the game's.
  The [Prefab rules tool](#the-prefab-rules-tool) shows which objects they act on. The tool gives the data; what the rules then do with the object happens in the game.
- **The files are read when the tool is chosen**, but the card's lists are made once: after adding entries or rules,
  restart the editor.

## The Prefab rules tool

Expand World Prefabs' rules (`expand_world/expand_prefabs*.yaml`) act on objects when something happens to them: one
is made, destroyed, hit, poked, a player says a word nearby… The tool shows what the rules would do to the objects of
the open area. It changes nothing: the rules run in the game.

1. Open an area in the 3D editor and choose **Prefab rules** on the rail, under **Mods**.
2. Choose a **Rule**, or "All rules on object creation". The card says what starts the rule, the objects it is for,
   what it does (removes, swaps, spawns, sets data, drops, runs commands…) and which of its conditions only the game
   checks.
3. The objects it acts on are marked: **green** when every condition holds, **amber** when the rule also has
   conditions only the game knows (an environment, a chance, nearby objects, a key…).
4. Click an object: the status line lists the rules on it, and whether each surely or maybe acts.

What the editor checks, and what it leaves to the game:

- **Checked**: the object's name ("prefab", "excludePrefab"), its biome ("biomes", "bannedBiomes"), its distance from
  the world's middle ("minDistance", "maxDistance"; below 1 a fraction of 10 000 m, as the mod reads it), its height
  above the sea ("minAltitude", "maxAltitude"), its place ("minX"… "maxZ") and plain data filters (`int, key, 1`).
  A value the object does not hold is read as the game reads it: 0, false or empty.
- **Left to the game**: environments, global and player keys, events and their distance, nearby objects and
  locations, groups, day and night, paint and terrain height, chance and weight, conditions, filters with a weight,
  range or parameter or a "filterLimit", and a bound or biome that is no plain value or a name the editor does not
  know (an alt biome, a biome a mod adds).
- **Rules drawn together are amber**: the game draws one of the plain rules an object meets on the same trigger, and
  a "fallback" rule acts only when no other does, so such a rule is amber while another may act instead. A
  "separate" rule acts beside the others.
- **Not marked**: a rule's objects named by wildcard (`Troll*`), value group or a name the game does not have, and
  rules for the player. The card names them.
- **Which trigger**: only "create" rules run while a world is made; the others (destroy, state, say, poke, time…) run
  while it is played. Choosing one still marks the objects it is for, and the card says it is the game's.
- **The file is read when the tool is chosen**, but the card's list is made once: after adding or reordering rules,
  restart the editor.

## With Riverheim

A world made with Riverheim keeps its own ground, and its own biomes unless Expand World Data's world data chooses
them; which Expand World settings still apply to it is in [the Riverheim add-on](../Riverheim/README.md#with-expand-world).

## Live mode

A live world (your game, a dedicated server) follows the mods the game itself runs: the add-on asks the game's WorldEditorBridge (1.54.0 or newer) which plugins it loaded and reads the mods' settings
and data files from the game's own `BepInEx/config` through it, with the world's settings from its file on the
game's disk. The files are copied under `live-bepinex` in the editor's data folder and read as a local folder
would be; the log says how many came. The YamlDotNet library the data files need cannot come that way: it is
taken from a local BepInEx folder (the first the editor knows that has it). Blueprint locations' files are copied too, those the location file names.

With an older plugin, or when the bridge cannot be read, the local folders answer as for a world opened from
its files, and the log says so. When only the world's file cannot be served (the game has none yet), the
world's settings are those the game loaded, and the mods' files are still read through the bridge.
