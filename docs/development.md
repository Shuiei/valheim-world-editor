# Development

## Project layout

| Path | What it holds |
|---|---|
| `Desktop/` | The app, `ValheimWorldEditor` (Avalonia window, 3D view and map drawn with OpenGL through Silk.NET). `Program.cs` (command line), `StartPage`, `MapPage` + `MapView` (the map), `MainWindow` (the 3D editor's page: top bar, tool rail, panels, keys) with `GlView` (the 3D view and its mouse), `WorldSession` / `WorldScene` / `EditSession` (the open world, the loaded area, its pending changes and history), one class per tool and panel (`AreaTool` + `AreaPanel`, `PlaceTool` + `PlacePanel`, `SelectTool` + `SelectPanel`, `PathTool`, `Sculpt`, `Erosion`, `Mask`…), `ModelStore` and `GameLookGl` (the game's models and terrain shader), `Prefs` (remembered choices), `Driver` (`--driver`, below). |
| `Core/` | `ValheimWorldEditor.Core`, what is not about the window: |
| `Core/App/` | Settings (`AppSettings`), the log (`Log`), the game's files (`GameLook`, `GameBundles`, `GameLookData`, `GameShader`, `GameTextures`), Regrow, search, blueprint formats, servers and SSH (`ServerConfig`, `Tunnel`), the local game (`LocalGame`), characters, zone statistics, folders (`Places`). |
| `Core/Save/` | Save reader (`WorldSave`, `ValheimReader`), writer (`WorldWriter`), new worlds (`WorldCreator`), object building (`ZdoTools`: copies and blank objects), the `.db2` zone list, and live mode (`LiveBridge`, `LiveSync`). |
| `Core/Editing/` | Pending changes: terrain per zone, deleted and added objects, zone resets (`EditStore`); heights of a block of zones (`HeightGrid`). |
| `Core/WorldGen/` | Valheim's world generator (bit-exact base terrain), map data, location flattening, the vegetation rules (`vegetation.json`) (the game data the editor reads from the player's Valheim, the terrain modifiers, prefabs, build pieces, object data names, cave rocks and piece costs, is not kept: `Core/App/GameData.cs`). |
| `plugin/WorldEditorBridge/` | The BepInEx plugin for live mode (.NET Framework 4.7.2). |
| `tests/Desktop.Tests/` | Every test (xUnit v3, Avalonia's headless mode), see [Tests](#tests). `tests/fixtures/` holds the test world. |
| `tools/WorldCheck/` | Developer checks of the generator and the writer, saved objects byte by byte, and new worlds from a seed, see below. |
| `tools/DocsScreenshots/` | `Scenes.cs` builds and takes the pictures in `docs/images/`, through `Editor.cs`, which drives the app. |
| `tools/release.sh`, `tools/check-package.sh` | The release packages, and their check. |

## Command line

```
ValheimWorldEditor                                         start page
ValheimWorldEditor "<world folder>"                        that world's map
ValheimWorldEditor --live <bridge url> --token <token>     a live game's map (token also from WORLD_BRIDGE_TOKEN)
ValheimWorldEditor --world <folder or name> [--zone x,z] [--size n]   straight into the 3D editor
```

For tests and the documentation: `--driver` (driven over its input and output, below), `--data
<folder>` (settings, servers, blueprints, the log… in that folder instead of the user's; the game's
files are still read from the user's Valheim, and its index kept in the user's data folder) and `--window <width>x<height>` (the window's size).

### The driver (`--driver`)

The app reads one command per line on its input and answers each with one line, `@@ ok <json>`
(the state: page, objects, pending, frames drawn, OpenGL errors, selection, camera…) or `@@ error
<message>`. It prints `@@ ready` when it can take commands. The full list is at the top of
`Desktop/Driver.cs`; the main ones:

| Command | What it does |
|---|---|
| `gamedata <folder>` | The game's data from the tests' copies (`tests/fixtures`), where there is no Valheim (CI): without it no world opens. |
| `world <folder>` / `area <zx> <zz> <size>` / `map` | Open a world (its map), an area in the 3D editor, back to the map. |
| `camera <x> <z> <yaw°> <pitch°> <distance>` | The 3D camera on world x, z. |
| `mouse <down\|move\|up> <x> <z> [button] [mods]`, `wheel`, `key <name> [mods]` | Real pointer and key events at world positions. |
| `click <text>` / `choose <text>` | The visible button or switch with that label / the entry so named in a visible list (also in dialogs). |
| `stroke`, `mapview`, `search`, `hit`, `zones`, `look` | A brush stroke, the map's place, its search (and a result picked) and zone filter, the View look switches. |
| `set <label>\|<value>`, `type <hint>\|<text>`, `panel`, `message [text]` | A slider, box or list beside a label; a text box by its placeholder; the right-hand panel shown; the status bar's message (none: cleared). |
| `picture <file.png>` / `shot <file.png>` | The view shown (3D or map) once it is drawn / the whole window, panels and view, as Avalonia's compositor draws it (never a capture of the screen). |
| `bench <seconds>` / `state` / `quit` | Frame rates while the camera turns / the state / quit without asking. |

`tools/DocsScreenshots/Editor.cs` wraps it for the documentation's pictures: it starts the app with
a stand-in home folder (none of this computer's characters, worlds or servers show; driven, the app
looks for Steam only in that home, so for the game's look its settings name this computer's Valheim), sends commands and saves `shot`s as JPEG, the status bar's message cleared first.
`tools/DocsScreenshots/Scenes.cs` holds one scene per picture: each opens a fresh editor and builds
only what its picture shows, in worlds made from seeds and never saved:

```sh
WorldCheck create <dir>/Docs Docs yjRO99yNTI 16 --flat     # the bare zone the tools are shown on
WorldCheck create <dir>/Fjordheim Fjordheim Fjord2026 3     # a second world for the start page
dotnet run --project tools/DocsScreenshots -- <dir> [scene ...]  # all scenes, or only those named
```

The pictures go to `docs/images` (or `$DOCS_OUT`). The red on ground at the ±8 m limit is switched
off in every scene (it would hide the edits).

## Building

With the .NET 10 SDK (`global.json` asks for it; the plugin builds with it too). The app targets
`net10.0`; the release packages carry the runtime.

```sh
dotnet publish Desktop/ValheimWorldEditor.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimWorldEditor
```

One file, `ValheimWorldEditor`, with the native libraries it needs (Skia, HarfBuzz, SPIRV-Cross)
packed inside. With `-r win-x64`, `ValheimWorldEditor.exe` and its four native DLLs beside it (ANGLE,
Skia, HarfBuzz, SPIRV-Cross): a program that unpacks DLLs to a temporary folder at start is what
antivirus heuristics take for a dropper. Publish outside the source folder: a folder inside it is
picked up by the next build. `tools/release.sh <folder>` builds the complete release packages: the
program (with its DLLs on Windows) and the readmes;
`tools/check-package.sh <package>` checks one has what it needs and no source code, debug files,
game files or plugin. The plugin is its own package: `tools/thunderstore.sh <folder>` builds
`WorldEditorBridge-<version>.zip` (for the release page, Thunderstore and Hexium).

The plugin: build `plugin/WorldEditorBridge/WorldEditorBridge.csproj` after pointing its
`HintPath`s at your BepInEx `core` folder and the game's `*_Data/Managed` folder.

Thunderstore (team `Tie`): only the plugin, `WorldEditorBridge`. Thunderstore does not host
programs (it rejected the editor packages of 0.3.3), so the editor is only on the GitHub releases
page, which the plugin's mod page points to. `tools/thunderstore.sh <folder>` builds
`WorldEditorBridge-<version>.zip` from `tools/thunderstore/bridge/` (`manifest.json` and the mod
page `README.md`), the editor's `CHANGELOG.md` (made into the plugin's changelog by
`tools/thunderstore/package.cs`, a .NET file-based app, with links to the matching editor on GitHub)
and the icon, with
the DLL of the release packages (reused when that version is already in the folder). It checks
Thunderstore's rules (name, 250-character description, 256x256 icon). Upload the zip at
https://thunderstore.io/c/valheim/create/.

## Versions

One version number for everything: the `VERSION` file. `Directory.Build.props` reads it into the
editor and the plugin (`BuildInfo.Version`: the start page, the window title, the log, the plugin's
BepInEx version and its `/status`), and both release scripts name their packages after it. For a
release:

1. Raise `VERSION` (Major.Minor.Patch).
2. Add a `## v<version>` section to `CHANGELOG.md` (a test checks it). Put changes to the plugin
   under a `### WorldEditorBridge` part of that section. The Thunderstore package's changelog is made
   from it: each version shows the plugin's part (or "unchanged") and then the editor's.
3. Keep the BepInExPack dependency in the `tools/thunderstore/*/manifest.json` current.
4. `tools/release.sh <folder>` and `tools/thunderstore.sh <folder>`; tag `v<version>`, publish the
   GitHub release, upload the Thunderstore zip (Thunderstore refuses a version it already has).
   Publishing the release signs its files (`.github/workflows/sign-release.yml`, Sigstore): each
   gets a `<file>.sigstore.json` on the release a minute later. Attach every file before publishing
   (`gh release create` with the files does); a file added afterwards is signed by running the
   workflow by hand (Actions > Sign release, with the tag).

## The game's files

The in-game look uses files that belong to the game, so they are never in git or in the release
packages, and nothing is copied: the app reads them from the user's own Valheim, straight from its
asset bundles (`valheim_Data/StreamingAssets/SoftRef/Bundles`), in C# with
[AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (the bundles carry their type trees, so
every field reads by name). `Core/App/GameLook.cs` finds Valheim in the Steam libraries (or the folder
the user chose) and keeps the one reader, `Core/App/GameBundles.cs`:

- **Index:** which bundle holds each prefab, from each bundle's table of contents (its AssetBundle's
  `m_Container`); a prefab name in two bundles is the one with a ZNetView. A few other assets are
  found there by file name (`GameBundles.NamedAssets`: the terrain material and texture arrays, the
  map material). Building it reads every bundle's table (about 5 s); it is kept in `game-index.json`
  in the data folder with each bundle's name, size and time, and made again when they change (a game
  update).
- **Models** (`Desktop/BundleModels.cs` for `Desktop/ModelStore.cs`): a prefab's parts (MeshFilter or
  SkinnedMeshRenderer with their materials, placed by the transforms), leaving out the worn and broken
  looks of pieces, lower levels of detail, inactive objects and, in dungeon rooms, their networked
  objects and random parts not there half the time. Meshes are decoded from their vertex streams; z
  is mirrored (Unity is left-handed). A small piece takes tens of milliseconds, the biggest dungeon
  room about 10 s, on a worker thread.
- **Textures** (`Core/App/GameTextures.cs`): the mip level no larger than 1024 pixels, decoded to RGBA
  ([BCnEncoder.NET](https://github.com/Nominom/BCnEncoder.NET) for DXT1, DXT5, BC4, BC5, BC7); cut-out
  textures get the colour of nearby opaque pixels in their transparent ones, so filtering does not
  bleed the hidden colour into leaf edges.
- **Terrain and map** (`Core/App/GameLookData.cs`, drawn by `Desktop/GameLookGl.cs` and
  `Desktop/MapView.cs`): the `Heightmap_basematerial` textures, the diffuse and normal texture arrays,
  the `minimap` material's textures, and the terrain shader (`Core/App/GameShader.cs`): from the
  OpenGL core program one deferred-pass fragment variant converted to GLSL ES 3.0 (`GameLookGl` adds
  its own `main()`); Valheim for Windows has no OpenGL programs, so there the Vulkan one is taken
  (SMOL-V decoded by `Core/App/Smolv.cs`, its uniforms' names put back from its parameter blob) and
  SPIRV-Cross turns it into the same GLSL (`Core/App/TerrainShader.cs`). If a game update changes the
  shader, the converter says so instead of drawing a broken one.

`tests/Desktop.Tests/GameFilesTests.cs` (`--filter Category=Game`) checks the models against a copy the
older Python exporter made, where both are on the computer.

The catalogues that are in git (`Core/WorldGen/`, embedded in the app) are made from the game's files
by the editor itself, in C# on the same reader (`Core/App/GameCatalogues*.cs`): after a game update,
`ValheimWorldEditor --export-catalogues Core/WorldGen` makes them all again (about 6 minutes: every
bundle is read once), and `--export-catalogues <folder> <name> ...` only those named (`piece-place`,
`vegetation`...). They are `piece-place.json.gz` (the hammer's placement flags, snap points and colliders,
for the Workshop's Build), `piece-support.json` (materials and collider boxes for the stability
check), `vegetation.json` (the game's vegetation rules for Regrow nature,
ZoneSystem's and the location lists', with the random draws each kind makes when it is created) and
`dungeon-rooms.json.gz` (the Dungeon tool's catalogue: each dungeon kind's themes and space, and each
room's size, openings and the objects the game makes in it with the random parts that decide them). The files are written exactly as before (`Core/App/CatalogueJson.cs`: the same
number format, key order and separators), so a new export shows in git only what the game changed.

Some of the game's data is not kept in the repository at all: the editor reads it from the player's
Valheim at its start (`Core/App/GameData.cs`, one entry per kind), in seconds, and keeps each kind in
the data folder (`game-<kind>.json`) until the game's files change. No world or Workshop opens without
it. The kinds found in the bundles are read together, in one pass over them (each bundle opened once
and given to each kind's reader, `GameCatalogues.ReadAtStart`; about 10 s for them all, the first
time and after a game update). The kinds so far:

- `terrain-modifiers`: how locations and a few objects shape the ground before the saved edits, found
  from each `TerrainModifier` script (told by the script its file lists for it) up to its prefab,
  rather than through every object.
- `prefabs`: every networked prefab with what a new object of it needs in the save; also container
  sizes, ward radii, crafting station build ranges, what each sapling grows into, and what stands may
  hold: each item's type, attach points, variants and durability, each item stand's rule and each
  armour stand's slots, for the inspector's Holds / Wears and Claude's `set_contents`. Scripts are told
  by their fields in the type tree; the prefabs are found from each `ZNetView` up to its root through
  the bundle's tree read raw (a few numbers per object: the largest bundle's whole tree in one sweep,
  as the block cache cannot hold it), and only the scripts needed are read whole.
- `pieces`: each build piece's category, comfort, footprint and snap points, and which build tool's
  menu has it (hammer, hoe, cultivator, feaster: for the builder the editor writes on new pieces),
  found from each `Piece` script up to its prefab; what is below the prefab is read a level at a time,
  in the order of the bundle's file (its objects are far apart in the largest bundle).
- `zdo-keys`: the names of object data keys for the object inspector, from the string literals of
  `assembly_valheim.dll` (the save only keeps their hashes).
- `cave-rocks`: the meshes of the rocks the Path tool's Cave hangs as its roof, from the two prefabs'
  bundle only (found with the game files' index; under a second).
- `piece-cost`: each piece's English name, station and resources, for the blueprint library and the
  Workshop's Build, found from each `Piece` script up to its prefab, with the English texts of
  `resources.assets`. Its test copy keeps the pieces it has and the items they take.

The tests use trimmed copies, `tests/fixtures/<kind>.json` (what they meet; the driver's
`gamedata <folder>` gives them to the smoke tests). After a game update, make them again with
`ValheimWorldEditor --export-test-data tests/fixtures`, which keeps the entries each copy has.

The game's location rules (for the New world page's start, bosses and traders) are read by the editor
itself, in C# (`Core/App/GameLocations.cs`), from the player's Valheim, and kept in the data folder
until the game changes; the editor keeps no copy of them. The tests, which run without the game, use
`tests/fixtures/locations.json` (only the kinds the page shows): after a game update, make it again
with `ValheimWorldEditor --export-locations tests/fixtures/locations.json`. The order of the game's
location lists comes from the game's own log (`Player.log`, its "Added N locations, M vegetations ...
from main" lines), so it follows game updates once the game has been started; without a log, the
editor uses `GameLocations.BuiltInOrder` (Valheim 0.221), which is worth refreshing from a new log
after an update.

## WorldCheck

`tools/WorldCheck` holds the developer checks, run with `dotnet run --project tools/WorldCheck --
<check> …`:

| Check | What it does |
|---|---|
| `verify <dump> [seed]` | The world generator against a dump recorded in the game (`DumpVerifier`). |
| `verify-ingame <world> <file>` | The editor's ground against heightmaps recorded in the game by the TerrainCheck plugin. |
| `selftest-save <world copy>` | Edits, saves and reads back a copy of a world (only under `/tmp`). |
| `inspect <world>` / `summary <world>` | The chunk mapping and terrain objects / the overview map and the most edited zones. |
| `create <folder> <name> <seed> [radius]` | A new world from a seed, in the game's save format; its middle (`radius` zones around spawn) generated with the game's vegetation rules (no locations). It refuses a folder that already holds a world. |
| `look <seed> [metres]` | The biomes and water around a seed's spawn, to choose one. |
| `objects <world> [prefab]` | The most common kinds of saved objects; with a prefab (name or hash), each of its objects and its saved bytes in hex, to check them byte by byte. |

## Tests

Every test is in `tests/Desktop.Tests` (xUnit v3): the windows and panels run in Avalonia's
headless mode, driven with real mouse and keyboard events, and the core (save round trips, the
generator against values recorded in the game, live mode against a stand-in bridge, settings,
servers, the SSH tunnel) is tested there too.

- **Visual tests** (`[Trait("Category", "Visual")]`, in `VisualTests.cs`) start the real app with
  `--driver` and check what it draws with OpenGL: one app per group of tests (`EditorProcess`),
  each test opening a fresh copy of the test world. They need a display and skip without one. One
  group starts the app with `VWE_GPU_BUDGET=1`: the models and textures on the graphics card past
  that many bytes (1 GiB by default) are let go when another area opens, so every area reads its
  models again.
- **The user's files are never touched.** A module initializer (`TestApp` in `InputTests.cs`) points
  the settings, servers, Place memory, stamps, preferences, saved selections and the data folder at
  temporary files before any test runs, and turns off the search of the computer's own Valheim and
  mod manager profiles. Tests that change the environment or these paths (`HOME`, `XDG_*`, the data
  folder) run in the non-parallel `DataDir` collection and put everything back. The visual tests'
  app gets `--data` and never copies the game look.
- **The SSH tunnel** is tested against a real `sshd` (`LocalSshd`): run as the test user on a free
  local port, with its own keys and settings in a temporary folder (never `~/.ssh`, never port 22).
  Without OpenSSH's `sshd` those tests skip, unless `VWE_NEED_SSHD=1` makes that a failure.

The **test world** is `tests/fixtures/CITest`: a brand-new world made by a dedicated server and
filled live through the editor (ground edits, trees, rocks, bushes, pickables, a crop, floors, walls,
chests). `tests/fixtures/CITest.snapshot` is the same world as the plugin sends it.

CircleCI (`.circleci/config.yml`) runs on every push and pull request:

| Job | What it checks |
|---|---|
| `native-tests` | `tests/Desktop.Tests` without the visual tests, with OpenSSH's server installed (`VWE_NEED_SSHD=1`), on three containers that each run a third of the test classes, shared out by their last times (`tools/ci-tests.sh`). |
| `packages` | Both release packages build (without the plugin), and `tools/check-package.sh` finds everything they need and no source code, debug files or game files. |

Not covered by CI: the visual tests (no display), building the plugin and the game-look export
(both need Valheim's own files), and live mode against a real game.

Running them locally:

```sh
dotnet test tests/Desktop.Tests                                  # everything (visual tests need a display)
dotnet test tests/Desktop.Tests --filter "Category!=Visual"     # without the visual tests
dotnet test tests/Desktop.Tests --filter "Category=Visual"      # only them
dotnet-coverage collect -s tests/Desktop.Tests/coverage.config -f cobertura -o cov.xml "dotnet test tests/Desktop.Tests --filter Category!=Visual"
tools/release.sh /tmp/dist && tools/check-package.sh /tmp/dist/*.tar.gz
```

The coverage counts only our own code: `coverage.config` leaves out the FastNoise library, which the
test against the game's values checks along with the world generator.
