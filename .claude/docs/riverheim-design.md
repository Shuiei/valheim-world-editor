# Riverheim compatibility: design

Status: design of 2026-10-20, checked against the game (§5: every row of §3 measured, all exact). Built: step 1
(#478) and step 2 (`feature/riverheim-terrain`); the rest in §6.

Decisions taken (with the user, 2026-10-20):

- **The editor runs the player's Riverheim.** Riverheim has no licence and no public source, so the
  editor carries none of its code. It loads `Riverheim.dll` from the player's BepInEx plugins folder
  at run time and calls its generator, as it already loads YamlDotNet for Expand World Data
  (`Core/App/YamlReader.cs`). It is the second mod the editor supports by name, after Expand World:
  it too is world generation, which the SDK has no surface for.
- **A Settings switch, off by default.** Like Expand World, the **Riverheim** switch
  (`AppSettings.Riverheim`) must be on before the mod counts. Off, the editor works as if Riverheim
  were not installed.
- **No Riverheim, no Riverheim world.** A world made with Riverheim cannot be drawn without it (the
  game itself refuses it, `[BAD_VERSION]`). The editor marks it in the world list and refuses to
  open it, saying why and what to do. It never draws vanilla ground for it.
- **New world makes Riverheim worlds**, with the seed preview, start/boss/trader prediction and seed
  search. Seed search is slow: a Riverheim world takes about 4 s to lay out.
- **With or without Expand World.** Every combination the game allows is drawn as the modded game
  draws it (§3), including the ones Riverheim's README calls broken.

Sources: Riverheim 1.2.1 (Thunderstore `Gurebu-Riverheim`, Riverheim.dll, sha256
`a2dc58b7…0814`), read with ilspycmd in a scratchpad only; Expand World Size, Data and Rivers from
their public-domain repositories (`JereKuusela/valheim-expand_world_*`), versions as in
`expand-world-design.md`.

## 1. What "compatible" means here

A world played with Riverheim, alone or with Expand World, must:

1. **Open and save without harm.** The world file's Riverheim header survives a save. It already
   does: the editor copies the `.fwl2` through untouched (`WorldWriter.Commit`).
2. **Look like the game.** The ground and biomes of zones the game has not made yet, the map, the
   ground paint, forests: to the bit, as the editor's vanilla generator is today.
3. **Predict like the game.** Locations, vegetation regrowth, the start temple, the dungeon
   entrance feature: the game's placement code is unchanged by Riverheim, it only asks a different
   generator. So these follow once the generator answers as Riverheim does.
4. **Create worlds for it.** New world writes a world the game opens as a Riverheim world.

Out of scope: Riverheim's beta (`dev.gurebu.riverheim.unstable`, deprecated, its worlds cannot be
loaded by 1.x); Better Continents (Riverheim refuses to load with it).

## 2. What Riverheim does

### 2.1 The world file

Riverheim patches the game's world save and load (`World.SaveWorldMetaData`, `World.LoadWorld`).
Inside the `.fwl2`'s length-prefixed package, a Riverheim world reads:

| Field | Type | Value |
|---|---|---|
| marker | int | `-1` (a vanilla world starts with its file version, always positive) |
| generator id | string | `dev.gurebu.riverheim.stable` |
| file version | int | the game's (as in a vanilla world) |
| setting count | ZPackage item count | 2 in 1.2.1 |
| settings | (string name, typed value) × count | `preset` string (`builtin.amur`), `preset.version` int (2) |
| … | | then the vanilla fields from the name on |

The value type comes from the setting's name, and an unknown name makes the game refuse the world
(`WorldSettingSerialization.GetSettingType`). The game also refuses a preset version above what the
installed Riverheim knows (`ConfigManager.Validate`). The menu world and vanilla worlds have no
header. In multiplayer, the server sends the same id and settings after the world generator
version (`ZNet.SendPeerInfo`), and peers must share Riverheim's major.minor version.

**Today the editor misreads these files**: `WorldSave.ReadMetadata` takes `-1` as the file version
and the id as the world name, so the name, seed and keys that follow are wrong.

### 2.2 The generator

All of Riverheim's generation is plain .NET (netstandard2.1, namespaces `Riverheim.*` outside
`Riverheim.Plugin`), deterministic across 64-bit platforms by design (`DeterministicMath`). Only the
`Riverheim.Plugin` classes touch the game. The game-side code is a few lines per patch:

```
pipeline = new GeneratorPipeline.Builder("Rendering/WorldRenderer").SetParallel(true).Build()
pipeline.Compute(world.m_seed, ConfigManager.GetConfig(settings))
renderer = pipeline.GetResult<WorldRenderer>("Rendering/WorldRenderer")
```

Prefixes that return false (the game's body never runs) on `WorldGenerator`:

| Game method | Riverheim's answer |
|---|---|
| `Pregenerate` | lays the world out as above; no vanilla lakes, rivers or streams |
| `GetBiome(wx, wy, oceanLevel, waterAlwaysOcean)` | `renderer.GetBiome((wx, wy))`, mapped to the game's biome (below) |
| `GetBiomeHeight(biome, wx, wy, out mask, …)` | `30f + (float)p.height`, `p = renderer.RenderPoint((wx, wy))`; the biome argument is ignored |
| `GetHeight(wx, wy)` and `GetHeight(wx, wy, out mask)` | `GetBiomeHeight(None, wx, wy, …)`, through the patched method (so other mods' patches on it run) |
| `GetForestFactor(pos)` | `(float)(2.15 * (1.0 - renderer.GetForestation((pos.x, pos.z))))` |
| `GetAshlandsHeight(wx, wy, out mask, cheap)` | the same as `GetBiomeHeight`, in metres (the game's own returns generator units) |

The mask is `Color((float)p.dirt, (float)p.snow, (float)p.pave, (float)max(p.lava, p.fertility))`.
Coordinates go in as the game's floats widened to double. Riverheim's biomes and the game's:
Meadows, Marsh → Meadows; Forest, ForestLowlands → BlackForest; Swamp; Mountain; Plains; Ashlands,
Lavaplain → AshLands; DeepNorth; Ocean; Mistlands, Mistglade → Mistlands; None → None.

Everything else in `WorldGenerator` stays the game's: `IsAshlands`, `IsDeepnorth`,
`GetAshlandsOceanGradient`, `GetBiomeArea` (which asks the patched `GetBiome`), `GetTerrainDelta`
(which asks the patched `GetHeight`), `WorldAngle`. Riverheim's world is 10500 m across its edge
(`CommonConfig.worldSize`), fixed.

Measured on this machine (seed 12345, .NET 10, the player's dll loaded as below): the layout takes
3.9 s, a point about 2 µs, 200,000 points in parallel give bit-identical results to one at a time,
one laid-out world holds 185 MB.

### 2.3 Loading the dll outside the game

`GeneratorPipeline.Builder.Build` scans the whole assembly (`Assembly.GetTypes`), plugin classes
included, so their base types must resolve: `BepInEx` (`BaseUnityPlugin`), `UnityEngine.CoreModule`
and the `UnityEngine` / `UnityEngine.SharedInternalsModule` facades, `0Harmony`. Nothing of
`assembly_valheim` is needed and no plugin code runs. A player has all of these: BepInEx's `core`
folder next to the plugins folder Riverheim was found in, and the game's
`valheim_Data/Managed` (the editor already needs the Valheim install). On CI, which has no Valheim,
the tests resolve them to small test-only stand-ins (§5).

## 3. With Expand World

Harmony (BepInEx ships HarmonyX 2.9) runs the prefixes by priority; Expand World Size's stretch
prefixes are `HigherThanNormal`, so they run before Riverheim's. Postfixes still run after a prefix
that skipped the body. Transpilers change only the game's bodies, which Riverheim skips. So:

| Expand World setting | On a Riverheim world |
|---|---|
| Size: Stretch world | **applies**: Riverheim gets the stretched point in `GetBiome`, `GetBiomeHeight` (so `GetHeight`), the map mask. Not in `GetForestFactor` (no stretch prefix there, as in vanilla) |
| Size: Stretch biomes | nothing (a transpiler on the game's `GetBiome` body) |
| Size: World radius, edge size | the generator ignores them (Riverheim's edge stays at 10500 × stretch); they still size the map, the save grid, location radii and the Ashlands/Deep North limits, as without Riverheim. Riverheim's README asks for radius + edge = 10500 × stretch |
| Size: Water depth multiplier | **applies** (postfix on `GetBiomeHeight`), on Riverheim's metres |
| Size: Forest multiplier | **applies** (postfix on `GetForestFactor`) |
| Size: Altitude multiplier/delta, Offset X/Y, Height variation seed | nothing (they patch `GetBaseHeight` and the biome shapes, never called) |
| Size: Locations, save grid, minimap | as without Riverheim |
| Data: World data / Biome data | off by default: no effect. On (with the data files, loaded when the world starts): **the biomes are Data's** (its `GetBiome` prefix wins; 1272 of 3000 points kept Riverheim's), **the heights stay Riverheim's** (exact). The README's "overrides Riverheim's biome distribution" |
| Data: everything else (locations, vegetation, alt biomes, dungeons, rooms, blueprints) | as without Riverheim: the placement code asks the generator |
| Rivers: lakes, rivers, streams | nothing (never laid out) |
| Rivers: water level, waves | **applies** outside the generator, and Size's depth postfix uses it as its water level (measured with 27) |

Each **applies** row is already in the editor's Expand World code; the generator keeps those
transforms and swaps only the game's bodies for Riverheim's. Every row was measured against the game
(§5). Size's depth postfix is evaluated by Mono in double: `(float)(((double)h - water) * depth + water)`
matches all 3000 points, the float formula only 2414: the editor's `_v.Depth` must round the same way.

## 4. What the editor does

### 4.1 Finding and loading Riverheim

- `Core/App/Riverheim.cs` (new): `Find(settings)` looks in the same BepInEx folders as Expand World
  (`ExpandWorld.Find` order: the Settings folder, the Valheim install, mod manager profiles) for a
  plugin file named exactly `Riverheim.dll` (same rule as `HasPlugin`: case-insensitive name, a
  manager's `.dll.old` does not count). Switch off → not found.
- `RiverheimLibrary` (new, the `YamlReader` pattern): one `AssemblyLoadContext` per dll path,
  loaded from the file's bytes (the file stays free), cached; a `Resolving` handler that answers
  from the BepInEx `core` folder and the game's `Managed` folder; types and methods found by
  reflection, by name. Exposed: `Version`, `DefaultSettings()`, `CurrentPresetVersion(preset)`,
  `Validate(preset, version)`, `LayOut(seed, preset, version) → RiverheimWorld`. Riverheim's own
  logger is pointed at the editor's log.
- `RiverheimWorld` (new): wraps one laid-out renderer, thread-safe; `Biome(x, z)`,
  `Point(x, z) → (height, mask)`, `Forest(x, z)`, with the game-side conversions of §2.2 (our own
  few lines, they describe the game's calls, not Riverheim's work).
- Any failure (dll missing a type, a method renamed by a Riverheim update, an exception in the
  layout) is logged with the dll's version and becomes a refusal (§4.4), never vanilla ground.

### 4.2 The world file

- `WorldSave.ReadMetadata` reads the header: `Metadata` gains `Generator` (null for a vanilla
  world; else id + the settings as read). Unknown setting names are kept by name; a type the editor
  cannot know makes the world unreadable with a message naming the setting (as the game refuses
  it).
- Live mode reads the game's file through the bridge with the same reader, so it gets this too.
- `WorldCreator.WriteMetadata` writes the header for a Riverheim New world (§4.6), taking the
  preset and version from the player's Riverheim (`DefaultSettings`), not from a constant.

### 4.3 Rules and generator

- `WorldRules` gains `Riverheim` (null = vanilla): the dll path and version, preset, preset
  version. It is part of `Json`/`Stamp`, so `WorldGenerator.Identity` and the map's overview cache
  tell a Riverheim world from a vanilla one with the same seed.
- `WorldGenerator`: when `Rules.Riverheim` is set, the constructor lays the world out through
  `RiverheimLibrary` (cached per dll + seed + preset + version, so the map, the 3D view and Regrow
  share one) instead of `LayOutWater`; the seed's offset draws stay (they do in the game: the
  constructor runs before `Pregenerate`). `GetBiome`, `GetBiomeHeight`, `GetHeight`,
  `GetForestFactor`, `GetAshlandsHeight` call it in place of the game's bodies, keeping the
  Expand World pieces of §3 where they sit today (`Stretched` before, `_v.Depth` and `_v.Forest`
  after, Data's prefix/postfix where the dumps put them).
- Empty lakes/rivers/streams are already a supported state (the menu world): `RiverAt` weighs 0.
- Nothing else changes: map, 3D ground, `BaseTerrain`, `SeedLocations`, Regrow, `WorldCreator`
  all go through these methods.

### 4.4 Opening, refusing

- `Worlds.Info` gains `Generator` (`"Riverheim"` or null). The world list shows
  `"{0} · Riverheim"` like `· Expand World`, and when Riverheim cannot be used, `Problem` says why
  (below), so the player sees it before clicking.
- `WorldSession.Open` and `OpenLive` resolve Riverheim after the save loads, with the Expand World
  rules. When it cannot be used they throw, and `MainWindow.OpenWorld` shows it as today
  (`world.msg.openFailed`). The reasons, each its own `Lang` key:
  - switch off: "made with Riverheim: turn Riverheim on in Settings";
  - not found: "made with Riverheim: install Riverheim in a BepInEx folder the editor knows (Settings)";
  - preset unknown or too new: "made with Riverheim preset {0} v{1}, which Riverheim {2} does not know: update Riverheim";
  - beta id: "made with Riverheim's beta, which cannot be opened";
  - load or layout failure: "Riverheim {0} could not lay out this world (see the log)".
- The layout takes seconds: the open runs it on the background thread it already uses, with a
  status line ("Laying out the Riverheim world…").

### 4.5 Settings

A **Riverheim** switch under its own heading in the Settings dialog, after Expand World, with a
hint (where the dll is looked for, which folder was found and its version). It uses the same
BepInEx folder box as Expand World (`ExpandWorldFolder`, renamed in the UI to "BepInEx folder" if
both need it). `Lang` keys `settings.riverheim*`, `tip.settings.riverheim*`.

### 4.6 New world

- With the switch on and Riverheim found, the page shows a **World generator** choice: Vanilla /
  Riverheim {version}. Default Riverheim, as the game makes every new world a Riverheim world while
  the mod is installed.
- `ExpandWorld.ForNewWorld` (or a wrapper) adds `Riverheim` to the rules; the preview, map,
  start/boss/trader prediction and pre-generated zones follow from the generator.
- Seed search lays out one world per seed: at most a few at once (memory: 185 MB each, freed
  after the seed is scored), with a progress count. Slower than vanilla, as decided.
- `WorldCreator.Create` writes the header (§4.2).

### 4.7 Live mode

- The bridge's `GET /plugins` gives Riverheim's guid and version; `GET /world/meta` the header.
  The editor needs a local Riverheim of the same major.minor (the game's own peer rule) to draw
  the world; otherwise it refuses as in §4.4, naming the server's version.

## 5. Checking against the game

Done on 2026-10-20 (a scratch dump plugin, `RhDump`, recording for 3000 points out to 21.5 km the game's
biome, `GetBiomeHeight` with its four mask channels, `GetHeight` and `GetForestFactor`, compared with
Riverheim run in .NET 10 from the player's dll):

| Setup | Result |
|---|---|
| Riverheim alone (seed `5DCcdIcuYJ`) | 3000/3000 exact on all eight values |
| + Size: radius 20500, edge 500, stretch 2, biomes 2, depth 1.5, forest 1.5, altitude ×1.3 +4 | exact with the point stretched, depth in double, forest divided; altitude has no effect |
| + Data, defaults | exact (no effect) |
| + Data, World and Biome data on (world loaded, seed `qxuhL2HHrK`) | heights exact, biomes Data's |
| + Rivers, water level 27 | exact with depth around 27 |

So Riverheim is deterministic between Mono (the Linux server) and .NET 10, and the composition of §3
holds. Still to check once the editor is built: the editor's own generator against the same kind of dumps, through a `tools/WorldCheck` command, on the
local test server only (`~/.cache/vwe-server-test`, never the live server):

1. The five setups above, plus the 8 reference zones built as `HeightmapBuilder` does.
2. With Data's World data on: the editor's port of Data's biomes over Riverheim's heights.
3. Locations and regrow (`WorldCheck locations`, `regrow`) on a Riverheim world, to see placement follow.

Tests: dump 1 becomes `tests/fixtures/riverheim-reference.txt` (trimmed like the vanilla one), run
by a reference test as `WorldGeneratorReferenceTests` does. `tests/TestMods.targets` downloads
Riverheim 1.2.1 and BepInExPack_Valheim (for `BepInEx.dll`, `0Harmony.dll`); the Unity types the
plugin classes derive from (`UnityEngine.Object` → `Component` → `Behaviour` → `MonoBehaviour`) come
from a stand-in assembly the test support builds (test-only, like `YamlStandIn`). Unit tests: header
read/write round trip, unknown setting, beta id, refusal reasons, dll detection, the rules' Stamp.

## 6. PR plan (stacked, minor bumps, one branch from the previous)

1. **Riverheim worlds recognised** (`feature/riverheim-worlds`): header read (§4.2), `Worlds.Info`
   mark, Settings switch, refusal of every Riverheim world (the generator is not there yet) with
   the reasons that already apply; this design. Fixes the misread name/seed.
2. **Riverheim terrain** (`feature/riverheim-terrain`): `RiverheimLibrary`, `RiverheimWorld`,
   `WorldRules.Riverheim`, generator swap, opening allowed, dumps 1 and 5, reference fixture,
   test mods + stand-in.
3. **With Expand World** (`feature/riverheim-expand-world`): dumps 2–4, the order of Data's
   patches, any row of §3 the game corrects.
4. **New world** (`feature/riverheim-new-world`): generator choice, header writing, seed search.
5. **Live** (`feature/riverheim-live`): bridge plugins + version rule.
6. **Docs** (`docs/riverheim.md`, README row, FAQ), in step 2 for what exists and each later step
   for its part.

## 7. Risks

- **Riverheim's internals are not an API.** A Riverheim update may rename what the editor calls by
  reflection. The editor then refuses with the version in the message; worlds are never drawn
  wrong. Worth asking the author whether a small stable entry point could be kept.
- **Determinism Mono vs .NET 10**: checked by the dumps; if it fails, Riverheim worlds cannot be
  drawn to the bit and the design changes.
- **Memory and time**: one laid-out world per open session (185 MB, 4 s); seed search bounded.
