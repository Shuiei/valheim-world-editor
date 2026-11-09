# Edit a rented or panel-hosted server

Edit the world of a server you rent from a game host, or one that a game panel such as Pterodactyl or Pelican runs
in a container on your own machine.

The editor reaches a dedicated server through SSH, to the plugin listening on the server's own `127.0.0.1` (see
[Edit a dedicated server while people play](edit-a-server-live.md)). Two kinds of servers do not fit that:

- **A game server rented from a host** (you get a web panel and a file manager or FTP/SFTP, but no login to the
  machine): the editor cannot reach the plugin there. Edit the world's files instead: see
  [A rented game server](#a-rented-game-server).
- **A game panel on your own machine** (Pterodactyl, Pelican, or any server running in a Docker container): live
  editing works, through a tunnel you open yourself. See [A server in a container](#a-server-in-a-container).

If your host gives you a real SSH login to the machine that runs the game (a rented VPS or dedicated machine, not
only a game server), follow [Edit a dedicated server while people play](edit-a-server-live.md) instead.

## A rented game server

**You need:** the host's panel or file access (its file manager, FTP or SFTP) and the right to stop and start the
server.

Live mode is not possible: the plugin only answers on the machine itself, and the host gives no way in. A panel's
SFTP login (often on port 2022) only hands out files: the editor's SSH cannot open a tunnel through it. Edit the saved world instead:

1. In the host's panel, **stop** the server. A running server saves over the files you put back.
2. With the panel's file manager or an SFTP program (FileZilla, WinSCP), download the world's folder,
   `worlds_local/<World>` in the server's save folder. Keep a copy of it untouched: it is your backup.
3. In the editor, open that folder: **A saved world** → **Another world folder** → **Browse…** (see
   [Open a world that is not in the list](open-a-world.md)).
4. Make your changes and press **Save to world**.
5. Upload the folder back to the same place, replacing the files there, then start the server.

The server must stay stopped from step 1 to step 5: what players do in between is lost when you upload.

## A server in a container

**You need:** an SSH login to the machine the panel runs on (your VPS, as root or a user allowed to run `docker`),
and the right to edit the game server's files in the panel.

A panel runs each game server in its own Docker container, which has its own `127.0.0.1`. The plugin listens there,
inside the container, while the editor's own tunnel reaches the machine's `127.0.0.1`, where nothing answers: the
editor says the plugin does not answer, though the server runs with it. The way in is a tunnel to the container's own
address:

```text
 your computer               your VPS          the server's container
┌───────────────────────┐   ┌───────────┐     ┌───────────────────────┐
│ editor                │SSH│           │     │ WorldEditorBridge     │
│ 127.0.0.1:15182 ══════╪═══╪═► sshd ───┼────►│ 172.18.0.x:5182       │
└───────────────────────┘   └───────────┘     └───────────────────────┘
```

1. In the panel, install BepInEx and WorldEditorBridge in the server, as for any dedicated server (steps 1 to 3 of
   [Edit a dedicated server while people play](edit-a-server-live.md)), and start it once.
2. In the panel's file manager, open `BepInEx/config/Tie.WorldEditorBridge.cfg`. Set `BindAddress = +` and copy the
   value of the `Token` line.
3. Restart the server. Its console or `BepInEx/LogOutput.log` says `WorldEditorBridge <version> listening on
   http://+:5182/`, then a warning that the token travels unencrypted: inside the container, that is only the panel's
   own network.
4. Log in to the VPS over SSH and find the container's address. The container is named after the server's id (the
   panel shows it in the server's settings); `docker ps` lists them all:

   ```sh
   docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' <server id>
   ```

   It answers an address such as `172.18.0.5`.
5. On your computer, open a tunnel to it and leave it open while you edit (replace the address and the login):

   ```sh
   ssh -N -L 15182:172.18.0.5:5182 user@your-vps
   ```

   With PuTTY: **Connection** → **SSH** → **Tunnels**, **Source port** `15182`, **Destination** `172.18.0.5:5182`,
   **Add**, then open the session.
6. In the editor's start page: **A dedicated server** → **More options** → **I made my own tunnel**. Type
   `127.0.0.1:15182` as **Bridge address**, paste the token, and press **Connect**.

Then edit as on any server: see [Live mode](../live-mode.md#use-it).

**Keep in mind:**

- `+`, not the container's address nor `0.0.0.0`: with the container's address the plugin refuses every request
  that comes through a tunnel, and with `0.0.0.0` every request ("The server answered with an error (400)").
- Do not add the plugin's port (5182) to the server's ports (allocations) in the panel: that opens it to the
  internet.
- The container's address can change when the server is rebuilt or reinstalled. If the tunnel stops answering, look
  it up again (step 4).
- Keep `BindAddress = 127.0.0.1` on a server that does not run in a container: there, `+` makes the plugin listen on
  every network the machine is on (callers from the internet are still refused).

## Check it worked

- **A rented game server:** the server starts, and your changes are there in game.
- **A server in a container:** the top bar shows **LIVE**; after **Apply live**, "All applied" in the status bar.

If the editor shows a message instead, see [A server will not connect](../help/troubleshooting.md#a-server-will-not-connect).

## Undo it

- **A rented game server:** stop the server and upload the copy you kept in step 2.
- **A server in a container:** `Ctrl+Z`, then **Apply live** again. To close the plugin again, set `BindAddress =
  127.0.0.1` and restart the server.

## Related

- [Edit a dedicated server while people play](edit-a-server-live.md)
- [Plugin settings](../live-mode.md#plugin-settings): `BindAddress`, `Port`, `Token`
- [Open a world that is not in the list](open-a-world.md)
- [Back up a world before editing](back-up-a-world.md)
