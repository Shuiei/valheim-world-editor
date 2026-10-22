# Edit a world made with Expand World, Riverheim or Tunnelheim

Install the editor's add-on for your mod, so a world made with it opens, draws and saves as the mod makes it.

**You need:** the mod installed in a BepInEx folder on this computer (your Valheim, a mod manager profile, or a copy of
a server's `BepInEx` folder), and the add-on of the same editor release.

| Add-on | For | What it adds |
|---|---|---|
| [Expand World](../../addons/ExpandWorld/README.md) | Expand World Size, Rivers and Data | Ground, map, biomes, water, locations, vegetation and New world predictions as the mods make them |
| [Riverheim](../../addons/Riverheim/README.md) | Riverheim (`Riverheim.dll`) | Opens Riverheim worlds (refused without it), drawn with your own copy of the mod; Riverheim as a New world generator |
| [Tunnelheim](../../addons/Tunnelheim/README.md) | Tunnelheim (`ValheimTunneling.dll`) | Draws the mod's tunnels, and a **Tunnel** tool (digs, Add Earth, bores, rooms, shafts, stairs); tunnels go with copies and blueprints |

1. Close the editor.
2. From the editor's [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest) (or Nexus Mods),
   download `ValheimWorldEditor-addon-<Name>-<version>.zip`.
3. Unpack it into the `addons` folder next to the editor (on macOS, next to `ValheimWorldEditor.app`), so that you
   have `addons/<Name>/worldeditor.json`.
4. Start the editor and open **Settings** on the start page.
5. Under **Add-ons**, find the add-on. It should say "Running." (or "Not read yet" until a world opens). If it says
   "Waiting for its mod: … is not in the plugins folders above.", add the BepInEx folder that holds the mod under
   **BepInEx folders**, then **Save**.
6. For a server's world opened from its files, add a copy of the server's `BepInEx` folder under **BepInEx folders**,
   so the editor reads the server's mod settings.
7. Open the world under **A saved world**. Its card shows the mod (for example **Riverheim** or "Expand World").
8. Edit and save as usual. With Tunnelheim, choose **Tunnel** under **Mods** on the tool rail, pick an action, and
   click the ground or the rock.

## Check it worked

- The world opens, and its ground and map look as in game (Riverheim's rivers, Expand World's size and biomes).
- The editor's log (**Open log** on the start page) says what the add-on found and read.
- A world card that stays greyed out says why: see [A world will not open or is greyed out](../help/troubleshooting.md#a-world-will-not-open-or-is-greyed-out).

## Undo it

Each edit undoes like any other (`Ctrl+Z`, History). To stop using an add-on, turn its switch off under
**Settings → Add-ons** (its folder stays), or delete its folder from `addons`. With it off, Expand World worlds are
generated as the game does without the mods, and Riverheim worlds are refused.

## Good to know

- An add-on is used only while its mod's DLL is in a plugins folder the editor knows; a mod a mod manager turned off,
  or settings a removed mod left behind, do not count.
- Expand World's data files need the YamlDotNet library the mod installs under `BepInEx/plugins`; without it they
  are not read, and the log says so.
- Your Riverheim must know the world's preset (update it if it is older than the one that made the world). Worlds
  made with Riverheim's deprecated beta cannot be opened. Live, your Riverheim and the game's must be of the same
  release.
- A Riverheim world needs Riverheim in every player's game, so console players on crossplay cannot join it.
- Live, Expand World's files are read from the game itself through the plugin: see [Live mode](../live-mode.md).

## Related

- [A new world](../new-world.md): Riverheim and Expand World when making a world
- [Live mode](../live-mode.md): modded games, live
- [Open a world that is not in the list](open-a-world.md)
