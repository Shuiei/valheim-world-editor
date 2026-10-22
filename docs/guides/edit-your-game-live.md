# Edit the world you are playing

Change the world of your own running game (single player, or the world you host) and see each change in game at once.

**You need:** Valheim on this computer, the editor, and about 5 minutes to install BepInEx and the WorldEditorBridge
plugin once.

<!-- picture: start-game-setup.jpg: start page, My game chosen in the side menu, Valheim without BepInEx: the card "Your game needs BepInEx and the WorldEditorBridge plugin (once, about 5 minutes)" with its three numbered steps fully visible -->

1. Install **BepInEx** in your Valheim, once: [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/),
   with a mod manager (r2modman, Thunderstore Mod Manager) or by hand into the Valheim folder, as its page explains.
   On Linux with the native game, BepInEx starts through the pack's start script (its page shows the Steam launch
   option); a mod manager does that for you.
2. Add the plugin. With a mod manager, install **WorldEditorBridge** from
   [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) or
   [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge). By hand, copy `WorldEditorBridge.dll` from
   `WorldEditorBridge-<version>.zip` (on the editor's [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest))
   into `BepInEx/plugins` of your Valheim or mod manager profile. Use the plugin of the same release as your editor.
3. Start Valheim with BepInEx and load your world (single player, or start a server from the game to host it). On
   this first start the plugin writes its settings and secret token, which the editor reads by itself.
4. In the editor, open the start page. **Home** shows "Valheim is running with the world …" and how many players are
   connected; **My game** in the side menu says *running*.
5. Click **Edit live**. The world opens on its map.
6. Click a spot near you and choose **Edit in 3D**.
7. Make a change, for example raise a small hill with the **Raise** brush.
8. Press **Apply live** (or `Ctrl+S`).

![My game on the start page: Valheim is running with the world, the number of players, and the Edit live button](../images/start-game.jpg)
*The start page has noticed the running game by itself: **Edit live** opens its world.*

## Check it worked

- The top bar shows **LIVE**, and after the apply "All applied".
- The status bar says "Applied to the running game: …".
- In game, the hill is there at once. The game saves it like any other change.

Until the game is ready, **My game** says what is missing: the three steps, "BepInEx is installed: only steps 2 and 3
are left", "Start Valheim once with BepInEx" (the plugin has never run), or "Start Valheim and load your world". If it
stays there, see [My game shows steps to do](../help/troubleshooting.md#my-game-shows-steps-to-do).

## Undo it

Press `Ctrl+Z` (or **Remove** the step in History), then **Apply live** again: the game takes the change back. Zone
resets cannot be undone once applied. To have every change applied as you make it, switch on **Auto**.

## Related

- [Live mode](../live-mode.md): Apply live, Auto, following the game, players
- [Edit a dedicated server while people play](edit-a-server-live.md)
- [Live editing](../concepts/live-editing.md#how-it-connects): how the editor reaches the game
- [Undo a step, take back one change, or discard everything](undo-changes.md)
