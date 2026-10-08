"""The documentation's pictures (docs/images), taken in the native editor.

The worlds are made from seeds with tools/WorldCheck (see docs/development.md) and never change:

    WorldCheck create <dir>/Docs Docs yjRO99yNTI 16 --flat     the bare zone the tools are shown on
    WorldCheck create <dir>/Fjordheim Fjordheim Fjord2026 3     a second world for the start page

Each scene opens a fresh editor (a stand-in home: no one's names or folders), builds what it shows
with the editor's own tools on the Docs world's bare zone, takes its pictures and leaves without
saving: what a picture shows is only that scene's own changes. Run:

    python3 tools/docs-screenshots/scenes.py <dir> [scene ...]

Pictures go to docs/images (or $DOCS_OUT). With scene names, only those scenes run.
"""
import os
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from editor import Editor, REPO  # noqa: E402

DIR = None
OUT = Path(os.environ.get("DOCS_OUT", REPO / "docs" / "images"))

# The Docs world's bare zone (WorldCheck create --flat): its middle, in world metres.
ZONE = (-8, 1)
C = (ZONE[0] * 64, ZONE[1] * 64)


def P(dx, dz):
    """A point on the bare zone, dx east and dz north of its middle."""
    return (C[0] + dx, C[1] + dz)


def world():
    return f"{DIR}/Docs"


def bare(e, view=False, water=False, size=1, unsaved=True, slope=False, limit=False):
    """The bare zone in the 3D editor, a camera that sees all of it while the scene works. The View
    switches are set first, while the View panel is open (closed afterwards unless view): the sea
    hidden, the red ±8 m limit tint off (it would hide the edits), the green unsaved marks as asked."""
    e.send(f"world {world()}")
    e.send(f"area {ZONE[0]} {ZONE[1]} {size}")
    for switch, wanted, default in (("Water", water, True), ("Limit marks", limit, True), ("Unsaved marks", unsaved, True), ("Slope colours", slope, False)):
        if wanted != default:
            e.send(f"click {switch}")
    if not view:
        e.send("click View")
    camera(e, 0, 0, 200, 70, 95)


def camera(e, dx, dz, yaw, pitch, distance):
    e.send(f"camera {C[0] + dx} {C[1] + dz} {yaw} {pitch} {distance}")


def move(e, p, mods=""):
    return e.send(f"mouse move {p[0]} {p[1]} left {mods}".strip())


def drag(e, a, b, steps=4, button="left", mods="", hold=30, release=True):
    """A mouse drag on the ground from a to b (release=False: the button stays down)."""
    move(e, a, mods)
    e.send(f"mouse down {a[0]} {a[1]} {button} {mods}".strip())
    for i in range(1, steps + 1):
        x = a[0] + (b[0] - a[0]) * i / steps
        z = a[1] + (b[1] - a[1]) * i / steps
        e.send(f"mouse move {x} {z} {button} {mods}".strip())
        e.send(f"wait {hold}")
    return e.send(f"mouse up {b[0]} {b[1]} {button} {mods}".strip()) if release else None


def path(e, points, steps=3, hold=60, button="left", mods="", release=True):
    """A drag through several points (a brush stroke along them)."""
    move(e, points[0], mods)
    e.send(f"mouse down {points[0][0]} {points[0][1]} {button} {mods}".strip())
    for a, b in zip(points, points[1:]):
        for i in range(1, steps + 1):
            x = a[0] + (b[0] - a[0]) * i / steps
            z = a[1] + (b[1] - a[1]) * i / steps
            e.send(f"mouse move {x} {z} {button} {mods}".strip())
            e.send(f"wait {hold}")
    end = points[-1]
    return e.send(f"mouse up {end[0]} {end[1]} {button} {mods}".strip()) if release else None


def click(e, p, mods=""):
    move(e, p, mods)
    e.send(f"mouse down {p[0]} {p[1]} left {mods}".strip())
    return e.send(f"mouse up {p[0]} {p[1]} left {mods}".strip())


def stroke(e, key, at, size, hold=1200, steps=0):
    """A brush held still at a point for a while (raise a mound, dig a hole)."""
    e.send(f"key {key}")
    e.send(f"set Size|{size}")
    move(e, at)
    e.send(f"mouse down {at[0]} {at[1]}")
    for _ in range(max(1, steps)):
        e.send(f"wait {hold // max(1, steps)}")
    return e.send(f"mouse up {at[0]} {at[1]}")


def area_action(e, a, b, action, apply=True):
    """The Area tool: a box from a to b, the action, Enter."""
    e.send("key B")
    drag(e, a, b)
    e.send(f"choose {action}")
    return e.send("key Return") if apply else e.send("state")


def kinds(e, *names):
    """The Place tool's kinds: exactly these."""
    e.send("click + Add kinds")
    e.send("click Untick all")
    for n in names:
        e.send(f"type Search kinds|{n}")
        e.send(f"click {n}")
    e.send("type Search kinds|")
    e.send("click Done")


def pieces(e):
    """Building pieces as the game places them: true size, upright, all facing the rotation."""
    e.send("set Tilt|0")
    e.send("set Size %|100,100")
    e.send("set Rotation|0")
    e.send("click Random facing (off: all face the rotation)")


def nature(e):
    """Trees and rocks as nature has them: sizes, tilts and facings vary."""
    e.send("set Tilt|4")
    e.send("set Size %|80,120")


def level(e, a=P(-16, -14), b=P(16, 14)):
    """Ground levelled at its average height, its edge softened (a change of this scene, not saved)."""
    area_action(e, a, b, "Flatten", apply=False)
    e.send("set Soft edge|8")
    e.send("click avg")
    e.send("key Return")
    e.send("click Clear (Esc)")


def trees(e, a, b, density=6, kinds_=("Beech1", "Oak1", "Birch1")):
    """A wood planted with the Place brush along a stroke."""
    e.send("key T")
    kinds(e, *kinds_)
    e.send("click Brush")
    nature(e)
    e.send("set Brush size|8")
    e.send(f"set Density|{density}")
    drag(e, a, b, steps=6, hold=120)
    e.send("key Escape")


def build_base(e):
    """A small wooden house on a floor, a crop field and a fire pit, inside a palisade."""
    level(e, P(-18, -18), P(18, 14))
    # The field: cultivated ground.
    e.send("key D7")
    e.send("set Size|3")
    e.send("set Strength|1")
    for dz in (-14, -11, -8):
        path(e, [P(-2, dz), P(14, dz)], steps=6, hold=150)
    e.send("key T")
    pieces(e)
    # The floor: a grid of 2 m floor pieces, then walls around it, end to end, two high.
    kinds(e, "wood_floor")
    e.send("click Grid")
    e.send("set Spacing|2")
    drag(e, P(-14, 2), P(-2, 10))
    e.send("key Return")
    kinds(e, "woodwall")
    e.send("click Line")
    e.send("click Rectangle")
    e.send("set Layers|2")
    drag(e, P(-14, 2), P(-2, 10))
    e.send("key Return")
    # Carrots and turnips in the field.
    kinds(e, "sapling_carrot", "sapling_turnip")
    e.send("click Grid")
    e.send("set Spacing|1")
    drag(e, P(-1, -15), P(13, -7))
    e.send("key Return")
    # A fire pit and a chest by the house.
    kinds(e, "fire_pit")
    e.send("click Brush")
    e.send("click One at a time, exactly at the cursor")
    click(e, P(4, 4))
    kinds(e, "piece_chest_wood")
    click(e, P(-1, 8))
    e.send("click One at a time, exactly at the cursor")
    # A palisade around it all.
    kinds(e, "stake_wall")
    e.send("click Line")
    e.send("click Circle")
    drag(e, P(0, 0), P(24, 0))
    e.send("key Return")
    e.send("key Escape")


# ---- The scenes.

def scene_overview(e):
    # The new pieces' green marks off: every piece of this base is unsaved, the dots would hide it.
    # The limit marks on, as by default (nothing here reaches the limit).
    bare(e, view=True, unsaved=False, limit=True)
    build_base(e)
    trees(e, P(-26, 26), P(26, 27))
    e.send("panel view")
    camera(e, -2, 2, 20, 30, 60)
    e.shot(OUT / "overview.jpg")


def scene_unsaved(e):
    bare(e)
    level(e)
    trees(e, P(-10, 4), P(10, 10), density=4)
    camera(e, 0, 0, 20, 40, 48)
    e.shot(OUT / "unsaved.jpg", keep_message=True)


def scene_sculpt(e):
    bare(e)
    stroke(e, "D1", P(0, 0), 9, hold=2500, steps=5)
    e.send("key D3")
    e.send("set Size|7")
    move(e, P(7, -3))
    camera(e, 0, 0, 200, 38, 40)
    e.shot(OUT / "sculpt.jpg")


def scene_paint(e):
    bare(e)
    level(e)
    for key, dz in (("D6", 9), ("D7", 0), ("D8", -9)):
        e.send(f"key {key}")
        e.send("set Size|4")
        e.send("set Strength|1")
        path(e, [P(-16, dz), P(16, dz)], steps=8, hold=150)
    move(e, P(20, -9))
    camera(e, 0, 0, 200, 50, 46)
    e.shot(OUT / "paint.jpg")


def scene_path(e):
    bare(e)
    stroke(e, "D1", P(-6, 8), 10, hold=3000, steps=5)
    stroke(e, "D1", P(12, -6), 8, hold=2500, steps=5)
    e.send("key P")
    for p in (P(-24, -20), P(-10, -6), P(2, 2), P(14, 12), P(24, 24)):
        click(e, p)
    camera(e, 0, 0, 200, 40, 62)
    e.shot(OUT / "path.jpg")


def scene_mask(e):
    """The Mask limiting Raise to Meadows below 64 m and under 25°: the stroke across a steep mound
    raises the flat ground on both sides and leaves the mound's steep sides as they were."""
    bare(e)
    stroke(e, "D1", P(0, 2), 6, hold=4000, steps=6)
    e.send("click Mask")
    e.send("click Meadows")
    e.send("set Height|,64")
    e.send("set Slope|,25")
    e.send("key D1")
    e.send("set Size|9")
    e.send("set Strength|0.6")
    path(e, [P(-26, 2), P(26, 2)], steps=12, hold=150)
    move(e, P(20, 2))
    camera(e, 0, 0, 20, 30, 46)
    e.shot(OUT / "mask.jpg")


def scene_measure(e):
    bare(e, slope=True)
    stroke(e, "D1", P(0, 0), 10, hold=2200, steps=6)
    e.send("key M")
    click(e, P(-20, -6))
    click(e, P(16, 6))
    camera(e, 0, 0, 200, 35, 48)
    e.shot(OUT / "measure.jpg")


def scene_area(e):
    bare(e, unsaved=False)
    trees(e, P(-22, -10), P(22, 12), density=5, kinds_=("Beech1", "Birch1", "Rock_3", "Bush01"))
    area_action(e, P(-14, -12), P(10, 10), "Remove objects", apply=False)
    camera(e, 0, 0, 200, 50, 55)
    e.shot(OUT / "area.jpg")


def scene_paste(e):
    bare(e, unsaved=False)
    trees(e, P(-24, -18), P(-12, -8), density=8)
    e.send("key B")
    drag(e, P(-27, -21), P(-9, -5))
    e.send("key C ctrl")
    e.send("key V ctrl")
    # One copy: the caption says "a copy", and more 22 m copies run off the 64 m zone.
    e.send("set Copies|1")
    for _ in range(2):
        e.send("key OemComma shift")
    move(e, P(10, 10))
    camera(e, 0, 0, 200, 62, 95)
    e.shot(OUT / "paste.jpg")


def scene_plant_brush(e):
    bare(e, unsaved=False)
    e.send("key T")
    e.send("choose Meadows woods")
    nature(e)
    e.send("click Brush")
    e.send("set Brush size|10")
    drag(e, P(-26, 18), P(-6, 22), steps=6, hold=150)
    # Far enough that the preview (a beech is about 20 m tall) does not fill the picture.
    camera(e, 2, 2, 20, 58, 105)
    move(e, P(8, -8))
    e.send("wait 500")
    move(e, P(8, -7))
    e.shot(OUT / "plant-brush.jpg")


def scene_plant_line(e):
    bare(e)
    level(e)
    e.send("key T")
    pieces(e)
    kinds(e, "wood_fence")
    e.send("click Line")
    e.send("click Points")
    for p in (P(-22, -12), P(-6, -2), P(8, -6), P(20, 10)):
        click(e, p)
    move(e, P(20, 10))
    camera(e, 0, 0, 200, 40, 48)
    e.shot(OUT / "plant-line.jpg")


def scene_plant_grid(e):
    bare(e)
    level(e)
    e.send("key D7")
    e.send("set Size|4")
    e.send("set Strength|1")
    for dz in range(-12, 13, 4):
        path(e, [P(-16, dz), P(16, dz)], steps=6, hold=150)
    e.send("key T")
    pieces(e)
    kinds(e, "sapling_carrot", "sapling_turnip")
    e.send("click Grid")
    e.send("set Spacing|1.5")
    e.send("set Rotation|15")
    drag(e, P(-12, -9), P(12, 9))
    camera(e, 0, 0, 200, 45, 42)
    e.shot(OUT / "plant-grid.jpg")


def scene_plant_zone(e):
    bare(e)
    e.send("key T")
    nature(e)
    kinds(e, "Beech_small1", "Bush01", "RaspberryBush")
    e.send("click Zone")
    for p in (P(-20, -14), P(-4, -20), P(16, -12), P(22, 6), P(6, 18), P(-14, 14)):
        click(e, p)
    camera(e, 0, 0, 20, 50, 58)
    e.shot(OUT / "plant-zone.jpg")


def chest(e):
    """A chest on levelled ground, selected."""
    level(e)
    e.send("key T")
    pieces(e)
    kinds(e, "piece_chest_wood")
    e.send("click Brush")
    e.send("click One at a time, exactly at the cursor")
    click(e, P(0, 0))
    e.send("key E")
    click(e, P(0, 0))


def scene_select_arrows(e):
    # A rock, as the caption says (the chest is the Inspector's picture).
    bare(e)
    level(e)
    e.send("key T")
    nature(e)
    kinds(e, "Rock_3")
    e.send("click Brush")
    e.send("click One at a time, exactly at the cursor")
    click(e, P(0, 0))
    e.send("key E")
    click(e, P(0, 0))
    camera(e, 0, 0, 200, 35, 14)
    e.shot(OUT / "select-arrows.jpg")


def scene_inspector(e):
    bare(e)
    chest(e)
    e.send("key I")
    camera(e, 0, 0, 200, 35, 10)
    e.shot(OUT / "inspector.jpg")


def scene_select_zone(e):
    bare(e, unsaved=False)
    trees(e, P(-20, -8), P(20, 8), density=6, kinds_=("Rock_3", "Bush01", "Beech_small1", "RaspberryBush"))
    e.send("key E")
    ring = [P(-12, -10), P(6, -12), P(14, -2), P(10, 10), P(-6, 12), P(-14, 2), P(-12, -9)]
    path(e, ring, steps=2, hold=30, mods="alt", release=False)
    camera(e, 0, 0, 200, 48, 36)
    e.shot(OUT / "select-zone-drawing.jpg")
    e.send(f"mouse up {ring[-1][0]} {ring[-1][1]} left alt")
    e.send("wait 300")
    e.shot(OUT / "select-zone.jpg", keep_message=True)


def scene_history(e):
    bare(e)
    stroke(e, "D1", P(-8, 0), 8, hold=1200, steps=3)
    stroke(e, "D1", P(8, 4), 8, hold=1200, steps=3)
    stroke(e, "D2", P(0, -10), 6, hold=1000, steps=3)
    e.send("key D6")
    e.send("set Size|3")
    path(e, [P(-16, 12), P(16, 12)], steps=6)
    stroke(e, "D4", P(0, 0), 10, hold=1500, steps=3)
    e.send("key Escape")
    e.send("click History")
    e.send("click Back to here")
    camera(e, 0, 0, 200, 40, 55)
    e.shot(OUT / "history.jpg")


def scene_help(e):
    bare(e)
    e.send("key F3")
    camera(e, 0, 0, 200, 40, 55)
    e.shot(OUT / "help.jpg")


def scene_place_chooser(e):
    bare(e)
    e.send("key T")
    e.send("click + Add kinds")
    camera(e, 0, 0, 200, 40, 55)
    e.shot(OUT / "place-chooser.jpg")


def scene_blueprints(e):
    bare(e, unsaved=False)
    level(e, P(-12, -10), P(12, 10))
    e.send("key T")
    pieces(e)
    kinds(e, "wood_floor")
    e.send("click Grid")
    e.send("set Spacing|2")
    drag(e, P(-4, -3), P(4, 3))
    e.send("key Return")
    kinds(e, "woodwall")
    e.send("click Line")
    e.send("click Rectangle")
    e.send("set Layers|2")
    drag(e, P(-4, -3), P(4, 3))
    e.send("key Return")
    e.send("key B")
    drag(e, P(-7, -6), P(7, 6))
    e.send("choose Copy and paste")
    e.send("key C ctrl")
    e.send("click Save blueprint…")
    e.send("wait 500")
    e.send("type |Small hut")
    e.send("click OK")
    e.send("wait 500")
    e.send("click Blueprints…")
    camera(e, 0, 0, 200, 40, 55)
    e.shot(OUT / "blueprints.jpg", keep_message=True)


def scene_map(e):
    e.send(f"world {world()}")
    e.send(f"mapview {C[0] + 200} {C[1]} 1.6")
    e.send(f"mappick {ZONE[0]} {ZONE[1]}")
    e.shot(OUT / "map.jpg")


# ---- The start page and its settings (no world opened).

def scene_start(e):
    e.send("click My game")
    e.send("wait 3000")
    e.shot(OUT / "start-game.jpg")
    e.send("click A dedicated server")
    e.shot(OUT / "start-server.jpg")
    e.send("click A saved world")
    e.send("wait 1500")
    e.shot(OUT / "start-offline.jpg")
    e.send("click Settings")
    e.send("wait 800")
    e.shot(OUT / "settings.jpg")


SCENES = {name[len("scene_"):].replace("_", "-"): f for name, f in globals().items() if name.startswith("scene_")}


def main():
    global DIR
    DIR = sys.argv[1]
    names = sys.argv[2:] or list(SCENES)
    OUT.mkdir(parents=True, exist_ok=True)
    for name in names:
        started = time.time()
        # Each scene in a fresh editor: nothing carries over, nothing is saved.
        start = name == "start"
        with Editor(game_world="Docs" if start else None, worlds=(f"{DIR}/Docs", f"{DIR}/Fjordheim") if start else ()) as e:
            try:
                SCENES[name](e)
            except Exception as ex:
                print(f"{name}: FAILED {ex}")
                continue
        print(f"{name}: {time.time() - started:.0f} s")


if __name__ == "__main__":
    main()
