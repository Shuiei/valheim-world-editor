# Development

## Project layout

| Path | What it holds |
|---|---|
| `Program.cs`, `App/` | The app: window (`NativeWindow`, Photino), start page (`Launcher`), game-look setup (`GameLook`), settings, and the editor session with its HTTP API (`EditorSession`: `/api/world`, `/api/region`, `/api/objects`, `/api/save`, …). |
| `Save/` | Save reader (`WorldSave`, `ValheimReader`), writer (`WorldWriter`), object building (`ZdoTools`: copies and blank objects), the `.db2` zone list, and live mode (`LiveBridge`, `LiveSync`). |
| `Editing/` | Pending changes: terrain per zone, deleted and added objects, zone resets (`EditStore`). |
| `WorldGen/` | Port of Valheim's world generator (bit-exact base terrain), map data, location flattening, the build-piece catalogue (`pieces.json`) and the prefab catalogue (`prefabs.json`). |
| `wwwroot/` | The map (`index.html`, `mapview.js`) and the 3D editor (`editor.html`, `editor/*.js`, `terrain/*.js`, three.js in `lib/`). |
| `wwwroot/editor/tips.js` | The hover text of every control. Keep it and the docs in step. |
| `plugin/WorldEditorBridge/` | The BepInEx plugin for live mode (.NET Framework 4.7.2). |
| `tools/asset-export/` | Python (UnityPy) scripts that extract textures, shaders, models and catalogues from the game. |
| `tools/zdo_scan.py` | Minimal chunk reader, to check saved objects byte by byte. |
| `tools/docs-screenshots/` | Scripts that take the screenshots in `docs/images/`. |

## Command line

```
ValheimWorldEditor                                    start page (window, or the browser as fallback)
ValheimWorldEditor [worldFolder] [--port 5180] [--browser]
ValheimWorldEditor --live <bridge url> --token <token> [--port 5181] [--browser]
```

Other options are for checking the world generator against the game and are not needed for
editing: `--summary`, `--inspect`, `--verify <dump>`, `--verify-ingame <file>`, and
`--selftest-save <copy under /tmp>` (it refuses any other folder).

## Building

```sh
dotnet publish TerrainEditor.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimWorldEditor
```

The build copies `wwwroot` into the output. Publish outside the source folder: an output folder
inside it is picked up as content by the next build. `-r win-x64` builds for Windows; it compiles,
and under Wine editing and saving work, but the 58 MB world-map reply never arrived there (probably
Wine's networking; not tried on real Windows).

The plugin: build `plugin/WorldEditorBridge/WorldEditorBridge.csproj` after pointing its
`HintPath`s at your BepInEx `core` folder and the game's `*_Data/Managed` folder.

Thunderstore (team `Tie`): only the plugin, `WorldEditorBridge`. Thunderstore does not host
programs (it rejected the editor packages of 0.3.3), so the editor is only on the GitHub releases
page, which the plugin's mod page points to. `tools/thunderstore.sh <folder>` builds
`WorldEditorBridge-<version>.zip` from `tools/thunderstore/bridge/` (`manifest.json` and the mod
page `README.md`), the editor's `CHANGELOG.md` (made into the plugin's changelog by
`tools/thunderstore/changelog.py`, with links to the matching editor on GitHub) and `wwwroot/icon.png`, with the DLL of the
release packages (reused when that version is already in the folder). It checks Thunderstore's
rules (name, 250-character description, 256x256 icon). Upload the zip at
https://thunderstore.io/c/valheim/create/.

## Versions

One version number for everything: the `VERSION` file. `Directory.Build.props` reads it into the
editor and the plugin (`BuildInfo.Version`: the start page, the window title, the plugin's BepInEx
version and its `/status`), and both release scripts name their packages after it. For a release:

1. Raise `VERSION` (Major.Minor.Patch).
2. Add a `## v<version>` section to `CHANGELOG.md` (a test checks it). Put changes to the plugin
   under a `### WorldEditorBridge` part of that section. The Thunderstore package's changelog is made
   from it: each version shows the plugin's part (or "unchanged") and then the editor's.
3. Keep the BepInExPack dependency in the three `tools/thunderstore/*/manifest.json` current.
4. `tools/release.sh <folder>` and `tools/thunderstore.sh <folder>`; tag `v<version>`, publish the
   GitHub release, upload the Thunderstore zip (Thunderstore refuses a version it already has).

## Files extracted from the game

The in-game look uses files that belong to the game, so they are not in git (see `.gitignore`) or
in the release packages:

- `wwwroot/terrain/*.png` and `heightmap.frag.glsl`: terrain textures and the converted terrain shader;
- `wwwroot/maptex/`: map textures;
- `wwwroot/models/`: building, tree, rock and bush models, their textures and `objects.json`.

The app runs the exporter by itself (`App/GameLook.cs`): it finds Valheim in the Steam libraries
(or the folder the user chose), runs `export-game-files/export_all.py` with the bundled Python
runtime into the per-user `game-look` folder (served after `wwwroot`), and runs it again when
Steam's build id of the game changes. `tools/make-python-runtime.sh` builds that runtime, then
`tools/python-runtime/trim.py` removes every file a full export does not use: the list of what stays
is `tools/python-runtime/keep-<linux-x64|win-x64>.txt` (plus the `encodings` package, the used
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
python3 tools/asset-export/export_all.py --valheim ~/.local/share/Steam/steamapps/common/Valheim --out wwwroot
```

| Option | Meaning |
|---|---|
| `--valheim` | The game client's folder (with `valheim_Data`). |
| `--out` | The editor's `wwwroot`. |
| `--objects all` / `world` / `none` | Models for every placeable kind (default), only the kinds in the save given with `--world <world folder>`, or build pieces only. |
| `--only terrain` / `map` / `models` | Run only some steps (repeatable). |
| `--work` | Cache folder (default `<out>/../export-cache`). |

What it does:

1. Reads every asset bundle once and records where the terrain material, the terrain texture
   arrays, the map material and each prefab's root object are (cached in `--work/scan.json`).
2. **Terrain:** the textures of the `Heightmap` material, the diffuse and normal texture arrays
   stacked into vertical strips, and the OpenGL core build of the `Custom/Heightmap` shader, one
   deferred-pass fragment variant converted for WebGL 2 (`look.js` adds its own `main()`). If a game
   update changes the shader, the converter stops with a message instead of writing a broken file.
3. **Map:** the textures of the `minimap` material. The map shader itself is hand-ported in
   `wwwroot/mapview.js`.
4. **Models:** `export_pieces.py` (meshes, textures, materials; incremental, so an interrupted run
   continues), then `fix_normals.py` (Unity's DXT5nm normal maps to plain RGB) and `fix_alpha.py`
   (bleeds the colour of cut-out textures into their transparent pixels), then `objects.json`.

A full run takes a few minutes and writes about 150 MB. The terrain and map output was checked to
be byte-identical to the files made by hand during development.

The catalogues that are in git are made by `scan_pieces.py` (`WorldGen/pieces.json`, with each
piece's snap points),
`scan_modifiers.py` (`WorldGen/terrain-modifiers.json`) and `scan_prefabs.py`
(`WorldGen/prefabs.json`: also container sizes, ward radii and crafting station build ranges);
each takes the output file as argument and the bundle folder in `VWE_BUNDLES`.
`scan_vegetation.py WorldGen/vegetation.json` makes the game's vegetation rules for Regrow nature
(ZoneSystem's and the location lists', with the random draws each kind makes when it is created).
`RegrowProbe` (with `REALWORLD=<world folder> OUT=<file>`) reports how many saved trees and rocks of a
world the game generated sit where Regrow puts them (61 % on a played Meadows / Black Forest world;
the rest is mostly the game's physics check against what it placed before, which the editor cannot do).
`scan_grown.py WorldGen/prefabs.json` (run after `scan_prefabs.py`) adds what each sapling grows into
(grown crops and trees keep their sapling's grow radius).
`scan_build_tools.py WorldGen/pieces.json WorldGen/prefabs.json` (run after the other two) adds
which build tool's menu has each piece (hammer, hoe, cultivator, feaster): the editor writes a
builder on new pieces of those kinds.
`scan_zdo_keys.py <Valheim folder> WorldGen/zdo-keys.json` makes the names of object data keys for
the object inspector, from the string literals of `assembly_valheim.dll` (the save only keeps their
hashes).

## Tests

CircleCI (`.circleci/config.yml`) runs everything below on every push and pull request.

| Job | What it checks |
|---|---|
| `dotnet-tests` | `tests/WorldEditor.Tests` (xUnit): the save round trip on the test world (ground edits, deleted and new objects, blank prefabs, copies and moves, zone reset, the 8 m limit, zones that are not generated), the prefab catalogue, live-mode bookkeeping against a stand-in bridge (apply only the difference, undo after applying), the 10-second connection check, `servers.cfg`, plugin settings, world and folder discovery. |
| `build-app` + `browser-tests` | `tests/browser`: the real app (`--browser`) on the test world in headless Chrome: start page, map, 3D editor, a brush stroke and Save, Plant line and grid, the move arrows and End, zone selection, copy and paste, undo, back to Worlds, and no JavaScript errors. Box models stand in for the game's. |
| `packages` | Both release packages build, and `tools/check-package.sh` finds everything they need and no source code, debug files or game files. |

Not covered by CI: building the plugin and the game-look export (both need Valheim's own files) and
live mode against a real game.

The **test world** is `tests/fixtures/CITest`: a brand-new world made by a dedicated server and
filled live through the editor (ground edits, trees, rocks, bushes, pickables, a crop, floors, walls,
chests). `tests/fixtures/CITest.snapshot` is the same world as the plugin sends it.

Running them locally:

```sh
dotnet test tests/WorldEditor.Tests
dotnet publish TerrainEditor.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o out
cd tests/browser && npm ci && npm test        # APP=<program> to test another build
SKIP_PLUGIN=1 tools/release.sh /tmp/dist && tools/check-package.sh /tmp/dist/*.tar.gz --no-plugin
```

## Screenshots

`tools/docs-screenshots/` holds the scripts that took the pictures in `docs/images/`. They were made
for one particular world (places are given as world coordinates), so with another world change the
coordinates at the top of each script. Run them in order against an editor on a world copy:

```sh
for f in tools/docs-screenshots/[0-9]*.mjs; do node "$f" http://127.0.0.1:5191 docs/images; done
```
