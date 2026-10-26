# Development

How the parts fit and the rules to keep when changing them: [architecture.md](architecture.md).

## Project layout

The repository has three parts with code: `editor/`, the editor itself (its core, the app, the live-mode plugin and
their tests); `sdk/`, what mod authors build against (the SDK, the bridge's API for mods' plugins, and the samples);
and `addons/`, editor add-ons for other people's mods (see [Add-ons in this repository](#add-ons-in-this-repository)).
The rest is shared: `docs/`, `tools/`, the CI files and `VERSION`, the one version of the editor, the plugin and the
SDK.

| Path | What it holds |
|---|---|
| `editor/Desktop/` | The app, `ValheimWorldEditor` (Avalonia window, 3D view and map drawn with OpenGL through Silk.NET). `Program.cs` (command line), `StartPage`, `MapPage` + `MapView` (the map), `MainWindow` (the 3D editor's page: top bar, tool rail, panels, keys) with `GlView` (the 3D view and its mouse), `WorldSession` / `WorldScene` / `EditSession` (the open world, the loaded area, its pending changes and history), one class per tool and panel (`AreaTool` + `AreaPanel`, `PlaceTool` + `PlacePanel`, `SelectTool` + `SelectPanel`, `PathTool`, `Sculpt`, `Erosion`, `Mask`…), `ModelStore` and `GameLookGl` (the game's models and terrain shader), `Prefs` (remembered choices), `Driver` (`--driver`, below). |
| `editor/Core/` | `ValheimWorldEditor.Core`, what is not about the window: |
| `editor/Core/App/` | Settings (`AppSettings`), the log (`Log`), the game's files (`GameLook`, `GameBundles`, `GameLookData`, `GameShader`, `GameTextures`), Regrow, search, blueprint formats, servers and SSH (`ServerConfig`, `Tunnel`), the local game (`LocalGame`), characters, zone statistics, folders (`Places`). |
| `editor/Core/Save/` | Save reader (`WorldSave`, `ValheimReader`), writer (`WorldWriter`), new worlds (`WorldCreator`), object building (`ZdoTools`: copies and blank objects), the `.db2` zone list, and live mode (`LiveBridge`, `LiveSync`). |
| `editor/Core/Editing/` | Pending changes: terrain per zone, deleted and added objects, zone resets (`EditStore`); heights of a block of zones (`HeightGrid`). |
| `editor/Core/WorldGen/` | Valheim's world generator (bit-exact base terrain), map data, location flattening; no game data (what the editor needs of the game, the terrain modifiers, prefabs, build pieces, pieces' support and placement, object data names, cave rocks, piece costs, vegetation rules and dungeon rooms, is read from the player's Valheim: `editor/Core/App/GameData.cs`). |
| `editor/plugin/WorldEditorBridge/` | The BepInEx plugin for live mode (.NET Framework 4.7.2). |
| `editor/tests/Core.Tests/`, `editor/tests/Desktop.Tests/` | Core's tests and the app's (xUnit v3, the app's in Avalonia's headless mode), laid out like `editor/Core/` and `editor/Desktop/`, see [Tests](#tests). `editor/tests/Support/` holds the helpers both share, `editor/tests/fixtures/` the test world. |
| `tools/WorldCheck/` | Developer checks of the generator and the writer, saved objects byte by byte, and new worlds from a seed, see below. |
| `tools/DocsScreenshots/` | `Scenes.cs` builds and takes the pictures in `docs/images/`, through `Editor.cs`, which drives the app. |
| `tools/release.sh`, `tools/check-package.sh`, `tools/sign-windows.sh` | The release packages, their check, and the Windows program's signature. |

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
`editor/Desktop/Driver.cs`; the main ones:

| Command | What it does |
|---|---|
| `gamedata <folder>` | The game's data from the tests' copies (`editor/tests/fixtures`), where there is no Valheim (CI): without it no world opens. |
| `world <folder>` / `area <zx> <zz> <size>` / `map` | Open a world (its map), an area in the 3D editor, back to the map. |
| `camera <x> <z> <yaw°> <pitch°> <distance>` | The 3D camera on world x, z. |
| `mouse <down\|move\|up> <x> <z> [button] [mods]`, `wheel`, `key <name> [mods]` | Real pointer and key events at world positions. |
| `click <text>` / `choose <text>` | The visible button or switch with that label / the entry so named in a visible drop-down or list box (also in dialogs). |
| `stroke`, `mapview`, `search`, `hit`, `zones`, `look` | A brush stroke, the map's place, its search (and a result picked) and zone filter, the View look switches. |
| `set <label>\|<value>`, `type <hint>\|<text>`, `panel`, `message [text]` | A slider, box or list beside a label; a text box by its placeholder; the right-hand panel shown; the status bar's message (none: cleared). |
| `picture <file.png>` / `shot <file.png>` | The view shown (3D or map) once it is drawn / the whole window, panels and view, as Avalonia's compositor draws it (never a capture of the screen). |
| `bounds <prefab>...` | Each prefab's model box in its own frame (its parts' meshes, the root's scale): the feet and sizes the dungeon generator places props by. Needs an area or the Workshop open. |
| `docs <page[#heading]>`, `docs search <words>`, `docs shot <file.png>`, `docs close` | The Help window at a place of the documentation (`map.md#controls`), its search, a picture of it, closed. |
| `note close` | The note on the locations near the area closed, as its × does. |
| `bench <seconds>` / `state` / `quit` | Frame rates while the camera turns / the state / quit without asking. |

`tools/DocsScreenshots/Editor.cs` wraps it for the documentation's pictures: it starts the app with
a stand-in home folder (none of this computer's characters, worlds or servers show; driven, the app
looks for Steam only in that home, so for the game's look its settings name this computer's Valheim), sends commands and saves `shot`s as JPEG, the status bar's message cleared first.
The scenes are in `tools/DocsScreenshots/Scenes*.cs`, split by documentation page (ground tools,
objects, building and dungeons, the pages around the 3D editor): each scene opens a fresh editor,
builds only what its pictures show (often a tool's settings side by side) and takes them, in worlds
made from seeds and never saved:

```sh
WorldCheck create <dir>/Docs Docs yjRO99yNTI 16 --flat     # the bare zone the tools are shown on (zone -10, -5)
WorldCheck create <dir>/Fjordheim Fjordheim Fjord2026 3     # a second world for the start page
dotnet run --project tools/DocsScreenshots -- <dir> [scene ...]  # all scenes, or only those named
```

Run it on a display of its own, never your desktop: `Xvfb :91 -screen 0 1920x1080x24 &`, then
`DISPLAY=:91 VGL_EGLLIB=/usr/lib64/libEGL.so.1 VGL_GLLIB=/usr/lib64/libGL.so.1 vglrun -d egl dotnet run --project
tools/DocsScreenshots -- <dir> …` (VirtualGL draws on the graphics card; Xvfb alone is very slow). Some scenes need
more: `xdotool` (clicks the driver cannot make), ImageMagick's `import` (an open list, which the window shot leaves
out), `area-heightmap` a `DBUS_SESSION_BUS_ADDRESS` naming no bus (so the file picker opens on that display), and
`settings` and `claude-history` port 5731 free (no other editor allowing Claude; or run them under `unshare -rn` with
the loopback up). Pages say where a picture is still to be taken with a `<!-- picture: … -->` comment
([writing the documentation](dev/writing-docs.md#pictures)).

The pictures go to `docs/images` (or `$DOCS_OUT`). `$DOCS_APP` names a released `ValheimWorldEditor`
to drive instead of the Debug build (the pictures of a release are taken with the release; put it in a
neutral folder such as `/tmp/ValheimWorldEditor`, since Settings shows its path). The red on ground
at the ±8 m limit is switched off in every scene (it would hide the edits).

A few things the driver cannot reach are done on the display with `xdotool` (Linux, X11): scrolling
a panel (the dungeon generator's plan, Settings, the found seeds). Without `xdotool` those pictures
show the panel's top. (The note on the locations near the area is closed with the driver's `note
close`.) Run it on a virtual display, never your own:

```sh
Xvfb :88 -screen 0 1920x1080x24 -nolisten tcp &
DISPLAY=:88 vglrun -d egl dotnet run --project tools/DocsScreenshots -- <dir>   # VirtualGL: the GPU instead of llvmpipe
```

(If VirtualGL stops with "undefined symbol: eglGetProcAddress", name the system's libraries:
`VGL_EGLLIB=/usr/lib64/libEGL.so.1 VGL_GLLIB=/usr/lib64/libGL.so.1`.)

Keep a picture clear: nothing in front of its subject (the tool panel stays on the left, so frame
the subject right of it), neither too close nor too far. At the usual yaw of 200° east is left on the
picture and north is near the camera.

## Building

With the .NET 10 SDK (`global.json` asks for it; the plugin builds with it too). The app targets
`net10.0`; the release packages carry the runtime.

```sh
dotnet publish editor/Desktop/ValheimWorldEditor.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimWorldEditor
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

The plugin: build `editor/plugin/WorldEditorBridge/WorldEditorBridge.csproj` with `ValheimDir` set to a
Valheim dedicated server's folder (it reads `valheim_server_Data/Managed`) and `BepInExDir` to a
BepInEx folder (its `core`), as MSBuild properties (`-p:ValheimDir=...`) or environment variables.

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
BepInEx version and its `/status`), and both release scripts name their packages after it. Each pull
request raises it: a feature the minor version, a fix the patch, with a `## v<version> — unreleased`
section in `CHANGELOG.md` (a test checks there is one). Put changes to the plugin under a
`### WorldEditorBridge` part of that section. The plugin package's changelog is made from it: each
version shows the plugin's part (or "unchanged") and then the editor's. Keep the BepInExPack
dependency in `tools/thunderstore/*/manifest.json` current.

## Releasing

CircleCI makes the release once you approve it (`.circleci/config.yml`, `.circleci/continue.yml`):

1. Open a release pull request titled **`Release v<version>`** (`VERSION`'s version): it dates the
   `CHANGELOG.md` headings to release (`## v1.25.1 — 2026-10-12` instead of `— unreleased`), when
   they are not dated yet. Optionally, write the GitHub release's notes in
   `docs/releases/v<version>.md`; without it the notes are the CHANGELOG's sections since the last
   release. Make its branch from main and name it `release/v<version>`: when it changes only
   `CHANGELOG.md` and `docs/releases/`, its pipeline runs no tests (its code is main's, tested as each
   pull request merged) and the checks main's ruleset requires pass at once.
2. Merge it (squash: main's commit is then `Release v<version> (#<number>)`). On main, that commit,
   with a dated heading for `VERSION` and no `v<version>` tag yet, makes the pipeline a release one:
   **hold-release** waits at once, before anything runs. Merging any other pull request, even one
   that bumps `VERSION` and dates its heading, runs main's full pipeline instead: the tests, code
   analysis and formatting, **coverage**, **packages**, **windows-smoke** and **windows-tests**. Only
   a release pull request releases. The tests are not run again:
   they ran on the pull request, on the same tree (main's ruleset keeps a pull request up to date
   with main before it merges).
3. In CircleCI, open that pipeline's workflow (`release`) and approve **hold-release**. Then
   **packages** builds the editor packages and **windows-smoke** tries the Windows one, as on every
   push to main, and:
   - **github-release** signs the Windows program and `spirv-cross.dll` (`tools/sign-windows.sh`:
     Authenticode, with the maintainer's Certum code-signing certificate through SimplySign's cloud,
     timestamped by Certum),
     builds the plugin's zip (against a Valheim dedicated server downloaded with SteamCMD and the
     BepInExPack its manifest names), tags the commit `v<version>` and publishes the GitHub release
     with the two editor packages the pipeline tested (the Windows one signed), the plugin, the SDK's zip
     and one zip for each add-on (`tools/addons.sh`, `ValheimWorldEditor-addon-<Name>-<its version>.zip`).
     Publishing signs the files (`.github/workflows/sign-release.yml`, Sigstore): each gets a
     `<file>.sigstore.json` a minute later.
   - **publish-thunderstore**, **publish-hexium** and **publish-nexus** then publish side by side
     (`tools/release/publish.cs`, with the sites' settings in `tools/release/sites.json`): the
     plugin, and only the plugin, on Thunderstore and Hexium (team `Tie`); on the Nexus Mods page, a
     new version of each of its three files (the plugin, the Windows and the Linux editor, repacked as
     a zip: Nexus takes no `.tar.gz`), with the notes as the mod's changelog, and every add-on's zip as
     a file of its own (`Valheim World Editor add-on - <Name>`, made at the add-on's first release, a
     new version when the add-on's own version changed, not downloadable by mod managers: it goes in
     the editor's addons folder, not the game's).

A version already on a site fails its job: no site takes a version twice. A failed job can be rerun
from CircleCI (Rerun from failed); the others are done. Still by hand: **unhide** the new version on
Hexium (its Versions page on hexium.gg; uploads arrive hidden and the API cannot unhide them), and
the Nexus page's description and pictures (no API changes them).

The release jobs' secrets are in CircleCI's **mod-token** context, restricted to the maintainer (only
they can approve): `GITHUB_TOKEN` (a GitHub token with Contents read and write on this repository),
`THUNDERSTORE_TOKEN` (a service account of team Tie, from old.thunderstore.io's team settings),
`HEXIUM_TOKEN` (a team API token, from the team page on hexium.gg), `NEXUS_API_KEY` (a personal
API key, nexusmods.com/settings/api-keys), and for the Windows signature `CERTUM_EMAIL` (the Certum
account) and `CERTUM_OTP` (the account's SimplySign TOTP seed: the `otpauth://` URI behind the QR
code SimplySign shows when a device is enrolled, as secret as a private key: with it and the e-mail
anyone can sign as the maintainer; to revoke it, enrol SimplySign again for a new one).

To check the publishing without publishing:
`dotnet run tools/release/publish.cs -- <thunderstore|hexium|nexus> <folder> --dry-run`, with the
packages in the folder and the site's token in the environment. It checks the token, that the version
is not on the site yet, the categories and the files.

By hand, without CI: `tools/release.sh <folder>`, `tools/sign-windows.sh <folder>/<the win-x64 zip>`
(with [ssign](https://github.com/Le-Syl21/ssign) installed, `CERTUM_EMAIL` and `CERTUM_TOKEN`, the
current six-digit code from the SimplySign app, in the environment) and `tools/thunderstore.sh <folder>`,
then tag `v<version>` and `gh release create` with every file (a file added after publishing is signed by
running Actions > Sign release by hand, with the tag), then `publish.cs` for each site.

## Add-ons in this repository

An add-on is editor support for someone else's mod, installed by the player into the `addons` folder next to the
editor ([modding.md](modding.md#add-ons)). The ones made here live in `addons/<Name>/`, each on its own:

| Path | What it holds |
|---|---|
| `addons/<Name>/src/<Project>/` | The extension: one netstandard2.0 project (net10.0 when it needs what only the editor's .NET has, as the Riverheim add-on loading the player's Riverheim, or the SDK's world-rules model, as the Expand World add-on) referencing `sdk/ValheimWorldEditor.Sdk` (never the editor), and its `worldeditor.json` (with `serves`, the mod's DLL, unless it looks for its mods itself). One DLL: the editor loads nothing else from the add-on's folder. |
| `addons/<Name>/tests/<Project>.Tests/` | Its own tests, against the SDK only (the Expand World add-on's also against the editor's Core and its tests' helpers and fixtures, to check its rules with the editor's generator), run by CI's `addon-tests` job with every other add-on's. |
| `addons/<Name>/VERSION`, `CHANGELOG.md` | Its own version and changes: the zip's name and the DLL's version. `Directory.Build.props` there reads it over the repository's settings. |
| `addons/<Name>/README.md`, `package-readme.txt` | What it does, for this repository; what the player reads in the zip (`README.txt`). |

`editor/tests/AddOns.Tests` checks every add-on in the editor itself, with nothing to add for a new one: it is
installed into an addons folder as its zip holds it, with a stand-in for the mod it serves; the editor reads its
manifest (its data holders known), loads its extension (running, without being allowed in Settings) and calls each of
its hooks on an empty area without turning it off. An add-on may add checks of its own there (`Tunnelheim/`: the
editor draws the reference records' rock, every mesh kept, the ground cut over exactly the columns the game rebuilt).

`tools/addons.sh <dist>` builds every add-on and zips each as players install it (a `<Name>` folder with the DLL, its
`worldeditor.json`, `README.txt` and the licence). Every release carries every add-on's zip beside the editor's
packages and the SDK's; an add-on whose version did not change goes out again as it was. Nothing of an add-on goes
into the editor's packages: the editor ships nothing for any mod.

## The game's files

The in-game look uses files that belong to the game, so they are never in git or in the release
packages, and nothing is copied: the app reads them from the user's own Valheim, straight from its
asset bundles (`valheim_Data/StreamingAssets/SoftRef/Bundles`), in C# with
[AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (the bundles carry their type trees, so
every field reads by name). `editor/Core/App/GameLook.cs` finds Valheim in the Steam libraries (or the folder
the user chose) and keeps the one reader, `editor/Core/App/GameBundles.cs`:

- **Index:** which bundle holds each prefab, from each bundle's table of contents (its AssetBundle's
  `m_Container`); a prefab name in two bundles is the one with a ZNetView. A few other assets are
  found there by file name (`GameBundles.NamedAssets`: the terrain material and texture arrays, the
  map material). Building it reads every bundle's table (about 5 s); it is kept in `game-index.json`
  in the data folder with each bundle's name, size and time, and made again when they change (a game
  update).
- **Models** (`editor/Desktop/BundleModels.cs` for `editor/Desktop/ModelStore.cs`): a prefab's parts (MeshFilter or
  SkinnedMeshRenderer with their materials, placed by the transforms), leaving out the worn and broken
  looks of pieces, lower levels of detail, inactive objects and, in dungeon rooms, their networked
  objects and random parts not there half the time. Meshes are decoded from their vertex streams; z
  is mirrored (Unity is left-handed). A small piece takes tens of milliseconds, the biggest dungeon
  room about 10 s, on a worker thread.
- **Textures** (`editor/Core/App/GameTextures.cs`): the mip level no larger than 1024 pixels, decoded to RGBA
  ([BCnEncoder.NET](https://github.com/Nominom/BCnEncoder.NET) for DXT1, DXT5, BC4, BC5, BC7); cut-out
  textures get the colour of nearby opaque pixels in their transparent ones, so filtering does not
  bleed the hidden colour into leaf edges.
- **Terrain and map** (`editor/Core/App/GameLookData.cs`, drawn by `editor/Desktop/GameLookGl.cs` and
  `editor/Desktop/MapView.cs`): the `Heightmap_basematerial` textures, the diffuse and normal texture arrays,
  the `minimap` material's textures, and the terrain shader (`editor/Core/App/GameShader.cs`): from the
  OpenGL core program one deferred-pass fragment variant converted to GLSL ES 3.0 (`GameLookGl` adds
  its own `main()`); Valheim for Windows has no OpenGL programs, so there the Vulkan one is taken
  (SMOL-V decoded by `editor/Core/App/Smolv.cs`, its uniforms' names put back from its parameter blob) and
  SPIRV-Cross turns it into the same GLSL (`editor/Core/App/TerrainShader.cs`). If a game update changes the
  shader, the converter says so instead of drawing a broken one.

`editor/tests/Core.Tests/Integration/GameFilesTests.cs` (`--filter-trait Category=Game`) checks the terrain and map read from
the game, where Valheim is on the computer.

None of the game's data is kept in the repository or the app: the editor reads what it needs from
the player's Valheim at its start (`editor/Core/App/GameData.cs`, one entry per kind; the readers are in
`editor/Core/App/GameCatalogues*.cs`, on the same reader as the models), in seconds, and keeps each kind in
the data folder (`game-<kind>.json`) until the game's files change. No world or Workshop opens without
it. The kinds found in the bundles are read together, in one pass over them (each bundle opened once
and given to each kind's reader, `GameCatalogues.ReadAtStart`; about 12 s for them all, the first
time and after a game update). The kinds:

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
- `piece-support`: each piece's material, centre of mass, whether it holds others and its colliders as
  boxes (those on a Rigidbody marked, as the game's support check leaves them out), for the Workshop's
  stability check (and Claude's `support_check`) and the blueprint pictures' stand-in shapes; the pieces
  with a `WearNTear` script on their root. The trees below the pieces are read once for `pieces`,
  `piece-support` and `piece-place` together (inactive branches and every collider kind too); of the
  mesh colliders' meshes only the bounds are kept here.
- `piece-place`: how the game's hammer places each piece, for the Workshop's Build (and Claude's
  `place_pieces`): its placement flags, snap points and the colliders the build ray meets (mesh
  colliders as their triangles: each mesh read once, its vertices and triangles kept, not its fields).
- `zdo-keys`: the names of object data keys for the object inspector, from the string literals of
  `assembly_valheim.dll` (the save only keeps their hashes).
- `cave-rocks`: the meshes of the rocks the Path tool's Cave hangs as its roof, from the two prefabs'
  bundle only (found with the game files' index; under a second).
- `piece-cost`: each piece's English name, station and resources, for the blueprint library and the
  Workshop's Build, found from each `Piece` script up to its prefab, with the English texts of
  `resources.assets`. Its test copy keeps the pieces it has and the items they take.
- `vegetation`: the game's vegetation rules for Regrow nature (and new worlds' generated middle),
  ZoneSystem's and the location lists', from the main scene's bundles only (found with the SoftRef
  manifest, as the location rules; only the scripts with vegetation read), then the prefabs' bundles
  for their names (about 1 s). Its
  test copy is a list: it keeps the rules it has, by name and prefab (all of them: Regrow's tests go
  over every biome).
- `dungeon-rooms`: the Dungeon tool's dungeons and rooms: each dungeon kind's themes, space and doors,
  and each room's size, openings and the objects the game makes in it with the random parts that
  decide them, found from the `Room` and `DungeonGenerator` scripts up to their prefabs. A room's
  tree is read raw, a level at a time; of its scripts only those that matter, where it is active, and
  the many `ZNetView`s and `RandomSpawn`s raw too (about 2 s). Location prefabs (`Location` script)
  are read the same way, for what the game makes of one it spawns (new worlds' generated zones). Its
  test copy keeps every dungeon kind, the rooms it has (those of the kinds the tests generate: the
  biomes' own and the Frost Caves) and the locations it has (the start temple, a crypt's entrance).
- `wake-draws`: the random draws a new object of each prefab makes as it wakes (its scripts' `Awake`, and
  `OnEnable` for an enabled one), in the order they wake, for Regrow nature and New world's location contents:
  a run of draws, a building piece, a LOD fade, a snow melter, a container's default items. Found from each
  drawing script (told by its class) up to its prefab, the tree read raw a level at a time (about 2 s);
  locations, rooms and dungeons are left out (their objects are listed alone). Its test copy keeps the prefabs
  it has.

The tests use trimmed copies, `editor/tests/fixtures/<kind>.json` (what they meet; the driver's
`gamedata <folder>` gives them to the smoke tests). After a game update, make them again with
`ValheimWorldEditor --export-test-data editor/tests/fixtures`, which keeps the entries each copy has.

The game's location rules (for the New world page's start, bosses and traders) are read by the editor
itself, in C# (`editor/Core/App/GameLocations.cs`), from the player's Valheim, and kept in the data folder
until the game changes; the editor keeps no copy of them. The tests, which run without the game, use
`editor/tests/fixtures/locations.json` (only the kinds the page shows): after a game update, make it again
with `ValheimWorldEditor --export-locations editor/tests/fixtures/locations.json`. The order of the game's
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
| `altbiomes <dump> <seed> [valheim folder]` | The biome areas and the alt biomes the editor lays out for a seed, line by line against those the game made (the AltDump plugin's `AltDump.txt`). |
| `locations <dump> <seed> [valheim folder]` | Every location the editor lays out for a seed against those the game laid out (`LocDump.txt`). |
| `contents <world> <BepInEx folder> <name part>` | New world's build of the locations whose contents Expand World Data changes (kinds whose name holds the part) against a world where the game made them: each object's prefab, place on the ground and data. |
| `regrow <world> <zones> [--no-alts] [--all-kinds]` | Regrow nature on zones the game generated (`Zones.txt`) against what the game put there: in alt biome areas and elsewhere, and per kind an alt biome adds or blocks (every kind with `--all-kinds`; `--no-alts` as if there were no alt biomes). |

The last three take their files from the AltDump test plugin (`_src/AltDump` in the workspace, next to TerrainCheck
and TerrainDump), run on a local dedicated server, never a live one: copy its dll into the server's
`BepInEx/plugins`, start the server with a new world in a scratch save folder and `-altdump-quit`
(`-savedir <scratch> -world AltTest -altdump-quit`), and it writes `AltDump.txt` when the game has laid out the
alt biomes, `LocDump.txt` when it has laid out the locations, then generates zones in alt biome areas and others
(`Zones.txt`), saves the world and stops. The seed's name is in the dump's first line. With `-altdump-loc <name
part>` it generates only the zones of the locations whose names hold the part (for `contents`, with Expand World
Data and a test location file in the server's BepInEx).

## Tests

The tests (xUnit v3) are in two projects, laid out like the code they test:

- `editor/tests/Core.Tests` tests `editor/Core/` without a window (save round trips, the generator against values
  recorded in the game, live mode against a stand-in bridge, settings, servers, the SSH tunnel), in
  `App/`, `Editing/`, `Save/` and `WorldGen/` as in `editor/Core/`.
- `editor/tests/Desktop.Tests` tests `editor/Desktop/`: the windows and panels run in Avalonia's headless mode,
  driven with real mouse and keyboard events.

Each test class is in a file of its own, named after the file it tests (`editor/Core/Save/WorldWriter.cs` is
tested by `editor/tests/Core.Tests/Save/WorldWriterTests.cs`). Several classes for one file are told apart by
what they test (`PlaceToolTests`, `PlaceToolMouseTests`, `PlaceToolEdgeTests`). Tests that go through
several files at once (window workflows, live mode, saving safely, the start-up, the visual tests) are
in each project's `Integration/` folder, named after what they test. The helpers are in `Support/`:
each project's own, and `editor/tests/Support/` for those both use (the test world `TempWorld`, `Fixtures`,
the stand-in game `FakeGame`, the `DataDir` collection), compiled into both projects.

- **Visual tests** (`[Trait("Category", "Visual")]`, in `Integration/*VisualTests.cs`) start the real app with
  `--driver` and check what it draws with OpenGL: one app per group of tests (`EditorProcess`),
  each test opening a fresh copy of the test world. They need a display and skip without one. One
  group starts the app with `VWE_GPU_BUDGET=1`: the models and textures on the graphics card past
  that many bytes (1 GiB by default) are let go when another area opens, so every area reads its
  models again. Their groups are non-parallel collections: each app runs alone, after the other tests,
  since its map keeps every core but one busy (on CI's two cores, under coverage, pictures timed out).
- **The user's files are never touched.** Module initializers (`TestFiles` in `editor/tests/Support`, and
  `TestApp` for the app's own) point the settings, servers, Place memory, stamps, preferences, saved
  selections and the data folder at temporary files before any test runs, and turn off the search of
  the computer's own Valheim and mod manager profiles. Tests that change the environment or these paths (`HOME`, `XDG_*`, the data
  folder) run in the non-parallel `DataDir` collection and put everything back. The visual tests'
  app gets `--data` and never copies the game look.
- **The SSH tunnel** is tested against a real `sshd` (`LocalSshd`): run as the test user on a free
  local port, with its own keys and settings in a temporary folder (never `~/.ssh`, never port 22).
  Without OpenSSH's `sshd` those tests skip, unless `VWE_NEED_SSHD=1` makes that a failure.

The **test world** is `editor/tests/fixtures/CITest`: a brand-new world made by a dedicated server and
filled live through the editor (ground edits, trees, rocks, bushes, pickables, a crop, floors, walls,
chests). `editor/tests/fixtures/CITest.snapshot` is the same world as the plugin sends it, and `editor/tests/fixtures/CITest42` the
same world as Valheim 1.0.20 saves it, in save format 42 (its dedicated server loaded CITest, one object of each chunk
file was moved and moved back live so that the game wrote every file again, then it saved).

The **game mods** the tests install into their BepInEx folders (Expand World Size, Data and Rivers, Riverheim, and
BepInEx's own pack for the BepInEx and Harmony assemblies Riverheim names, at the versions the editor follows) are
never kept in the repository: building a test project downloads them from Thunderstore (`editor/tests/TestMods.targets`) into
the system's temporary folder (`vwe-test-mods/<project>`), and the test run deletes them when it ends
(`editor/tests/Support/TestMods.cs`; CI deletes them after its last test process). Building the tests needs the network for
them. An add-on's tests may download only its own mod (`TestModsOwnList`, their own `TestMod` items: the Railroads
add-on's, Railroads itself, whose model files they read). Unity's classes Riverheim's plugin derives from come from a stand-in the Riverheim add-on's tests write (`addons/Riverheim/tests/Riverheim.WorldEditor.Tests/Support/UnityStandIn.cs`); those tests link `editor/tests/Support/TestMods.cs` and import `editor/tests/TestMods.targets`; the Expand World add-on's link all of `editor/tests/Support`, with a copy of YamlDotNet standing in for the one Expand World Data installs (`YamlStandIn`).

CircleCI (`.circleci/continue.yml`) runs on every push and pull request (`packages` and
`windows-smoke` on main only: they are slow, and the packaging they check seldom changes; `windows-tests` on main
and on the branches whose name has `windows` in it, so a Windows fix is checked before it is merged):

| Job | What it checks |
|---|---|
| `code-analysis` | `Desktop` (with `Core` and `Sdk`) and `WorldEditorBridge.Api` build with every analyzer warning an error, and the repository's XML files are well formed. |
| `formatting` | `tools/format-check.sh`: the five projects are formatted as `.editorconfig` says (`dotnet format whitespace` and `style`), on two containers. A pull request checks only the projects with a changed file (all of them when `.editorconfig`, `Directory.Build.props` or `global.json` changed); main checks all of them. |
| `core-tests` | `editor/tests/Core.Tests`, with OpenSSH's server installed (`VWE_NEED_SSHD=1`), on three containers (the world generator's tests run several times slower under coverage). |
| `app-tests` | `editor/tests/Desktop.Tests` on six containers, the visual tests included: they draw on a virtual screen (Xvfb, Mesa's software OpenGL), so CI checks the shapes they draw but not the game's look (CI has no Valheim: its editor draws plain colours). Both test jobs let CircleCI share the classes out by their last times (`circleci tests run`, each class a `--filter-class`), so **Rerun failed tests** runs only the classes that failed; every test runs, failing or not. A test stuck 5 minutes is dumped and stopped (`editor/tests/testconfig.json`); a crashed or stopped test process shows as one failed test of its class. |
| `addon-tests` | Every add-on, found by itself under `addons/`: `editor/tests/AddOns.Tests` installs each as a player does and has the editor load it and call its hooks (so a change of the editor or the SDK that breaks an add-on fails here), then each add-on's own tests (`addons/<Name>/tests`), then every add-on's zip as a release makes it (`tools/addons.sh`). One of the checks main's ruleset requires (with code-analysis, formatting, core-tests and app-tests); not in Codecov's figure. |
| `packages` | On main only: the release packages build (without the plugin), and `tools/check-package.sh` finds everything they need and no source code, debug files or game files. |
| `windows-tests` | `editor/tests/Core.Tests` on a Windows machine (the newest .NET 10 SDK, the style analyzers off: the formatting is checked on Linux), for the code paths only Windows takes: the profile folders, file locks, the paths' letter case. Its results are stored, so CircleCI names the failing tests. |

Not covered by CI: the game's look in the visual tests, building the plugin and reading the game's data
(both need Valheim's own files: the tests use their copies in `editor/tests/fixtures`), and live mode
against a real game.

Running them locally (they run on Microsoft.Testing.Platform, as `global.json` says: `dotnet test`
takes the project with `--project`, and xUnit's own filters):

```sh
dotnet test --project editor/tests/Core.Tests                                    # Core's tests
dotnet test --project editor/tests/Desktop.Tests                                 # the app's (visual tests need a display)
dotnet test --project editor/tests/Desktop.Tests --filter-not-trait Category=Visual  # without the visual tests
dotnet test --project editor/tests/Desktop.Tests --filter-trait Category=Visual      # only them
dotnet test --project editor/tests/Core.Tests --filter-class '*.WorldWriterTests'    # one class
for p in Core.Tests Desktop.Tests; do                                     # with coverage (Cobertura)
  dotnet test --project editor/tests/$p --filter-not-trait Category=Visual --coverage --coverage-output-format cobertura \
    --coverage-settings "$PWD/tests/coverage.runsettings" --coverage-output /tmp/coverage/$p/coverage.cobertura.xml
done
tools/release.sh /tmp/dist && tools/check-package.sh /tmp/dist/*.tar.gz
```

**Coverage**: on `main` only, each test container keeps its coverage, and once `core-tests` and
`app-tests` have both passed, the `coverage` job merges all nine into one report (`tools/MergeCoverage`,
whose total its log shows: a line's hits added up, its branches all taken when one report took them all) and sends that one file to
[Codecov](https://app.codecov.io/gh/Shuiei/valheim-world-editor) (`codecov.yml`), which checks each of main's commits. A failed test sends nothing, so Codecov keeps the last full
coverage rather than show part of it (rerun main's workflow from the start, not "Rerun failed tests",
which would send only the rerun classes' coverage): `codecov/project` fails when the editor's line coverage is under
**95%**. Pull requests run the tests without coverage, several times faster, so they get no coverage
check: a drop shows on `main` once merged. Codecov's page shows each file's covered and uncovered
lines.

The coverage counts only our own code (`editor/tests/coverage.runsettings`, for both test projects): the core
and the app, not the tests, the libraries or generated code, and not the FastNoise library either, which
the test against the game's values checks along with the world generator. Code that CI cannot run (it
needs a display, the game's files or a real game) can be left out with
`[ExcludeFromCodeCoverage(Justification = "...")]`. A test that starts the editor in a process of its own
starts the copy in the tests' folder (`EditorApp.Path`): the collector measures that process too, but one
started from `editor/Desktop/bin` would replace the test process's coverage of the app with its own.

## Fuzzing

The readers of files that come from outside the editor (blueprints, saves, live snapshots, heightmap
pictures) are fuzzed: given millions of made-up and damaged inputs, each must read the input or refuse
it as damaged (`InvalidDataException`, `EndOfStreamException`, `FormatException`, `JsonException`;
the `inventory` target also `NotSupportedException`, for an older item format the editor does not
read). Any other exception, a hang or a huge allocation is a bug.

- **`editor/tests/Fuzz`**: one target per reader, run by libFuzzer through
  [SharpFuzz](https://github.com/Metalnem/sharpfuzz): `blueprint`, `homestead`, `zdo`, `inventory`,
  `dungeon`, `zonedb`, `snapshot`, `chunk`, `png`. Each has its seeds (`editor/tests/Fuzz/seeds/<target>`,
  taken from the test fixtures) and a dictionary of its format's words (`editor/tests/Fuzz/dict`).
- **`.clusterfuzzlite/`**: how [ClusterFuzzLite](https://google.github.io/clusterfuzzlite/) builds them.
  It has no .NET of its own, so each target is a native libFuzzer binary: libfuzzer-dotnet compiled
  with `editor/tests/Fuzz/target.cc`, which points it at the .NET program and the target's name
  (`project.yaml` says `c++`). Only `ValheimWorldEditor.Core.dll` is instrumented.
- **Every night on main** (`.github/workflows/cflite_batch.yml`): an hour over every target. A crash
  fails the run, shows in code scanning (category `clusterfuzzlite`) and its input is the run's
  artifact. Not on pull requests: it takes too long; run the workflow by hand from the Actions tab to
  fuzz a change first. `cflite_cron.yml` prunes the corpus once a week.
- **`FuzzPropertiesTests`** (FsCheck, `editor/tests/Core.Tests/Integration`) checks the same readers in the
  test suite on every pull request: made-up and cut-short inputs are read or refused, and what the
  editor writes reads back.

To fuzz on your computer, with OSS-Fuzz's helper (`git clone https://github.com/google/oss-fuzz`) and
Docker or Podman:

```sh
python3 oss-fuzz/infra/helper.py build_image --external <repository>
python3 oss-fuzz/infra/helper.py build_fuzzers --external --sanitizer address <repository>
python3 oss-fuzz/infra/helper.py check_build --external --sanitizer address <repository>
python3 oss-fuzz/infra/helper.py run_fuzzer --external --corpus-dir=<folder> <repository> blueprint -- -max_total_time=300
```

`build_image` copies the sources in: run it again after a change. With SELinux (Fedora and the like)
the helper's own folders cannot be read in the container; run the runner yourself with `:z` mounts:
`podman run --rm --privileged -v <out>:/out:z -v <corpus>:/corpus:z gcr.io/oss-fuzz-base/base-runner
/out/<target> /corpus -dict=/out/<target>.dict`. To reproduce a crash, give its input file in place
of the corpus folder.

A crash found is fixed in the reader (a damaged input refused with `InvalidDataException`), and its
minimized input (`-minimize_crash=1`) becomes a test in `FuzzFindingsTests`
(`editor/tests/Core.Tests/Integration`). A new reader of outside files gets a target in
`editor/tests/Fuzz/Program.cs` and the `TARGETS` list of `.clusterfuzzlite/build.sh`, seeds and a
dictionary, and a property in `FuzzPropertiesTests`.
