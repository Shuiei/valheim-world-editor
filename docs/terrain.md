# Ground tools

![A raised mound and the Flatten brush](images/sculpt.jpg)

The **Sculpt** and **Paint** tools work like the game's hoe and pickaxe, but with a brush you
hold and drag. The yellow ring on the ground is the brush. All of them work with the
[Mask](masks.md), and every stroke is one step in undo and History.

## Sculpt tools

| Tool | Key | What it does |
|---|---|---|
| **Raise** | `1` | Lifts the ground under the brush while you hold the button. |
| **Lower** | `2` | Digs the ground down. |
| **Flatten** | `3` | Levels the ground to one height: the height where the stroke starts, or a fixed height (below). |
| **Smooth** | `4` | Evens out bumps and sharp edges. |
| **Naturalize** | `0` | Turns flat, tool-made ground into natural-looking bumps (settings below). |
| **Restore** | `5` | Brings the ground back to how the world generated it, and removes paint. |
| **Erode** | `O` | Weathers the ground like rain and time do (settings below). |

### Brush settings (all brushes)

| Control | What it does |
|---|---|
| **Size** | Brush radius in metres (`[` and `]` change it). The effect fades out smoothly towards the edge of the ring. |
| **Strength** | How fast the brush works while you hold the button. |
| **Shape** | **Circle**; **Square**; **Ring** (only the band between 40% of the size and the edge, shown by a second, fainter outline inside: crater rims, moats, walls of earth); **Ragged (noise)** (a noisy, natural-looking edge); or a picture (**Stamp: …**), see [Stamps](#stamps). The ring on the ground shows the shape. Remembered. |
| **Falloff** | How the effect fades from the middle to the edge: **Smooth** (the default), **Linear**, **Dome** (round top, steep sides), **Flat top** (full strength almost to the edge: pads and terraces), **Peak** (strong in the middle only: spires and pits), **Sharp edge (pickaxe)** (full strength right to the edge and nothing beyond: steep walls, like the game's pickaxe). Remembered. |
| **Turn** | Turns a square brush or a picture (degrees); `,` and `.` change it by 1° (Shift: 15°). |
| **Mask** | Limits the brush to some ground, see [Mask](masks.md). |

### Flatten settings

| Control | What it does |
|---|---|
| **Level to the height where the stroke starts** | On (default): the ground under your first click sets the height; lower ground around it is raised and higher ground is cut, so you get a flat pad at that height. The Height box shows the height in use. |
| **Height (m)** | Used when the option above is off: everything is levelled to this height. **Alt + click** the ground picks its height and switches to this fixed mode, handy to make several pads match. |

### Naturalize settings

| Control | What it does |
|---|---|
| **Bumps** | How tall the natural bumps are (m). |
| **Bump size** | How wide they are (m): small values give rough ground, large values gentle swells. |
| **New pattern** | A new random bump pattern for the next stroke (and for natural paths). |

The pattern is continuous across the world, so neighbouring strokes and areas match.

### Erode settings

| Control | What it does |
|---|---|
| **Thermal** | Where the ground is steeper than the **Rest angle**, material slides down to its lower neighbours until it rests: screes below cliffs, softened tool-made walls. No ground is lost, it only moves. |
| **Water** | Drops of rain run downhill from random points under the brush, dig where they speed up and leave what they carry where they slow down: gullies down slopes, fans at their feet, smoother valleys. |
| **Rest angle** | Thermal: the steepest slope that stays put (degrees). |

Hold and drag like any brush; **Strength** sets how fast it works, and the shape, falloff and Mask
apply. The Area tool's **Erode** action does it over a whole selection.

## Stamps

A stamp is a picture used as the brush shape, like WorldPainter's custom brushes: white parts work
fully, grey parts partly, black parts not at all. Choose one in **Shape** (they are listed as **Stamp: …**):

| Stamp | Shape |
|---|---|
| **Mountain** | A rough peak. |
| **Mesa (flat top)** | A flat top with steep, slightly ragged sides. |
| **Crater rim** | A ring wall, for craters (Lower inside it afterwards) and old earthworks. |
| **Dunes** | Parallel ridges inside a round patch. |
| **Rocky ground** | Lumpy, broken ground. |

| Control | What it does |
|---|---|
| **Load stamp…** | Uses any picture (PNG, JPEG...) as a stamp: a heightmap from another tool, a logo, a hand-drawn shape. It is shrunk to 128 × 128 points and kept in the editor's data folder, under its file name. |
| **Forget stamp** | Removes the loaded picture chosen as Shape (the built-in stamps stay). |
| **Stamp once** | With Raise or Lower: one click puts the whole stamp into the ground at once, the white parts **Height** metres up (or down), instead of painting while the button is held. One undo step; the ±8 m limit and the Mask apply. |

Stamps scale with **Size** and turn with **Turn** (`,` `.`). Without Stamp once they work like any
brush shape, with every sculpt and paint tool.

## Paint tools

![Paved, dirt and cultivated paint](images/paint.jpg)

| Tool | Key | What it does |
|---|---|---|
| **Dirt** | `6` | Bare dirt, like a trodden path. |
| **Cultivate** | `7` | Cultivated soil, as made by the cultivator. Crops only grow on it. |
| **Paved** | `8` | Paved stone ground. |
| **Clear** | `9` | Removes paint: back to the biome's own ground. |

Paint uses the same Size, Strength and Mask settings. Paint changes only how the ground looks and
what grows there; it does not change its height.

## Things to know

- **Steep walls.** The ground has one height point per metre, so a wall always spans about a metre:
  it cannot be more vertical than that, in the editor or in game. Each square metre is drawn as two
  triangles split along the same diagonal as in game (south-east to north-west corner), so an edge
  running north-west to south-east comes out clean, and one running north-east to south-west shows
  a small sawtooth, in game too.

- **The 8 m limit.** The game stores ground changes as an offset from the generated ground and
  allows at most ±8 m. Points that reach it turn red and the status bar says so.
- **Locked edge.** The outer line of points of the loaded area cannot be changed, so it always
  joins the next area seamlessly. Move the area with the top-bar arrows to edit there.
- **Locations.** Villages, the trader, dungeon entrances and similar places flatten the ground
  around them while the game runs. A note at the bottom left of the editor counts them; the editor
  already shows the ground with that flattening.
- **Status bar.** It shows the ground height under the cursor, its original height, and the change
  (and says when the point is at the ±8 m limit).

## Shape (`G`)

Like WorldEdit's `//generate`: a click puts a whole shape into the ground, centred where you click.
The yellow circle shows its radius.

| Control | What it does |
|---|---|
| **Shape** | **Mound**, **Cone**, **Mesa** (flat top), **Crater** (a hollow with a rim), **Moat** (a ring ditch), **Bowl**, **Ridged hill**, or **Formula…** for your own. Each preset is a formula, shown below, that you can change. |
| **Radius** | Size of the shape (m). Only the ground within it changes. |
| **Height** | How high it rises, or how deep craters, moats and bowls dig (m). It is `h` in the formula. |
| **Formula** | How many metres to add to the ground at each point; negative digs. |

In the formula:

| Name | Meaning |
|---|---|
| `x`, `z` | Metres east and north of the click. |
| `d`, `r`, `h` | Distance from the click, the radius, the height. |
| `n(x, z)` | Smooth noise from -1 to 1 (use `n(x / 10, z / 10)` for wide bumps). |
| Functions | `smooth(t)` (0 below 0, 1 above 1, smooth between), `bell(t)` (1 at 0, fading out by ±1), `sin`, `cos`, `tan`, `abs`, `sqrt`, `min`, `max`, `pow`, `clamp(v, a, b)`, `exp`, `log`, `floor`, `ceil`, `round`, `sign`, `atan2`, `pi`. |
| Operators | `+ - * / % ^`, comparisons `< > <= >= == !=` (1 or 0), `&&`, `||`, `!`, and `a ? b : c`. |

For example `h * smooth(1 - d / r) * (1 + 0.3 * n(x / 8, z / 8))` is a bumpy hill, and
`abs(x) < 3 ? -h : 0` digs a 6 m wide trench through the circle. A mistake in the formula is shown
under it. The Mask and the ±8 m limit apply; one click is one undo step.
