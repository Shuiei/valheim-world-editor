"""Drives the native editor for the documentation's pictures (its --driver mode, see Desktop/Driver.cs).

The editor runs with a stand-in home: none of this computer's characters, worlds, servers or
settings show in the pictures. Only the copied game look is shared (read only), so the pictures
have the game's textures and models. Nothing is ever saved: each scene's changes stay pending and
are discarded before the next one.
"""
import json
import os
import shutil
import subprocess
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[2]


class FakeGame:
    """Answers the start page's "is Valheim running?" like the WorldEditorBridge plugin, for the
    "My game" picture (only /status: nothing is ever edited live)."""

    def __init__(self, world, token="docs"):
        game = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                ok = self.path.startswith("/status") and self.headers.get("X-Bridge-Token") == game.token
                body = json.dumps({"world": world, "players": 0}).encode() if ok else b"{}"
                self.send_response(200 if ok else 403)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, *_):
                pass

        self.token = token
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.port = self.server.server_address[1]
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def close(self):
        self.server.shutdown()


class Editor:
    def __init__(self, app=None, width=1440, height=900, game_world=None, worlds=(), homestead=None):
        self.home = Path(tempfile.mkdtemp(prefix="vwe-docs-home-"))
        self.game = None
        # The stand-in home's own data folder (~/.local/share/ValheimWorldEditor): the start page
        # shows it as such. The copied game look is the real one, read only.
        data = self.home / ".local" / "share"
        (data / "ValheimWorldEditor").mkdir(parents=True)
        look = Path.home() / ".local" / "share" / "ValheimWorldEditor" / "game-look"
        if look.is_dir():
            (data / "ValheimWorldEditor" / "game-look").symlink_to(look)
        # A stand-in Valheim in the home's Steam library ("Found automatically" in Settings); the editor
        # is driven, so it never looks for Steam outside this home.
        valheim = self.home / ".local" / "share" / "Steam" / "steamapps" / "common" / "Valheim"
        (valheim / "valheim_Data" / "StreamingAssets" / "SoftRef" / "Bundles").mkdir(parents=True)
        # Worlds in Valheim's own folder, as a player has them (links to the read-only originals).
        local = self.home / ".config" / "unity3d" / "IronGate" / "Valheim" / "worlds_local"
        for w in worlds:
            local.mkdir(parents=True, exist_ok=True)
            (local / Path(w).name).symlink_to(Path(w).resolve())
        if game_world:
            # The plugin in an r2modman profile, as on many players' computers.
            self.game = FakeGame(game_world)
            bep = self.home / ".config" / "r2modmanPlus-local" / "Valheim" / "profiles" / "Default" / "BepInEx"
            (bep / "plugins").mkdir(parents=True)
            (bep / "plugins" / "WorldEditorBridge.dll").write_bytes(b"")
            (bep / "config").mkdir()
            (bep / "config" / "Tie.WorldEditorBridge.cfg").write_text(f"[Bridge]\nPort = {self.game.port}\nToken = {self.game.token}\n")
        if homestead:
            # Homestead (a copy of its Homestead.dll: the editor shows its version), in the stand-in
            # game's BepInEx, or with the plugin in its mod manager profile.
            plugins = (bep if game_world else valheim / "BepInEx") / "plugins"
            plugins.mkdir(parents=True, exist_ok=True)
            shutil.copy(homestead, plugins / "Homestead.dll")
        env = dict(os.environ, HOME=str(self.home), XDG_DATA_HOME=str(data), XDG_CONFIG_HOME=str(self.home / ".config"))
        app = app or REPO / "Desktop" / "bin" / "Debug" / "net8.0" / "ValheimWorldEditor.dll"
        cmd = ["dotnet", str(app)] if str(app).endswith(".dll") else [str(app)]
        cmd += ["--driver", "--window", f"{width}x{height}"]
        self.width = width
        self.p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, env=env)
        if self._answer() != "@@ ready":
            raise RuntimeError("the editor did not start")

    def _answer(self):
        while True:
            line = self.p.stdout.readline()
            if not line:
                raise RuntimeError("the editor stopped")
            if line.startswith("@@ "):
                return line.rstrip("\n")

    def send(self, command):
        self.p.stdin.write(command + "\n")
        self.p.stdin.flush()
        answer = self._answer()
        if not answer.startswith("@@ ok "):
            raise RuntimeError(f"{command}: {answer}")
        return json.loads(answer[len("@@ ok "):])

    def shot(self, path, quality=85, keep_message=False):
        """The whole window, saved as a JPEG for the docs (the status bar's message cleared unless
        kept: it would tell how the scene was set up)."""
        if not keep_message:
            self.send("message")
        png = self.home / "shot.png"
        state = self.send(f"shot {png}")
        img = Image.open(png).convert("RGB")
        # At the window's own size (the screen's scaling makes the snapshot bigger).
        if img.width > self.width:
            img = img.resize((self.width, round(img.height * self.width / img.width)), Image.LANCZOS)
        img.save(path, quality=quality, optimize=True)
        return state

    def close(self):
        try:
            self.p.stdin.write("quit\n")
            self.p.stdin.flush()
            self.p.wait(15)
        except Exception:
            self.p.kill()
        if self.game:
            self.game.close()
        shutil.rmtree(self.home, ignore_errors=True)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
