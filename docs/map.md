# World map

![World map](images/map.jpg)

The first page (http://127.0.0.1:5180) is a map of the whole world, drawn with the game's own map
shader, so it looks like the in-game map with everything revealed.

## Using it

- **Drag** to pan, **scroll** to zoom.
- **Click** any spot: a box shows the zone and its centre, with an **Edit in 3D** link and a size
  choice. Clicking an edited zone also shows its details.
- The coordinates and zone under the cursor are shown at the bottom.

## Controls

| Control | What it does |
|---|---|
| **Show buildings** | Draws the footprint of every player-built piece. The number is how many pieces the world has. |
| **Outline edited zones** | Outlines the zones whose ground was edited, in game or in the editor. |
| **Show painted ground** | When zoomed in, draws dirt, paved and cultivated ground. |
| **Zone grid** | Draws the 64 m zone grid. |
| **Clouds** | Draws the drifting cloud shadows of the in-game map. |
| **Edit in 3D** | Opens the 3D editor around the clicked spot. |
| **Size** (next to Edit in 3D) | How much ground the editor loads: 3 × 3 zones (192 m), 5 × 5 (320 m) or 7 × 7 (448 m). Bigger areas are slower to load and draw. |
| **Save to world…** / **Discard** | Same as in the editor: write or throw away the pending changes. |
| **Reload from the game** | Live mode only: load the world again from the running game. |

## Edited zones list

The table lists every zone with ground edits: how many height points (`h`) and paint points (`p`)
were changed, and the range of the height changes in metres. Click a row to see that zone:

- its biome and its height range, original and now;
- a picture of the terrain with your edits, of the height edits alone, and of the paint.
