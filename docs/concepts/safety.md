# Safety and privacy

The editor is a program on your computer. It writes to your worlds only when you press **Save to world**, sends
nothing anywhere unless you connect it to a server or to Claude, and keeps secrets out of its log. This page says
what it writes, what can leave your computer, and what runs with your rights.

## What the editor writes

**Your worlds**, only when you save. Until then every change is pending and lives in the editor's memory (see
[Pending changes](saving-and-history.md#pending-changes)). A save adds a new numbered save to the world's folder and
removes nothing old until it has read the new one back correctly (see
[Saving a world](saving-and-history.md#saving-a-world)). A new world goes into a new folder, never over a world of
the same name. Close the game first: a running game writes over the files the editor saved.

**A live game**, only when you apply. The game then saves the changes itself, as it saves what players build.

**Your blueprints**: a `.blueprint` file and its `.png` picture in Homestead's folder in Valheim's save folder, when you
save a blueprint. **Delete** removes both, after asking.

**Its own data folder**: `~/.local/share/ValheimWorldEditor` on Linux, `%LOCALAPPDATA%\ValheimWorldEditor` on Windows,
`~/Library/Application Support/ValheimWorldEditor` on macOS (the bottom of the start page's menu shows it). It holds:

| What | Files |
|---|---|
| Your settings and choices | `settings.json`, `prefs.json`, `place.json` (Place presets, favourites, recent kinds), `stamps.json`, `selections.json` |
| Saved servers | `servers.cfg`: address, user, token, the server's identity, and the password when you ticked **Save password** (see below) |
| History | `history/`, one file per world (see [History](saving-and-history.md#history)) |
| Your work | `scripts/` (saved scripts), `heightmaps/` (exported heightmaps), `blueprints/export` (`.vbuild` files) |
| What it learned from your game | `game-index.json` and other `game-*.json` files (where things are in Valheim's files), `mod-catalogue.json` (mods' objects seen in a live game), `mod-bundles/` (mods' models, copied out of their DLLs) |
| The log | `ValheimWorldEditor.log`, a new one each run; `perf-native.log` only while **Record frame rates** is on |

**What it never changes.** The editor reads your Valheim game folder, your BepInEx folders and your mods' files (models,
textures, the game's rules, the plugin's settings) and writes nothing there. It reads mods' code to learn the names
of their values, and never runs it.

**Passwords.** A saved password is kept as plain text in `servers.cfg`. On Linux the file can be read only by you;
still, prefer an SSH key where you can. A key's passphrase is never saved. **forget** next to a saved server removes
it, its password and its token with it.

## Nothing leaves your computer

The editor has no account, no tracking and no update check. It talks to the outside only in the cases below, each
of them your choice:

| When | What goes where |
|---|---|
| You connect to a **dedicated server** | An SSH connection, encrypted, to that server only, with the account you give. The plugin is reached through it, never over the open internet (see [How it connects](live-editing.md#how-it-connects)). |
| You edit **your own game** live | Nothing leaves the computer: the editor talks to the plugin on `127.0.0.1`. |
| You connect **Claude** | Claude's app on your computer talks to the editor on `127.0.0.1`. What the editor answers (the area's data, pictures of the 3D view) goes to Claude, as anything you give Claude does. |
| You click a link | **Get Homestead**, or a help page opened on GitHub, opens in your web browser. |

**The plugin stays private.** WorldEditorBridge listens only on the computer it runs on, and answers only requests
that carry its secret token. For a server you type the token yourself; the editor never reads it from the server, so
an SSH login alone cannot change the world. Keep the token private: anyone with it and access to the port can change
the world. Empty the `Token` line of `Tie.WorldEditorBridge.cfg` and restart the game to get a new one.

**The server's identity is checked.** The editor remembers a server's identity on the first connection. If it ever
changes, the editor refuses to connect, because someone could be pretending to be your server. After you reinstall a
server, empty its `HostKey` line in `servers.cfg` to accept the new one.

## Scripts and Claude

**Scripts are programs.** A [script](../scripting.md) is C# code, and the editor runs it with your rights, like any
program you start. It runs in a separate process so that **Stop** can always end it, but that process is not a
sandbox: a script can read and write your files and do anything else your account can. Run only scripts you wrote or
trust, the same as you would with a mod.

**Claude works for you, under limits.** With **Allow Claude to connect** on in Settings, [Claude](../claude.md) can use
the editor's tools. The editor holds it to this:

- **This computer only, with a token.** The editor listens on `127.0.0.1` only, refuses requests addressed to another
  name (so a web page cannot reach it under a made-up address), and refuses any request without its token (48 random
  characters). **New token** makes another; turn the switch off and nothing can connect.
- **Pending only.** Whatever Claude changes stays pending, one step each, tagged **Claude** in History. Claude never
  saves, applies live, discards, reloads, writes or deletes your files, or connects to a live game: those stay yours.
  It does not open another world while yours has unsaved changes.
- **Nothing live with Auto on.** While a live world has **Auto** on, each change would reach the players at once, so
  Claude changes nothing and asks you to turn Auto off.
- **No scripts without your leave.** Claude's scripts are refused until you turn on **Claude may run scripts** in
  Settings (off by default), a choice you make once, as for a document's macros. Its other tools run the editor's own
  code and need no leave.
- **Easy to take back.** **Take back Claude's changes** in History removes every step Claude made in the open area
  and keeps yours (see [History](saving-and-history.md#history)).

## Mods' extensions and add-ons

A mod can ship an **editor extension**: a small piece of code the editor loads to read and draw that mod's objects.
Like a script, it runs with your rights, so you decide. Each extension is **off** until you turn it on in **Settings →
Mod extensions**, and only that exact file: the editor remembers its fingerprint (SHA-256), so a changed file, such as
a mod update, is off again until you turn it on again.

**Add-ons**, which you download and unpack into the editor's `addons` folder yourself, run as installed, since
installing one is already your choice. **Settings → Add-ons** turns any of them off.

## What the log keeps out

The editor writes what it does, and every error it handles, to `ValheimWorldEditor.log`. **Open log** at the bottom
of the start page's menu opens it, so you can attach it to a bug report. Before a line is written, personal data is
taken out:

- your home folder is written `~`, and other file paths, web addresses, `user@host` names, IP and server addresses
  and your account name are replaced by placeholders such as `<path>`;
- the saved servers' accounts, passwords, passphrases and tokens never reach it.

The WorldEditorBridge plugin writes to the game's own `BepInEx/LogOutput.log`, with the server's home folder written
`~` too.

Still, read a log before you post it publicly. And keep your own copies of the worlds you care about (see
[Back up a world before editing](../guides/back-up-a-world.md)).
