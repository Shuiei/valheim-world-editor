# Draw a world

Paint your own world on a map, each biome, river, lake and sea where you want it, and see the world the game will make
from it.

**Where:** start page, **Create** → **A new world** → **Draw a world** · **Key:** none · **Works in:** the start page
(no world needs to be open)

<!-- picture: draw-world.jpg: Draw a world, an island with a mountain range, a Black Forest, Plains, a river and the start point -->

You draw the master map: the whole world seen from above, north up, one pixel about 10 m. The drawing decides the
land's shape and where each biome is. Everything else stays the game's: the hills, rocks and trees of each biome, the
rivers and lakes it adds, and where it puts the start temple, the bosses' altars, the traders and the dungeons.

> [!NOTE]
> Making the world itself (**Create world**) is not there yet: a drawn world needs a small mod on the server and on
> every player's game, which comes next. For now you can draw, save the drawing, and see the world it makes.

## Use it

1. On the start page, choose **A new world** under **Create**, then **Draw a world**. An empty sea is shown.
2. In the **Legend**, choose a kind (**Meadows** to start), then drag on the map to paint land. Set the brush's
   **Size** first: big for continents, small for rivers.
3. Paint the other biomes over it, then rivers, lakes and the sea along the coasts.
4. Optional: choose **Start point** and click where players should arrive.
5. Press **World** to see the world your drawing makes, with where the game will put its places. Press **Drawing** to
   paint again.
6. Press **Save…** to keep the drawing as a PNG picture.

## Settings

| Setting | What it does |
|---|---|
| **Brush** | Paints the kind chosen in the legend along your drag. |
| **Fill** | Paints the whole area you click (all of it of one kind, touching) with the kind chosen. |
| **Start point** | Puts the start where you click. Without one, the start is at the middle of the world. |
| **Size** | The brush's radius, from 10 m to 3 km. |
| **Legend** | The only colours a master map has: one per biome, and four for water. The number beside each is its share of the land (of the whole map for water). |
| **New** | Starts again on an empty sea. |
| **Open…** | Opens a master map saved as a PNG picture. |
| **Save…** | Saves the drawing as a PNG picture. |
| **Undo**, **Redo** | Takes back the last changes, or makes them again. |
| **Seed** | Where the world's small things come from: bumps, trees, rocks, which spot each place takes. The drawing stays the same. |
| **Drawing**, **World** | Shows the drawing to paint on, or the world it makes. |

## The legend

| Kind | Colour | What it makes |
|---|---|---|
| Meadows | `#92A75C` | Meadows |
| Black Forest | `#6B743F` | Black Forest |
| Swamp | `#A37258` | Swamp: low, wet ground |
| Mountain | `#E0E0D6` | Mountain, rising the further inside it; foothills round it |
| Plains | `#E7AB78` | Plains, mostly flat |
| Mistlands | `#333333` | Mistlands |
| Ashlands | `#7B2020` | Ashlands |
| Deep North | `#FFFFFF` | Deep North |
| River | `#7CC4F0` | A channel under water, in the biome of its banks |
| Lake | `#3C8DD6` | Shallow water inland, in the biome of its shores |
| Sea | `#1F5FA8` | The sea, shallow along the coast |
| Deep sea | `#0B2A5B` | The deep sea |
| Start point | `#FF00FF` | Where players arrive (one pixel is enough) |

The colours are the game's map colours, so a drawing looks like the in-game map.

## Draw in another program

A master map is a square PNG picture with the legend's colours. You can paint one in any paint program and open it
here:

- Any square size works: it is scaled to 2048 × 2048 pixels for the whole world, 20 km across.
- Colours close to the legend's count as them, so soft or blurred edges are fine. Colours far from all of them are
  counted when you open the picture, and each is taken as the nearest colour of the legend.
- Several separate start points: only the first (the highest on the picture) is kept, and you are told.

## The world it makes

**World** shows the world as the game will make it from the drawing, with the start (red), the bosses' altars (purple)
and the traders' spots (yellow). It takes a few seconds.

The game still lays out its places by its own rules: each altar, trader and dungeon only goes on its biome, at a height
it accepts, at its distance from the middle of the world. When one has nowhere to go, the World card names it: draw
more of its biome, or bigger areas of it. The places are shown once the editor has read your Valheim's files.

## Tips and limits

- The dashed circle is the world's edge. Past it the game sinks everything into the sea, whatever you draw there.
- Draw some sea between biomes and the world's edge, and along coasts, so that boats can reach them.
- The game adds its own small rivers and lakes on the land you draw.
- Leaving the page does not keep the drawing: **Save…** it first.

## Related

- [A new world](new-world.md): pick a seed's world and make it.
- [Areas and zones](concepts/areas-and-zones.md): how the game builds the world zone by zone.
