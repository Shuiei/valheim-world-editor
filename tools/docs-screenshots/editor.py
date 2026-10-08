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
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[2]


class Editor:
    def __init__(self, app=None, width=1440, height=900):
        self.home = Path(tempfile.mkdtemp(prefix="vwe-docs-home-"))
        data = self.home / ".local" / "share"
        (data / "ValheimWorldEditor").mkdir(parents=True)
        look = Path.home() / ".local" / "share" / "ValheimWorldEditor" / "game-look"
        if look.is_dir():
            (data / "ValheimWorldEditor" / "game-look").symlink_to(look)
        env = dict(os.environ, HOME=str(self.home), XDG_DATA_HOME=str(data), XDG_CONFIG_HOME=str(self.home / ".config"))
        app = app or REPO / "Desktop" / "bin" / "Debug" / "net8.0" / "ValheimWorldEditor.dll"
        cmd = ["dotnet", str(app)] if str(app).endswith(".dll") else [str(app)]
        cmd += ["--driver", "--data", str(self.home / "data"), "--window", f"{width}x{height}"]
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

    def shot(self, path, quality=85):
        """The whole window, saved as a JPEG for the docs."""
        png = self.home / "shot.png"
        state = self.send(f"shot {png}")
        Image.open(png).convert("RGB").save(path, quality=quality, optimize=True)
        return state

    def close(self):
        try:
            self.p.stdin.write("quit\n")
            self.p.stdin.flush()
            self.p.wait(15)
        except Exception:
            self.p.kill()
        shutil.rmtree(self.home, ignore_errors=True)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
