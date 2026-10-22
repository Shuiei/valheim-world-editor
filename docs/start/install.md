# Install the editor

Download the editor, start it, and let it find your Valheim. At the end, the editor is open on its start page and
has read the game's files, ready for [your first edit](first-edit.md).

## What you need

- **Windows 10 or 11**, or **Linux**, 64-bit; or **macOS 12 or later on a Mac with Apple silicon** (M1 and later).
  The Mac build is experimental: it has not been tried on a Mac yet, and Intel Macs are not supported.
- **Valheim**, installed through Steam on the same computer. Owning it is enough: the editor reads the game's files
  (its textures, models, terrain and map), it does not need the game running. Nothing is copied or changed.

You need nothing else installed. Everything the editor needs comes in the download.

## Download

1. Open the [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest).
2. Download the file for your computer:
   - Windows: `ValheimWorldEditor-v<version>-win-x64.zip`
   - Linux: `ValheimWorldEditor-v<version>-linux-x64.tar.gz`
   - macOS: `ValheimWorldEditor-v<version>-macos-arm64-experimental.zip`

The editor is also on [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348). The editor is a program, not a mod:
Thunderstore and Hexium carry only its plugin (see [The plugin for live editing](#the-plugin-for-live-editing)).

## Start it on Windows

1. Unzip the download anywhere, for example on the Desktop. Keep its files together: `ValheimWorldEditor.exe` needs
   the `.dll` files beside it. Do not run it from inside the zip.
2. Double-click `ValheimWorldEditor.exe`.
3. If Windows says "Windows protected your PC", click **More info** (it names the publisher), then **Run anyway**.
   Windows asks this until SmartScreen knows the editor's signature.

<!-- picture: install-smartscreen.jpg: Windows 11, the blue "Windows protected your PC" dialog after More info was clicked, the publisher line and the Run anyway button visible, nothing else on screen -->

## Start it on Linux

1. Unpack the download anywhere, for example in your home folder.
2. Double-click `ValheimWorldEditor` in the folder. If your file manager opens it as text or asks what to do, choose
   **Run** (or right-click, **Run as program**). From a terminal: `./ValheimWorldEditor`.

The editor draws with OpenGL 3.3, which any graphics driver of the last ten years has (Mesa, AMD, NVIDIA, Intel).

## Start it on macOS (experimental)

1. Unzip the download and move **ValheimWorldEditor.app** where you like, for example into Applications.
2. Double-click it once. macOS refuses to open it, because the editor is not signed with an Apple Developer
   certificate. Close the message.
3. Open **System Settings › Privacy & Security**, scroll down to **Security**, click **Open Anyway** next to
   ValheimWorldEditor, and confirm with your password.
4. Double-click the app again. From now on it opens with a double-click.

Instead of steps 2 and 3, you can run in Terminal:
`xattr -dr com.apple.quarantine /Applications/ValheimWorldEditor.app` (with the path where you put it).

On a Mac, `Cmd` works wherever the documentation says `Ctrl`: `Cmd+Z` undoes, `Cmd`+click adds to a selection. The
game's terrain is drawn with the game's own shader when the Mac game ships one the editor can read, otherwise in
plain colours. If something does not work,
[open an issue](https://github.com/Shuiei/valheim-world-editor/issues) with the editor's log (**Open log** on the start
page).

## The first start

The editor opens in its own window, on the start page's **Home**. The first time, and after each Valheim update, a
card says **Preparing the game's look** while the editor reads the game's files for a few seconds. Worlds, servers
and the Workshop open once it says **The game's look is ready.**

![The start page's Home](../images/start-home.jpg)
*The start page: the side menu on the left, Home on the right. Once a world is opened, it shows under Recent.*

## When Valheim is not found

The editor looks in every Steam library by itself (Flatpak Steam too). If it does not find the game, the card says
**Get the game's look** and "Valheim was not found on this computer."

<!-- picture: start-valheim-missing.jpg: the start page's Home on a computer without Valheim found: the "Get the game's look" card with its four numbered steps, the folder box, Browse… and Use, the side menu visible on the left -->

1. In Steam, right-click Valheim in your Library, then **Manage › Browse local files**. The folder that opens is the
   one to choose: it holds `valheim_Data` (`valheim.app` on a Mac).
2. Copy its path, paste it into the card's box (or click **Browse…** and choose it), then click **Use**.

The usual places are:

- Windows: `C:\Program Files (x86)\Steam\steamapps\common\Valheim`
- Linux: `~/.local/share/Steam/steamapps/common/Valheim` (Flatpak Steam:
  `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Valheim`)
- macOS: `~/Library/Application Support/Steam/steamapps/common/Valheim`
- Another Steam library: `<library>/steamapps/common/Valheim`

A folder near it works too (`steamapps`, `common`, `valheim_Data` or `valheim.app`): the editor finds the game
folder from it. You can change it later in **Settings** (**Valheim game folder**), see
[Settings](../editor-basics.md#settings).

## The plugin for live editing

To edit a world while the game runs (your own game, or a dedicated server while people play), the game that hosts
the world needs **BepInEx** and the editor's **WorldEditorBridge** plugin. Players who join need neither, console
players included. To edit saved worlds with the game closed, or to build in the Workshop, you need neither.

For your own game:

1. Install BepInEx once: [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/),
   with a mod manager (r2modman, Thunderstore Mod Manager) or by hand into the Valheim folder, following its page.
2. Add the plugin. With a mod manager, install WorldEditorBridge from
   [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) or
   [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge) (BepInEx comes with it). By hand, copy
   `WorldEditorBridge.dll` from `WorldEditorBridge-<version>.zip` (on the
   [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest) or
   [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348)) into `BepInEx/plugins` of your Valheim. Use the same
   version as the editor.
3. Start Valheim with BepInEx and load your world (single player, or a server you start from the game). On this first
   start the plugin writes its settings, `BepInEx/config/Tie.WorldEditorBridge.cfg`, with a random **token**.
4. On the editor's start page, **Home** now says "Valheim is running with the world …" and **My game** shows
   *running*. The editor finds the plugin and its token by itself, mod manager profiles included. Click **Edit
   live**.

![My game on the start page, the game running](../images/start-game.jpg)
***My game** with the game running: the card names the world, the players connected and where the plugin was found
(here an r2modman profile), with **Edit live**.*

Until the game runs, **My game** says what is missing, step by step.

<!-- picture: start-game-setup.jpg: the start page on My game, Valheim not running and no plugin installed: the card "Your game needs BepInEx and the WorldEditorBridge plugin (once, about 5 minutes)" with its three numbered steps fully readable; side menu visible -->

Then follow
[Edit the world you are playing](../guides/edit-your-game-live.md).

For a **dedicated server**, install BepInEx (following BepInExPack's instructions for dedicated servers) and copy
`WorldEditorBridge.dll` into the server's `BepInEx/plugins` (or install the plugin in a mod manager's server
profile), then start the server once so it writes its token. The editor connects to it over SSH, through its own
encrypted tunnel: see [Edit a dedicated server while people play](../guides/edit-a-server-live.md).

## Add-ons for mods

Some world generation and building mods need an **add-on** for the editor to draw or edit what they make. Each is
its own download on the [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest):
`ValheimWorldEditor-addon-<Name>-<version>.zip`.

1. Unpack the add-on into the `addons` folder next to the editor (on macOS, next to `ValheimWorldEditor.app`).
2. Open **Settings**: under **Add-ons** it lists the add-on and says whether it is in use. An add-on is used while its
   mod is in your BepInEx plugins folders; its switch turns it off without removing it.

| Add-on | For |
|---|---|
| [Expand World](../../addons/ExpandWorld/README.md) | Worlds made with the Expand World mods (Size, Rivers, Data). |
| [Riverheim](../../addons/Riverheim/README.md) | Worlds made with Riverheim. |
| [Tunnelheim](../../addons/Tunnelheim/README.md) | The tunnels Tunnelheim digs, and a Tunnel tool. |

An add-on is code that runs inside the editor: install only those you trust. See
[Edit a world made with Expand World, Riverheim or Tunnelheim](../guides/use-mods-worlds.md).

## Where the editor keeps its files

Its settings, saved servers (`servers.cfg`), what it remembers between runs, the index of the game's files
(`game-index.json`) and its log (`ValheimWorldEditor.log`, a new one each run) are in:

- Windows: `%LOCALAPPDATA%\ValheimWorldEditor`
- Linux: `~/.local/share/ValheimWorldEditor`
- macOS: `~/Library/Application Support/ValheimWorldEditor`

The bottom of the start page's menu shows this folder, and **Open log** opens the log. Your blueprints are in
Homestead's folder, see [the Workshop](../workshop.md).

## Next

[Your first edit](first-edit.md): back up a world, raise a hill, plant trees, save, and see it in game.
