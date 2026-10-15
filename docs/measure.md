# Measure and overlays (`M`)

![A measure across a mound with slope colours on, and the Measure panel's readouts](images/measure.jpg)
*A measure from the foot of a mound over its top, with slope colours on. The line follows the ground; the panel
shows the distance, the heights and the slope.*

The **Measure** tool is a measuring tape: click two points and read the distance, the height difference and the
slope between them. Two ground overlays in the View panel, **Slope colours** and **Height lines**, show steepness and
height everywhere at once while you use any tool. Together they help you plan building sites, ramps, roads and
rivers.

## How to use it

1. Choose **Measure** (`M`) in the rail's **TOOLS** section.
2. Click a first point on the ground. The line then follows the cursor.
3. Click a second point to fix the measure.
4. Click again to start a new measure. **Clear (Esc)** removes it; a second `Esc` leaves the tool.

The line is drawn along the ground, so it shows the profile between the two points. The hint at the top of the
panel says what the next click does.

## The readouts

| Value | Meaning |
|---|---|
| **Distance** | The straight distance seen from above, as on a map (m). |
| **Along the slope** | The distance including the height difference: the length of a straight line from A to B (m). |
| **Height A → B** | The ground height at both points and the difference, for example `35.0 → 42.5 m (+7.5)`. |
| **Slope** | The average slope from A to B: the height difference divided by the distance, in % and in degrees. A 100% slope is 45°. |
| **Lowest / highest** | The lowest and highest ground along the line, read every metre: a bump or dip between the points shows here even when A and B are level. |
| **Water depth** | How deep the lower of the two points is under the sea (the sea is at 30 m), or "above sea". |

Heights are the game's (the same as in the status bar). You can select the values in the panel and copy them.

To plan a ramp, measure from its foot to its top: **Slope** is the slope the [Path](path.md) tool's **Ramp** will
give along a straight line between the same points.

## Ground overlays

In the View panel, under **GROUND (game look)**:

| Control | What it does |
|---|---|
| **Slope colours** | Colours the ground by its steepness: green under 10°, yellow-green 10–20°, yellow 20–30°, orange 30–45°, red above 45°. The colour is mixed with the ground's own, and lit by the sun, so hills keep their shape. |
| **Height lines every** *N* **m** | Draws height lines (contours) at the chosen step: **1**, **2** (default), **5** or **10** m. Every fifth line is darker and thicker. |

Both need **Game look** (the View panel's ground look), and both stay on while you use other tools, which helps when
you flatten a building site or plan a ramp. Both are remembered.

**How the lines are placed.** Height lines are drawn at whole multiples of the step, counted from the game's zero
height. The sea level (30 m) is a multiple of every step, so it always has a line, and the coast line is easy to see.
With a 2 m step, a darker line is every 10 m. Lines close together mean steep ground; far apart, gentle ground.

![Height lines every 2 m over two mounds and a hollow](images/height-lines.jpg)
*Height lines every 2 m (the default step) over two mounds and a hollow, with the View panel open. Every fifth line is
darker.*

## Good to know

- The measure is not saved and changes nothing in the world.
- The heights of A and B are read when you click. After you change the ground, measure again.
- **Limit marks** in the same View panel tints red the ground that reached the game's ±8 m limit (see [the ±8 m
  limit](terrain.md#how-the-ground-works)).
