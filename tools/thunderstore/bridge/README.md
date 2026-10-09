# WorldEditorBridge

**Live editing for [Valheim World Editor](https://github.com/Shuiei/valheim-world-editor)**, a 3D
world editor for Valheim in the spirit of Minecraft's MCEdit and WorldPainter. Install this plugin
where your world is hosted. The editor can then change that world while it runs, and you and your
players see each change in game as soon as it is applied.

![The 3D editor showing a base](https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/images/overview.jpg)

> **This plugin is only the bridge.** The editor is a separate program for Windows and Linux.
> Download it from the **[releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest)**,
> unpack it anywhere and start it. Use the same version of the editor and of this plugin.

## What the editor does

- **Sculpt and paint the ground**: raise, lower, flatten, smooth, erode or restore it, and paint
  dirt, cultivated soil or paved stone.
- **Go past the game's ±8 m limit.** Raise mountains and ridges or dig canyons. Players need no mod
  to see them, console players included.
- **Draw roads, ramps, rivers and caves** along a line.
- **Build in the Workshop** with every piece the game has. Pieces snap exactly like the hammer, and
  a support check shows the game's colours. Buildings are saved as
  [Homestead](https://thunderstore.io/c/valheim/p/sighsorry/Homestead/) blueprints, ready to build
  in game.
- **Paste blueprints into your world.** The ground in the way is dug out, and the trees and rocks
  there are removed.
- **Place anything the game has**: trees, rocks, crops and building pieces, with a brush, lines,
  grids or whole zones.
- **Select, move, copy and delete** objects and whole buildings. Change chest contents, sign texts
  and portal tags.
- **Restore an area from a backup** to undo griefing without rolling back the whole world.
- **Undo anything**, with a full history.

The editor also opens saved worlds with the game closed. That needs no plugin.

## Where to install it

Install it **where the world is hosted**, nowhere else:

| You play… | Install the plugin in… |
|---|---|
| Single player, or a world you host from the game | Your own game, with your mod manager like any other mod |
| On a dedicated server | The server, with a mod manager's server profile or by copying `WorldEditorBridge.dll` into its `BepInEx/plugins` |

**Players who join need nothing**: no plugin and no BepInEx. Console players can join as usual.

## Connecting the editor

**Start the game or server once with the plugin before connecting.** On that first start it writes
`BepInEx/config/Tie.WorldEditorBridge.cfg` with a random **Token**, and the editor needs that token.
Keep it secret: with it and access to the port, anyone can change the world.

- **Your own game**: in the editor, choose **My game** on the start page and load your world in
  Valheim. The editor finds the plugin and its token by itself, mod manager profiles included, and
  shows **Edit live**.
- **A dedicated server**: choose **A dedicated server** and enter the server's address, your SSH
  login and the token from the server's config file, then **Connect**. The editor opens its own
  encrypted tunnel and remembers the server, so next time it is one click.

## Settings

In `BepInEx/config/Tie.WorldEditorBridge.cfg`:

| Setting | Default | Meaning |
|---|---|---|
| `BindAddress` | `127.0.0.1` | Only this computer can connect. A server is reached through the editor's SSH tunnel and is never exposed to the internet. |
| `Port` | `5182` | Change it if something else uses 5182. |
| `Token` | random | Leave it empty to get a new one at the next start. |

Once the plugin runs, the BepInEx log shows `WorldEditorBridge <version> listening on http://127.0.0.1:5182/`.

## More

- [Full guide](https://github.com/Shuiei/valheim-world-editor), with screenshots of every tool
- [Live mode in detail](https://github.com/Shuiei/valheim-world-editor/blob/main/docs/live-mode.md), and what each connection error means
- [The Workshop and blueprints](https://github.com/Shuiei/valheim-world-editor/blob/main/docs/workshop.md)
- [Report a problem or suggest an idea](https://github.com/Shuiei/valheim-world-editor/issues)
