# Mask

Make a tool change only the ground you choose, by biome, height, slope or paint: pave only the flat parts of a hill,
level a valley floor without touching its sides, plant only on unpainted ground.

**Where:** the **Mask** card next to the tool's panel · **Key:** none (`Alt`+`Shift`+click sets a height range) ·
**Works in:** saved worlds, live

![The same wide Paved stroke across a mound, with the Mask off and on, and the Mask card](images/mask.jpg)
*The same wide stroke of Paved paint across a mound. Left, Mask off: everything is paved. Right, Mask on with slopes
under 15°: the flat ground is paved, the mound's steep sides are not. The Mask card is next to the tool panel.*

The Mask works with the sculpt and paint brushes, Stamp once, [Shape](shape.md), [Mountain](mountain.md),
[Path](path.md) (Cave too), the [Area](area.md) tool's actions, pasting and [Place](place.md).

## Use it

1. Choose a tool that uses the Mask. The **Mask** card shows next to its panel.
2. On the card, tick **Mask**. Its settings open below it.
3. Set the biomes, height, slope and paint you want. Leave a setting empty to not limit by it.
4. Use the tool as usual: it changes only the ground that matches every setting.
5. When you are done, untick **Mask**. It stays on, with its settings, when you change tools.

## Settings

| Setting | What it does |
|---|---|
| **Mask** | On: the tool changes only ground that matches everything below. Off: no limit. |
| Biome chips | **Meadows**, **Black Forest**, **Swamp**, **Mountain**, **Plains**, **Mistlands**, **Ashlands**, **Deep North**, **Ocean**: only these biomes. None picked: every biome. |
| **Height** min / max | Only ground between these heights (m; the sea is at 30 m). Leave a box empty ("none") for no limit on that side. |
| **Slope** min / max | Only ground between these steepnesses, 0° to 90°. A maximum of 0 lets only perfectly flat ground through. |
| **Paint** | **Any ground**, **Only unpainted**, **Only painted**, **Only dirt**, **Only cultivated** or **Only paved**. |

An orange warning shows under the settings when they let nothing through: a maximum slope of 0°, or a minimum above
the maximum.

<!-- picture: mask-warning.jpg: the Mask card alone, cropped to it, Mask ticked, Slope min 30 and max 10, the orange "Slope min is above max: nothing matches." line visible under the settings -->

## Shortcuts

| Key or mouse | What it does |
|---|---|
| `Alt`+`Shift`+click the ground | With a sculpt or paint brush: turns the Mask on and fills **Height** from 2 m below to 2 m above that spot. Handy to work on one terrace or one shore line only. |

## Tips and limits

- **The Mask has no soft edge.** Each ground point (one per metre) matches or not, so sculpting with it can leave a
  step where matching ground meets the rest, most of all with Raise, Lower and Mountain. Go over the step with the
  **Smooth** brush afterwards, with the Mask off.
- **During a brush stroke the Mask looks at the ground as it was when you pressed the button.** A Raise stroke under
  a height limit keeps rising past it until you let go. One-click edits (Shape, Mountain, Stamp once, Path, Area,
  paste) look at the ground just before them.
- **Biome is the biome the world generated** there. Reshaping the ground does not change it.
- **Paint:** ground wiped with **Clear paint** counts as unpainted. **Only dirt**, **Only cultivated** and
  **Only paved** need that paint to be at least half of the colour.
- **Nothing changed?** The status bar says "Nothing changed: the Mask leaves out all the ground under the brush (check
  its height, slope, biome and paint settings)."
- **The Mask is a setting, not a change**: it is not undone with `Ctrl+Z` and not saved in the world.

Ideas:

- To pave a path only on the flat parts of a hillside, set **Slope** max and use the Path tool's **Paint paved**.
- To plant only on unpainted or only on cultivated ground, set **Paint** and use [Place](place.md).
- To level the bottom of a valley without touching its sides, set **Height** max a little above the valley floor and
  use **Flatten** or the [Area](area.md) tool.

## Related

- [Clear trees and rocks, level a building site](guides/clear-an-area.md)
- [Plant a forest, a field or a hedge](guides/plant-a-forest.md)
- [Sculpt and paint](terrain.md), [Path](path.md), [Area](area.md)
- [How the mask judges the ground](concepts/ground-limits.md#how-the-mask-judges-the-ground)
