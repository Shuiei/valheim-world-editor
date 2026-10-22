# Generate zones again

Have the game make chosen zones again from the seed, with new trees, rocks, ore and dungeons, for example to get a
Valheim update's new content in places nobody built.

**You need:** the world closed in the game (or the server stopped), or [live mode](edit-a-server-live.md), and a
[backup of the world](back-up-a-world.md): a saved reset cannot be undone.

1. Back up the world.
2. Open the world. Its [map](../map.md) opens.
3. In the side panel, open **Reset zones across the world**.
4. Set the filter: the biomes to reset, and keep **No buildings in the zone or within N zone(s)** on so that bases and
   the land around them are left alone. Use **From the centre … to … m** to reset only part of the world.
5. Press **Show matching**. The first time, the editor reads every zone. Matching zones turn blue, and a line says how
   many match and how many objects they hold.
6. Change the filter until the blue zones are the ones you want.
7. Press **Mark for reset…** and agree. The zones turn red.
8. Move tamed animals out of the red zones in game first: everything in a reset zone goes, but player-built pieces and
   tombstones.
9. **Save to world** (or **Apply live**).

![Reset zones across the world: matching zones blue, marked zones red](../images/map-reset.jpg)
*Blue zones match the filter; red zones are marked for reset.*

To reset only the zones under one place, use the [Area](../area.md#reset-zones) tool's **Reset zones** action instead:
draw a box over them and press **Reset zones… (Enter)**.

## Check it worked

- Before saving, the line under the buttons counts the zones marked for reset.
- In game, go to a reset zone: it is made again as you come near, with new trees, rocks and its location.

## Undo it

- **Before saving:** **Unmark all** cancels every marked reset (one marked with the Area tool's **Reset zones**: `Ctrl+Z` too).
- **After saving:** it cannot be undone in the editor. Put back the world backup from step 1.

## Related

- [World map](../map.md#reset-zones-across-the-world)
- [Zones](../concepts/areas-and-zones.md#zones)
- [Back up a world before editing](back-up-a-world.md)
