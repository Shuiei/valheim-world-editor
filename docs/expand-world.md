# Expand World

The editor can generate, draw and predict a world the way JereKuusela's **Expand World** mods make it:
**Expand World Size** (a wider or stretched world, another altitude, the Ashlands and Deep North gaps),
**Expand World Rivers** (the water level, lakes, rivers and streams) and **Expand World Data** (the biome
file, the world file that lays the biomes out, territories, locations, vegetation and alt biomes).

Nothing of this is on unless you ask for it: with the switch off, every world is generated as the game
itself does, whatever it was made with.

## Turning it on

1. Open **Settings** and turn on **Generate worlds as the Expand World mods make them**.
2. Leave the **folder** empty to use the first BepInEx folder the editor knows that has the mods (the Valheim
   folder, the editor's own profile, the folders added above, r2modman's and Thunderstore Mod Manager's
   profiles), or point it at another BepInEx folder: a copy of a server's, say, for a server world opened from
   its files.
3. Open the world. The log says what was found and read.

The setting is taken when a world is opened or made.

## What is read from where

| What | Where it comes from |
|---|---|
| Expand World Size's settings (radius, edge, stretch, altitude, water depth, forest, offsets, height seed, the Ashlands' limits, the gaps, the save grid) | The world's own file keeps every setting that was changed when the world was made (the mod writes them on the first save); a world without any uses `BepInEx/config/expand_world_size.cfg`. |
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
  shape, colour, lava and height transform, the territories.
- **The biomes**: a biome the file adds is drawn on the map and on the New world page's seed map with the
  map colour the file gives it (white without one, as the game draws a biome it does not know), named as
  the file names it, and its ground drawn as the ground it borrows. A territory is drawn in its own map
  colour on both.
- **The predictions** (the New world page, Regrow nature, a new world's generated zones): the location
  kinds of the location file (clones, blueprints, groups, "away from" and "close to" rules, a location
  wider than a zone at its zone's middle, the start temple forced into the middle zone), the vegetation
  file's rules (several prefabs, a scale by axis, a centre of the rule's own, the world's global keys),
  what the alt biomes keep out, Expand World Size's locations multiplier, no zone spawner when the mod
  says so.
- **The save grid**: a world wider than 16 km lies on a bigger grid of chunk files; the editor reads and
  writes it as it lies, switch or no switch.
- **A new world** (the New world page): the seeds looked at, the search and the world made follow the
  folder's rules; the page says so above Create. The world file gets no settings of the mod's: the game
  writes them on the first save.

## What does not

- **Locations whose contents the mod changes** (swapped or added objects, data, commands, a random seed,
  blueprint locations) are placed where the mod places them, but their zones are left to the game by the
  New world page's generated zones, as dungeons are. With **Random locations** on, every location is.
- **Clear areas** of vegetation, the vegetation of **alt biomes**, `pregenerate` and the map icons: not
  modelled (Regrow nature does not model clear areas at all).
- **Status effects** of biomes and territories, and their **water colours**: the editor neither applies nor
  draws them. As in the mod, a biome's status effects and a territory's status effects and water colours still
  count towards whether its settings are in use; a biome's water colours do not.
- **The game's map shader** keeps the game's own water line.
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
