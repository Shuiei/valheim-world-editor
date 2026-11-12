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
  each straight, curve, slope, switch, crossing, loop and junction, the buffer stop, the drawbridge (its leaves, chains
  and counterweights lowered or raised, as the mod keeps it), the station signs, consoles, old stations (with their
  legs) and the feedback post, the crossing lights (with their 2 lamps, unlit) and the crossing barrier (its
  red-striped arm raised, as with no train coming), the train workshop, and the locomotive (with the name boards on its
  boiler), wagons, flatbed and hand cart on their wheels, with their couplers. The levers, rods and lamps (wood or wisp
  torches) of the vehicles, the words on the boards and
  signs, the wagons' loads and what hangs on the locomotive's item stand are left out, and couplers stand straight out
  (in the game they turn towards the next vehicle on slopes). The pieces are picked by what is drawn.
- **Names the mod's data** in the inspector (station name, train name and number, coal, lamps' fuel, wagon sign,
  restaurant name, whether a drawbridge is raised, the torch at each of a vehicle's lamps, what a piece was paid with
  and the rest), and shows in words what a
  train keeps as bytes: the track lines it stands on and runs along, its wagons and the later legs of its trip. What
  the mod sets itself is shown but not editable.
- **Keeps pieces right wherever you put them.** The mod works some things out only when a player builds a piece
  with the hammer, or as the world loads; wherever the editor places, pastes, moves or turns a piece (any tool, a
  script or Claude), the add-on sets them as the mod would:
  - track, the drawbridge too, keeps the turn you give it (the mod gives each piece back the turn it keeps as the world
    loads, which would turn it back);
  - a locomotive, wagon or hand cart stands on the line of track nearest it (within 1.5 m), at that point and facing
    its way, stopped: the game runs it from there. With no track that near, the mod puts it on track when a player
    comes near, as a vehicle just built;
  - a station sign stops trains at the nearest point of straight track (or of a loop's circle) within 10 m, as when
    it is built, and is in the trains' station menu; never on a drawbridge, as in the game.
- **Trains and flatbeds stay whole.** A locomotive and the wagons it pulls are one train, a flatbed and
  what is built on its deck one deck, and a train takes its flatbed's deck along: selecting any of them and moving,
  turning or copying takes the whole train,
  deleting the locomotive takes its wagons, and the train goes on the track where you put it.
- **Pasted copies are pieces of their own.** The mod names other objects by the game's ids and ids of its own, which
  in a copy would name the originals: a pasted train pulls the copy's own wagons (the mod couples them as it does when
  a world loads) and leaves the original's driver, trip, sender and number behind (it keeps its name, coal, sign and
  load, and its wagons turned to face its back stay turned; the server gives it a number of its own, as to a train just
  built), a pasted flatbed carries the copy's own deck, a wagon pasted without its locomotive stands loose, and a piece
  pasted without its flatbed is an ordinary piece where it lands.
- **Groups the pieces** in the Workshop's Build panel (**Mods** tab) as Track (with the buffer stop), Stations, Trains
  and Other (the crossing lights and barrier, the train workshop); the pieces the mod retired (its 6 m switches, and the feedback post since Railroads
  0.4.5) are not offered, those in worlds still show. The old station, back on the hammer since Railroads 0.3.5, is
  offered again.

Checked on a dedicated server running Railroads 0.2.6 and the editor's plugin, live: the add-on puts vehicles on the
same lines and at the same points as the mod, and signs' stops at the same place; pasted, moved, turned and deleted
track and vehicles are applied in the game, keep their turn after a restart, and leave the original train as it was;
a train moved whole (its wagons and the floor on its flatbed with it) and a pasted coupled train stay on their track
and coupled after a restart, each flatbed with its own deck.

Then on a local headless server running Railroads 0.2.9, the version the add-on follows, on the world the tests'
reference was recorded on (with 0.2.6): the server numbers its train once it is loaded; a train pasted as the add-on
pastes it keeps its name and gets the next number, kept after a restart, where a copy keeping the original's number
would go by it for good. 0.2.7 to 0.2.9 change none of the track, the placing of vehicles and signs or the data the
add-on sets (checked by comparing the two versions); of the models, only the locomotive's (its name boards) and the
portal wagon's (no posts on its open side), which the add-on reads from your copy of the mod as they are.

Then on the same server and world running Railroads 0.3.6: a railway with 2
drawbridges, the first raised by its lever, which held a train sent past it until it was lowered again, a wagon the
mod put on the second, and a feedback post; after a restart both bridges kept the state they were saved in. The add-on puts vehicles on
the drawbridge's line as the mod does, and keeps a sign's stop off it. 0.3.0 to 0.3.6 change none of the track the
add-on knew, the placing of vehicles and signs or the data it set (checked by comparing the two versions); they add
the drawbridge and the feedback post, take the couplers off the vehicles' models (the mod builds them apart, from a
model of their own) and put the old station back on the hammer.

Railroads 0.3.7 to 0.4.0 change none of the models, the track, the placing of
vehicles and signs or the data the add-on knew (checked by comparing the versions' files, model by model, and their
data keys): a station sign's turn now picks the side of the ghost train, which the mod works out from the turn and keeps
nowhere; tame boars and vanilla carts ride on a flatbed under the keys of the pieces built on it. 0.4.0 adds the fitted
track, whose shape is in its values, as the mod's own code builds it: drawn and laid out as the mod does from those
values, it was not yet built in a game for these tests.

Railroads 0.4.1 changes none of the models, the track, the placing of vehicles and signs or the data the add-on
knew (checked by comparing the versions' files, model by model, and their data keys). It lays no line for a fitted
track with a shape no game builds (a far end more than 33 m away, a way shorter than half a metre or longer than one
and a half): the add-on draws such a piece as its own model and puts no vehicle on it, as the game does. It adds 2
gauges on the locomotive's boiler, which the add-on leaves out with the levers and rods, and keeps on the locomotive
the train's load (which the server weighs again every second) and a sent train's coal rate for its trip, which a
pasted or moved train leaves behind with the rest of its trip.

Railroads 0.4.9: 0.4.2 to 0.4.9 change none of the models the add-on read, the
track, the fitted track's bounds, the placing of vehicles and signs or the data it knew (checked by comparing the
versions' files, model by model, and their data keys). 0.4.4 keeps a wagon turned the way it was coupled, by its id on
the locomotive: a train pasted as the add-on pastes it names the copy's own turned wagons. 0.4.5 retires the feedback
post, 0.4.6 keeps on track the chains it was paid with, and 0.4.7 adds the crossing lights and barrier, whose lamps and
arm the mod moves only while a train comes, keeping nothing of it. Then on the local headless server and world of the
0.3.6 checks, running 0.4.9: the arm and lamps stand at rest where the add-on draws them; on a train written by a probe
as the add-on pastes it (its wagons with new ids, the last one turned), the mod kept that wagon turned, after a restart
too, where a copy still naming the original's turned wagon was turned back to face the train's way. The crossings were
not built in a game: that needs a player.

Railroads 0.5.0 is the version the add-on follows. 0.4.10 to 0.5.0 change none of the models the add-on read, the
track, the placing of vehicles and signs or the data it knew (checked by comparing the versions' files, model by model,
and their data keys). 0.4.13 changes where new supports end (on rocks, trees and pieces too), which the add-on never
builds; 0.4.14 lets track be paid with core wood, kept as before. 0.4.15 adds the buffer stop, track of its own: a beam
over the end of 0.6 m of track, which it lays from where it snaps onto a free track end. 0.5.0 adds the train workshop,
which keeps nothing, and the torch at each lamp of a locomotive or wagon (`bf_light0`, `bf_light1`: a wood torch,
none, or a wisp torch), which the workshop changes. Then on the local headless server and world of the 0.3.6 checks,
running 0.5.0, with track written as the editor writes it (an 8 m straight turned 30°, a buffer stop at each end, one
turned to face away): the mod laid each buffer stop's track where the add-on lays it, joined to the straight and
ending there, and offered neither end to a fitted track, after a restart too; a train workshop and a wagon with a wisp
torch and none were kept as written. No train was driven against a buffer stop: that needs a player.

## Good to know

- **Fitted track.** Since Railroads 0.4.0 one piece joins 2 track ends that do not line up, up to 32 m: the mod builds it
  unturned, its far end and the ways it leaves and reaches its ends in its values, and bends 1 m straights between them.
  Pasted or moved turned, the editor turns those values and leaves the piece unturned, as the mod would have built it
  there (with an editor before 1.119.0 the piece keeps its turn in the editor, so turn it once only before saving).

- **A train follows its track.** A train moved or turned as a block lands where you put it, and the game then lays
  its wagons out behind the locomotive along the track, as it does while the train runs: put it down on track that
  carries the whole train.
- **Supports are pieces of their own.** The poles and knees the mod builds under track and stations are the game's
  own pole pieces (the inspector says what they are made of and which part they are). The mod keeps no tie between
  them and the track, and leaves them standing when track is removed: the editor does the same. Select them with the
  track to move or copy them along (a box over both).
- **Train names.** Since Railroads 0.2.7 a train has a name, shown on the boards on its boiler, on the map and in the
  train console; without one it goes by the number the server gave it ("Train 3"). A pasted train keeps the original's
  name (trains may share one: rename it in the inspector or in the game) and is numbered anew. The server keeps the
  last number it gave on an object of its own at the world's centre, which the editor never draws or lists.
- **Station names.** A pasted sign keeps the original's name; give it its own in the game (the mod does not let two
  stations share a name when you name them there).
- **Drawbridges.** Since Railroads 0.3.1 a drawbridge carries 16 m of track between 2 stone towers; its levers raise
  its leaves for ships, and a train stops before a raised one until it is lowered. The editor draws it as it ends its
  swing (a swing takes 8 seconds of the game's time), and **Raised** in the inspector raises or lowers it: the mod
  itself raises it only with no train on it, so lower it before you put a train there.
- **Crossings.** Since Railroads 0.4.7 the crossing lights and barrier stand beside the track where a road crosses it:
  the lamps light in turns and the arm comes down across the road only while a train comes (within the server's
  `CrossingDistance` along the track, 50 m by default), and the mod keeps nothing of it. The editor draws them at
  rest, the lamps unlit and the arm up.
- **Buffer stops.** Since Railroads 0.4.15 a buffer stop closes a line: put it on a free track end, facing away from
  the track, and a train stops with its front against its beam. Its 0.6 m of track counts as the line's last piece
  (a vehicle can stand on it), and the mod offers its far end to no fitted track.
- **Torches.** Since Railroads 0.5.0 the train workshop changes the torches on a locomotive's or wagon's lamps: a wood
  torch, none, or a wisp torch, which burns no fuel and clears the mist round the train. **First lamp's torch** and
  **Second lamp's torch** in the inspector say which (0 wood, 1 none, 2 wisp; not set is wood) and change it, at no
  price: a torn down vehicle gives back the torches it has, as the mod pays for them.
- **Turned wagons.** Since Railroads 0.4.4 a wagon keeps the way it faced when it was coupled (a portal wagon can face
  either side of the track); the locomotive names the turned ones (**Turned wagons** in the inspector).

## Install

1. The Valheim World Editor 1.111.0 or later (1.119.0 or later to turn a fitted track more than once before saving).
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

Building the tests downloads Railroads 0.5.0, the version the add-on follows, from Thunderstore (never kept in the
repository): every model of it is read and drawn. The tests also replay objects the real mod made on a local server
(`tests/Railroads.WorldEditor.Tests/fixtures/railroads-reference.txt`: track, a station sign, a train sent to it with
a wagon and a flatbed carrying a floor, a hand cart), and track and an old station a player built, with their prices
and the supports the mod built under them (`railroads-supports.txt`), both with Railroads 0.2.6; and with 0.2.9, that
world's train named and numbered by the server, with locomotives made as copies are and the register of numbers
(`railroads-names.txt`); and with 0.3.6, the railway of its 2 drawbridges, the first raised and holding a train sent
past it (`railroads-bridge.txt`). The add-on's version is `VERSION` here, its changes `CHANGELOG.md`: it goes out with every
editor release, under its own version.
