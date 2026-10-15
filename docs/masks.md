# Mask

![A wide paved stroke across a mound, the Mask set to slope under 15°](images/mask.jpg)
*One wide stroke of Paved paint across a mound, with the Mask set to slopes under 15°. The flat ground is paved; the
mound's steep sides stay grass. The Mask card is next to the tool panel.*

The Mask limits a tool to the ground that matches **all** of its settings, like WorldEdit masks or WorldPainter
filters: only the Meadows, only below 40 m, only slopes under 20°, only unpainted ground. It works with the sculpt
and paint brushes, Stamp once, Shape, Mountain, the Path tool (Cave too), the Area tool's actions, pasting and the
Place tool.

## How to use it

1. Choose a tool that uses it. The **Mask** card shows next to the tool's panel.
2. Tick the **Mask** box on the card. Its settings open below it.
3. Set the biomes, height, slope and paint you want. Leave a setting empty to not limit by it.
4. Use the tool as usual: it changes only the matching ground.

The Mask stays on, with its settings, when you change tools, until you untick it.

## Controls

| Control | What it does |
|---|---|
| **Mask** | On: the tool only changes ground that matches everything below. Off: no limit. |
| **Biome chips** | **Meadows**, **Black Forest**, **Swamp**, **Mountain**, **Plains**, **Mistlands**, **Ashlands**, **Deep North**, **Ocean**: only these biomes. None picked = every biome. |
| **Height** min / max | Only ground between these heights (m). Leave a box empty ("none") for no limit on that side. |
| **Slope** min / max | Only ground between these steepnesses, 0° to 90°. Leave a box empty for no limit. A maximum of 0 lets only perfectly flat ground through. |
| **Paint** | Only ground with this paint: **Any ground**, **Only unpainted**, **Only painted**, **Only dirt**, **Only cultivated**, **Only paved**. |

**Alt + Shift + click** the ground, with a sculpt or paint brush chosen, switches the Mask on and fills **Height**
with the range 2 m below to 2 m above that spot. Handy to work on one terrace or one shore line only.

A warning appears in orange under the settings when they let nothing through: a maximum slope of 0°, or a minimum
above the maximum.

## How it is judged

Each ground point (one per metre) either matches or does not. There is no blend: where the Mask leaves ground out,
the tool does nothing at all, and where it lets ground through, the tool works as usual.

- **Biome** is the biome the world generated at that point, as the game decides it from the seed. Editing the
  ground does not change it.
- **Height** is the ground's height there, as the game counts it (the sea is at 30 m).
- **Slope** is worked out from the heights 1 m either side of the point, east–west and north–south.
- **Paint**: a point counts as painted when it was painted and has some colour (Clear paint leaves it unpainted for
  the Mask). **Only dirt**, **Only cultivated** and **Only paved** need that colour to be at least half of the
  paint.

**During a brush stroke** the Mask looks at the ground **as it was when the stroke started**. So a Raise stroke on
ground below the height limit keeps rising past it until you let go, and a stroke that changes the slope does not
switch itself off halfway. Edits made all at once (Mountain, Shape, Paste, Stamp once, Path, the Area tool's actions)
are each judged on the ground as it was just before them.

If a stroke changes nothing because the Mask leaves out all the ground under the brush, the status bar says so:
"Nothing changed: the Mask leaves out all the ground under the brush (check its height, slope, biome and paint
settings)."

## Good to know

- Because the Mask has no soft edge, sculpting with it can leave a step where matching ground meets ground left out
  (most of all with Raise, Lower and Mountain). Go over the step with **Smooth** afterwards, with the Mask off.
- To paint a path only on the flat parts of a hillside, set **Slope** max and use the Path tool's **Paint** actions.
- To plant only on unpainted ground, or only on cultivated ground, use **Paint** with the [Place](place.md) tool.
- To level the bottom of a valley without touching its sides, set **Height** max a little above the valley floor and
  use Flatten or the [Area](area.md) tool.
