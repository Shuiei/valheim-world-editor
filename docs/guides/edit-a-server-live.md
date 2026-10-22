# Edit a dedicated server while people play

Connect the editor to your dedicated server and change its world while players stay online.

**You need:** an SSH login to the server (a password or a key file), the right to install BepInEx and the
WorldEditorBridge plugin there and to restart it.

![The dedicated server form: name, server address and SSH port, user and password, SSH key file, plugin token, the server's Valheim folder, Save password and Connect](../images/start-server.jpg)
*The form under **A dedicated server**. After the first connection the server is saved above it, for one-click access.*

1. On the server, install BepInEx ([BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/),
   following its dedicated-server instructions).
2. Copy `WorldEditorBridge.dll` (from `WorldEditorBridge-<version>.zip` on the
   [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest)) into the server's
   `BepInEx/plugins`, or install WorldEditorBridge in a mod manager's server profile.
3. Restart the server with BepInEx. Its `BepInEx/LogOutput.log` now says `WorldEditorBridge <version> listening on
   http://127.0.0.1:5182/`.
4. On the server, open `BepInEx/config/Tie.WorldEditorBridge.cfg` and copy the value of its `Token` line. The file
   exists only once the server has started with the plugin.
5. In the editor's start page, choose **A dedicated server**, then **Connect to a server**.
6. Fill in **Server address**, **User**, and **Password** or **SSH key file (optional)**. Paste the token into
   **Plugin token**.
7. Optional: fill in **Server's Valheim folder (optional)** (the folder that holds `BepInEx`), so the editor can say
   exactly what is wrong if the plugin does not answer. Tick **Save password** to not type it again.
8. Press **Connect**. The editor logs in over SSH, opens its own encrypted tunnel to the plugin and loads the world.
9. On the map, choose a spot, **Edit in 3D**, make a change and press **Apply live**.

## Check it worked

- The top bar shows **LIVE**; after the apply, "All applied" and "Applied to the running game: …" in the status bar.
- Players near the change see it at once.
- The server now shows above the form, and on **Home**, for one-click access next time.

If a message appears under the form instead, see [A server will not connect](../help/troubleshooting.md#a-server-will-not-connect).

## Undo it

Press `Ctrl+Z` (or **Remove** the step in History), then **Apply live** again. Zone resets cannot be undone once
applied.

## The form's other fields

| Field | What it is |
|---|---|
| **Name** | How the server shows in your list (optional; the address otherwise). |
| **Server address** | The server's name or IP address. You can also type `user@address` here. |
| **SSH port** | 22 unless the server's SSH was changed. |
| **SSH key file (optional)** | Your private key (**Browse…** to choose it). Empty: the usual keys in `~/.ssh` (`id_ed25519`, `id_ecdsa`, `id_rsa`) are tried; one the editor cannot use is skipped, and the others and the password are still tried. |
| **Plugin token** | Always typed by you: the editor never reads it from the server, so an SSH login alone cannot change the world. |
| **More options** → **Key passphrase** | The key's passphrase, when it has one. Never saved. |
| **More options** → **Plugin port** | The plugin's port on the server, when it is not 5182 and no folder is given. |
| **More options** → **I made my own tunnel** | **Bridge address** and **Token**, then **Connect**: for a tunnel you made with `ssh -L` or PuTTY. |

## Saved servers

After the first connection the server is listed above the form, most recent first, with **Connect**, **edit** and
**forget**; the form folds away under **Connect to another server**. Each row shows `user@address`, whether it uses a
key file, a saved password or asks for one, and boxes for what is not saved.

- **edit** fills the form with the server; change what you need, then **Connect** updates it.
- **forget** removes it, with its password and token.

The servers are kept in `servers.cfg` in the editor's data folder (the start page's side menu shows it), one section
per server (`Host`, `SshPort`, `User`, `Password`, `KeyFile`, `Token`, `BridgePort`, `GameFolder`, `HostKey`), so you
can also edit it by hand. A server is one account on one address and port: connecting again with the same ones
updates its section.

`Password` is only filled when **Save password** was ticked, as plain text (on Linux only you can read the file):
prefer an SSH key.

`HostKey` is the server's identity, remembered on the first connection. If it ever changes, the editor refuses to
connect, as someone could be pretending to be your server. If you reinstalled the server, empty the `HostKey` line of
each saved server with that address to accept the new identity.

## Related

- [Live mode](../live-mode.md): Apply live, Auto, following the game, players
- [Live editing](../concepts/live-editing.md#how-it-connects): the SSH tunnel and the token
- [Edit the world you are playing](edit-your-game-live.md)
- [Back up a world before editing](back-up-a-world.md)
