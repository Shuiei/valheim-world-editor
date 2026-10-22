# Plant a forest, a field or a hedge

Fill a stretch of land with trees in natural groves, a field of crops in rows, or a hedge along a line.

**You need:** a world open in the editor (a backup first: see [Back up a world](back-up-a-world.md)).

## A forest

1. Press `T` to open [Place](../place.md).
2. Under **Choose a preset**, choose **Meadows woods** (or **Black forest**, **Swamp**).
3. At the top of the panel, choose **Zone**.
4. Click points on the ground around the forest's outline, or hold and drag to draw it freely.
5. Optional: under **ZONE FILL**, raise **Clumping** to about 50% for groves with clearings between them, and set
   **Patch size** to how big the groves should be.
6. Press `R` (**New layout (R)**) until the preview looks right.
7. Press `Enter` to plant it.
8. Click **Save to world** (or **Apply live**).

<!-- picture: guide-forest-zone.jpg: Place tool, Zone mode, Meadows woods preset, Clumping 50%, Patch size 30 m; a zone of about 60 x 40 m drawn on open meadow with the see-through preview in groves and clearings; camera 40 m up at an angle; Place panel visible with the ZONE FILL section -->

## A field

1. Paint the field with **Cultivate** (tool rail, PAINT section): crops grow only on cultivated ground.
2. In Place, click **Choose kinds…**, search `carrot`, tick the crops you want, then click **Done**.
3. Choose **Grid**, and set **Spacing** under **GRID** (1 m suits carrots).
4. Drag a box over the cultivated ground, then press `Enter`.

Keep **Leave saplings and crops room to grow** on, so every crop has the room the game needs to grow.

## A hedge

1. In Place, click **Choose kinds…**, search `bush`, and tick one or two bushes.
2. Choose **Line**, then **Points**.
3. Click points along the hedge; set **Every** to about 1.5 m and **Wiggle** to 0.25 m.
4. Press `Enter`.

## Check it worked

The see-through preview turns solid where you planted, with a green unsaved dot on each object. The status bar says
how many objects were placed. In game, walk there after saving.

## Undo it

- Before saving: `Ctrl+Z` takes back the last zone, grid, line or brush stroke.
- To remove a few: Brush mode with the same kinds ticked, `Shift` + drag over them.
- After saving: see [Undo a step, take back one change, or discard everything](undo-changes.md).

## Related

- [Place](../place.md)
- [Growing rules](../concepts/objects.md#growing-rules)
- [Clear trees and rocks, level a building site](clear-an-area.md)
