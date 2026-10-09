# Development

## Project layout

| Path | What it holds |
|---|---|
| `Desktop/` | The app, `ValheimWorldEditor` (Avalonia window, 3D view and map drawn with OpenGL through Silk.NET). `Program.cs` (command line), `StartPage`, `MapPage` + `MapView` (the map), `MainWindow` (the 3D editor's page: top bar, tool rail, panels, keys) with `GlView` (the 3D view and its mouse), `WorldSession` / `WorldScene` / `EditSession` (the open world, the loaded area, its pending changes and history), one class per tool and panel (`AreaTool` + `AreaPanel`, `PlaceTool` + `PlacePanel`, `SelectTool` + `SelectPanel`, `PathTool`, `Sculpt`, `Erosion`, `Mask`…), `ModelStore` and `GameLookGl` (the game's models and terrain shader), `Prefs` (remembered choices), `Driver` (`--driver`, below). |
| `Core/` | `ValheimWorldEditor.Core`, what is not about the window: |
| `Core/App/` | Settings (`AppSettings`), the log (`Log`), game-look setup (`GameLook`), Regrow, search, blueprint formats, servers and SSH (`ServerConfig`, `Tunnel`), the local game (`LocalGame`), characters, zone statistics, folders (`Places`). |
| `Core/Save/` | Save reader (`WorldSave`, `ValheimReader`), writer (`WorldWriter`), new worlds (`WorldCreator`), object building (`ZdoTools`: copies and blank objects), the `.db2` zone list, and live mode (`LiveBridge`, `LiveSync`). |
| `Core/Editing/` | Pending changes: terrain per zone, deleted and added objects, zone resets (`EditStore`); heights of a block of zones (`HeightGrid`). |
| `Core/WorldGen/` | Valheim's world generator (bit-exact base terrain), map data, location flattening, the build-piece catalogue (`pieces.json`), the prefab catalogue (`prefabs.json`), the vegetation rules (`vegetation.json`) and the object data names (`zdo-keys.json`). |
| `plugin/WorldEditorBridge/` | The BepInEx plugin for live mode (.NET Framework 4.7.2). |
| `tests/Desktop.Tests/` | Every test (xUnit v3, Avalonia's headless mode), see [Tests](#tests). `tests/fixtures/` holds the test world. |
| `tools/WorldCheck/` | Developer checks of the generator and the writer, and new worlds from a seed, see below. |
| `tools/asset-export/` | Python (UnityPy) scripts that extract textures, shaders, models and catalogues from the game. |
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
<folder>` (settings, servers, blueprints, the log… in that folder instead of the user's; the copied
game look is still read from the user's) and `--window <width>x<height>` (the window's size).

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
| `stroke`, `mapview`, `search`, `zones`, `look` | A brush stroke, the map's place, its search and zone filter, the View look switches. |
| `set <label>\|<value>`, `type <hint>\|<text>`, `panel`, `message [text]` | A slider, box or list beside a label; a text box by its placeholder; the right-hand panel shown; the status bar's message (none: cleared). |
| `picture <file.png>` / `shot <file.png>` | The view shown (3D or map) once it is drawn / the whole window, panels and view, as Avalonia's compositor draws it (never a capture of the screen). |
| `bench <seconds>` / `state` / `quit` | Frame rates while the camera turns / the state / quit without asking. |

`tools/docs-screenshots/editor.py` wraps it for the documentation's pictures: it starts the app with
a stand-in home folder (none of this computer's characters, worlds or servers show; the game look
is linked in; driven, the app looks for Steam only in that home, so it never finds this computer's
own Valheim), sends commands and saves `shot`s as JPEG, the status bar's message cleared first.
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

## Files extracted from the game

The in-game look uses files that belong to the game, so they are never in git or in the release
packages. The app copies them from the user's own Valheim (`Core/App/GameLook.cs`, started by the
start page): it finds Valheim in the Steam libraries (or the folder the user chose), runs
`export-game-files/export_all.py` with the bundled Python runtime into the per-user `game-look`
folder of the data folder, and runs it again when Steam's build id of the game changes. That folder
holds:

- `terrain/*.png` and `heightmap.frag.glsl`: terrain textures and the converted terrain shader
  (drawn by `Desktop/GameLookGl.cs`);
- `maptex/`: map textures (the map shader itself is hand-ported in `Desktop/MapShader.cs`);
- `models/`: building, tree, rock and bush models, their textures and `objects.json`
  (`Desktop/ModelStore.cs`).

`tools/make-python-runtime.sh` builds that runtime, then `tools/python-runtime/trim.py` removes every
file a full export does not use: the list of what stays is
`tools/python-runtime/keep-<linux-x64|win-x64>.txt` (plus the `encodings` package, the used
packages' Python files and licence files; see the top of `trim.py`). After changing
`requirements.txt`, the Python version or the exporter, make the lists again and commit them:

```sh
python3 tools/python-runtime/trace.py linux-x64 ~/.local/share/Steam/steamapps/common/Valheim
python3 tools/python-runtime/trace.py win-x64 ~/.local/share/Steam/steamapps/common/Valheim   # under Wine
```

Each runs a full export with the untrimmed runtime and logs what it opens and loads. The Windows
runtime also carries `msvcp140.dll` (from Microsoft's `msvc-runtime` wheel), which UnityPy needs
and Windows only has when a program installed the Visual C++ runtime.

`tools/asset-export/export_all.py` can also be run by hand (Python 3; `pip install
-r tools/asset-export/requirements.txt`):

```sh
python3 tools/asset-export/export_all.py --valheim ~/.local/share/Steam/steamapps/common/Valheim --out ~/.local/share/ValheimWorldEditor/game-look
```

| Option | Meaning |
|---|---|
| `--valheim` | The game client's folder (with `valheim_Data`). |
| `--out` | The `game-look` folder. |
| `--objects all` / `world` / `none` | Models for every placeable kind (default), only the kinds in the save given with `--world <world folder>`, or build pieces only. |
| `--only terrain` / `map` / `models` | Run only some steps (repeatable). |
| `--work` | Cache folder (default `<out>/../export-cache`). |

What it does:

1. Reads every asset bundle once and records where the terrain material, the terrain texture
   arrays, the map material and each prefab's root object are (cached in `--work/scan.json`).
2. **Terrain:** the textures of the `Heightmap` material, the diffuse and normal texture arrays
   stacked into vertical strips, and the OpenGL core build of the `Custom/Heightmap` shader, one
   deferred-pass fragment variant converted to GLSL ES 3.0 (`GameLookGl` adds its own `main()`). If
   a game update changes the shader, the converter stops with a message instead of writing a broken
   file.
3. **Map:** the textures of the `minimap` material.
4. **Models:** `export_pieces.py` (meshes, textures, materials; incremental, so an interrupted run
   continues), then `fix_normals.py` (Unity's DXT5nm normal maps to plain RGB) and `fix_alpha.py`
   (bleeds the colour of cut-out textures into their transparent pixels), then `objects.json`.

A full run takes a few minutes and writes about 150 MB.

The catalogues that are in git are made by `scan_pieces.py` (`Core/WorldGen/pieces.json`, with each
piece's snap points), `scan_modifiers.py` (`Core/WorldGen/terrain-modifiers.json`) and
`scan_prefabs.py` (`Core/WorldGen/prefabs.json`: also container sizes, ward radii and crafting
station build ranges); each takes the output file as argument and the bundle folder in
`VWE_BUNDLES`. `scan_vegetation.py Core/WorldGen/vegetation.json` makes the game's vegetation rules
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
  each test opening a fresh copy of the test world. They need a display and skip without one.
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
