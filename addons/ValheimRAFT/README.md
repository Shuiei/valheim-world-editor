# ValheimRAFT add-on

An add-on for the Valheim World Editor that teaches it about [ValheimRAFT](https://thunderstore.io/c/valheim/p/zolantris/ValheimRAFT/),
the mod that builds ships and land vehicles out of building pieces. The editor draws each vehicle whole, as the mod
builds it, and moves, copies and deletes it as one, in a saved world or live.

Not made by ValheimRAFT's author: the add-on only reads what the mod keeps in a world, following what the mod does
with it. ValheimRAFT itself is unchanged and stays in `BepInEx/plugins`; this repository holds none of its code.

## What it does

- **Draws vehicles whole.** The game saves every piece of a vehicle at the vehicle's place and the mod builds it again
  from where it sits on the vehicle; the editor draws each piece there, turned and tilted with the vehicle:
  - the pieces on a **swivel** on its platform, as the mod left it: turned by its angles, or moved by its offset;
  - a **sail on a custom mast** on the mast's yard, where the mod hangs it;
  - a **land vehicle parked on a ship** where the mod builds it when the world loads: at the ship's place, with its
    own turn;
  - what a vehicle **carries** (a cart, an animal) where it was saved.

  Checked on a server running ValheimRAFT 5.1.1, as on 5.0.9 before it: on tilted ships, every piece the editor draws
  stands where the mod built it, to a few hundredths of a millimetre.
- **Moves, copies and deletes them whole.** Selecting any piece takes the whole vehicle, as with any
  [linked objects](../../docs/select.md#ships-and-other-linked-objects): moving, turning, deleting, the Area tool's
  Remove objects, copies and blueprints. What a vehicle carries goes with it. Each pasted vehicle gets ids of its own;
  its ropes, sails, switches and plants follow the copy, and a rope to something that was not copied is let go. A
  pasted vehicle is not docked (the mod would hold it at the original's dock clamp) and the mod works out its hull
  shape again (a mirrored copy's would be the original's).
- **Live too.** Connected to a game or server, moving, turning, pasting and deleting a vehicle reach the game as any
  other change. Checked on a server running the mod: it builds a moved or pasted ship where the editor draws it, the
  pasted ship's ropes tied to its own pieces and what it carries with it; a deleted ship leaves nothing behind. Needs
  WorldEditorBridge 1.110.0 or later in the game, so that the mod finds a pasted ship's pieces by their ids at once.
- **Draws what the mod makes while the game runs**, which the game's files have no model of: custom sails as their
  cloth between their corners, in their colour; ropes from each rope anchor to the tie point the mod picks on what it
  is tied to (another anchor, the dock, a Karve's or a longship's rope points, an animal's neck); rope ladders with
  their ropes and rungs, down to the ground or 2 m into the water.
- **Names the mod's values** in the inspector (the vehicle a piece is built on, its place there, a swivel's settings,
  a sail's corners, mast and colours, a vehicle's build mode, hull shape and the dock clamp it is docked at), with a
  rope anchor's ropes and a sail's colours in words. Values the mod sets itself are read-only.
- **Sorts the mod's pieces** in the Workshop's Build panel (**Mods** tab) under the vehicle hammer's own headings:
  Hull, Structure, Propulsion, Power and mechanisms, Vehicles, Tools and Old hull pieces. The hull boundary tools and
  the build mode toggle, which delete themselves once placed, are not offered.

## Limits

What the game's saved data cannot tell:

- **A swivel following the wind** is drawn at rest: the mod turns it to the wind while the game runs, and saves no
  angle.
- **A rope ladder** stops at the ground or 2 m into the water. The mod stops it at anything below, pieces and rocks
  too, which the editor does not test.
- **A rope tied to an animal** ends at its neck as the animal stands at rest; the game moves it as the animal walks.
- **Ropes kept the old way** (by an object's id, before version 2 of the mod's ropes) are not drawn and a copy leaves
  them out: Valheim gives every object a new id each time a world loads, so such a rope names nothing once the world
  has been loaded again. The mod drops them too.
- **A land vehicle parked on a ship** is drawn where the mod builds it when the world loads, not where it stood
  aboard when the world was saved, and keeps its own turn when the ship is turned, as in the game.
- **Deleting a vehicle** deletes what it carries too.
- **A docked vehicle** names its dock clamp by the id the game gives that object while the world runs, given again
  each time the world loads: the inspector shows it, the editor does not follow it.
- **Live, with a player near the vehicle**: not checked (there was no game client). The bridge then makes the objects
  again where they go instead of moving them, as for a paste, which was checked.

The first rafts (`MBRaft`, removed from the mod in 3.0) are not loaded by the mod any more. Pieces whose turn older
versions kept as a quaternion are read.

## Install

1. The Valheim World Editor 1.110.0 or later (and WorldEditorBridge 1.110.0 or later for live editing).
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

The tests read two ships the real mod built, saved and built again on a local server
(`tests/ValheimRAFT.WorldEditor.Tests/fixtures/`: `valheimraft-reference.txt`, with ropes and a sail;
`valheimraft-swivels.txt`, with turned and moved swivels, a sail on a turned mast, a cart and a parked land vehicle);
the editor's own tests (`editor/tests/AddOns.Tests/ValheimRAFT`) check that the editor draws each of their pieces
where the mod built it. Both were recorded on ValheimRAFT 5.0.9. On 5.1.1, the same probe had the mod build the same
two ships on a local server, which saved the world and loaded it again: every piece stood where the add-on puts it
(within 0.04 mm), on the swivels' platforms, the sail on its mast's yard and the parked land vehicle too; the rope
anchor's, dock clamp's, rope ladder's and swivel's prefab values, and the tie points on a Karve and a boar, were
unchanged. The first ship's ropes were not tied: 5.1 saves and loads ropes as before, but no longer ties a new rope from
an anchor on a vehicle to a piece on a vehicle, unless that piece is another anchor. The add-on's version is `VERSION` here, its changes
`CHANGELOG.md`: it goes out with every editor release, under its own version.
