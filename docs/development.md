# Development

## Project layout

| Path | What it holds |
|---|---|
| `Desktop/` | The app, `ValheimWorldEditor` (Avalonia window, 3D view and map drawn with OpenGL through Silk.NET). `Program.cs` (command line), `StartPage`, `MapPage` + `MapView` (the map), `MainWindow` (the 3D editor's page: top bar, tool rail, panels, keys) with `GlView` (the 3D view and its mouse), `WorldSession` / `WorldScene` / `EditSession` (the open world, the loaded area, its pending changes and history), one class per tool and panel (`AreaTool` + `AreaPanel`, `PlaceTool` + `PlacePanel`, `SelectTool` + `SelectPanel`, `PathTool`, `Sculpt`, `Erosion`, `Mask`…), `ModelStore` and `GameLookGl` (the game's models and terrain shader), `Prefs` (remembered choices), `Driver` (`--driver`, below). |
| `Core/` | `ValheimWorldEditor.Core`, what is not about the window: |
| `Core/App/` | Settings (`AppSettings`), the log (`Log`), the game's files (`GameLook`, `GameBundles`, `GameLookData`, `GameShader`, `GameTextures`), Regrow, search, blueprint formats, servers and SSH (`ServerConfig`, `Tunnel`), the local game (`LocalGame`), characters, zone statistics, folders (`Places`). |
| `Core/Save/` | Save reader (`WorldSave`, `ValheimReader`), writer (`WorldWriter`), new worlds (`WorldCreator`), object building (`ZdoTools`: copies and blank objects), the `.db2` zone list, and live mode (`LiveBridge`, `LiveSync`). |
| `Core/Editing/` | Pending changes: terrain per zone, deleted and added objects, zone resets (`EditStore`); heights of a block of zones (`HeightGrid`). |
| `Core/WorldGen/` | Valheim's world generator (bit-exact base terrain), map data, location flattening, the build-piece catalogue (`pieces.json`), the prefab catalogue (`prefabs.json`), the vegetation rules (`vegetation.json`) and the object data names (`zdo-keys.json`). |
| `plugin/WorldEditorBridge/` | The BepInEx plugin for live mode (.NET Framework 4.7.2). |
| `tests/Desktop.Tests/` | Every test (xUnit v3, Avalonia's headless mode), see [Tests](#tests). `tests/fixtures/` holds the test world. |
| `tools/WorldCheck/` | Developer checks of the generator and the writer, and new worlds from a seed, see below. |
| `tools/asset-export/` | Python (UnityPy) scripts that make the catalogues in `Core/WorldGen/` from the game's files (developers only; not in the release). |
| `tools/docs-screenshots/` | `scenes.py` builds and takes the pictures in `docs/images/`, through `editor.py`, which drives the app. |
| `tools/release.sh`, `tools/check-package.sh` | The release packages, and their check. |
| `tools/zdo_scan.py` | Minimal chunk reader, to check saved objects byte by byte. |

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
| `world <folder>` / `area <zx> <zz> <size>` / `map` | Open a world (its map), an area in the 3D editor, back to the map. |
| `camera <x> <z> <yaw°> <pitch°> <distance>` | The 3D camera on world x, z. |
| `mouse <down\|move\|up> <x> <z> [button] [mods]`, `wheel`, `key <name> [mods]` | Real pointer and key events at world positions. |
| `click <text>` / `choose <text>` | The visible button or switch with that label / the entry so named in a visible list (also in dialogs). |
| `stroke`, `mapview`, `search`, `hit`, `zones`, `look` | A brush stroke, the map's place, its search (and a result picked) and zone filter, the View look switches. |
| `set <label>\|<value>`, `type <hint>\|<text>`, `panel`, `message [text]` | A slider, box or list beside a label; a text box by its placeholder; the right-hand panel shown; the status bar's message (none: cleared). |
| `picture <file.png>` / `shot <file.png>` | The view shown (3D or map) once it is drawn / the whole window, panels and view, as Avalonia's compositor draws it (never a capture of the screen). |
| `bench <seconds>` / `state` / `quit` | Frame rates while the camera turns / the state / quit without asking. |

`tools/docs-screenshots/editor.py` wraps it for the documentation's pictures: it starts the app with
a stand-in home folder (none of this computer's characters, worlds or servers show; driven, the app
looks for Steam only in that home, so for the game's look its settings name this computer's Valheim), sends commands and saves `shot`s as JPEG, the status bar's message cleared first.
`tools/docs-screenshots/scenes.py` holds one scene per picture: each opens a fresh editor and builds
only what its picture shows, in worlds made from seeds and never saved:

```sh
WorldCheck create <dir>/Docs Docs yjRO99yNTI 16 --flat     # the bare zone the tools are shown on
WorldCheck create <dir>/Fjordheim Fjordheim Fjord2026 3     # a second world for the start page
python3 tools/docs-screenshots/scenes.py <dir> [scene ...]  # all scenes, or only those named
```

The pictures go to `docs/images` (or `$DOCS_OUT`). The red on ground at the ±8 m limit is switched
off in every scene (it would hide the edits).

## Building

With the .NET 10 SDK (`global.json` asks for it; the plugin builds with it too). The app targets
`net10.0`; the release packages carry the runtime.

```sh
dotnet publish Desktop/ValheimWorldEditor.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimWorldEditor
```

One file, `ValheimWorldEditor` (`-r win-x64`: `ValheimWorldEditor.exe`), with the libraries it needs
(Skia, HarfBuzz, ANGLE on Windows) packed inside. Publish outside the source folder: a folder inside
it is picked up by the next build. `tools/release.sh <folder>` builds the complete release packages:
the program, the game-look exporter with its own Python runtime and the readmes;
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
`tools/thunderstore/changelog.py`, with links to the matching editor on GitHub) and the icon, with
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

The catalogues that are in git are made by `scan_pieces.py` (`Core/WorldGen/pieces.json`, with each
piece's snap points), `scan_modifiers.py` (`Core/WorldGen/terrain-modifiers.json`) and
`scan_prefabs.py` (`Core/WorldGen/prefabs.json`: also container sizes, ward radii and crafting
station build ranges); each takes the output file as argument and the bundle folder in
`VWE_BUNDLES`, and follows references between bundles with `tools/asset-export/cab_index.json`, made
by `make_cab_index.py` (once, and again after a game update; `pip install -r
tools/asset-export/requirements.txt` first). `scan_vegetation.py Core/WorldGen/vegetation.json` makes the game's vegetation rules
for Regrow nature (ZoneSystem's and the location lists', with the random draws each kind makes when
it is created). `scan_grown.py Core/WorldGen/prefabs.json` (run after `scan_prefabs.py`) adds what
each sapling grows into (grown crops and trees keep their sapling's grow radius).
`scan_build_tools.py Core/WorldGen/pieces.json Core/WorldGen/prefabs.json` (run after the other two)
adds which build tool's menu has each piece (hammer, hoe, cultivator, feaster): the editor writes a
builder on new pieces of those kinds. `scan_zdo_keys.py <Valheim folder>
Core/WorldGen/zdo-keys.json` makes the names of object data keys for the object inspector, from the
string literals of `assembly_valheim.dll` (the save only keeps their hashes).
`scan_dungeon_rooms.py Core/WorldGen/dungeon-rooms.json` (then gzip it to `dungeon-rooms.json.gz`)
makes the Dungeon tool's catalogue: each dungeon kind's themes and space, and each room's size,
openings and the objects the game makes in it with the random parts that decide them.

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
| `native-tests` | `tests/Desktop.Tests` without the visual tests, with OpenSSH's server installed (`VWE_NEED_SSHD=1`). |
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

The coverage counts only our own code: `coverage.config` leaves out FastNoise and the generator
decompiled from Valheim (`WorldGenerator.cs`, `DUtils.cs`), which the test against the game's values
checks as a whole.
