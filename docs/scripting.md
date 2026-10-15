# Script

![The Script tool: the Terraced hill example, run on a 3 × 3 area](images/script.jpg)

*The Terraced hill example run on a 3 × 3 area: the code in the panel, and under it the result line
saying what the script changed.*

Write a few lines of C# and the editor runs them on the open area: shape the ground, paint it, place,
move and remove objects. Anything the other tools do by hand, a script can do by rule and at once: a
mountain range across the area, a hundred boulders where the noise says so, a forest only on gentle
slopes, terraces, a canyon with a river, a flat paved square at exactly the average height... and
whatever you can think of.

You do not need to install anything: the editor compiles the script itself. If you have never
written C#, start from an [example](#the-examples) and change its numbers; the [recipes](#recipes)
below show the common patterns.

## How to use it

1. Open an area in the 3D editor. A script works on the **open area** (the zones you see). The
   Script tool is not offered in the Workshop.
2. Choose **Script** in the CREATE section of the tool rail.
3. Pick an example in the list (or a script you saved), or write your own in the code box.
4. Press **Run (Ctrl+Enter)**, or `Ctrl+Enter` in the code box. The box under the buttons shows
   "Compiling…", then "Running…", then what the script printed and what it changed, or what went
   wrong.
5. Look at the result. `Ctrl+Z` takes the whole run back in one step; run it again with other
   numbers until you like it.
6. **Save to world** keeps it, like any other change.

## The panel

| Control | What it does |
|---|---|
| **List** | The seven examples ("Example: …"), then your saved scripts, newest first. Picking one **replaces** the text in the code box (save your own script first). Picking an example empties the name box; picking a saved script puts its name there. |
| **Code box** | The script. `Tab` indents; `Ctrl+Enter` runs it. |
| **Run (Ctrl+Enter)** | Compiles and runs the script on the open area. Everything it changes is **one undo step**, named "Script: " and the list entry's name in the [History](editor-basics.md#history). |
| **Stop** | Stops a running script: nothing it did is kept. See [Stopping a script](#stopping-a-script). |
| Name box / **Save** | Saves the code under the name typed ("name to save it as") and selects it in the list. Characters a file name cannot hold become `_`. A saved script of the same name is replaced without asking. |
| **Output** | What the script printed (`Print`), then what it changed, or its mistakes or error with the line they are on. |

Saved scripts are `<name>.csx` files in the `scripts` folder of the editor's data folder
(`~/.local/share/ValheimWorldEditor/scripts` on Linux, `%LOCALAPPDATA%\ValheimWorldEditor\scripts`
on Windows). You can edit them with any text editor, or share them: a `.csx` file put there shows in
the list when the editor starts again, or after you save a script.

## The examples

Each example is a whole script, ready to run on any open area. Read them to see how the API is used.

| Example | What it does |
|---|---|
| **Mountain range across the area** | Four Lone peak mountains in a line from the south-west of the area to the north-east, each 35 to 60 m high and of its own shape. `Rnd.Seed = 7`, so it is the same range each run. Past the ±8 m: saving turns them into ground discs. |
| **Terraced hill** | A hill 30 m high and 60 m in radius in the middle of the area, cut into six flat terraces like rice fields, each rising to the next over the last 15% of its width. The trees, rocks and bushes it would bury are taken away first (buildings stay); the steep steps between terraces are painted dirt. |
| **Canyon with a river** | A winding canyon 18 m wide across the area, from west to east, dug down to 3 m below sea level so water fills its bottom. Its line winds with noise, and it fades out over the last 30 m at the area's west and east edges. |
| **Scatter boulders** | Big boulders (`rock4_forest`, `rock4_coast`, `rock4_heath`) every 6 m where the noise is high, more on slopes, each turned and sized at random. Prints how many. |
| **Ring of trees** | A ring of 36 beeches, 40 m from the middle of the area; the trees standing inside the ring are taken away. |
| **Flat paved base** | A 40 × 40 m square in the middle of the area, levelled to its own average height and paved, with a smooth 10 m slope back to the ground around it. Keeps within the game's ±8 m (`Ground.NoLimit = false`). Prints the height. |
| **Noise landscape** | Rolling land over the whole area: broad hills and valleys up to 25 m up or down, with smaller bumps on top, fading back to the ground as it was over the last 40 m at the area's edges. |

## How it works

- **Compiled in the editor.** Run compiles your code with the editor's own C# compiler (C# 12,
  against .NET 10), together with the editor's API (`Area`, `Ground`, `Objects`, `Noise`, `Rnd`,
  `Print`). Nothing else is needed on your computer.
- **On a snapshot.** When you press Run, the editor takes a copy of the open area: its ground, its
  paint, its biomes and its objects. The script runs in the background on that copy. It sees its own
  changes as it goes (raise a point, then `Ground.Height` gives the new height), but the area itself
  does not change while it runs.
- **All at once, at the end.** When the script has finished, everything it changed (ground, paint,
  objects placed, moved and taken away) goes into the area at once, as one undo step. If it has
  mistakes, fails, is stopped or runs for 2 minutes, nothing changes.
- **Hands off while it runs.** If the area changes while the script runs (a stroke, an undo, a save
  that reads the world again) or another area opens, the script's result is dropped: "The area
  changed while the script ran: nothing changed. Run it again." Its heights and objects were those
  of the area when it started: putting them in would undo your change or hit other objects.
- **A new start each run.** Each run is compiled and run anew: variables, `Rnd` and `Objects.All`
  start over. Only one script runs at a time.

## How a script sees the world

- **Plain C#.** A script is a list of statements run from top to bottom (C#'s top-level
  statements). It can declare variables, loops, functions and classes, and use .NET (`MathF`,
  `System.Linq`...). `using System`, `System.Linq` and `System.Collections.Generic` are already there.
- **World metres.** Every position is in metres in the world: **x** east, **z** north, the same
  numbers as the status bar's readout and the game's `pos` command. Heights are metres above the
  world's zero: the sea is at `Area.Water` (30 m).
- **One point per metre.** The ground has a point every metre. `Ground.Set(x, z, ...)` changes the
  point nearest to (x, z); `Ground.Height` reads between points smoothly, as the ground is drawn.
- **The open area only.** The script sees and changes the open area, not beyond it. The outer row
  of points stays as it is, so the area still joins its neighbours: changes there, or outside the
  area, are ignored without an error. Open a bigger area (the top bar's size list, up to 17 × 17
  zones) for bigger works.
- **Past the ±8 m.** Scripts can move the ground as far as they like (`Ground.NoLimit` is on), like
  the [No limit](terrain.md#no-limit) switch and the [Mountain](terrain.md#mountain) tool: up to
  400 m above or below the generated ground. Saving turns that ground into invisible ground discs
  that every player's game counts as generated ground, console players too. Set
  `Ground.NoLimit = false;` to keep within the game's ±8 m of the original ground, as the hoe and
  pickaxe do: points past it stop there, and the result line says how many.
- **No Mask.** The [Mask](masks.md) limits the brushes, not scripts. Test what you need in the
  script itself (`Ground.Biome`, the height, the slope).

## What a script can use

### Area

The open area.

| | |
|---|---|
| `Area.MinX`, `Area.MinZ`, `Area.MaxX`, `Area.MaxZ` | Its corners (world metres). A 3 × 3 area is 192 m across. |
| `Area.CenterX`, `Area.CenterZ` | Its middle. |
| `Area.Points()` | Every ground point the script can change (the outer row left out), as `(x, z)`, row by row from the south: `foreach (var (x, z) in Area.Points()) { ... }`. `Area.Points(step: 4)` takes every 4th metre, for scattering things. |
| `Area.Inside(x, z)` | Whether a point is inside the area (not on its edge). |
| `Area.Water` | Sea level (30 m). |
| `Area.WorldSeed`, `Area.World` | The world's seed (a number) and its name. |

### Ground

| | |
|---|---|
| `Ground.Height(x, z)` | The ground's height there now (with the script's changes so far). |
| `Ground.Original(x, z)` | The ground before any edit: as the world generated it (with the ground discs and lifts already saved). The difference with `Height` is what has been dug or built up. |
| `Ground.Set(x, z, height)` | Puts the ground point there at that height. |
| `Ground.Raise(x, z, metres)`, `Ground.Lower(x, z, metres)` | Moves it up or down by that much. |
| `Ground.Shape(x, z, radius, (dx, dz) => metres)` | Adds the metres your function gives to every point within the radius of (x, z). `dx` and `dz` are the metres from the middle: `MathF.Sqrt(dx * dx + dz * dz)` is the distance. A negative value digs. |
| `Ground.Mountain(x, z, "Lone peak", height:, radius:, rough:, turn:, seed:)` | A mountain of the [Mountain](terrain.md#mountain) tool, its middle at (x, z): `"Lone peak"`, `"Ridge"`, `"Mountain range"`, `"Mesa"`, `"Volcano"` or `"Rolling hills"`. `height` and `radius` in metres, `rough` from 0 to 1 (natural detail), `turn` in degrees (the direction of a ridge or range). Leave a value out for a random one in the preset's range; the same `seed` gives the same mountain. It reaches about 1.15 × its radius from the middle. The trees, rocks, ore, bushes and pickables it buries by more than 2 m are taken away, as with the tool; unlike the tool, it does not grow the biome's trees on it afterwards. |
| `Ground.Paint(x, z, "dirt")` | Paints the point: `"dirt"`, `"cultivated"`, `"paved"` or `"clear"` (back to the biome's own ground). A last number from 0 to 1 paints only partly (`0.5f`), blending with the paint there. |
| `Ground.Biome(x, z)` | The biome there: `"Meadows"`, `"BlackForest"`, `"Swamp"`, `"Mountain"`, `"Plains"`, `"Ocean"`, `"Mistlands"`, `"AshLands"` or `"DeepNorth"` (`"None"` outside the area). |
| `Ground.NoLimit` | On (`true`) by default: changes go past the game's ±8 m. `false`: they stop at it. What counts is its value when the script ends. |

### Objects

What stands in the area, as it was when the script started. Each object (`Obj`) has its `Prefab`
name, its position `X`, `Y`, `Z`, its turn `Yaw` and tilts `Pitch` and `Roll` (degrees), its size
`Scale` (1: the game's own), its `Kind` and whether it is a `Building` (a piece a player built).
`Objects.All` does not change while the script runs: objects you place are not in it, and those you
take away are still listed.

| | |
|---|---|
| `Objects.All` | Every object of the area. |
| `Objects.OfKind("Trees")` | Those of a kind (capitals do not matter): `Buildings`, `Ruins`, `Furniture`, `Trees`, `Rocks`, `Ore`, `Bushes`, `Pickables`, `Remains`, `Animals` (tamed), `Creatures` (wild, traders, birds), `SpawnPoints`, `Nests`, `Runestones`, `Dungeons`, `Effects`, `Other`. These are the kinds of the [View panel](editor-basics.md#view-panel). |
| `Objects.Near(x, z, radius)` | Those within the radius of a point (measured flat: heights do not count). |
| `Objects.Remove(o)` | Takes it away. |
| `Objects.Place("Beech1", x, z, yaw:, scale:, y:, pitch:, roll:, snap:)` | A new object of the game, by its prefab name (`Beech1`, `Pinetree_01`, `rock4_forest`, `piece_chest_wood`...: the names the [Place](place.md) tool lists). Without `y` it stands on the ground **as the script leaves it** (dig first, then place: it sits in the hole). `yaw` turns it (degrees; 0: its front faces north, 90: east), `pitch` and `roll` tilt it, `scale` sizes it (`0.5f` half, `2` double; leave it out for the game's own size). With `snap: true`, a building piece moves to meet the snap points of the pieces there (and of those the script placed before it) within 0.5 m, as the hammer does. Building pieces are player built, by the builder chosen in View → Building → **Built by** (see [Player-built pieces](editor-basics.md#player-built-pieces)). |
| `Objects.Move(o, x:, z:, y:, yaw:, pitch:, roll:, scale:)` | Moves, turns or sizes an object that is there; what you leave out stays as it is (or as an earlier `Move` of the script left it). It keeps what it holds (chest contents, builder...). The size must be above 0. |
| `Objects.CanPlace("Beech1")` | Whether the editor knows how to make that object. Use it before `Place` when a name may be wrong: `Place` with a name the editor cannot make stops the script. |

### Noise and random numbers

| | |
|---|---|
| `Noise.At(x, z, scale, seed)` | Smooth natural noise, from about -1 to 1: the same kind of layered noise Valheim's terrain and the editor's Mountain tool are made of. `scale` is the size of its bumps in metres (default 32); another `seed` (default 0) gives another pattern. Use it for anything that should vary naturally: heights, where trees grow, how wide a path is. The pattern repeats every 256 × `scale` metres. |
| `Rnd.Seed = 42;` | Makes the random numbers below the same on every run (without it, each run differs). |
| `Rnd.Next()` | A random number from 0 to 1. |
| `Rnd.Range(min, max)` | A random number between `min` and `max`. |
| `Rnd.Int(min, max)` | A random whole number from `min` up to, but not including, `max`. |
| `Rnd.Chance(0.3f)` | `true` 30% of the time. |
| `Rnd.Pick("Beech1", "Oak1", "Birch1")` | One of the values, at random. |

### Printing

`Print(anything)` writes a line in the output box under the script: values, counts, what it is
doing (2,000 lines at most). Handy to check numbers before changing anything: comment out the
changing lines (`//`) and print instead.

## Recipes

Each recipe is a whole script: paste it into the code box and run it on any open area.

**A round hill in the middle of the area**, 25 m high, 40 m across:

```csharp
Ground.Shape(Area.CenterX, Area.CenterZ, 40, (dx, dz) =>
{
    float d = MathF.Sqrt(dx * dx + dz * dz) / 40;   // 0 in the middle, 1 at the edge
    return 25 * (1 - d * d) * (1 - d * d);           // a smooth bell
});
```

**A lake**: dig a round hollow below sea level; the sea fills it (in Valheim, ground below 30 m is
under water):

```csharp
float cx = Area.CenterX, cz = Area.CenterZ, radius = 35;
Ground.Shape(cx, cz, radius, (dx, dz) =>
{
    float d = MathF.Sqrt(dx * dx + dz * dz) / radius;
    float bed = Area.Water - 4 * (1 - d * d);         // 4 m deep in the middle
    float now = Ground.Height(cx + dx, cz + dz);
    return d < 0.8f ? bed - now : (bed - now) * (1 - d) / 0.2f;   // a soft shore
});
```

**Pine forest only on gentle slopes**, denser where the noise is high, none in water:

```csharp
Rnd.Seed = 1;
int planted = 0;
foreach (var (x, z) in Area.Points(step: 5))
{
    float slope = MathF.Abs(Ground.Height(x + 1, z) - Ground.Height(x - 1, z)) / 2;   // rise per metre
    if (slope > 0.45f || Ground.Height(x, z) < Area.Water + 1) continue;             // about 25°, and dry land
    if (Noise.At(x, z, 50, seed: 2) < 0 || !Rnd.Chance(0.6f)) continue;
    Objects.Place(Rnd.Pick("Pinetree_01", "FirTree"), x + Rnd.Range(-2, 2), z + Rnd.Range(-2, 2), yaw: Rnd.Range(0, 360));
    planted++;
}
Print($"{planted} trees planted.");
```

**Clear the undergrowth**: take away every bush and pickable of the area (trees and buildings stay):

```csharp
int n = 0;
foreach (var o in Objects.OfKind("Bushes").Concat(Objects.OfKind("Pickables")))
{
    Objects.Remove(o);
    n++;
}
Print($"{n} removed.");
```

**A paved road** between two points, 6 m wide, levelled from one end's height to the other's:

```csharp
float x1 = Area.MinX + 20, z1 = Area.CenterZ, x2 = Area.MaxX - 20, z2 = Area.CenterZ + 30;
float h1 = Ground.Height(x1, z1), h2 = Ground.Height(x2, z2);
float length = MathF.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));
for (float s = 0; s <= length; s += 0.5f)
{
    float t = s / length, x = x1 + (x2 - x1) * t, z = z1 + (z2 - z1) * t;
    // Across the road: 3 m each side of the line.
    float ax = -(z2 - z1) / length, az = (x2 - x1) / length;
    for (float w = -3; w <= 3; w += 0.5f)
    {
        Ground.Set(x + ax * w, z + az * w, h1 + (h2 - h1) * t);
        Ground.Paint(x + ax * w, z + az * w, "paved");
    }
}
```

**Mountains by biome**: a mesa in the Plains, a lone peak anywhere else, in the middle of the area:

```csharp
float cx = Area.CenterX, cz = Area.CenterZ;
string preset = Ground.Biome(cx, cz) == "Plains" ? "Mesa" : "Lone peak";
Ground.Mountain(cx, cz, preset, height: 40, radius: 70, seed: 123);
Print($"{preset} in the {Ground.Biome(cx, cz)}.");
```

**Your own function**: functions make bigger scripts readable. Here, a ring of standing stones:

```csharp
void Stone(float x, float z) =>
    Objects.Place("highstone", x, z, yaw: Rnd.Range(0, 360), scale: Rnd.Range(0.7f, 1.1f));

Rnd.Seed = 5;
for (int i = 0; i < 12; i++)
{
    float a = i * MathF.Tau / 12;
    Stone(Area.CenterX + MathF.Cos(a) * 18, Area.CenterZ + MathF.Sin(a) * 18);
}
```

**Stand the rocks up**: every tilted rock of the area set upright, keeping its turn:

```csharp
int n = 0;
foreach (var o in Objects.OfKind("Rocks"))
{
    if (o.Pitch == 0 && o.Roll == 0) continue;
    Objects.Move(o, pitch: 0, roll: 0);
    n++;
}
Print($"{n} rocks stood up.");
```

**A wooden floor** of 2 m floor pieces, snapped together as the hammer would, on a pad levelled
under it:

```csharp
float cx = Area.CenterX, cz = Area.CenterZ, level = Ground.Height(cx, cz);
for (float z = cz - 6; z <= cz + 6; z++)
    for (float x = cx - 6; x <= cx + 6; x++)
        Ground.Set(x, z, level);
for (int i = -2; i <= 2; i++)
    for (int j = -2; j <= 2; j++)
        Objects.Place("wood_floor", cx + i * 2, cz + j * 2, y: level, snap: true);
```

## When something goes wrong

The output box says what happened; the status bar says it in short.

- **Mistakes** (the script cannot be compiled) are listed under it with their line, 20 at most:
  `line 4: The name 'Groud' does not exist in the current context` means a typing mistake on line 4.
  Nothing has changed.
- **Errors** while it runs stop it, with the line they happened on: an object name the editor
  cannot make (`the editor cannot make a “Beech” (a prefab name, like Beech1 or rock4_forest)`), a
  paint or mountain preset it does not know (the message lists the right ones), a size of 0 or less
  in `Move`, a division by zero... Nothing has changed.
- **Nothing changed**: the output says "The script ran and changed nothing." Check with `Print`
  that your loop finds what you expect (a kind name spelled differently, points outside the area or
  on its edge...).
- **The area changed while the script ran**: you edited, undid or saved while it ran. Run it again
  and wait for it.

### Stopping a script

A loop over a whole 17 × 17 area is about 1.2 million points: a few seconds of work. Use
`Area.Points(step: 4)` when every point is not needed.

**Stop**, and the 2-minute limit, end a script the next time it starts a new row of
`Area.Points()`, `Ground.Shape` or `Ground.Mountain`. Nothing it did is kept. A loop of your own
that uses none of these (a `while` or `for` loop with only `Ground.Set` and `Ground.Height` in it)
cannot be stopped there: the editor waits for it to finish. Avoid endless loops: a loop over
`Area.Points()` is stopped at its next row, so only once the row it is on has finished.

## Good to know

- `Rnd.Seed` makes a run repeatable: the same script and seed give the same result on the same
  area. Change the seed for another variation, as the Mountain tool's Randomize does.
- A script's changes are ordinary edits: **Discard** drops them, the History panel lists the run
  as one step, and live (on a running game or server) **Apply live** sends them like any other (see
  [Live mode](live-mode.md)).
- Objects you place are new objects; ones you remove are gone when you save. Removing a runestone
  removes its location for good, as in the [Select](select.md) tool.
- A moved object is made again at its new place with all its data, as the Select tool's moves are;
  in live mode it moves in the game under its own id when it can (see
  [Moving things](live-mode.md#moving-things)).
- A script handles each object on its own: it does not take a
  [linked group](select.md#linked-objects-ships-and-other-mods-groups) (a mod's ship and its pieces)
  whole, as the Select tool does. Move or remove such groups with the Select tool.
- [Claude](claude.md) runs scripts through the same engine (its `run_script` tool), and can do a
  dry run that only says what a script would change.
- **Scripts are programs.** Like any program you run, a script can read and write files and do
  anything else your account can on this computer. Run only scripts you wrote or trust, the same as
  you would with a mod.
