# Expand World compatibility: design

Status: design of 2026-10-18; PR 1 (`feature/expand-world-rules`, v1.49.0) implements §4.2, §4.3 and the
`ews_` keys and settings files of §4.1. The rest is still to do, in the order of §5.

Decisions taken: the editor reads Expand World's own files directly (an exception to the rule that
the editor ships nothing for a real mod, see `docs/modding.md`); and **vanilla stays the default**: the
mods' settings apply only once the player turns the **Expand World** switch on in Settings
(`AppSettings.ExpandWorld`); off, every world is generated as the game itself does, whatever it was
made with (a world with `ews_` keys is noted in the log). Every later step keys off the same switch.
This document says what the mod changes, what the editor assumed, what the editor does, and in which
order it gets built.

Sources read for this design: the mod's repositories at these versions, all released into the
public domain (Unlicense): Expand World Size 1.45.0, Expand World Data 1.79.0, Expand World
Rivers 1.11.0. The pack repository (`valheim-expand_world`) is only a README. Expand World Events,
Spawns, Prefabs, Factions and Drops change nothing the editor computes and are out of scope.

## 1. What "compatible" means here

A world played with Expand World (any of the three mods, in any combination) must:

1. **Open and save without harm.** Any radius, any save grid, any water level, any biome id. The
   `ews_` settings the Size mod keeps in the world file survive a save (they already do: the
   editor copies the `.fwl2` file through untouched).
2. **Look like the game.** The ground of zones the game has not made yet, the map overview, the
   water plane, the biome colours and names, rivers and streams: as the modded game makes them, to
   the bit where the editor's vanilla generator is to the bit today.
3. **Predict like the game.** Regrow, the dungeon and location layout (the New world page's marks,
   the dungeon entrance feature), the start temple: by the mod's rules, with the mod's random draws.
4. **Create worlds for it.** The New world page previews and creates a world with the Expand World
   settings of the chosen BepInEx folder, and the zones it pre-generates follow those rules.

Out of scope, now: dungeon generators and rooms (`expand_dungeons.yaml`, `expand_rooms.yaml`),
blueprint locations and blueprint vegetation drawn as their pieces (they get a marker and their
clear area only), the `data`, `objects`, `objectSwap`, `commands` and `paint` fields of locations
and vegetation, environments, clutter, status effects, water colours, Better Continents.

## 2. What the mod changes

### 2.1 Expand World Size (`expand_world_size`)

Config file `BepInEx/config/expand_world_size.cfg`, sections `1. General` and `2. Poles`. Floats
are bound as strings (a plain number, invariant culture). Settings and defaults:

| Key | Default | What it does |
|---|---|---|
| World radius | 10000 | The land's radius, metres. |
| World edge size | 500 | Added to the radius for the total (where the ground drops to -400 m). |
| Save system size | 0 | Save grid in units of 64 zones; 0 = computed from the radius: `clamp(ceil(total / 32 / 64) * 64, 512, 2048)` zones. |
| Minimap size | 1 | Multiplies the game's minimap texture size. |
| Minimap pixel size | 0 | Multiplies the minimap's metres per pixel; 0 = `total / 10500 / Minimap size`. |
| Stretch world | 1 | Every generator coordinate is divided by it (`x / stretch`), so the same world covers a bigger area. |
| Stretch biomes | 1 | Divides the coordinates of the land biomes' noise fields only. |
| Forest multiplier | 1 | The forest factor is divided by it. |
| Altitude multiplier | 1 | Base height: `water + (h - water) * multiplier + delta` (in generator units, water = 30 / 200). |
| Altitude delta | 0 | Metres, see above. |
| Water depth multiplier | 1 | Biome height under the water level: `(h - water) * depth + water`. |
| Locations | 1 | Multiplies every location rule's quantity (not the start temple; at least 1; 0 = none). |
| Offset X, Offset Y | none | Replace the seed's continent offsets (`m_offset0`, `m_offset1`) in the base height. |
| Seed | "" | Replaces the world's seed (saved into the world file itself). |
| Height variation seed | none | Replaces `m_offset3` in every biome's height noise. |
| Restrict Ashlands position | true | Off: the Ashlands' width and length limits are infinite. |
| Ashlands width restriction | 7500 | Replaces the game's 7500 in the Ashlands height. |
| Ashlands length restriction | 1000 | Replaces the game's 1000. |
| Ashlands gap, Deep North gap | true | Off: the ocean gap before the pole is not cut (`CreateAshlandsGap` returns 1). |
| Fix water color, Remove Ashlands water | true, false | Rendering only. |

**Per-world settings.** Every non-default setting is saved into the world's `.fwl2`, in the
starting global keys list, as `ews_<key> <value>`: the key is the setting's name lower-cased with
spaces as underscores (`ews_world_radius 20000`, `ews_altitude_delta 10`), the value the setting's
serialized text. The host also writes `ews_gridwidth <zones>` (the chunk grid the files on disk
use). On load the mod takes these keys out of the list (vanilla never sees them) and sets its
config from them; a world without any `ews_` key is older than the feature and uses the config as
it is. So for the editor: **the world file is the truth for a Size setting when it has `ews_` keys;
otherwise the cfg file is.**

**The save grid.** Vanilla keeps 512 x 512 zones (±256 zones, 16384 m); a chunk id is one byte per
axis (`x / 8`, `z / 8`), so 2048 zones is the most that fits. With the mod the grid is
`GetWidth()` above; `ZDOMan` is built with it, and the sector-to-index, chunk-key and
chunk-size maths use `width` and `width / 2` where vanilla has 512 and 256. Chunk files are only
containers: when the saved grid (`ews_gridwidth`) differs from the current one, the mod re-saves
everything under the new keys and the old files are removed on the next load.

**World generation patches** (Harmony, only applied when the setting differs from vanilla):

- `GetBaseHeight`: literals 10000 → `radius / stretch` (twice), 10500 → `total / stretch` (twice),
  10490 → `(total - 10) / stretch`; the offsets replaced when set; then the postfix
  `water + (h - water) * altitudeMultiplier + delta / 200`.
- `GetBiome`: a prefix divides `x` and `z` by the stretch (priority higher than normal, so before
  Expand World Data's prefix); a transpiler divides the four land-biome noise coordinates by the
  biome stretch and replaces the distance bands with fractions of `radius / stretch`: 2000 → 0.2,
  6000 → 0.6, 10000 → 1, 3000 → 0.3, 8000 → 0.8, 600 → 0.06, 6000 → 0.6, 5000 → 0.5 (same
  float/double types as the game's literals).
- `IsDeepnorth`: 4000 → 0.4, 12000 → 1.2 of the stretched radius; `CreateAshlandsGap` 400 → 0.04;
  `CreateDeepNorthGap` 4000 → 0.4, 12000 → 1.2, 400 → 0.04. The static fields
  `ashlandsMinDistance = 1.2 * stretched radius` and `ashlandsYOffset = -0.4 * stretched radius`.
- `GetAshlandsHeight`: 10150 → `(total + 150) / stretch`; the two restrictions.
- `GetBiomeHeight`: a prefix divides the coordinates by the stretch; 10500 → `total / stretch`;
  the water-depth postfix. `GetForestFactor`: `/ forestMultiplier`. Seven biome height methods:
  `m_offset3` → the height seed.
- `FindLakes`: the scan's ±10000 → ±radius (loop coordinates, not stretched; stretched before
  `GetBaseHeight`); `IsRiverAllowed` stretches the coordinates; `FindStreamStartPoint` ±10000 →
  ±radius; `AddRivers` multiplies incoming coordinates by the stretch. The river and stream
  **seeds and widths are not changed** by Size (Rivers does that).
- `maxMarshDistance = 6000 (or 8000 for old worlds) * radius / 10000 / stretch`.
- Locations: `ZoneSystem.GetRandomZone` and `GenerateLocationsTimeSliced` 10000 → radius; every
  rule's `m_minDistance`/`m_maxDistance` scaled by `radius / 10000` **only when Expand World Data
  is absent** (Data scales them itself, from fractions); quantities by the Locations multiplier.
- The biome point map (`AltBiomeWorldData`, the 2048 x 2048 grid of 12 m points the game draws
  location spots from): size `ceil(2048 * total / 10500 / stretch)`, pixel `12 * stretch`,
  centre `size / 2`, half pixel `6 * stretch`; the generation's `110250000` (10500²) → `total²`.
- `Minimap`: texture size `× Minimap size`, pixel size as in the table, a parallel generation.

### 2.2 Expand World Data (`expand_world_data`)

Config file `BepInEx/config/expand_world_data.cfg`. Data files in `BepInEx/config/expand_world/`,
read at any depth under it; several files per kind (`expand_world*.yaml`), loaded in **reverse
alphabetical order** and concatenated; each file is a YAML list of entries with the field names of
the mod's docs; unknown fields are ignored. When a kind's file exists it **replaces the game's
whole list** (missing entries are written back into the file by the mod, so the file is complete);
no file, or the kind's `Data` switch off, means vanilla. Each kind has a switch in the cfg
(`Biome data`, `World data`, `Territory data`, `Alt biome data`, `Location data`, `Vegetation
data`, all `false` by default: **a switch off means the file is ignored even if present**). The
server sends the synced kinds to clients; locations and vegetation are server only.

Settings that matter to the editor:

| Key (section) | Default | Effect |
|---|---|---|
| Legacy generation (1. General) | false | The older biome rule evaluation (differences below). |
| Zone spawners | true | Off: no `_ZoneCtrl` spawner (the editor's New world writes it: must follow). |
| Random locations | false | Locations' random draws not from their coordinates (the editor cannot predict such worlds; say so). |
| Distance wiggle length / width (2. Features) | 500 / 0.01 | Sector wiggle defaults. |
| Wiggle frequency / width | 20 / 100 | Distance wiggle defaults; frequency ≠ 20 also replaces `WorldAngle` with `sin(atan2(x, z) * frequency)`. |
| Scale location altitude requirement | false | Location altitude limits scaled by the biome's altitude transform. |
| Restrict Ashlands position, Ashlands width/length restriction, Ashlands gap, Deep North gap (4. Poles) | as Size | The same patches as Size; the docs say to set them in one mod only. |

**`expand_biomes.yaml`** (`Biome data`): one entry per biome, vanilla ones by their name
(`Meadows`, `Swamp`, `Mountain`, `BlackForest`, `Plains`, `AshLands`, `DeepNorth`, `Ocean`,
`Mistlands`, case-insensitive) and new ones in file order. A new biome gets the next id: Mistlands
is 0x200, the first new one 0x400, then doubling to 0x80000000, then 0x80 (the unused vanilla
bit); more than that throws. Fields the editor uses: `biome`, `name`, `terrain` (the base biome
whose height algorithm and terrain style it uses; itself when it is a vanilla name), `nature`,
`altitudeMultiplier`, `altitudeDelta`, `waterDepthMultiplier`, `minimumAltitude` (-1000),
`maximumAltitude` (10000), `excessFactor` (0.5; its sign is kept apart), `forestMultiplier`,
`paint` (the default terrain paint), `colorTerrain` (r,g,b,a), `colorMap`, `mapColorMultiplier`,
`lava`, `lavaAmount`, `lavaStretch`. The biome's height transform, in metres relative to the water
level: `h *= mult; h += delta; if h < 0: h *= depth; over max: max + sign * (h - max)^factor;
under min: min - sign * (min - h)^factor`.

**`expand_world.yaml`** (`World data`): the biome layout, an ordered list of rules; the first
rule a point passes wins; none: Ocean. The mod's default list (also what the file is created
with) is: ashlands (centerY 0.4, minDistance 1.2, maxDistance 1.6, boiling), ocean (maxAltitude
-26), deepnorth (centerY -0.4, 1.2..1.6), mountain (minAltitude 50), swamp (no distance wiggle,
0.2..0.6, altitude -20..20, amount 0.4), mistlands (minDistance 0.6, amount 0.6), plains
(0.3..0.8, amount 0.6), blackforest (0.06..0.6, amount 0.6), blackforest (minDistance 0.5),
meadows. **This default list is not the vanilla algorithm**: with `World data` on and the default
file, the world already differs from vanilla (no `WorldAngle` wave on the Ashlands/Deep North
edges, the mountains by altitude instead of continent height, etc.). The editor must evaluate
the rules whenever the switch is on, never assume "default file = vanilla".

A rule's fields: `biome`, `territory`, `altBiome`, `minAltitude`/`maxAltitude` (metres from the
water level; **exclusive** bounds in the current evaluation, inclusive in legacy),
`minDistance`/`maxDistance` (fractions of the radius: `× radius`), `minSector`/`maxSector`
(fractions of the circle from south, clockwise), `centerX`/`centerY` (fractions of the radius),
`sizeX`/`sizeY`/`rotation` (a rectangle, not in legacy), `amount`, `stretch`, `seed` (a number,
or a biome name whose seed to use; default the rule's own biome's terrain), `wiggleDistance`
(+ Length/Width), `wiggleSector` (+ Length/Width), `wiggleRectangle` (+ Length/Width),
`boiling`. Sectors are only checked when some rule has a sector other than 0..1.

Evaluation of one point (`BiomeCalculator.GetEntry`), in the game's order, for coordinates that
Size has already divided by the stretch (`wx`, `wy`):

```
sx = wx * stretch, sy = wy * stretch              (back to world metres)
magnitude = length(sx, sy); past the total radius: no rule (Ocean)
altitude = GetBaseHeight(wx, wy) * 200 - waterLevel   (Size's base-height patches included)
baseAngle = (atan2(wx, wy) + π) / 2π               (only when sectors are checked)
bx = wx / biomeStretch, by = wy / biomeStretch
for each rule:
  altitude must be > min and < max
  min = minDistance (+ sin(atan2(wx, wy) * wiggleDistanceLength) * wiggleDistanceWidth when > 0; -0.1 when 0)
  mag = magnitude, or length(sx - centerX, sy - centerY) when a centre is set
  mag > min and (maxDistance >= radius or mag < maxDistance)
  rectangle: rotate (sx - cx, sy - cy) by -rotation; |lx| < halfX + sin(ly / len) * width, same for y
  sector: angle = baseAngle + sin(magnitude / wiggleSectorLength) * wiggleSectorWidth, wrapped to 0..1,
          in [minSector, maxSector) (wrapping when min > max)
  amount < 1: PerlinNoise((seed + bx / stretch) * 0.001, (seed + by / stretch) * 0.001) must be > 1 - amount
  → this rule
```

The seeds: `m_offset0` for Swamp, `m_offset1` Plains, `m_offset2` BlackForest, `m_offset3`
Meadows, `m_offset4` Mistlands, and for AshLands, DeepNorth, Mountain and Ocean **four extra
`Random.Range(-10000, 10000)` draws made in a `Pregenerate` prefix** (after the generator's own
seven draws; to be confirmed against a dump, see §6). A custom biome uses its `terrain` biome's
seed. `boiling` (the Ashlands' hot water and `IsAshlands`): `boiling * (distance past the rule's
inner edge) / 300`; with world data, `IsAshlands(x, z)` becomes "the rule here boils" and the
ocean gradient comes from it too.

The **legacy** evaluation differs in: inclusive altitude bounds, `mag <= max`, no rectangles, and
the amount test `< 1 - amount` instead of `<=`.

**`expand_territories.yaml`** (`Territory data`): named areas chosen by the same rule list (a
rule's `territory` field), each with its own altitude transform (same formula as a biome's,
applied after the biome's), `colorTerrain`, `colorMap`, `forestMultiplier`, `noBuild`.

**`expand_altbiomes.yaml`** (`Alt biome data`): replaces the game's alt biome list (Dark
Meadows and such): `name`, `biome`, `chance`, `minDistanceFromCenter` (× stretch), amounts,
neighbours, edge sizes, heights, `aboveWorldX`… (× stretch), `blockVegetationNames`,
`blockLocationNames`, `terrainTextureOverride`. A world rule's `altBiome` forces those alt biomes
onto every biome sector whose centre passes the rule (deterministic, unlike the game's random
pick), and such sectors get no random ones.

**`expand_locations.yaml`** (`Location data`, server side): replaces `ZoneSystem.m_locations`
whole, in file order. Fields map one to one onto the game's `ZoneLocation` (and the editor's
`SeedLocations.Rule`), with these conversions: `biome` a comma list of names (empty = every
biome the mod knows), `biomeArea` a comma list of `edge`/`median` (empty = both),
`minDistance`/`maxDistance` fractions of the radius, `quantity`, `prioritized`, `centerFirst`,
`unique`, `group`/`minDistanceFromSimilar`, `groupMax`/`maxDistanceFromSimilar`, `minAltitude`,
`maxAltitude` (10000 = the game's 1000 "no limit"), `minTerrainDelta`/`maxTerrainDelta`,
`inForest` + thresholds, `randomRotation`, `slopeRotation`, `snapToWater`, `minVegetation`,
`maxVegetation`, `surroundCheck*`, `altBiome`, `exteriorRadius`/`interiorRadius` (0 = the
prefab's own), `clearArea`, `enabled`. `prefab` may be `Name:Tag`, a clone of location `Name` (same
prefab, own rule and own random draws, since the draws are seeded by the rule's name), or a
`.blueprint` file name (a location the editor has no prefab for: predicted and marked, not drawn).
The mod's own grouping (`groups`, `awayFrom`, `closeTo`) is a second placement check the editor
must port for prediction. The start temple: when none was placed, the mod puts one at the world's
centre zone (vanilla would have none).

**`expand_vegetation.yaml`** (`Vegetation data`, server side): replaces `m_vegetation` whole.
Fields map onto the game's `ZoneVegetation` and the editor's Regrow rule; `prefab` may list several
names (one drawn per placement with `Random.Range(0, n)`: one more draw, in the game's order),
`minDistance`/`maxDistance` fractions of the radius (with `centerX`/`centerY`, a custom check
instead of the game's), `scaleMin`/`scaleMax` a number or `x,z,y` (non-uniform scaling draws
differently), `clearArea`/`clearRadius`, `requiredGlobalKey`/`forbiddenGlobalKey` (the world's
keys), `altBiome`.

Other unconditional patches of Data that change the game's draws even with no data file:
`AltBiomeWorldData.RandomBiomeFromBiomes` picks uniformly among the set bits (the editor's
`BiomeMap.RandomBiome` ports vanilla's walk); `BiomeHelpers.ToBiomeIndex` maps bit `b` to index
`b + 1`, except bit 7 → 10, 8 → 8, 9 → 9; a location whose biome has no point on the map is
skipped with a warning instead of failing; locations wider than a zone are placed at the zone's
centre; clear areas of neighbouring zones' large locations count. **So the editor must know
whether Data is installed at all**, not only whether it has data (§4.1).

### 2.3 Expand World Rivers (`expand_world_rivers`)

Config file `BepInEx/config/expand_world_rivers.cfg`:

| Key | Default | Replaces in the game |
|---|---|---|
| Water level | 30 | `ZoneSystem.m_waterLevel`, the water volumes' height; altitude = base height × 200 − water level everywhere the mods compute it. |
| Wave multiplier, Wave only height | 1, true | Rendering only. |
| Lake search interval | 128 | `FindLakes` 128 (twice). |
| Lake depth | -20 | `FindLakes` and `IsRiverAllowed` 0.05 → `(water + depth) / 200`. |
| Lake merge radius | 800 | `FindLakes` 800. |
| Lake max distance 1, 2 | 2000, 5000 | `PlaceRivers`. |
| River max altitude | 50 | `PlaceRivers` 0.4 → `(water + 50) / 200` (twice). |
| River check interval | 128 | `PlaceRivers` 128 (twice). |
| River min/max width | 60, 100 | `PlaceRivers` 60, 100, 60. |
| River curve width, wave length | 15, 20 | `PlaceRivers` 15, 20. |
| River seed | none | `m_riverSeed`. |
| Rivers, Streams | true | Off: no rivers / no streams at all. |
| Stream seed | none | `m_streamSeed`. |
| Stream max amount | 3000 | `PlaceStreams` 3000. |
| Stream search iterations | 100 | `PlaceStreams` 100 (twice). |
| Stream start min/max altitude | -4, 1 | `PlaceStreams` 26, 31 → `water + value`. |
| Stream end min/max altitude | 6, 14 | `PlaceStreams` 36, 44 → `water + value` (also the 26 and 44 of the later check). |
| Stream min/max length | 80, 200 | `PlaceStreams` 80, 200. |
| Stream min/max width | 20, 20 | `PlaceStreams` 20. |
| Stream curve width, wave length | 15, 20 | `PlaceStreams` 15, 20. |

Rivers also feeds the water level to Data (`WorldInfo.SetWaterLevel`) and Size, so one water level
rules all three. Note the game's own stream start window 26..31 is `30 − 4 .. 30 + 1`: these
literals in the editor (`WorldGenerator.Streams.cs:57`) are the water level in disguise.

### 2.4 Patch order facts the port depends on

- `GetBiome`: Size's stretch prefix runs first (higher priority), then Data's rule prefix (which
  replaces the vanilla body entirely when `World data` is on; Size's distance-band transpiler then
  no longer matters). Without Data, Size's transpiled vanilla body runs.
- `GetBiomeHeight`: Size's stretch prefix, Data's prefix swapping the biome for its `terrain`
  biome, the vanilla body (with Size's 10500 replaced), then two postfixes: Data's altitude
  transforms (biome then territory, relative to the water level, with the mask colour and lava
  amount) and Size's water depth. Their relative order is Harmony's registration order; both have
  normal priority. **To confirm with a dump** (§6): the result differs when a biome has a
  `minimumAltitude` clamp and the depth multiplier is not 1.
- `GetBaseHeight`: Size's transpiler (radius, offsets) then Size's altitude postfix. Data's
  rules call it through the patched method, so their altitude test sees Size's transform.
- Data's `Pregenerate` prefix draws four random numbers (§2.2); Size's `Pregenerate` prefix (higher
  priority, runs first) only clears the river caches.

Quirks of the mods found while porting (PR 1), each mirrored as the mod does it, to be confirmed by the
dumps of §6:

- **The Ashlands' sink.** Size replaces the 10150 of `GetAshlandsHeight` with `(total radius + 150) /
  stretch`, which is 10650 at the defaults, and applies that whenever the mod is installed (the
  condition compares with 10150). So with Size installed the Ashlands' terrain sinks to the sea floor
  past 10650 m, not 10150 m: `WorldRules.SizeInstalled`.
- **The height seed** replaces only the first use of the detail offset in each of the seven biome
  heights, the x coordinate's; z keeps the seed's. The pregeneration heights and the menu's keep both.
- **The stream width.** The game sets a stream's widest and narrowest width to 20 in two statements;
  Rivers replaces the first literal only, so "Stream max width" moves the widest and "Stream min
  width" does nothing.
- **The water level** of Rivers moves the generator's lake, river and stream thresholds and the
  water itself; the game's zone system keeps its literal 30 for the vegetation's and locations'
  altitude and snapping, the biome point map's "above the sea" and the map's mask, and neither mod
  patches those (`WorldGenerator.LiteralWaterLevel`).
- **Stretch.** The point is divided on the way into `GetBiome` and `GetBiomeHeight` (and the map's
  mask), so a call from inside one of them into another divides again (`GetBiome` with
  `waterAlwaysOcean` calls `GetHeight`): mirrored. The rivers' drawing asks `IsDeepnorth` with
  world metres, unstretched, as the game does.

## 3. What the editor assumes today

From the survey of `Core/WorldGen`, `Core/App`, `Core/Save` and `Desktop` (file:line as of main
v1.47.32):

| Assumption | Where |
|---|---|
| Radius 10000, edge 10500, 10490, 10150, 7500, 1000 (Ashlands), 12000/±4000 edges | `WorldGenerator.cs:126,131`, `WorldGenerator.Ground.cs:66-73`, `WorldGenerator.Terrain.cs:302-304`, `WorldGenerator.Biomes.cs:76` |
| Lakes scanned over ±10000 at 128 m, streams from ±10000, start window 26..31, river reach 2000/5000, widths 60..100 | `WorldGenerator.Lakes.cs:13-17`, `Streams.cs:55-57`, `Rivers.cs:23-35` |
| The land biomes' bands and thresholds, mountain at continent > 0.4, ocean ≤ 0.02 | `WorldGenerator.LandBiomes.cs`, `Biomes.cs:47` |
| `HeightScale = 200`; metres per unit | `WorldGenerator.cs:136` |
| Water level 30 | `TerrainService.cs:14` and literal copies: `SeedLocations.Placer.Ground.cs:31`, `SeedLocations.BiomeMap.cs:84`, `MapData.Colors.cs:31`, `Desktop/MapShader.cs:69,78,80,164` (29.5 and -29), `Desktop/ClaudeGuide.cs:115`, docs `measure.md`, `scripting.md`, `path.md` |
| The biome enum is the vanilla nine, with tables keyed by it | `Heightmap.cs`, `MapData.cs:25-34`, `MapData.Colors.cs:29-56`, `TerrainService.Overview.cs:72-83`, `SeedLocations.BiomeMap.cs:21-25,96-106`, `SeedPreview.cs:20-24` |
| The biome point map is 2048 points of 12 m, `(x - 1024) * 12 + 6` | `SeedLocations.BiomeMap.cs:16,44,50` |
| The map is 2048 × 12 m = 24576 m, water at 29.5 in the shader, zoom defaults from 10500 | `MapData.cs:15,20`, `Desktop/MapView.cs:181`, `MapView.RenderMap.cs:67-80`, `MapShader.cs:37,47`, `MapPage.Lifecycle.cs:48`, `MapPage.ZoneControls.cs:41` |
| World radius 10500 for the overview, zone stats and the seed map's clamps | `TerrainService.cs:19`, `TerrainService.Overview.cs`, `ZoneStats.cs:43-50`, `Desktop/SeedMap.View.cs`, `SeedPreview.cs:17`, `SeedLocations.Placer.Zones.cs:25` |
| The save grid is 512 zones (±256) | `Core/Save/ChunkMath.cs`, used by `WorldWriter.Plan*.cs` |
| Location and vegetation rules come from the vanilla bundles only | `Core/App/GameLocations*.cs`, `GameCatalogues.VegetationScan*.cs` |
| The generator is built from `(Seed, SeedName, WorldGenVersion, Menu)` only | `Core/WorldGen/World.cs`, `TerrainService.cs:63-69`, `SeedPreview.cs:140` |
| The world file is read up to the generator version; the starting global keys are not read | `Core/Save/WorldSave.Loading.cs:37-49` |
| Nothing under `BepInEx/config` is read but the bridge's cfg; no YAML reader | `Core/App/LocalGame.cs` |
| Live mode: the bridge never asks the game's generator or any mod's settings | `plugin/WorldEditorBridge/*.Routes.cs` |

What happens today with such a world: it opens (radius up to 16 km), the `ews_` keys survive a
save, but every generated thing is vanilla: wrong ground in unmade zones, wrong overview, water at
30, vanilla biome colours, wrong predictions. With a larger save grid, objects past ±16 km are
missing ("Position is outside the world" on write) and chunks are written under the wrong keys.

## 4. The design

### 4.1 Finding the mod's settings: `ExpandWorld` (new, `Core/App/ExpandWorld*.cs`)

One reader builds an immutable `WorldRules` for a world, from these sources, in this order:

1. **The world file.** `WorldSave.LoadMetadata` reads on past the generator version: the
   `needsDB` flag and the starting global keys (as `World.LoadWorld` does), keeps the keys on the
   save, and `ExpandWorld.FromKeys` takes the `ews_` ones (Size settings and `ews_gridwidth`). A
   world with any `ews_` key is "made with Expand World Size": its Size settings come from the
   keys only (a key absent = the setting's default, as the mod does), never from the cfg.
2. **The BepInEx folder the world is played with.** The folders the editor already knows
   (`LocalGame.BepInExFolders`: the Valheim install, the editor's own profile, Settings → BepInEx
   folders, r2modman and Thunderstore Mod Manager profiles). The first folder holding any of
   `config/expand_world_size.cfg`, `config/expand_world_data.cfg`, `config/expand_world_rivers.cfg`
   or `config/expand_world/` is the world's. A new Settings entry, **Expand World folder**, lets
   the player point at another BepInEx folder (a copy of a server's, for a server world opened from
   its files); per world, kept with the world's other remembered things (`place.json`'s
   neighbour), since the same player opens local and server worlds.
3. **Live mode.** The bridge serves the files (§4.8); a bridge too old for that: the local folders.

Installed or not, per mod: a mod counts as installed when its plugin folder is under the folder's
`plugins/` (a DLL named `ExpandWorldSize.dll`, `ExpandWorldData.dll`, `ExpandWorldRivers.dll` at any
depth) or its cfg exists; in live mode, when the bridge lists its GUID among the loaded plugins.
`WorldRules` records the three flags: Data installed changes draws even without data (§2.2).

The reader parses: the three cfg files (BepInEx cfg: `Key = value` lines under `[Section]`,
comments `#`/`##`; a small parser, as `LocalGame.ParseText` already is for the bridge's); the
YAML files of each kind whose switch is on, every `expand_<kind>*.yaml` under `config/expand_world`
at any depth, in reverse alphabetical order of the full path, concatenated. YAML is read with
**YamlDotNet** (MIT; a NuGet dependency added to Core, named in `NOTICE.md`): a list of mappings
with scalar and nested-list values; unknown keys ignored; a file that fails to parse is logged and
skipped (the mod does the same), never fatal. Size limit 4 MB a file.

Logging: one line per world naming what was found ("Expand World: Size settings from the world
file (radius 20000 m, grid 1024 zones); Data 1.79 files from /…/BepInEx/config/expand_world:
biomes (12), world (14 rules), locations (231), vegetation (402); Rivers: water level 25 m") and
one warning per kind missing where the world needs it ("the world was made with Expand World
Size but no Expand World folder was found: the ground of unmade zones may differ").

Stamps: `WorldRules` carries a stamp (every file's path, size, mtime, plus the `ews_` keys) that
the caches below use.

### 4.2 `WorldRules`: the settings object

`Core/WorldGen/WorldRules.cs`, a sealed record with every value in §2 that the generator, the
map, the layout and the save need, with `WorldRules.Vanilla` as the default:

- Size: `Radius`, `EdgeSize`, `TotalRadius`, `Stretch`, `BiomeStretch`, `AltitudeMultiplier`,
  `AltitudeDelta`, `WaterDepth`, `ForestMultiplier`, `LocationsMultiplier`, `OffsetX?`,
  `OffsetY?`, `HeightSeed?`, `AshlandsWidth`, `AshlandsLength`, `RestrictAshlands`, `AshlandsGap`,
  `DeepNorthGap`, `GridWidth` (512..2048), `MinimapSize`, `MinimapPixel`.
- Rivers: `WaterLevel` and every lake, river and stream value; `Rivers`, `Streams` on/off.
- Data: `DataInstalled`, `LegacyGeneration`, `ZoneSpawners`, `RandomLocations`, the wiggle
  defaults, `ScaleLocationAltitude`; `Biomes` (the biome table, §4.4), `WorldEntries` (the rule
  list, pre-converted to metres as the mod's `WorldEntry` is, or null for vanilla),
  `Territories`, `AltBiomes`, `Locations` (a `SeedLocations.RuleSet` or null), `Vegetation` (a
  Regrow rule list or null).

`ValheimGen.World` gets a `Rules` field (default `WorldRules.Vanilla`), and `WorldGenerator`'s
`Identity` includes the rules' stamp, so two generators with different rules are never taken for
one (`TerrainService`, `MapData`, Regrow and the seed preview key their caches by it).

### 4.3 The generator: constants become rules

`Core/WorldGen/WorldGenerator*.cs` keeps the bit-exact contract (`docs/architecture.md`): every
replaced literal is computed with the **same expression and the same float/double type** the
mod's transpiler puts in its place, and `WorldGeneratorReferenceTests` must still pass unchanged
with `WorldRules.Vanilla` (every default makes the expressions collapse to the old literal: `0.2f
* 10000f / 1f` is exactly `2000f`). Where a mod patch is conditional on a non-default setting, the
editor's code path is the same (the value is the literal anyway), except the Data world rules and
the two `Pregenerate` draws, which are explicit branches on `Rules.WorldEntries != null` and
`Rules.DataInstalled`.

Changes, by file:

- `WorldGenerator.cs`: `WorldSize`/`EdgeOfWorld` become instance fields from the rules
  (`public float WorldSize` stays for callers); `GetForestFactor` divides by the forest
  multiplier (instance now); the seven draws, then (Data installed) the four extra draws kept as
  `_offsetAsh`, `_offsetNorth`, `_offsetMountain`, `_offsetOcean`; the river and stream seeds
  replaced when set; a `Stretched(x, z)` helper for the division.
- `Ground.cs` (`GetBaseHeight`): the two 10000, the 10500s and 10490 from the rules (stretched);
  the offsets replaced; the altitude postfix at the end.
- `Biomes.cs` (`GetBiome`): stretch first; with world rules: the `BiomeCalculator` port
  (`WorldGenerator.Rules.cs`, both evaluations, legacy by the flag), else the vanilla chain with
  Size's fractions; `IsAshlands`/`IsDeepnorth` with the stretched radius and, with world rules,
  the boiling test; `WorldAngle` with the wiggle frequency.
- `LandBiomes.cs`: biome stretch on the four fields, the bands as fractions.
- `Heights.cs`/`Terrain.cs` (`GetBiomeHeight`): stretch; the biome's `terrain` biome chosen for the
  algorithm; 10500, 10150, 7500, 1000 and the gaps from the rules; then the altitude transforms in
  the order §2.4 settles; the forest multiplier of the biome/territory where the mod applies it.
- `Lakes.cs`, `Rivers.cs`, `Streams.cs`, `RiverEnds.cs`: every value from the rules, altitudes
  relative to the water level, rivers and streams off when disabled, the stretch where Size puts
  it (loop coordinates unstretched, base-height lookups stretched).
- `HeightScale` stays 200.

`TerrainService.WaterLevel` and `MapRadius` become instance properties from the rules; every user
listed in §3 reads them from the service (or the rules) instead of the constant; the GLSL 29.5
becomes a uniform. `BaseTerrain`, `TerrainModifiers` and the zone size do not change (64 m zones
are the game's).

### 4.4 Biomes: a table instead of an enum

`Heightmap.Biome` stays the enum it is (the game's own numbers), but its values are no longer
assumed to be the nine: custom biomes are extra flag values (`(Biome)0x400`…, `0x80`) carried as
the enum type. A new `BiomeTable` (in `WorldRules.Biomes`) answers, for any biome value:
`Name` (display), `Id` (the yaml identifier), `Terrain` (base biome for the height algorithm and
the terrain style), `Nature`, `MapColor`, `TerrainColor` (the mask colour: the mod's
`colorTerrain`, else the game's), the altitude transform, `Lava`, the forest multiplier, and
`Index` (the biome point map index, by the mod's mapping when Data is installed, else the game's).
`BiomeTable.Vanilla` holds the nine with the editor's current colours.

Every table keyed by the enum goes through it: `MapData.BiomeColors`, `MapData.Colors.MaskColor`
(the switch becomes "the terrain biome's case"), `TerrainService.Overview.BiomeColor`,
`SeedPreview.Biomes` (the facts list: every biome in the table, in table order),
`SeedLocations.BiomeMap.ByIndex`/`Index` (sized by the table; the map's `Size`, `MapToWorld`,
`WorldToMap` from the rules' point map size and pixel), `BiomeMap.RandomBiome` (the uniform pick
when Data is installed, the walk otherwise), the 3D ground's per-biome texture choice (by
`Terrain`), the inspector's and the MCP tools' biome names, the New world page's legend. The
parsing of a biome name (rules, locations, vegetation) goes through `BiomeTable.Parse` (comma
lists, case-insensitive, empty = all the table's biomes).

### 4.5 The save grid

`Core/Save/ChunkMath` takes the grid width: `SectorOf` unchanged; `BaseChunk`, `InWorld` and
`Find` shift by `width / 2` and bound by `width` (the chunk id formula `x / 8 + (z / 8) << 8` is the
same up to 2048); `SizeFilters` unchanged. The width comes from `ews_gridwidth` when the world
file has it, else from the Size settings as `GetWidth()` computes it when Size is installed, else
512. `WorldWriter` plans and writes with the same width; `WorldSave.LoadChunks` reads with it. The
overflow chunk (sector 0, objects past the grid) is read and written as the game does. A world
whose files were saved under another width than the settings now say (the mod converts on its
next load): the editor reads with the saved width and writes with it too, and says so in the log.
No conversion in the editor.

### 4.6 Locations and vegetation from the files

`ExpandWorld.Locations(yaml, vanilla)` makes a `SeedLocations.RuleSet` from
`expand_locations*.yaml`: every entry in file order, converted as §2.2 says, the prefab's radii
and `name` taken from the vanilla rule of the base prefab (before the `:`), clones with their own
name, blueprint entries kept with the file's `exteriorRadius` (or 2 m) and a `Blueprint` flag,
`Enable` from `enabled`, distances × radius, the quantities × the Locations multiplier (not the
start temple, at least 1). The layout (`SeedLocations.Placer*`) gets: the world radius for
`GetRandomZone` and `centerFirst`, the mod's groups/`awayFrom`/`closeTo` checks, the "wider than a
zone → zone centre" rule, the neighbours' clear areas, the forced start temple, `altBiome` by the
mod's alt biome list and the forced sectors, and `RandomLocations` (when on: no prediction; the
New world page and the dungeon entrance feature say why). `SeedPreview.Landmarks`/`Dungeons`
match by base prefab name, so clones show as their kind.

`ExpandWorld.Vegetation(yaml, vanilla)` makes Regrow's rule list likewise: several prefabs = one
more draw per placement, `scaleMin`/`scaleMax` as the mod draws them, the centre and distance
checks, `clearArea`/`clearRadius` as a clear area, the world's global keys for the required and
forbidden ones, `altBiome`. `WorldCreator.GrowVegetation` uses the same rules.

Both are built on world open from the files (no bundle read), cached in the data folder as
`expand-locations-<stamp>.json` next to `game-locations.json` only if building proves slow (it
should not: it is a conversion, not a bundle scan).

### 4.7 The map, the seed page, the New world page

- `MapData`: the grid stays 2048 points; the pixel is `12 * totalRadius / 10500` (the map covers
  the world; the overview's and zone stats' radius is `TotalRadius`); `MapView.MapMeters` follows,
  the shader's water level and the `_quant`/normal widths scale with the pixel.
- `MapPage`: zoom defaults from the radius; the zone controls' placeholder from the grid.
- `SeedPreview`: `Radius` from the rules; the preview and search read the chosen BepInEx folder's
  rules (the New world page shows "World generation: Expand World (…)" above the map, with the
  folder, and a link to Settings); the facts list every biome of the table.
- `WorldCreator`: the generated centre zones use the rules (ground, vegetation, sites, `_ZoneCtrl`
  by `ZoneSpawners`); it writes **no** `ews_` keys (the Size mod writes them on the first save,
  and a world created for a folder without Size needs none).
- The 3D view: the water plane at the rules' water level; custom biomes' ground textured as their
  `terrain` biome, tinted by `colorTerrain` as the game's mask does.

### 4.8 Live mode: the bridge serves the files

Two routes on the bridge, both generic (any plugin's files or settings, read only, so they are not
Expand-World-shaped even though Expand World is their first user):

- `GET /plugins`: the loaded plugins' GUID, name and version (from the chainloader): the editor's
  "installed" flags, and in time the mods list on the Mods tab.
- `GET /config?path=<relative>`: a file or a folder under `BepInEx/config` only (no `..`, no
  links; a folder lists names, sizes and mtimes; a file returns its bytes, 4 MB cap). The editor
  fetches the three cfg files and `expand_world/` (recursively) through it and reads them as it
  reads local ones. The `ews_` keys in live mode: the bridge's `/status` reports the world's
  starting global keys **as read from the world file on disk** (the mod removes them from the
  loaded list), when the host has one; a client-only game has no file and the editor falls back
  to the cfg values the bridge serves.

Bridge version bump (minor); an older bridge: the local folders, with the usual "needs bridge x"
note in the log. The bridge API (`WorldEditorBridge.Api`) does not change.

### 4.9 Settings, docs and messages

- Settings: **Expand World folder** (per world, optional) under BepInEx folders.
- The Open page's world list shows an "Expand World" tag on worlds with `ews_` keys; the map's
  info panel shows the radius and water level when not vanilla.
- `docs/expand-world.md` (new user page: what is read from where, what is predicted, what is not),
  `docs/architecture.md` (a section on `WorldRules` and the rule that defaults stay bit-exact),
  `docs/new-world.md`, `docs/live-mode.md`, `docs/map.md` (radius), `docs/measure.md`,
  `docs/scripting.md`, `docs/path.md` (water level no longer always 30), `NOTICE.md` (YamlDotNet),
  `README.md` (a line in the mods section), `CHANGELOG.md` per PR.

## 5. Build order

Each PR is a minor bump, stacked on the previous one (`gh stack`), reviewed and merged in order.
Sizes are rough and include tests and docs.

| PR | Branch | Content | Size |
|---|---|---|---|
| 1 | `feature/expand-world-rules` | Done (v1.49.0): `WorldRules` + `World.Rules`; every Size and Rivers literal of the generator from the rules with vanilla defaults; `WaterLevel`/`MapRadius` instance (the map shader keeps the game's 29.5: it is the game's own map); `LoadMetadata` reads the starting global keys; `ExpandWorld` reader for the `ews_` keys and the Size and Rivers cfg files; the folder lookup and the log lines; the Settings switch. `WorldCheck look` and `verify` identical before and after. | large |
| 2 | `feature/expand-world-grid` | Done (v1.50.0): `ChunkMath` takes the grid width (512 to 2048, the chunk id formula unchanged); `WorldSave.GridWidth` from `ews_gridwidth`, whatever the switch (the files' layout is a fact); the writer, the uplift check and the WorldCheck tools pass it; a mismatch with the settings' width is noted in the log (the game converts). Tests on the chunk maths at 1024 and 2048 and on the loader. | medium |
| 3 | `feature/expand-world-biomes` | YamlDotNet; `BiomeTable`; `expand_biomes`, `expand_world`, `expand_territories`, `expand_altbiomes` read; the rule evaluation (both modes), the altitude transforms, the extra draws, boiling; every biome table in the editor through `BiomeTable`; the biome point map sized by the rules. | large |
| 4 | `feature/expand-world-places` | `expand_locations` and `expand_vegetation` to the layout and Regrow; the mod's placement checks; clones, blueprints, the forced start; Data-installed draw changes. | large |
| 5 | `feature/expand-world-map` | Map pixel, overview, zone stats, seed map clamps, zoom defaults, the New world page with the folder's rules, `WorldCreator` with the rules, Settings entry, Open page tag, user docs. | medium |
| 6 | `feature/expand-world-live` | Bridge `/plugins`, `/config`, `/status` keys; the editor's live source. | medium |
| 7 | `feature/expand-world-verify` | Reference fixtures from dumps of a modded game (§6) and the reference tests that compare the editor's modded generator with them; `WorldCheck verify` with a rules file. | medium |

PRs 1 and 2 are the data-safety ones. After PR 3 a modded world looks right; after 4 it predicts
right. PR 7 can start any time after PR 3 and should land before the release that announces the
feature. Release once, after 7.

## 6. Verification

The vanilla generator is proven by `tests/fixtures/worldgen-reference.txt`, recorded in the game
by the TerrainDump plugin (`_src/TerrainDump`: Unity's random numbers, Perlin noise, 3000 heights
and biomes, whole zones, the water's count, for seed `5DCcdIcuYJ`), and `WorldCheck verify`. The
same for Expand World:

1. A local test server (never the live one) with Size + Data + Rivers and a test configuration
   that exercises every branch: radius 20000, edge 500, stretch 1.5, biome stretch 0.5, altitude
   multiplier 1.2, delta 5, water depth 1.5, forest 2, offsets, height seed; water level 25, lake
   depth -15, stream window moved; `World data` on with a custom biome (terrain Mountain,
   altitude transform with clamps), a territory, a sector rule, a rectangle rule, an amount rule
   with a numeric seed, boiling 0.5; `Legacy generation` on in a second run. TerrainDump gets a
   `-terraindump-rules` switch that also records the settings it ran with, and extra lines: the
   four extra draws, `IsAshlands` at sample points, the biome point map size and a few indices.
2. Fixtures `tests/fixtures/worldgen-expand-*.txt` with the matching `expand_world` folder under
   `tests/fixtures/expand_world-*/` (small, hand-written files), and a reference test per fixture.
3. Unit tests: cfg parsing, YAML reading (order, unknown keys, bad files), the rule evaluation
   against hand-computed points, `BiomeTable` ids and indices, `ChunkMath` at 2048, location and
   vegetation conversion (every field), the forced start, the clone naming, `WorldSave` keys read
   and written back unchanged, the Settings folder, the bridge routes (path escapes refused).
4. The open questions settled by the dumps: the two `GetBiomeHeight` postfixes' order; where the
   four extra draws fall; whether `maxMarshDistance` is read before or after the stretch.

## 7. Risks and notes

- **The mod moves.** Expand World changes often (Data is at 1.79). The reader must be tolerant
  (unknown keys ignored, missing files = vanilla) and the docs say which versions were followed.
  A rule or field added later is a small PR.
- **Bit-exactness** holds for what the dumps cover; the mod's own arithmetic (its `DUtils.Length`,
  `Mathf.PerlinNoise` through Unity) is matched the way the vanilla port is, with the same care
  over float and double.
- **Alt biomes** stay partly random in the game (the random sectors); the forced ones are exact.
  The docs' existing caveat holds.
- **Minimap** changes (texture size, pixel size) only matter to the editor if it ever reads a
  player's explored map; it does not. Recorded in `WorldRules` for completeness only.
- **Performance**: the rule evaluation runs once per generated point; the default list is ten
  rules with a Perlin draw each for four of them, about the cost of the vanilla chain. The biome
  point map at radius 40000 is 7800² points: built lazily, as today.
- **No decompiled code**: the mod is public domain, but the editor's rule is its own structure
  and names anyway (`WorldGenerator.Rules.cs` follows `BiomeCalculator` step for step, in the
  editor's style, comments naming the mod's method).
