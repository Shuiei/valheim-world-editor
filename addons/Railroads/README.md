# Railroads add-on

An add-on for the Valheim World Editor that teaches it about [Railroads](https://thunderstore.io/c/valheim/p/VladasZ/Railroads/),
VladasZ's mod of stone track, coal trains and stations. The editor draws a world's track, stations and trains as the
game builds them, and copies of them pasted with the editor work in the game.

Not made by Railroads' author: the add-on reads the mod's own model files from your copy of the mod, and the data the
mod keeps in a world, following what the mod does with them. Railroads itself is unchanged and stays in
`BepInEx/plugins`; this repository holds none of its code or files.

## What it does

- **Draws track, stations and trains** in the 3D view. The mod builds them while the game runs from model files of
  its own, which no asset bundle holds, so the editor cannot find them by itself: the add-on reads those files from
  the `meshes` folder beside your `Railroads.dll` and draws every piece from them, in wood, stone and iron colours:
  each straight, curve, slope, switch, crossing, loop and junction, the station signs, consoles and old stations (with
  their legs), and the locomotive, wagons, flatbed and hand cart on their wheels. The levers, rods, lamps and the
  wagons' loads are left out.
- **Names the mod's data** in the inspector (station name, coal, wagon sign, restaurant name and the rest), and shows
  in words what a train keeps as bytes: the track lines it stands on and runs along, its wagons and the later legs of
  its trip. What the mod sets itself is shown but not editable.
- **Pasted copies work in the game.** The mod keeps world places and the game's object ids in its data, which name
  the originals; a pasted copy is made a piece of its own:
  - track keeps the turn it is pasted with (the mod turns each piece back to the turn it keeps as the world loads);
  - a locomotive, wagon or hand cart leaves the original's place on the track, trip, wagons and driver behind and
    keeps its coal, sign and load: the mod puts it on the track nearest where it stands (within 1.5 m) when a player
    comes near, as one just built;
  - a flatbed gets a deck of its own, and a piece built on a deck is pasted as an ordinary piece where it lands;
  - a station sign keeps its name but not the original's stop (see below).
- **Groups the pieces** in the Workshop's Build panel (**Mods** tab) as Track, Stations and Trains; the pieces the mod
  retired (its first station, its 6 m switches) are not offered, those in worlds still show.

## Limits

- **Station signs pasted or placed with the editor are no stop.** The mod finds a sign's stop on the track once, when
  it is built with the hammer. Build the sign again in the game.
- **Trains stay on their track.** A locomotive or a hand cart (and the wagons it pulls) moved with the Select tool is
  put back where the mod's track says when the game runs it; drive it there in the game, or paste a copy and delete
  the original.
- **Track turned with the Select tool turns back.** The mod gives each piece back its exact turn as the world loads.
  Moving track works; to turn it, paste a turned copy and delete the original.
- **A coupled train is not copied as one.** Pasted wagons stand loose: couple them again in the game.
- Each piece also shows the editor's grey box for things it has no model of (the add-on draws around it), and is
  picked by that box.

## Install

1. The Valheim World Editor 1.110.0 or later.
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
repository): every model of it is read and drawn. The add-on's version is `VERSION` here, its changes `CHANGELOG.md`:
it goes out with every editor release, under its own version.
