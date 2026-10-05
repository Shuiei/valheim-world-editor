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

### Brush settings (all brushes)

| Control | What it does |
|---|---|
| **Size** | Brush radius in metres (`[` and `]` change it). The effect fades out smoothly towards the edge of the ring. |
| **Strength** | How fast the brush works while you hold the button. |
| **Mask** | Limits the brush to some ground, see [Mask](masks.md). |

### Flatten settings

| Control | What it does |
|---|---|
| **Level to the height where the stroke starts** | On (default): the ground under your first click sets the height; lower ground around it is raised and higher ground is cut, so you get a flat pad at that height. The Height box shows the height in use. |
| **Height** | Used when the option above is off: everything is levelled to this height (m). **Alt + click** the ground picks its height and switches to this fixed mode, handy to make several pads match. |

### Naturalize settings

| Control | What it does |
|---|---|
| **Bumps** | How tall the natural bumps are (m). |
| **Size** | How wide they are (m): small values give rough ground, large values gentle swells. |
| **New pattern** | A new random bump pattern for the next stroke (and for natural paths). |

The pattern is continuous across the world, so neighbouring strokes and areas match.

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

- **The 8 m limit.** The game stores ground changes as an offset from the generated ground and
  allows at most ±8 m. Points that reach it turn red and the status bar says so.
- **Locked edge.** The outer line of points of the loaded area cannot be changed, so it always
  joins the next area seamlessly. Move the area with the top-bar arrows to edit there.
- **Locations.** Villages, the trader, dungeon entrances and similar places flatten the ground
  around them while the game runs. The warning box in the tool panel counts them; the editor already
  shows the ground with that flattening.
- **Status bar.** It shows the ground height under the cursor, its original height, and the change.
