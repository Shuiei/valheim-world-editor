# For mod authors

The editor works with building mods without anything from their authors. Connected to a game
(live mode), it learns every object the game's mods add: their names, costs, build tools and how
the game draws them; it reads their models and textures from the mods' own files on the player's
computer, and the names of the data mods keep on objects from the mods' code (read, never run). See
[Modded games](live-mode.md#modded-games).

A few things it cannot guess. A mod can tell them in a `worldeditor.json` file shipped with it,
anywhere in its folder under `BepInEx/plugins` (next to its DLL is fine): the editor reads every
such file in the player's Valheim folder and mod manager profiles each time a world or the Workshop
opens. The file is optional and only adds to what the editor learns by itself.

```json
{
  "$schema": "https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/worldeditor.schema.json",
  "version": 1,
  "data": {
    "MyMod_Vehicle": { "label": "Parent vehicle", "readOnly": true, "help": "The vehicle this piece is part of." },
    "MyMod_Offset": { "label": "Place on the vehicle", "readOnly": true }
  },
  "prefabs": {
    "MyMod_BoundaryMarker": { "hidden": true },
    "MyMod_hull_rib_wood": { "group": "Hulls" },
    "MyMod_Mast": { "group": "Masts and sails" }
  }
}
```

| Key | What the editor does with it |
|---|---|
| `version` | The format's version: `1`. A file of a later version, or without one, is skipped. |
| `data` | By data key, as your code names it (the name whose hash the game saves): `label`, how the inspector names the value (else its key); `readOnly`, the inspector shows it but does not let it be changed (values the mod sets itself and that would break if edited); `help`, what it is, shown when pointing at the value. |
| `prefabs` | By prefab name: `hidden`, not offered in Place or the Workshop's Build panel (your tools, effects, helpers; objects of it already in a world still show); `group`, the heading the piece is listed under in the Build panel's **Mods** tab (else its crafting station). |
| `links` | How your objects link into one (below). |
| `extension` | Your editor extension's DLL, relative to the file's folder and inside it (see [Extensions](#extensions)). |

Texts are at most 200 characters. Comments and trailing commas are allowed. An entry not in this
form is left out; the rest of the file still counts. When two files say something of the same name,
the newest file wins.

The [schema](worldeditor.schema.json) lets code editors check the file and complete its keys (the
`$schema` line above).

## Linked objects

If your mod builds objects out of others (a ship and the pieces built on it, a cart and its load),
the game saves each piece apart, and the editor sees loose pieces, piled where the game saved them.
Tell it how they link and it draws them as your mod builds them, and moves, copies and deletes each
group whole (see [Linked objects](select.md#linked-objects-ships-and-other-mods-groups)). It works for
mods that link the common way: the root (and any part others hang on) holds an int id of its own;
each part keeps, in its data, the id of what it hangs on and its offset and turn in that one's frame.

```json
{
  "version": 1,
  "links": [
    {
      "name": "vehicle",
      "id": "MyMod_Id",
      "parents": ["MyMod_Mount", "MyMod_Vehicle"],
      "offset": "MyMod_Offset",
      "rotation": "MyMod_Turn",
      "rotationQuaternion": "MyMod_TurnQuaternion",
      "storedAt": "root",
      "references": ["MyMod_SailMast", "MyMod_SwitchTarget"],
      "dropOnCopy": ["MyMod_PackedRopes", "MyMod_Aboard"]
    }
  ]
}
```

| Key | |
|---|---|
| `id` | Int data key of an object's own id, which its parts name. Required. |
| `parents` | Int data keys naming what a part hangs on, the first one set winning (a piece on a swivel names the swivel before the vehicle). At least one. |
| `offset` | Vec3 data key of a part's place in the frame of what it hangs on, in metres. Required. |
| `rotation` | Vec3 data key of its turn in that frame, as Unity Euler angles in degrees. |
| `rotationQuaternion` | Quaternion data key of that turn, used when `rotation` is not stored. |
| `storedAt` | Where the game saves a part: `root` (at its group's root; your mod builds it from its offset when the group loads; the default) or `own` (where it stands). The editor saves moved parts the same way. |
| `references` | Other int data keys naming an object's id (a sail's mast, a switch's target): a pasted copy's follow the copy's new ids, and are taken away when what they name was not copied. |
| `dropOnCopy` | Data keys a pasted copy leaves out: ids packed in a way the editor cannot rewrite (a byte array), ties that must not carry over. |
| `name` | What one group is called in the editor's messages ("a whole vehicle"). Default: `group`. |

Every pasted group gets new ids that no object in the world holds, so the game never takes two
copies for one. Data keys are named as your code names them, as in `data`. An object takes the
first rule whose `id` or `parents` it holds; a rule is known by its `id`: a newer file's rule of
the same `id` replaces an older one's, in its place.

## Extensions

What a file cannot say, code can. A mod can ship an **editor extension**: a small DLL, built
against the editor's SDK, that the editor loads to read and draw the mod's objects. It does not
run in the game, and it never sees the game's or Unity's code: only the objects' data, as the
editor has it.

- **Get the SDK** from a [release](https://github.com/Shuiei/valheim-world-editor/releases):
  `ValheimWorldEditor.Sdk-<version>.zip` holds `ValheimWorldEditor.Sdk.dll` (netstandard2.0, with
  its documentation file), and a whole sample to start from: the extension of a made-up mod,
  "Example Vehicles" (`samples/ExampleVehicles.WorldEditor`, with its `worldeditor.json`; it
  builds as it is), showing every hook: packed ids read and rewritten, sails drawn from their
  corners, cables drawn from post to post, the ground cut round its pits.
- **Build a netstandard2.0 class library** referencing `ValheimWorldEditor.Sdk.dll` (not copied
  to your output: the editor gives its own) and nothing else but the framework: an extension is
  one DLL, the editor loads no other from your folder. Mark the assembly and give it an entry:

```csharp
[assembly: WorldEditorExtension(typeof(MyMod.EditorExtension))]

public sealed class EditorExtension : IWorldEditorExtension
{
    public void Register(IExtensionRegistry registry)
    {
        registry.AddDecoder("MyMod_Packed", new PackedReader());          // inspector
        registry.AddCopyRewriter(new PackedIds(), "MyMod_Packed");      // paste
        registry.AddShapes(new BannerCloth(), "MyMod_Banner");          // 3D view
        registry.AddGroundCuts(new Cellars(), "MyMod_Hatch");           // the ground
    }
}
```

- **Ship it** with your mod and name it in your `worldeditor.json`:
  `"extension": "MyMod.WorldEditor.dll"`.

| Hook | What the editor does with it |
|---|---|
| `AddDecoder(key, IDataDecoder)` | The inspector shows a byte-array value in your words, under the value (instead of only its size). |
| `AddCopyRewriter(ICopyRewriter, keys)` | Each object of a pasted copy holding one of the keys is handed to you after the editor's own linking (`links`), with the copy's new ids for the originals' (`IIdMap`): rewrite ids you keep in your own form. The keys are yours: `dropOnCopy` no longer takes them out. |
| `AddGroundCuts(IGroundCutter, prefabs)` | The ground is not drawn inside the outlines you give for objects of the prefabs (`GroundCut`: points on the ground in the object's frame, or in the world's), as over the mouth of a tunnel or a pit your mod digs while the game runs; a click goes through the hole. The editor's ground is a height map, so the hole shows what you draw below it (shapes) and the height map's underside. |
| `AddShapes(IShapeProvider, prefabs)` | Objects of the prefabs are drawn with your shapes too: triangles in the object's frame (or in the world's: `WorldSpace`), one colour each (alpha below 1: see-through), for what your mod builds while the game runs (a sail from its corners, a cloth). The `IWorldView` given finds another object by an id it holds (`TryFind`, for ids under a `links` rule's `id` key), for a rope or a cable reaching it. |

Objects come as `IObjectView`: the prefab's name, the place, turn (Euler angles and a quaternion)
and size as drawn, and every value of its data by key (`TryGetInt`, `TryGetVector3`,
`TryGetBytes`…). Keys and prefabs are named as your code names them.

Rules:

- **The player decides.** An extension is code running in the editor with the player's rights, so
  it runs only once the player turns it on in **Settings → Mod extensions**, and only that file: a
  changed DLL (an update) is off until turned on again.
- **Hooks are called from several threads at once** and often (every redraw of the shapes and
  cuts, every pasted object): keep no state between calls, and be quick. A hook that throws turns the extension
  off until the editor starts again (Settings says why); the editor goes on without it.
- **Versions.** `SdkInfo.ApiVersion` is the API's version; an editor runs extensions built against
  its version or an earlier one. New hooks come as additions; the version changes only when the API
  breaks.
- The editor ships no extension, and no manifest, for any mod: supporting a mod in the editor is
  its author's to do, with these. The sample is a made-up mod's.

