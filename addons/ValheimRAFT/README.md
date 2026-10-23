# ValheimRAFT add-on

An add-on for the Valheim World Editor that teaches it about [ValheimRAFT](https://thunderstore.io/c/valheim/p/zolantris/ValheimRAFT/),
the mod that builds ships and land vehicles out of building pieces. The editor draws each vehicle whole, as the mod
builds it, and moves, copies and deletes it as one.

Not made by ValheimRAFT's author: the add-on only reads what the mod keeps in a world, following what the mod does
with it. ValheimRAFT itself is unchanged and stays in `BepInEx/plugins`; this repository holds none of its code.

## What it does

- **Draws vehicles whole.** The game saves every piece of a vehicle at the vehicle's place and the mod builds it again
  from where it sits on the vehicle; the editor draws each piece there, turned and tilted with the vehicle, and the
  pieces on a swivel with the swivel. Checked on a server running ValheimRAFT 5.0.9: on a sunk, tilted ship, every
  piece the editor draws stands where the mod built it, to a hundredth of a millimetre.
- **Moves, copies and deletes them whole.** Selecting any piece takes the whole vehicle, as with any
  [linked objects](../../docs/select.md#ships-and-other-linked-objects): moving, turning, deleting, the Area tool's
  Remove objects, copies and blueprints. Each pasted vehicle gets ids of its own; its ropes, its switches' swivels,
  its sails' masts and its plants follow the copy, and a rope to something that was not copied is let go. Checked on
  a server running the mod: a ship pasted and saved by the editor is built by the mod where the editor draws it, its
  ropes tied to its own pieces.
- **Draws the sails and ropes** the mod makes while the game runs, which the game's files have no model of: custom
  sails as their cloth between their corners, in their colour, and ropes from each rope anchor to what it is tied to.
- **Names the mod's values** in the inspector (the vehicle a piece is built on, its place there, a sail's corners and
  colours), with a rope anchor's ropes and a sail's colours in words. Values the mod sets itself are read-only.
- **Sorts the mod's pieces** in the Workshop's Build panel (**Mods** tab) under the vehicle hammer's own headings:
  Hull, Structure, Propulsion, Power and mechanisms, Vehicles, Tools and Old hull pieces. The hull boundary tools,
  which delete themselves once placed, are not offered.

## Limits

- **Swivels are drawn at rest.** The pieces on a swivel are drawn where the swivel holds them before it turns.
- **Rope ladders** are drawn without their rope, which the mod hangs while the game runs.
- **Ropes to a vanilla ship or an animal** end at its middle, not at the tie point the mod picks on it.
- **Carts and animals aboard** are not part of the vehicle: they are moved and copied on their own. A copy of one
  without its vehicle is no longer carried by it.
- **A land vehicle parked on a ship** (an experimental feature of the mod) is not drawn in place.
- **Old saves.** Ropes saved by older versions of the mod (by object, not by id) are counted in the inspector but not
  drawn, and a copy lets go of them. Pieces whose turn older versions kept as a quaternion are read too. The first
  rafts (`MBRaft`, removed from the mod in 3.0) are not loaded by the mod any more.
- **Live mode** adds nothing of its own: edits are applied as any other, and the mod builds a moved or pasted vehicle
  again when its area loads. Not checked in a game yet.

## Install

1. The Valheim World Editor 1.110.0 or later.
2. From the editor's [Releases](https://github.com/Shuiei/valheim-world-editor/releases) page,
   `ValheimWorldEditor-addon-ValheimRAFT-<version>.zip`: unpack it into the `addons` folder next to the editor (on
   macOS, next to `ValheimWorldEditor.app`), so that you have `addons/ValheimRAFT/worldeditor.json`.
3. The add-on is used while ValheimRAFT (`ValheimRAFT.dll`) is in one of the BepInEx plugins folders the editor knows
   (**Settings**, BepInEx folders). Settings lists it under **Add-ons**, with its state.

## Building and testing

From the repository's root, with the .NET 10 SDK:

```
dotnet build addons/ValheimRAFT/src/ValheimRAFT.WorldEditor/ValheimRAFT.WorldEditor.csproj
dotnet test --project addons/ValheimRAFT/tests/ValheimRAFT.WorldEditor.Tests
tools/addons.sh dist                    # dist/ValheimWorldEditor-addon-ValheimRAFT-<version>.zip
```

The tests read a ship the real mod built, saved and built again on a local server
(`tests/ValheimRAFT.WorldEditor.Tests/fixtures/valheimraft-reference.txt`); the editor's own tests
(`editor/tests/AddOns.Tests/ValheimRAFT`) check that the editor draws each of its pieces where the mod built it. The
add-on's version is `VERSION` here, its changes `CHANGELOG.md`: it goes out with every editor release, under its own
version.
