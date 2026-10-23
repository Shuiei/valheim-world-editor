# Railroads add-on

An add-on for the Valheim World Editor that teaches it about [Railroads](https://thunderstore.io/c/valheim/p/VladasZ/Railroads/),
VladasZ's mod of stone track, coal trains and stations. The editor draws a world's track, stations and trains as the
game builds them, and the track, trains and station signs you place, paste, move or turn with the editor work in the
game as if built there, in a saved world or live.

Not made by Railroads' author: the add-on reads the mod's own model files from your copy of the mod, and the data the
mod keeps in a world, following what the mod does with them. Railroads itself is unchanged and stays in
`BepInEx/plugins`; this repository holds none of its code or files.

## What it does

- **Draws track, stations and trains** in the 3D view. The mod builds them while the game runs from model files of
  its own, which no asset bundle holds, so the editor cannot find them by itself: the add-on reads those files from
  the `meshes` folder beside your `Railroads.dll` and draws every piece from them, in wood, stone and iron colours:
  each straight, curve, slope, switch, crossing, loop and junction, the station signs, consoles and old stations (with
  their legs), and the locomotive, wagons, flatbed and hand cart on their wheels. The levers, rods, lamps, the
  wagons' loads and what hangs on the locomotive's item stand are left out. The pieces are picked by what is drawn.
- **Names the mod's data** in the inspector (station name, coal, lamps' fuel, wagon sign, restaurant name and the
  rest), and shows in words what a train keeps as bytes: the track lines it stands on and runs along, its wagons and
  the later legs of its trip. What the mod sets itself is shown but not editable.
- **Keeps pieces right wherever you put them.** The mod works some things out only when a player builds a piece
  with the hammer, or as the world loads; wherever the editor places, pastes, moves or turns a piece (any tool, a
  script or Claude), the add-on sets them as the mod would:
  - track keeps the turn you give it (the mod gives each piece back the turn it keeps as the world loads, which would
    turn it back);
  - a locomotive, wagon or hand cart stands on the line of track nearest it (within 1.5 m), at that point and facing
    its way, stopped: the game runs it from there. With no track that near, the mod puts it on track when a player
    comes near, as a vehicle just built;
  - a station sign stops trains at the nearest point of straight track (or of a loop's circle) within 10 m, as when
    it is built, and is in the trains' station menu.
- **Trains and flatbeds stay whole.** A locomotive and the wagons it pulls are one train, a flatbed and
  what is built on its deck one deck: selecting any of them and moving, turning or copying takes the whole train,
  deleting the locomotive takes its wagons, and the train goes on the track where you put it.
- **Pasted copies are pieces of their own.** The mod names other objects by the game's ids and ids of its own, which
  in a copy would name the originals: a pasted train pulls the copy's own wagons (the mod couples them as it does when
  a world loads) and leaves the original's driver and trip behind (it keeps its coal, sign and load), a pasted flatbed
  carries the copy's own deck, a wagon pasted without its locomotive stands loose, and a piece pasted without its
  flatbed is an ordinary piece where it lands.
- **Groups the pieces** in the Workshop's Build panel (**Mods** tab) as Track, Stations and Trains; the pieces the mod
  retired (its first station, its 6 m switches) are not offered, those in worlds still show.

Checked on a dedicated server running Railroads 0.2.6 and the editor's plugin, live: the add-on puts vehicles on the
same lines and at the same points as the mod, and signs' stops at the same place; pasted, moved, turned and deleted
track and vehicles are applied in the game, keep their turn after a restart, and leave the original train as it was.

## Good to know

- **A train follows its track.** A train moved or turned as a block lands where you put it, and the game then lays
  its wagons out behind the locomotive along the track, as it does while the train runs: put it down on track that
  carries the whole train.
- **Station names.** A pasted sign keeps the original's name; give it its own in the game (the mod does not let two
  stations share a name when you name them there).

## Install

1. The Valheim World Editor 1.111.0 or later.
2. From the editor's [Releases](https://github.com/Shuiei/valheim-world-editor/releases) page,
   `ValheimWorldEditor-addon-Railroads-<version>.zip`: unpack it into the `addons` folder next to the editor (on
   macOS, next to `ValheimWorldEditor.app`), so that you have `addons/Railroads/worldeditor.json`.
3. The add-on is used while Railroads (`Railroads.dll`) is in one of the BepInEx plugins folders the editor knows
   (**Settings**, BepInEx folders), with its `meshes` folder beside it as the mod installs it. Settings lists it under
   **Add-ons**, with its state.

## Building and testing

From the repository's root, with the .NET 10 SDK:

```
dotnet build addons/Railroads/src/Railroads.WorldEditor/Railroads.WorldEditor.csproj
dotnet test --project addons/Railroads/tests/Railroads.WorldEditor.Tests
tools/addons.sh dist                    # dist/ValheimWorldEditor-addon-Railroads-<version>.zip
```

Building the tests downloads Railroads 0.2.6, the version the add-on follows, from Thunderstore (never kept in the
repository): every model of it is read and drawn. The tests also replay objects the real mod made on a local server
(`tests/Railroads.WorldEditor.Tests/fixtures/railroads-reference.txt`: track, a station sign, a train sent to it with
a wagon and a flatbed carrying a floor, a hand cart). The add-on's version is `VERSION` here, its changes
`CHANGELOG.md`: it goes out with every editor release, under its own version.
