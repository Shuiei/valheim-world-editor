# Expand World

The editor can generate, draw and predict a world the way JereKuusela's **Expand World** mods make it:
**Expand World Size** (a wider or stretched world, another altitude, the Ashlands and Deep North gaps),
**Expand World Rivers** (the water level, lakes, rivers and streams) and **Expand World Data** (the biome
file, the world file that lays the biomes out, territories, locations, vegetation and alt biomes).

Nothing of this is on unless you ask for it: with the switch off, every world is generated as the game
itself does, whatever it was made with. With the switch on, each mod counts only where it is installed and on
(its plugin under `BepInEx/plugins`; a mod a mod manager turned off does not count, nor do the settings and data
files a removed mod leaves behind): a world is generated as the game generates it without the mods the folder
lacks, even one made with Expand World Size (the game without the mod passes over the settings its world file
keeps).

## Turning it on

1. Open **Settings** and turn on **Generate worlds as the Expand World mods make them**.
2. Leave the **folder** empty to use the first BepInEx folder the editor knows that has the mods installed (the Valheim
   folder, the editor's own profile, the folders added above, r2modman's and Thunderstore Mod Manager's
   profiles), or point it at another BepInEx folder: a copy of a server's, say, for a server world opened from
   its files.
3. Open the world. The log says what was found and read.

The setting is taken when a world is opened or made.

## What is read from where

| What | Where it comes from |
|---|---|
| Expand World Size's settings (radius, edge, stretch, altitude, water depth, forest, offsets, height seed, the Ashlands' limits, the gaps, the save grid) | With the mod installed: the world's own file keeps every setting that was changed when the world was made (the mod writes them on the first save); a world without any uses `BepInEx/config/expand_world_size.cfg`. |
| Expand World Rivers' settings (the water level, lakes, rivers, streams) | `BepInEx/config/expand_world_rivers.cfg`. |
| Expand World Data's switches (legacy generation, wiggles, zone spawners, random locations, scaled altitudes, the Ashlands' limits and gaps, the blueprint folder) | `BepInEx/config/expand_world_data.cfg`. |
| The biomes, the world rules, the territories, the locations, the vegetation, the alt biomes | `BepInEx/config/expand_world/expand_<kind>*.yaml`, each kind only when its switch in the settings file is on, every file of a kind at any depth, in the order the mod reads them. A kind whose switch is on without a file is the mod's default (the game's own, except the world rules, which the mod writes as its own list). |

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
- **The alt biomes**: the alt biome file's (every field that says where an alt biome goes, a field left out
  being 0 as in the mod), the areas the world file's rules force alt biomes onto, Expand World Size's stretch
  of their distances, and the biome map Expand World Size sizes to the world. Locations and vegetation of an
  alt biome stand in its areas only, and what an alt biome keeps out stays out of them.
- **The save grid**: a world wider than 16 km lies on a bigger grid of chunk files; the editor reads and
  writes it as it lies, switch or no switch.
- **A new world** (the New world page): the seeds looked at, the search and the world made follow the
  folder's rules; the page says so above Create. A location whose contents the location file changes is built
  in the generated zones as the mod spawns it: objects added ("objects", with their chance, scale, place on the
  ground and data), its own objects swapped, removed or given data ("objectSwap", "objectData" and their
  "location..." forms), the location's own "data", "scaleMin"/"scaleMax", "offset"/"groundOffset",
  "randomCardinal" and "randomDamage" (its building pieces worn as the game wears them, "all" over their data too),
  and the ground of its zone levelled and painted round it ("levelArea", "levelRadius", "levelBorder", "paint",
  "paintRadius", "paintBorder"), written as the mod writes it. A blueprint location is built of its blueprint's
  pieces (made by chance, swapped and given data as the file says, the blueprints it holds in their places) on
  ground it levels unless the file says not to. Data comes from the data entries (`BepInEx/config/data/*.yaml` and
  `expand_world/expand_data*.yaml`), by name, as the "type, key, value" shorthand or written in place. The world file gets no settings of the mod's: the game
  writes them on the first save.

## What does not

- **Locations the editor cannot build ahead of the game** are placed where the mod places them, but their
  zones are left to the game by the New world page's generated zones, as dungeons are: a blueprint location
  whose blueprint has a terrain snapshot, "commands", "randomSeed", another "dungeon", and data the mod works out as it spawns an object (a choice of values, a range, a function,
  an inventory, a component's field) or a name it gives that is no prefab (a value group, a blueprint). With
  **Random locations** on, every location is. A location's random turn ("randomRotation", the game's own
  included) is drawn from whatever the game drew before it, so no tool can know it; New world draws one.
- **Vegetation the editor cannot write ahead of the game**: data the mod works out as it spawns an object, and a
  blueprint's part that is no prefab of the game, are left out of what Regrow nature and New world grow; the log
  says which rule. A blueprint's objects wake as the game's vegetation list counts them (and a building piece's
  timers); other objects' own draws are not known, so the draws after one can differ from the game's.
- `pregenerate`: not modelled. The map's location icons follow the location file's "iconAlways" and "iconPlaced"
  (an icon's size included), for icons the game's minimap has (the start, the traders); an icon named after a pin
  type, an item or a status effect is not drawn.
- **Status effects** of biomes and territories, and their **water colours**: the editor neither applies nor
  draws them. As in the mod, a biome's status effects and a territory's status effects and water colours still
  count towards whether its settings are in use; a biome's water colours do not.
- **The zone system's water**: the game's own vegetation and location checks use the sea at 30 m whatever
  Expand World Rivers puts the water at, and neither mod changes that, so the editor keeps it too.
- **The mods' quirks** are mirrored as they are (a few are listed in the architecture notes), so that
  what the editor predicts is what the mod does.

Followed: Expand World Size 1.45, Expand World Rivers 1.11, Expand World Data 1.79. A later version that
adds a setting or a field is read with its defaults until the editor follows it.

## Live mode

A live world (your game, a dedicated server) follows the mods the game itself runs: with the switch on, the
editor asks the game's WorldEditorBridge (1.54.0 or newer) which plugins it loaded and reads the mods' settings
and data files from the game's own `BepInEx/config` through it, with the world's settings from its file on the
game's disk. The files are copied under `live-bepinex` in the editor's data folder and read as a local folder
would be; the log says how many came. The YamlDotNet library the data files need cannot come that way: it is
taken from a local BepInEx folder (the Expand World folder in Settings first, then the folders the editor
knows). Blueprint locations' files are copied too, those the location file names.

With an older plugin, or when the bridge cannot be read, the local folders answer as for a world opened from
its files, and the log says so. When only the world's file cannot be served (the game has none yet), the
world's settings are those the game loaded, and the mods' files are still read through the bridge.
