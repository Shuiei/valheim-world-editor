# Path (`P`)

![The Path tool's points across two mounds, before Apply](images/path.jpg)
*A line of points drawn across two mounds, with **Flatten to height** chosen. Nothing has changed yet: the band
shows the path's width and soft edges until you press Enter.*

Draw a line, then change the ground along it: a level road, a ramp up a hill, a raised causeway, a ditch, a river
with water in it, a cave under rock, or a painted track. The line stays after you apply, so you can apply several
actions to the same line (flatten, then pave).

## How to use it

1. Choose **Path** (`P`) in the rail's **EDIT** section.
2. **Click points** along the route, or **hold and drag** to draw it freely (a point every 2 m). Before you click,
   a circle under the cursor shows the path's width (and a thinner one its soft edge). Once there are points, it
   also shows the stretch the next click adds. The middle line is the centre; the band and its edges show the full
   width, and the thin outer lines the soft edges.
3. **Fine-tune it**: drag a point (the dots on the line) to move it, drag the line between two points to add a
   point there and move it, **Ctrl + click** a point to remove it. `Backspace` removes the last point.
4. Pick an **Action** and its settings.
5. Press **Apply (Enter)**. The line stays, so you can apply a second action to it, or move its points and apply
   again.
6. **Clear (Esc)** removes the line. A second `Esc` leaves the tool.

The panel shows how many points the line has and how long it is. Each Apply is one undo step, named after the
action ("Path: Flatten to height").

![The same line applied: a level road through the mounds, paved along its middle](images/path-road.jpg)
*The line from the first picture, applied: a level road cut through the mounds with a natural look, then paved
along its middle.*

## Controls

| Control | What it does |
|---|---|
| **Action** | What happens along the line (see [Actions](#actions)). |
| **Width** | Width of the path, 1 to 40 m (default 8 m). The action works fully across it. |
| **Soft edge** | Width of the blend on each side, 0 to 15 m (default 4 m), so the path joins the ground around it smoothly. At **0** the edge is as sharp as the ground allows (see below). |
| **Height** | Flatten to height: the height of the path (m). **Alt + click** the ground picks it. |
| **Amount** | Raise by / Lower by: how much (m, default 2). |
| **Start**, **End** | Ramp: the height at the first and at the last point (m). |
| **Depth** | River / canal: how deep the bed is below sea level in its middle (m, default 2). Cave: how deep the floor is (2 to 20 m). |
| **Preset**, **Randomize**, **Headroom**, **Roof** | Cave only, see [Cave](#cave). |
| **Smooth curve through the points** | On (default): a smooth curve through your points. Off: straight lines between them. |
| **Natural look (ragged edges, bumps)** | Ragged edges, a width that varies along the way, and small bumps, so the path looks worn rather than tool-made. Its **Bumps** and **Bump size** appear below it when it is on. |
| **Apply (Enter)** | Does the action along the line. One undo step. |
| **Clear (Esc)** | Removes the line. |

The [Mask](masks.md) applies to every action, and so does the ±8 m limit unless [No limit](terrain.md#no-limit) is
on. When part of the path reaches the limit, the status bar says so and the points turn red.

## Actions

| Action | What it does |
|---|---|
| **Flatten to height** | Levels the path to **Height**: a road, a terrace along a slope, a causeway over a dip. |
| **Ramp (start → end)** | A slope that rises (or falls) evenly along the length of the line, from **Start** to **End**. |
| **Raise by** | Lifts the ground along the path by **Amount**, keeping its ups and downs: a bank, a dyke. |
| **Lower by** | Digs the ground along the path down by **Amount**: a ditch, a sunken lane. |
| **Smooth** | Evens out the ground along the path: each point moves towards the average of the 5 × 5 m around it. Apply it again for more. |
| **River / canal (water)** | Digs a bed below sea level, so it fills with water (see [River / canal](#river--canal)). |
| **Cave (dig and roof)** | Digs a trench and roofs it with boulders (see [Cave](#cave)). |
| **Paint dirt**, **Paint paved**, **Paint cultivated** | Paints the ground along the path, full colour across the width, blending out over the soft edge. |
| **Clear paint** | Paints plain biome ground along the path (see [paint](terrain.md#paint-tools)). |

### How the edge works

Every ground point near the line is measured from the nearest point of the line. Inside half the **Width** the
action works fully. Across the **Soft edge** it fades out along an S-curve, so a flattened road blends into the
slope above and below it. With **Soft edge** at 0, the points the edge line crosses get a share of the change
depending on how far the line passes from them, so the edge follows the line straight instead of stepping from
point to point (a trench with steep, straight sides). The ground still has one height per metre, so a wall always
spans about a metre.

### Natural look

With **Natural look** on, the path uses the Naturalize brush's pattern (**New pattern** there changes it):

- its width varies along the way, by up to 45% either way;
- its edges are ragged, moved in and out by noise fixed to the world;
- **Bumps** high, **Bump size** wide bumps are added to its surface (a third as much on river and cave beds).

### Ramp

![A ramp from the ground up onto a raised flat pad](images/path-ramp.jpg)
*A ramp drawn with two points, from the ground at its foot to the middle of a raised, flat pad, seen from the
side: its ends follow the ground there (35 m up to 43 m), so it rises evenly onto the pad.*

Until you type a height or pick one, **Start** and **End** follow the ground under the line's first and last points,
so moving the ends updates them. **Alt + click** the ground picks the start height, **Alt + Shift + click** the end
height. **Clear** lets them follow the ground again. The height rises evenly with the distance along the line,
curves included, so a winding ramp has the same slope all along.

### River / canal

![A river dug across the zone with River / canal, the water shown](images/path-river.jpg)
*A river dug across the zone with **River / canal (water)**: the sea fills it.*

The bed has a U shape: **Depth** below sea level (30 m, or the world's own) in the middle, rising to the waterline at the path's
**Width**. The **Soft edge** then makes the banks up to the ground. It only digs: ground already lower than the bed
stays as it is, so a river can run into a lake or the sea.

Valheim has no flowing water: the water in rivers is the sea, at the same level everywhere. So a river or canal has
water only where its bed is below sea level. A river through high ground needs a deep cut. With the ±8 m limit it
stops 8 m down (the status bar says so). Bring the ground down near the sea first, follow low ground, or turn on
[No limit](terrain.md#no-limit).

## Cave

![A cave dug through a big mound and roofed with the game's boulders, seen at its mouth](images/path-cave.jpg)
*A cave dug through a big mound with **Cave (dig and roof)**, roofed with the game's boulders, seen at its mouth.*

The **Cave** button, in the rail's **CREATE** section, opens the Path tool with its **Cave (dig and roof)** action
(you can also choose that action in the list). Draw the cave's line and press **Apply**: it digs a trench along the
line, with a slope up to the ground at each end as an entrance, and roofs it over with the game's big boulders.
Valheim's ground cannot overhang (it has one height per point), so a cave is always rock over dug ground, like the
game's own.

| Control | What it does |
|---|---|
| **Preset** | **Tunnel**, **Cave** (default) or **Cavern**: width, soft edge, depth and headroom to start from (table below). |
| **Randomize** | New sizes within 15% of the preset's, and new boulders for the next Apply. |
| **Width**, **Soft edge** | The floor's width, and how far the walls slope out on each side (m). |
| **Depth** | How far below the ground the floor is in the middle (2 to 20 m). Past 8 m needs [No limit](terrain.md#no-limit). |
| **Headroom** | The height inside, from the floor to the boulders' undersides (2 to 15 m). |
| **Roof** | The boulders: **By biome**, **Forest boulders**, **Coast boulders**, **Heath boulders** or **Mountain rocks**. |

| Preset | Width | Soft edge | Depth | Headroom | What it is |
|---|---|---|---|---|---|
| **Tunnel** | 5 m | 1.5 m | 6 m | 3.5 m | A narrow passage, just high enough to walk through. |
| **Cave** | 8 m | 2.5 m | 7.5 m | 4.5 m | A cave to explore or hide a base in. |
| **Cavern** | 14 m | 3.5 m | 8 m | 5.5 m | A wide hall under the rocks. |

**How it is built.**

1. **The floor.** The floor follows the lie of the land: at each point of the line it is the ground's average height
   over 6 m either way along the line, minus **Depth**. Near each end the depth eases to nothing over 2.5 times the
   depth (at least 8 m), which makes the entrances. Without No limit the floor stays within the game's 8 m.
2. **The roof.** Boulders go only where the cave is deeper than **Headroom** by 1.5 m, so the entrances stay open.
   One boulder is placed every 0.9 × **Width** metres along that stretch, then more wherever the floor is still open
   to the sky. Each one is turned upside down, so its flat side makes an even ceiling, then turned, tilted a little
   and sized at random. It is set by its real shape, just high enough that its underside clears the headroom over
   the whole floor.
3. **The walls.** Wherever a boulder's underside is above the cave's walls, the walls are raised half a metre into
   the rock, so no daylight comes in at the sides. Within the ±8 m, some points may not reach; the message says so.
4. **Clearing.** Trees, rocks, ore, bushes and pickables in the trench (where it is more than half a metre deep) are
   taken away.

**By biome** uses heath boulders in the Plains, mountain rocks in the Mountains and the Deep North, coast boulders
in the Ocean biome, and forest boulders elsewhere. On top, the roof shows as a rocky outcrop. Players can mine the
boulders with a pickaxe like any boulder, so a cave is not a safe vault.

The status bar says how long the cave is, how many boulders roof it and how many trees and rocks were taken away.
If the line is too short or **Depth** is not larger than **Headroom** by 1.5 m, only the trench is dug and the
message says why. A line drawn as a closed loop makes a ring-shaped cave with one entrance where the ends meet. The
whole cave (trench, boulders and clearing) is one undo step.

## Good to know

- The path works only inside the open area, and never moves the area's outermost line of points (see [the locked
  edge](terrain.md#how-the-ground-works)).
- Smooth curves pass through every point you click, so a sharp corner needs two points close together, or **Smooth
  curve through the points** off.
- For a road on a slope, use **Ramp** for the slope and **Flatten to height** for the level parts, then **Paint
  paved** with a smaller **Width** and **Soft edge**.
- Use the [Measure](measure.md) tool to check a ramp's slope before you apply it.
