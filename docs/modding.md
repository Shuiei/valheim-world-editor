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
    "MBParentId": { "label": "Parent vehicle", "readOnly": true, "help": "The vehicle this piece is part of." },
    "MBPosition": { "label": "Place on the vehicle", "readOnly": true }
  },
  "prefabs": {
    "ValheimVehicles_BoundaryChunk": { "hidden": true },
    "ValheimVehicles_hull_rib_wood": { "group": "Hulls" },
    "MBRaftMast": { "group": "Masts and sails" }
  }
}
```

| Key | What the editor does with it |
|---|---|
| `version` | The format's version: `1`. A file of a later version, or without one, is skipped. |
| `data` | By data key, as your code names it (the name whose hash the game saves): `label`, how the inspector names the value (else its key); `readOnly`, the inspector shows it but does not let it be changed (values the mod sets itself and that would break if edited); `help`, what it is, shown when pointing at the value. |
| `prefabs` | By prefab name: `hidden`, not offered in Place or the Workshop's Build panel (your tools, effects, helpers; objects of it already in a world still show); `group`, the heading the piece is listed under in the Build panel's **Mods** tab (else its crafting station). |

Texts are at most 200 characters. Comments and trailing commas are allowed. An entry not in this
form is left out; the rest of the file still counts. When two files say something of the same name,
the newest file wins.

The [schema](worldeditor.schema.json) lets code editors check the file and complete its keys (the
`$schema` line above).
