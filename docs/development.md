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
| `tools/tests/` | Browser tests (Puppeteer). |
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

## Files extracted from the game

The in-game look uses files that belong to the game, so they are not in git (see `.gitignore`) or
in the release packages:

- `wwwroot/terrain/*.png` and `heightmap.frag.glsl`: terrain textures and the converted terrain shader;
- `wwwroot/maptex/`: map textures;
- `wwwroot/models/`: building, tree, rock and bush models, their textures and `objects.json`.

The app runs the exporter by itself (`App/GameLook.cs`): it finds Valheim in the Steam libraries
(or the folder the user chose), runs `export-game-files/export_all.py` with the bundled Python
runtime into the per-user `game-look` folder (served after `wwwroot`), and runs it again when
Steam's build id of the game changes. `tools/make-python-runtime.sh` builds that runtime.

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

The catalogues that are in git are made by `scan_pieces.py` (`WorldGen/pieces.json`),
`scan_modifiers.py` (`WorldGen/terrain-modifiers.json`) and `scan_prefabs.py`
(`WorldGen/prefabs.json`); each takes the output file as argument and the bundle folder in
`VWE_BUNDLES`.

## Tests

The browser tests in `tools/tests` run against a running editor on a **copy** of a world, never the
real one:

```sh
cp -r ~/worlds/MyWorld /tmp/world-copy
VWE_NO_OPEN=1 ./ValheimWorldEditor /tmp/world-copy --browser --port 5191 &
node tools/tests/test-editor.mjs http://127.0.0.1:5191 /tmp/out
```

They need `npm install puppeteer`; headless Chrome draws with SwiftShader, so they are slow.

## Screenshots

`tools/docs-screenshots/` holds the scripts that took the pictures in `docs/images/`. They were made
for one particular world (places are given as world coordinates), so with another world change the
coordinates at the top of each script. Run them in order against an editor on a world copy:

```sh
for f in tools/docs-screenshots/[0-9]*.mjs; do node "$f" http://127.0.0.1:5191 docs/images; done
```
