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
