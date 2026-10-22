# Path

Draw a line, then flatten, ramp, raise, lower, smooth, dig, paint or roof over the ground along it: roads, ramps,
causeways, ditches, rivers and caves.

**Where:** tool rail, **EDIT** section (and **Cave** in **CREATE**) · **Key:** `P` · **Works in:** saved worlds, live

![The Path tool's five points across two mounds, Flatten to height chosen, before Apply](images/path.jpg)
*A line of five points drawn over two mounds, with **Flatten to height** chosen and its **Height** taken from the flat
ground with `Alt`+click. Nothing has changed yet: the band shows the path's width and soft edges until you press
**Apply (Enter)**; the circle at the last point is the pointer's.*

## Use it

1. In the tool rail, click **Path** (`P`).
2. Click points along the route, or hold and drag to draw it freely. Before you click, a circle shows the path's
   width; once there are points, the band shows its width and soft edges.
3. Optional: fine-tune the line. Drag a point to move it, drag the line between two points to add a point there,
   `Ctrl`+click a point to remove it, `Backspace` to remove the last one.
4. In the panel, choose an **Action** and set its settings.
5. Press **Apply (Enter)**. The change is one undo step, named after the action ("Path: Flatten to height").
6. Optional: choose another action and apply it to the same line (flatten, then pave), or move its points and apply
   again. The line stays until you press **Clear (Esc)**.

The panel shows how many points the line has and how long it is.

![The same line applied: a level road cut through both mounds, paved along its middle, the line still drawn](images/path-road.jpg)
*The line from the first picture, same camera: **Flatten to height** cut a level road through both mounds, then
**Paint paved** with **Width** 4 m and **Soft edge** 1 m paved its middle. The line stays for the next action.*

## Settings

| Setting | What it does |
|---|---|
| **Action** | What happens along the line (see [Actions](#actions)). |
| **Width** | Width of the path, 1 to 40 m (default 8 m). The action works fully across it. |
| **Soft edge** | How far the change fades out on each side, 0 to 15 m (default 4 m), so the path blends into the ground. At **0** the edge is sharp: a trench with straight, steep sides. |
| **Height** | **Flatten to height**: the height of the path (m). `Alt`+click the ground picks it. |
| **Amount** | **Raise by** / **Lower by**: how much (m, default 2). |
| **Start**, **End** | **Ramp**: the height at the first and the last point (m). See [Ramp](#ramp). |
| **Depth** | **River / canal**: how deep the bed is below sea level in its middle (m, default 2). **Cave**: see [Cave](#cave). |
| **Preset**, **Randomize**, **Headroom**, **Roof** | **Cave** only, see [Cave](#cave). |
| **Smooth curve through the points** | On (default): a smooth curve through your points. Off: straight lines between them. |
| **Natural look (ragged edges, bumps)** | A width that varies along the way (up to 45% either way), ragged edges and small bumps on the surface, so the path looks worn rather than tool-made. Its **Bumps** and **Bump size** show below it, shared with the [Naturalize](terrain.md#naturalize) brush. |

![Two trenches dug with Lower by 3 m side by side: left with steep walls, right with wide sloping banks](images/path-soft-edge.jpg)
*Two straight lines on flat ground, each **Lower by** 3 m with **Width** 6 m, seen along them. Left: **Soft edge** 0,
a trench with steep, straight walls. Right: **Soft edge** 8 m, the banks slope gently into the ground.*

## Actions

| Action | What it does |
|---|---|
| **Flatten to height** | Levels the path to **Height**: a road, a terrace along a slope, a causeway over a dip. |
| **Ramp (start → end)** | A slope that rises or falls evenly from **Start** to **End**. |
| **Raise by** | Lifts the path by **Amount**, keeping its ups and downs: a bank, a dyke. |
| **Lower by** | Digs the path down by **Amount**: a ditch, a sunken lane. |
| **Smooth** | Evens out the ground along the path. Apply it again for more. |
| **River / canal (water)** | Digs a bed below sea level, so it fills with water (see [River or canal](#river-or-canal)). |
| **Cave (dig and roof)** | Digs a trench and roofs it with boulders (see [Cave](#cave)). |
| **Paint dirt**, **Paint paved**, **Paint cultivated** | Paints the path, full colour across the width, blending out over the soft edge. |
| **Clear paint** | Paints plain biome ground along the path (see [Paint brushes](terrain.md#paint-brushes)). |

### Ramp

![A ramp seen from the side, rising evenly from the ground onto a raised pad, the Path panel with Start and End](images/path-ramp.jpg)
*A ramp of two points seen from the side, from the ground at its foot (**Start** 35.2 m) to the top of a pad
raised 8 m (**End** 43 m). It rises evenly the whole way.*

Until you type a height or pick one, **Start** and **End** follow the ground under the first and last points, so
moving the ends updates them. `Alt`+click the ground picks the start height, `Alt`+`Shift`+click the end height.
**Clear (Esc)** lets them follow the ground again. A winding ramp has the same slope all along.

### River or canal

![A river dug across the zone with River / canal, the water shown](images/path-river.jpg)
*A river dug across the zone with **River / canal (water)**: the sea fills it.*

Valheim has no flowing water: the water in rivers is the sea, at the same level (30 m) everywhere. So a river only
has water where its bed is below sea level. The action digs a U-shaped bed **Depth** below sea level in its middle,
rising to the waterline at the **Width**; the **Soft edge** makes the banks. It only digs: ground already lower stays
as it is, so a river can run into a lake or the sea.

Through high ground a river needs a deep cut, and the game's ±8 m limit stops it 8 m down (the status bar says so).
Bring the ground down near the sea first, follow low ground, or turn on [No limit](terrain.md#no-limit).

## Cave

![A cave mouth seen from its entrance slope, the boulder roof above and the Cave panel](images/path-cave.jpg)
*A cave dug through an 18 m mound with the **Cave** preset and No limit on, seen from above its entrance: the grassy
slope leads down under the boulder roof, with daylight at the far end. The status bar gives its length and boulders,
and no wall warning.*

**Cave** in the rail's **CREATE** section opens the Path tool with **Cave (dig and roof)** chosen. Draw the cave's
line and press **Apply (Enter)**: it digs a trench with a slope up to the ground at each end as an entrance, and roofs
it with the game's big boulders. The ground cannot overhang, so a cave is always rock over dug ground, like the
game's own.

| Setting | What it does |
|---|---|
| **Preset** | **Tunnel**, **Cave** (default) or **Cavern**: width, soft edge, depth and headroom to start from (below). |
| **Randomize** | New sizes within 15% of the preset's, and new boulders for the next Apply. |
| **Width**, **Soft edge** | The floor's width, and how far the walls slope out on each side (m). |
| **Depth** | How far below the ground the floor is in the middle, 2 to 20 m. Past 8 m needs [No limit](terrain.md#no-limit). |
| **Headroom** | The height inside, from the floor to the boulders' undersides, 2 to 15 m. |
| **Roof** | The boulders: **By biome**, **Forest boulders**, **Coast boulders**, **Heath boulders** or **Mountain rocks**. |

| Preset | Width | Soft edge | Depth | Headroom | What it is |
|---|---|---|---|---|---|
| **Tunnel** | 5 m | 1.5 m | 6 m | 3.5 m | A narrow passage, just high enough to walk through. |
| **Cave** | 8 m | 2.5 m | 7.5 m | 4.5 m | A cave to explore or hide a base in. |
| **Cavern** | 14 m | 3.5 m | 8 m | 5.5 m | A wide hall under the rocks. |

- **By biome** uses heath boulders in the Plains, mountain rocks in the Mountains and the Deep North, coast boulders
  in the Ocean, and forest boulders elsewhere. From above, the roof looks like a rocky outcrop.
- **Boulders go only where the floor is deeper than Headroom by 1.5 m**, so the entrances stay open. If the line is
  too short or **Depth** is too small, only the trench is dug and the status bar says why.
- **Trees, rocks, ore, bushes and pickables in the trench are taken away.** The status bar says how long the cave
  is, how many boulders roof it and how many trees and rocks went.
- **Walls may not reach the roof within ±8 m**; the status bar counts the points. Turn on No limit to close them.
- **A closed loop** makes a ring-shaped cave with one entrance where the ends meet.
- **Players can mine the boulders** with a pickaxe, so a cave is not a safe vault.
- The whole cave, trench, boulders and clearing, is one undo step.

## Shortcuts

| Key or mouse | What it does |
|---|---|
| `P` | Choose Path. |
| Click / hold and drag | Add points / draw the line freely (a point every 2 m). |
| Drag a point / drag the line | Move the point / add a point there and move it. |
| `Ctrl`+click a point | Remove it. |
| `Backspace` | Remove the last point. |
| `Alt`+click | Pick the height (Flatten to height) or the ramp's start. |
| `Alt`+`Shift`+click | Pick the ramp's end height. |
| `Enter` | Apply the action. |
| `Esc` | Clear the line; a second `Esc` leaves the tool. |
| `Ctrl+Z` | Undo the last Apply. |

## Tips and limits

- **The [Mask](masks.md) applies to every action**, and so does the ±8 m limit unless
  [No limit](terrain.md#no-limit) is on. Where the path reaches the limit, the points turn red and the status bar says
  so.
- **A sharp corner** needs two points close together, or **Smooth curve through the points** off: a smooth curve
  passes through every point you click.
- **Check a ramp's slope first** with [Measure](measure.md): measured from foot to top, **Slope** is what the ramp
  will give.
- **Clear (Esc) removes only the line.** What you applied stays; `Ctrl+Z` takes back one Apply at a time.
- **The path works only inside the open area**, and never moves its outermost line of points (see
  [The locked edge](concepts/areas-and-zones.md#the-locked-edge)).

## Related

- [Make a road or a ramp up a hill](guides/make-a-road.md)
- [Dig a river or a pond](guides/make-a-river.md)
- [Dig a cave](guides/dig-a-cave.md)
- [Ground limits](concepts/ground-limits.md)
- [Sculpt and paint](terrain.md), [Measure](measure.md)
- [How a cave is built](concepts/ground-limits.md#how-a-cave-is-built), [how a path's edge fades](concepts/ground-limits.md#how-a-paths-edge-fades)
