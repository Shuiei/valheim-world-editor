# Tunnelheim add-on

An add-on for the Valheim World Editor that teaches it about [Tunnelheim](https://thunderstore.io/c/valheim/p/Tunnelheim/Tunnelheim/), the mod that digs real tunnels with the
pickaxe. The editor draws a world's tunnels as the game builds them.

Not made by Tunnelheim's author: the add-on only reads the records the mod keeps in a world, following what the mod
does with them. Tunnelheim itself is unchanged and stays in `BepInEx/plugins`; this repository holds none of its code.

## What it does

- **Draws the tunnels** in the 3D view, from Tunnelheim's records and checkpoints: each 16 m column the mod rebuilt is
  replayed as the game replays it (bit for bit against Tunnelheim 1.0.0) and meshed as the mod meshes it, with the
  game's ground look and the mod's paint; the editor's ground is cut away over those columns, as the mod cuts the
  game's.
- **A Tunnel tool**, under **Mods** on the rail: pick an action, then click the ground or the rock.
  - The mod's own: **Dig (round)** and **Dig (square)** as the pickaxe digs (Size: the pickaxe's, 1 antler to 1.75
    black metal, 2.5 the tunneller), **Add Earth** (a lump a metre round, not above the building height), **Smooth
    Surface**, **Level Wall** (facing you) and **Level Ceiling**.
  - Shapes made of the same edits: **Bore** (click its start, then its end; a round tunnel of the Size's radius),
    **Room** (click the middle of its floor in the rock; width, depth and height), **Shaft** (straight down from the
    click) and **Stairs** (down the way you look).

  Each click is one step of the history. The edits go into the mod's records as the game saves its own: each kept
  by every 16 m column it changed, a column dug for the first time getting a record with the ground it was dug from.
  Checked on a server running the mod: the game rebuilds the same rock, to the bit, as the editor draws.
- **Tunnels go with copies and blueprints**: copying an area (or the Select tool's objects) keeps the edits dug there,
  and pasting plays them again where each copy lands, turned and mirrored with it, kept by the records already there
  (never a second record in a column). Checked on a server running the mod: it builds the pasted tunnels as the editor
  draws them. What the mod has baked into a column's checkpoint is no longer a list of edits and is not copied.
- **Shows tunnel records in words** in the inspector.

Planned: live editing, and the mod's own colours in the Deep North, the Mistlands and the Ashlands.

## Install

1. The Valheim World Editor 1.98.0 or later.
2. From the editor's [Releases](https://github.com/Shuiei/valheim-world-editor/releases) page,
   `ValheimWorldEditor-addon-Tunnelheim-<version>.zip`: unpack it into the `addons` folder next to the editor (on
   macOS, next to `ValheimWorldEditor.app`), so that you have `addons/Tunnelheim/worldeditor.json`.
3. The add-on is used while Tunnelheim (`ValheimTunneling.dll`) is in one of the BepInEx plugins folders the editor
   knows (**Settings**, BepInEx folders). Settings lists it under **Add-ons**, with its state.

## Building and testing

From the repository's root, with the .NET 10 SDK:

```
dotnet build addons/Tunnelheim/src/Tunnelheim.WorldEditor/Tunnelheim.WorldEditor.csproj
dotnet test --project addons/Tunnelheim/tests/Tunnelheim.WorldEditor.Tests
tools/addons.sh dist                    # dist/ValheimWorldEditor-addon-Tunnelheim-<version>.zip
```

The tests replay records taken from the real mod on a local server and compare every chunk's density, paint and mesh
with what the game built (`tests/Tunnelheim.WorldEditor.Tests/fixtures/tunnelheim-reference.txt`). The add-on's
version is `VERSION` here, its changes `CHANGELOG.md`: it goes out with every editor release, under its own version.

Known limits: the building height Add Earth and the smoothing tools use is the mod's default (64 m), not the server's
setting; rock in the Deep North, the Mistlands and the Ashlands has the default palette, not the mod's own colours
there.
