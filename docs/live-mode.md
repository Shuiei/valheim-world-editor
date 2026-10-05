# Live mode

In live mode the editor works on the world of a **running** game instead of a save on disk.
Players can stay connected while you edit, and they see the changes as soon as you apply them. The
game then saves them like any other change.

It needs the **WorldEditorBridge** plugin (BepInEx) in the game that hosts the world: normally the
dedicated server, or your own game when you play single player or host.

## Setting it up

See [Installation → Online (live) mode](../README.md#online-live-mode) for the steps. In short:

1. `WorldEditorBridge.dll` in the server's `BepInEx/plugins/`, server restarted.
2. The token from `BepInEx/config/local.worldeditorbridge.cfg`.
3. An SSH tunnel to the server: `ssh -N -L 5182:127.0.0.1:5182 user@server`.
4. `ValheimTerrainEditor --live http://127.0.0.1:5182 --token <token> --port 5181`, then open
   http://127.0.0.1:5181.

### Plugin settings (`local.worldeditorbridge.cfg`)

| Setting | Default | Meaning |
|---|---|---|
| `BindAddress` | `127.0.0.1` | Address the bridge listens on. Keep `127.0.0.1` and use an SSH tunnel; anything else exposes the bridge to the network. |
| `Port` | `5182` | TCP port of the bridge. |
| `Token` | generated | Secret the editor must send. Generated on first start when empty. Anyone with the token and access to the port can change the world, so keep it private. |

## What changes in the editor

| Control | What it does |
|---|---|
| **LIVE** badge | Shows the editor is connected to the running game. |
| **Apply live** | Sends every pending change to the game: ground (height and paint), deleted objects, and new objects (planted, pasted, replaced, moved). |
| **Auto** | Applies after every stroke, placement and undo, without pressing Apply live. |
| **Reload** | Loads the world again from the game, to pick up what players changed since. Changes you have not applied are dropped (you are asked first). |
| Player names | Connected players are drawn at their position, with their name, and follow them. |

The pending counter reads "Not applied: …" and the History panel marks applied changes.

## Undo after applying

Applied changes can still be undone: undo (or Remove in History) and apply again. A deleted object
comes back as a copy of what it was when the snapshot was taken; a placed object is removed from
the game again.

## Limits

- **Zone reset** is not applied live yet; use offline mode for it.
- The plugin only works in a game that **hosts** the world (server, single player or host). In a
  game that joined someone else's server it refuses to change anything.
- Objects placed live are created by the server; players see them like objects someone just
  built or planted.
- Big changes (thousands of objects at once) are applied in one go and can make the server hitch
  for a moment.
