# For mod authors

The editor works with building mods without anything from their authors: connected to a modded
game, it learns the mods' objects, draws them with their own models and names the data they keep.
This page is for mod authors who want more: an optional `worldeditor.json` file for what the editor
cannot guess, **linked objects** for mods that build one thing out of many objects, **editor
extensions** (a small DLL built against the editor's SDK) for what a file cannot say, **add-ons**
(the same, made by anyone for someone else's mod), and **bridge hooks** for a mod's own plugin to
take part in live mode.

Everything here is optional, and each step adds to the one before. The editor ships nothing for any
mod: supporting a mod in the editor is its author's to do, with these. The examples on this page
are those of a made-up mod, "Example Vehicles", whose full sample comes with the SDK (see
[The Example Vehicles sample](#the-example-vehicles-sample)).

## What the editor learns by itself

| What | How the editor learns it |
|---|---|
| The mods' objects: names, building pieces, costs, crafting stations, the tool that builds each | Connected to a game (live mode, WorldEditorBridge 1.26.0 or newer), the editor asks the game for every object it knows and keeps those that are not Valheim's own. They are kept in `mod-catalogue.json` in the editor's data folder, so a modded server's world opened later from its files, offline, shows them too. A mod's newer report of a piece replaces the older one; a mod never changes one of Valheim's own objects. |
| How they look | The game tells what it draws of each mod object (WorldEditorBridge 1.27.0 or newer); the editor finds those meshes, materials and textures in the mods' own files on the player's computer (their asset bundles, copied out of the DLLs once into `mod-bundles` in the data folder). Meshes a mod builds in code while the game runs cannot be found: those objects show untextured, unless an [extension](#extensions) draws them. |
| The names of the data mods keep on objects | The game saves only a hash of each data key. The editor finds the names among the text written in the mods' DLLs (read as bytes, never run) in the plugins folders it knows, and keeps them in `mod-zdo-keys.json` until a DLL changes. A key whose name is put together while the game runs (`"Corner" + i`) cannot be found this way: the inspector shows it as `#` and its hash. |
| Which mod an object comes from | The inspector says so (with Jötunn installed, which knows; else "From a mod"). The Workshop's Build panel lists the mods' pieces on its **Mods** tab, one mod at a time. |

See [Modded games](live-mode.md#modded-games) for the player's side.

## worldeditor.json

A few things the editor cannot guess. A mod can tell them in a `worldeditor.json` file shipped with
it, anywhere in its folder under `BepInEx/plugins` (next to its DLL is fine). This is Example
Vehicles' file:

```json
{
  "$schema": "https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/worldeditor.schema.json",
  "version": 1,
  "extension": "ExampleVehicles.WorldEditor.dll",
  "gameComponents": ["Ship"],
  "data": {
    "ExampleVehicles_Vehicle": { "label": "Vehicle", "readOnly": true, "help": "The vehicle this part is built on." },
    "ExampleVehicles_Offset": { "label": "Place on the vehicle", "readOnly": true },
    "ExampleVehicles_Packed": { "label": "Linked objects", "help": "The objects this one is tied to." }
  },
  "prefabs": {
    "example_boundary": { "hidden": true },
    "example_fleet": { "hidden": true, "data": true },
    "example_canal": { "hidden": true, "data": true },
    "example_hull": { "group": "Hulls" },
    "example_sail": { "group": "Sails and cables" },
    "example_post": { "group": "Sails and cables" },
    "example_pit": { "group": "Digging" }
  }
}
```

| Key | What the editor does with it |
|---|---|
| `version` | The format's version: `1`. A file of a later version, or without one, is skipped whole. |
| `data` | By data key, as your code names it (the name whose hash the game saves): `label`, how the [inspector](select.md#inspecting-and-changing-an-objects-data) names the value (else its key; the editor's own names for the game's keys come first); `readOnly`, the inspector shows the value but does not let it be changed (values your mod sets itself and that would break if edited); `help`, what it is, shown when pointing at the value. |
| `prefabs` | By prefab name: `hidden`, not offered in the [Place](place.md) list or the Workshop's Build panel (your tools, effects, helpers; objects of it already in a world still show); `group`, the heading your piece is listed under in the Build panel's **Mods** tab, after the mod's name ("*mod name* · Hulls"). Without a group, a piece is listed under its crafting station, else under "No station needed". `group` counts only for the mods' own pieces. `data`: objects of it only hold your mod's data (a register, what was dug in an area) and have nothing to see: the editor never draws, picks or lists them in an area (your extension still reads them), and a [zone reset](area.md) keeps them when it keeps the buildings, removing them with everything else otherwise. |
| `links` | How your objects link into one: see [Linked objects](#linked-objects). |
| `extension` | Your editor extension's DLL, relative to the file's folder and inside it: see [Extensions](#extensions). |
| `gameComponents` | Class names of the game's own scripts whose fields your extension needs (`"TerrainOp"`, `"Ship"`; 32 at most): while your extension runs, the editor reads every component of them in the game's prefabs from the player's Valheim (once, then kept until the game changes) and hands them to it as `Host.Game` (see [Hooks](#hooks)). |

### How the editor reads it

- **Where.** In every `plugins` folder the editor knows: the Valheim folder's `BepInEx`, the mod
  manager profile the editor itself was installed in, the folders added in **Settings → BepInEx
  folders**, and every r2modman and Thunderstore Mod Manager profile. Every file named
  `worldeditor.json` there counts, at any depth.
- **When.** Each time a world or the Workshop opens, and when **Settings** opens. A file changed
  while a world is open counts the next time.
- **Which mod.** A file belongs to the mod whose folder lies right under `plugins` (the
  `ExampleVehicles` of `plugins/ExampleVehicles/worldeditor.json`). Settings names extensions by it.
- **Two files, one name.** Files are read oldest first (by the time they were last changed): when
  two say something of the same data key, prefab or link `id`, the newest file wins.
- **Forgiving.** Comments and trailing commas are allowed. Texts are 1 to 200 characters; lists
  hold at most 32 names; a file is at most 1 MB. An entry not in this form is left out and the rest
  of the file still counts. A file that is not JSON, or not of a version the editor reads, is
  skipped; the editor's log says so ("mod manifest …: not a worldeditor.json this editor reads,
  skipped.").

The [schema](worldeditor.schema.json) describes the file: with the `$schema` line above, code
editors check the file and complete its keys as you type.

## Linked objects

If your mod builds objects out of others (a ship and the pieces built on it, a cart and its load),
the game saves each piece apart, and the editor sees loose pieces, piled where the game saved them.
Tell it how they link and it draws them as your mod builds them, and moves, copies and deletes each
group whole (see [Linked objects](select.md#linked-objects-ships-and-other-mods-groups) for what
the player sees). It works for mods that link the common way: the root (and any part others hang
on) holds an int id of its own; each part keeps, in its data, the id of what it hangs on and its
offset and turn in that one's frame.

```json
{
  "version": 1,
  "links": [
    {
      "name": "vehicle",
      "id": "ExampleVehicles_Id",
      "parents": ["ExampleVehicles_Mount", "ExampleVehicles_Vehicle"],
      "offset": "ExampleVehicles_Offset",
      "rotation": "ExampleVehicles_Turn",
      "rotationQuaternion": "ExampleVehicles_TurnQ",
      "storedAt": "root",
      "references": ["ExampleVehicles_Target", "ExampleVehicles_Drives"],
      "dropOnCopy": ["ExampleVehicles_Packed", "ExampleVehicles_Aboard"]
    }
  ]
}
```

| Key | |
|---|---|
| `id` | Int data key of an object's own id, which its parts name. Required. |
| `parents` | Int data keys naming what a part hangs on, the first one set winning (a piece on a swivel names the swivel before the vehicle). At least one. Required. |
| `offset` | Vec3 data key of a part's place in the frame of what it hangs on, in metres. Required. |
| `rotation` | Vec3 data key of its turn in that frame, as Unity Euler angles in degrees. |
| `rotationQuaternion` | Quaternion data key of that turn, used when `rotation` is not stored. With neither, a part is not turned in its parent's frame. |
| `storedAt` | Where the game saves a part: `root` (at its group's root; your mod builds it from its offset when the group loads; the default) or `own` (where it stands). The editor saves moved parts the same way. |
| `references` | Other int data keys naming an object's id (a sail's mast, a switch's target): in a pasted copy they follow the copy's new ids, and are taken away when what they name was not copied. |
| `dropOnCopy` | Data keys a pasted copy leaves out: ids packed in a way the editor cannot rewrite (a byte array), ties that must not carry over (what was aboard). |
| `name` | What one group is called in the editor's messages ("a whole vehicle"). Default: `group`. |

### How the editor uses the links

- **Drawing.** A part is drawn at its offset in its parent's frame, the parent where its own parent
  puts it, and so on up to the root, which stands where it is saved: a ship's pieces turn and lean
  with the hull. A part whose parent (or any parent up its chain) is not in the world, or whose
  chain loops or is more than 16 deep (damaged data), stays where it is saved.
- **Which rule.** An object takes the first rule whose `id` or `parents` keys it holds. Prefab
  names do not matter: only the data. A newer file's rule with the same `id` replaces an older
  one's, in its place in the order.
- **Moving and deleting.** Selecting any part and moving, turning or deleting takes the whole
  group. A moved group keeps its height and does not snap; its parts are saved as `storedAt` says.
- **Copying.** A copy takes the whole group. Each paste (and each of its repeats) gets new ids that
  no object in the world holds, even deleted ones, so the game never takes two copies for one; every
  parent and `references` value follows. A part copied without its parent is pasted as an ordinary
  object (its links taken away), where it was pasted. A mirrored paste mirrors the parts' offsets and
  turns too. Values in `dropOnCopy` go, unless an extension's copy rewriter handles them.
- Data keys are named as your code names them, as in `data`.

## Extensions

What a file cannot say, code can. A mod can ship an **editor extension**: a small DLL, built
against the editor's SDK, that the editor loads to read and draw the mod's objects. It does not run
in the game, and it never sees the game's or Unity's code: only the objects' data, as the editor has
it. It can:

- show a byte-array value in words in the inspector;
- rewrite ids your mod packs in its own form in pasted copies;
- draw what your mod builds in code while the game runs (a sail from its corners, a cable from post
  to post);
- leave the ground undrawn where your mod digs (the mouth of a tunnel, a pit);
- draw what your mod makes of an area as a whole (a canal, rock dug out of a hill) with the game's ground look, in place
  of the editor's ground;
- read the game's own scripts' settings (its `gameComponents`), as the player's Valheim has them;
- add a tool of your own to the 3D editor's rail, with its settings card, its preview and its edits
  in the history (digging a canal where the player clicks);
- in live mode, ask your mod's own plugin in the game for what only the game knows (see
  [Live mode](#live-mode)).

### Making one

1. **Get the SDK** from a [release](https://github.com/Shuiei/valheim-world-editor/releases):
   `ValheimWorldEditor.Sdk-<version>.zip` holds `ValheimWorldEditor.Sdk.dll` (netstandard2.0, what an
   extension builds against) and `WorldEditorBridge.Api.dll` (what a mod's plugin builds against for
   [live mode](#live-mode)), each with its documentation file for code editors, this page as
   `README.md`, and the whole Example Vehicles sample in `samples/` (it builds as it is).
2. **Build a netstandard2.0 class library** referencing `ValheimWorldEditor.Sdk.dll` without copying
   it to your output (`Private="false"`): the editor gives its own. Use nothing else but the
   framework (the sample's project also references the `System.Numerics.Vectors` package, for
   `Vector3` and `Quaternion`). An extension is **one DLL**: the editor loads no other file from your
   folder, so a helper library next to it is never found.
3. **Mark the assembly** and give it an entry: a public class with a constructor without
   parameters, implementing `IWorldEditorExtension`. The editor makes it once and calls `Register`
   once, before any hook is used. This is the sample's:

   ```csharp
   [assembly: WorldEditorExtension(typeof(ExampleVehicles.WorldEditor.Extension))]

   public sealed class Extension : IWorldEditorExtension
   {
       public void Register(IExtensionRegistry registry)
       {
           var packed = new PackedIds();
           registry.AddDecoder(Keys.Packed, packed);           // inspector
           registry.AddCopyRewriter(packed, Keys.Packed);      // paste
           registry.AddShapes(new Sails(), Keys.Sail);         // 3D view
           registry.AddShapes(new Cables(), Keys.Post);
           registry.AddGroundCuts(new Pits(), Keys.Pit);       // the ground
           registry.AddScene(new Canals());                    // the area as a whole
           registry.AddTool(new CanalTool());                  // a tool on the rail
           registry.AddLiveListener(new LiveLink(registry));   // live mode
           registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
       }
   }
   ```

4. **Ship it** with your mod, inside its folder, and name it in your `worldeditor.json`:
   `"extension": "ExampleVehicles.WorldEditor.dll"`. A path that leaves the file's folder, or a file
   that does not end in `.dll`, is not offered.

### Turning it on: Settings → Mod extensions

An extension is code that runs in the editor with the player's rights, so **the player decides**.
The editor finds the extensions offered by the mods in its plugins folders and lists them in
**Settings**, under **Mod extensions**: a switch per extension, named after the mod's folder and the
file ("ExampleVehicles · ExampleVehicles.WorldEditor.dll"). Pointing at it shows the file's path and
its SHA-256. Under each switch the editor says what became of it: "Running.", "Not running: …" with
the reason, or "Off: the file cannot be read.". When no mod offers one: "No mod in the folders above
offers one."

- **Off by default.** An extension runs only once the player turns it on and presses **Save**; it
  runs from then on, and those turned off stop.
- **That file only.** The editor remembers the file's SHA-256. A changed DLL (a mod update) is other
  code: it is off until the player turns it on again. The bytes checked are the bytes loaded.
- **Why it is not running.** "it is not marked as an extension ([assembly: WorldEditorExtension])",
  "it needs a newer editor (API …; this one has …)", "its entry … is not an IWorldEditorExtension",
  or the exception `Register` threw.

### Hooks

| Hook | What the editor does with it |
|---|---|
| `AddDecoder(key, IDataDecoder)` | The inspector shows a byte-array value in your words, under the value (instead of only its size). `Describe` returns a few short lines, or `null` when the bytes are not in a form it reads. When two extensions read one key, the first that gives text wins. |
| `AddCopyRewriter(ICopyRewriter, keys)` | Each object of a pasted copy holding one of the keys is handed to you (`ICopy`), after the editor's own linking (`links`), with the copy's new ids for the originals' (`IIdMap.TryGetNew`; false when the object holding the old id was not copied). Change its values in place (`SetInt`, `SetBytes`…; `null` takes a value away). The keys are yours: `dropOnCopy` no longer takes them out. |
| `AddShapes(IShapeProvider, prefabs)` | Objects of the prefabs are drawn with your shapes too: `Shape`s, triangles in the object's frame (metres; x right, y up, z forward; the object's place, turn and size applied) or in the world's (`WorldSpace = true`), one colour each (red, green, blue, alpha from 0 to 1; alpha below 1: see-through), wound clockwise seen from the front as in Unity, or seen from both sides (`BothSides = true`, for a sail or a cloth). They are drawn with the object's kind (hidden when the View panel hides it). A shape with a triangle naming a corner it lacks, or a corner that is not a number, is not drawn. |
| `AddGroundCuts(IGroundCutter, prefabs)` | The ground is not drawn inside the outlines (`GroundCut`) you give for objects of the prefabs: points on the ground (x, z) in the object's frame, or in the world's (`WorldSpace = true`); at least three, in any direction round (a self-crossing outline cuts by the even-odd rule). A click there goes through the hole. The editor's ground is a height map with a point every metre: the cut leaves out the ground between the points inside the outline, so its edge follows that 1 m grid, and what shows through is what you draw below (shapes) and the underside of the ground. |
| `AddScene(ISceneProvider)` | What your mod makes of an area as a whole, drawn in it: `Build(area)` is called on a worker each time an area opens and again whenever its objects, its ground or the game change (or you call `Host.Redraw()`), and returns a `SceneLayer` (or `null`). Its `Meshes` (`SceneMesh`: corners and their normals in the world's frame, metres, triangles wound clockwise seen from the front as in Unity) are drawn with the game's ground look when they have `BiomeColors` (each corner's terrain colour, as `area.BiomeColor` gives it) and painted from their `Mask` (a small texture of the terrain's paint mask, `MaskTexture`) at `MaskUvs`; without biome colours, in `Color`. Clicks land on them (unless `Pickable = false`). Its `Holes` (`GroundHole`: rectangles in whole metres) leave the editor's ground undrawn, cell by cell, exactly to their edges: draw what replaces it. The `area` (`IAreaView`) gives its bounds, its objects of a prefab as they are with the changes not saved yet (`ObjectsOf`, your data holders included), the ground's height as the game shows it (`GroundHeight`, between the points a metre apart along the game's own triangles), the biome, whether a point is in the Deep North or the Ashlands, and the terrain's colour and paint at a whole metre (`BiomeColor`, `GroundMask`). Work everything out from the area each time; keep nothing between calls. |
| `AddTool(IEditorTool)` | A tool of your own on the 3D editor's rail, under **Mods**, with its `Info` (`ToolInfo`: an id, unique in your extension; its name on the rail; a line of help in its tooltip; `Icon`, an SVG path drawn 16 × 16, else your name's first letter). Chosen, it shows a card of its `Controls`: `SliderControl` (min, max, value, `Step`, `Unit`), `ChoiceControl` (a list), `ToggleControl`, `ButtonControl` and `TextControl` (a line you change with `SetText`); the player's values are remembered from one run to the next. The editor calls `OnChosen` and `OnLeft` as it is chosen and left, `OnControl(id)` as a value changes or a button is pressed, `OnPointer` with each move, press, drag and release over the 3D view (`PointerInput`: the ray, the keys held, and what it points at: the ground, your scene layer, or a thing, with the point and the ground's normal there), and `OnKey` with each key pressed (the key's name, as "Enter", "Escape", "Back" or "A"; return true when you used it, false leaves it to the editor, so Ctrl+Z still undoes). The `IToolContext` handed to each call gives the area (`Area`, as for scene layers), your controls' values (`Number`, `Choice`, `IsOn`), shapes drawn over the area until you change them (`Preview`, in the world's frame), a line on the status bar (`Status`) and `Commit(label, ToolEdit)`: objects added (a prefab, a place, a turn and its data set through an `ICopy`), changed (a copy of an object the area gave you, with your changes) and removed, in one step of the area's history, undone and saved like any other edit. Your data holders are kept with the history as other objects are, though they are never drawn; an object you did not get from the area is left alone. |
| `AddLiveListener(ILiveListener)` | Told when the editor connects to a game (`OnConnected`, with the game) and when that ends (`OnDisconnected`: the world was closed, or another opened). See [Live mode](#live-mode). |
| `Host` | The editor, outside the hooks: `Host.Live`, the game connected (null when there is none: a saved world, the Workshop); `Host.Game`, the game's components your manifest's `gameComponents` asks for (`Components("Ship")`: each with its prefab, the object it is on and its fields by name, as the game saves them; ready once a world or the Workshop is open, so ask from your hooks, not from `Register`); and `Host.Redraw()`, for your shapes and cuts to be made again at the next frame (after the game told you something new). |
| `Log(message)` | Writes a line, named after your mod ("extension ExampleVehicles: …"), to the editor's log, `ValheimWorldEditor.log` in its data folder (**Open log** on the start page; started over at each run). |

Shape and cut hooks get the world around the object too (`IWorldView`): `TryFind(idKey, id, out
obj)` finds another object by an id it holds, for a rope or a cable reaching it. Only ids under a
key that a `links` rule names as its `id` are found (the editor indexes those).

Objects come as `IObjectView`: the prefab's name ("" when the editor does not know it), the place,
turn (Unity Euler angles in degrees, and `Orientation`, a quaternion) and size as drawn (for a linked
part, where your mod builds it, not where the game saves it), and every value of its data by key:
`TryGetInt`, `TryGetLong`, `TryGetFloat`, `TryGetVector3`, `TryGetQuaternion`, `TryGetString`,
`TryGetBytes`. Keys and prefabs are named as your code names them; the editor hashes them as the
game does.

### Rules

- **Be quick, keep no state.** Hooks are called from several threads at once and often (every
  redraw of the shapes and cuts, every pasted object): keep no state between calls (or lock it),
  and return fast.
- **Never throw.** A hook that throws turns the extension off until the editor starts again
  (Settings says why, and the log); the editor goes on without it. In code that goes on after a
  hook returns (an `async` call to the game), catch everything yourself, as the sample's `LiveLink`
  does.
- **Versions.** `SdkInfo.ApiVersion` is the API's version (now 1), and the attribute records the one
  you built against. An editor runs extensions built against its version or an earlier one. New
  hooks come as additions; the version changes only when the API breaks.

## Add-ons

An extension ships with its mod. When a mod's author has not made one (or the mod is no longer
kept up), anyone can: an **add-on** is a `worldeditor.json` and an extension for **someone else's
mod**, released on its own, that the player installs next to the editor. The mod itself stays as it
is, in `BepInEx/plugins`; the add-on only teaches the editor about it.

```
ValheimWorldEditor/                the editor's folder (on macOS: the folder holding ValheimWorldEditor.app)
  ValheimWorldEditor(.exe)
  addons/
    Tunnels/                       one folder per add-on, named as you like
      worldeditor.json
      Tunnels.WorldEditor.dll
```

- **Its file** is a `worldeditor.json` as above (data labels, prefabs, links, `extension`,
  `gameComponents`), with one more key: `"serves": "TheirMod.dll"`, the file name of the mod's DLL.
  The add-on is used while a DLL of that name is in one of the plugins folders the editor knows
  (Settings, BepInEx folders), anywhere under `plugins`, and left alone otherwise. Without `serves`
  it is always used (a tool of your own, for no mod in particular). A `serves` that is not a plain
  file name ending in `.dll`, or a file of a version this editor does not read, leaves the add-on
  out (the log says why).
- **Its extension** is made as any other (see [Making one](#making-one)): one DLL, inside the
  add-on's folder, every hook and rule the same.
- **No switch.** Installing an add-on is the player's choice, so it runs without being turned on
  in Settings. Settings lists the add-ons under **Add-ons** with what became of each: "Waiting for
  its mod: TheirMod.dll is not in the plugins folders above.", "Running.", "Not running: …", or
  that it only brings notes.
- **The mod's own word wins.** The add-ons' files are read before the mods' own: should the mod
  ship a `worldeditor.json` of its own one day, what it says replaces what the add-on said (an
  extension of its own then needs the player's switch, as always).
- **Release it as a `.zip` holding its folder**, for players to unpack into `addons`. The editor's
  packages come with that folder, empty but for a `README.txt`.

## Live mode

Connected to a running game (live mode), the editor applies its changes through the
WorldEditorBridge plugin, which destroys, makes and moves objects in the game. A mod that builds
things out of its objects while the game runs (a vehicle from its parts, a tunnel from what was dug)
only sees the editor's changes at the next load, unless its own plugin takes part. The bridge offers
mods' plugins two things, through `WorldEditorBridge.Api.dll`: it is shipped with the bridge, in its
`plugins` folder, and in the SDK's zip, and holds no game or Unity type, so a game update cannot
break it.

### Your plugin's side

Register from your plugin's `Awake`, with a soft dependency on the bridge, and keep the code that
touches the API in a class of its own: your plugin then loads without the bridge too. From the
sample's `samples/ExampleVehicles.Bridge`:

```csharp
[BepInPlugin(PluginId, "ExampleVehicles", "1.0.0")]
[BepInDependency(BridgeApi.BridgeGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleVehiclesBridgePlugin : BaseUnityPlugin
{
    public const string PluginId = "ExampleVehicles";

    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Register(Logger);   // a class of its own: this plugin loads without the bridge
        }
    }

    private void OnDestroy()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Unregister();       // BridgeApi.Remove(PluginId)
        }
    }
}

internal static class BridgeLink
{
    public static void Register(BepInEx.Logging.ManualLogSource log)
    {
        BridgeApi.AddRoutes(ExampleVehiclesBridgePlugin.PluginId, new Routes());          // answers the editor's calls
        BridgeApi.AddListener(ExampleVehiclesBridgePlugin.PluginId, new Listener(log));   // told what the editor applied
    }

    public static void Unregister() => BridgeApi.Remove(ExampleVehiclesBridgePlugin.PluginId);
    // Routes and Listener: see the sample.
}
```

| Hook | What the bridge does with it |
|---|---|
| `AddListener(modId, IBridgeListener)` | `OnApplied(AppliedChanges)` is called on the game's main thread right after each of the editor's applies, with what it changed: `Created` and `Destroyed`, the objects made and destroyed (each its ZDOID's `User` and `Id`, its prefab's hash and its position); `Moved`, the objects moved under their own ZDOID (bridge 1.40.0 or newer: `From` where each stood, `To` where it stands now; an object whose old and new places a player has both loaded is made again instead, and is in `Created` and `Destroyed`; from 1.97.0, an object whose data the editor changed is here too, its new data already on it); `TerrainZones`, the zones whose ground was rewritten; `ResetZones`, the zones emptied to be generated again. Put your things together again here. An exception is logged in the game's log and changes nothing of what the editor did. |
| `AddRoutes(modId, IBridgeRoutes)` | `Handle(name, body)` answers `POST /mods/<modId>/<name>` on the game's main thread, in whatever bytes you and your editor extension agree on; `null` answers 404 ("the mod has no route of that name"), an exception 500 with its message. The bridge waits at most 30 seconds for the main thread: be quick, or answer "started" and let the extension ask again. A call's body is at most 16 MB. |
| `Remove(modId)` | Takes your routes and listener away (a plugin shutting down). |

Registering again under the same id replaces what was registered before. The mod id is yours (your
plugin's id is a good one): 1 to 100 letters, digits, dots, dashes and underscores; another id
throws. `BridgeApi.Version` is the API's version (now 1); a bridge runs plugins built against its
version or an earlier one, and additions keep it. `GET /mods` on the bridge lists the API version
and the ids registered.

### Your extension's side

Your extension gets the game through `ILiveGame`: from `registry.Host.Live` at any time (null when
the editor is not connected), or in your live listener's `OnConnected`.

- `Mods`: the ids registered with the bridge, as the bridge reported them when the world was opened
  (a plugin that registers later is seen at the next connection).
- `CallAsync(modId, name, body)`: calls your route and gives back its answer's bytes. It throws when
  the mod or the route is not registered, when the route failed (with the game's message), or when
  the game cannot be reached.

Ask the game for what only it knows (what your mod built in code), keep it, and call
`registry.Host.Redraw()` for your shapes and cuts to be made again. The sample's `LiveLink` calls the
plugin's `ping` route when the editor connects and logs the answer; its plugin also answers
`players` (how many players are in the game).

When a world opens live, the editor's log names the mods registered ("bridge mods: …").

## The Example Vehicles sample

The SDK's zip (and the editor's repository) holds the source of a made-up mod's two halves, to copy
from. Example Vehicles builds vehicles out of a hull and the parts built on it.

| File | What it shows |
|---|---|
| `samples/ExampleVehicles.WorldEditor/worldeditor.json` | The mod's file: labels, a hidden helper, groups, the `vehicle` link rule, the extension. |
| `samples/ExampleVehicles.WorldEditor/Extension.cs` | The entry, registering one hook of each kind. |
| `samples/ExampleVehicles.WorldEditor/Keys.cs` | The mod's data keys and prefabs, as its code names them. |
| `samples/ExampleVehicles.WorldEditor/PackedIds.cs` | A decoder and copy rewriter for a list of ids packed in a byte array (a version byte, then four bytes per id): shown in the inspector ("Linked to 3 object(s): …"), and in a pasted copy made to name the copy's objects (ids of objects not copied go). |
| `samples/ExampleVehicles.WorldEditor/Sails.cs` | Shapes: a sail's cloth drawn between three or four corners kept in its data, in its colour, seen from both sides. |
| `samples/ExampleVehicles.WorldEditor/Cables.cs` | Shapes in world space: a cable from the top of one post to the top of the post it names, found with `TryFind`. |
| `samples/ExampleVehicles.WorldEditor/Canals.cs` | A scene layer: each canal (a data holder keeping its path, its width and depth) dug as a trench with the game's ground look, its bed painted dirt, the ground left undrawn over it. |
| `samples/ExampleVehicles.WorldEditor/CanalTool.cs` | A tool: click points on the ground for a canal's path (Backspace takes the last away), its width and depth from sliders, the path previewed; Enter or **Dig** adds the canal (a data holder), **Clear** or Escape drops the points. Shift+click fills a canal in (removes it), Ctrl+click gives it the sliders' width and depth (changes it). |
| `samples/ExampleVehicles.WorldEditor/Pits.cs` | A ground cut: a round hole as wide as the pit's radius (1.5 m when it keeps none). |
| `samples/ExampleVehicles.WorldEditor/LiveLink.cs` | The extension's side of live mode: names the game's own ships (`Host.Game`, from the manifest's `gameComponents`), checks that the plugin registered, calls its `ping` route, logs the answer, and catches everything. |
| `samples/ExampleVehicles.Bridge/ExampleVehiclesBridgePlugin.cs` | The mod's BepInEx plugin taking part in live mode: soft dependency, registration in a class of its own, routes `ping` and `players`, a listener that logs what the editor applied. It builds against the game's and BepInEx's DLLs like any plugin (`-p:ValheimDir=…`, a Valheim dedicated server's folder). |
