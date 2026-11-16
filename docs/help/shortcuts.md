# Shortcuts

Every key and mouse action of the editor, grouped by what you are doing. The **?** button in the top bar (or `F3`)
shows the main ones while you work.

The keys work in the 3D editor only: not while you type in a box, and not on the start page or the world map
(`F1` excepted: it works everywhere). On a
Mac, press `Cmd` wherever this page says `Ctrl` (`Cmd+Z` undoes, `Cmd`+click adds to a selection) and `Option` for
`Alt`.

## Camera

| Key or mouse | Action |
|---|---|
| Right drag | Turn the view. In Walk and Fly: look around. |
| Wheel | Zoom, from 3 m to 3 km from the point you look at. |
| Middle drag | Slide the view. |
| `Space` + left drag, `Shift` + right drag | Slide the view, in any tool (handy without a middle button). |
| Left drag (**View** tool) | Slide the view. |
| `W` `A` `S` `D` or the arrow keys | Move around. The further you are zoomed out, the faster. Not while `Ctrl` or `Alt` is held. |
| `F` | Walk at eye height, then Fly, then back to the usual view, in every tool. |
| `Shift` (Walk) | Run: 12 m/s instead of 6. |
| `Space` / `C` (Fly) | Go up / down, through the ground too (into a mod's tunnels and cellars). `Shift`: 45 m/s instead of 15. |

While walking or flying the wheel does not zoom and drags do not slide the view. See
[Walk and Fly](../editor-basics.md#walk-and-fly).

## Panels and saving

| Key | Action |
|---|---|
| `V` | Open or close the **View** panel. |
| `L` | Open or close **History**. |
| `?` (`Shift+/`) or `F3` | Open or close the **Controls** help card. |
| `F1` | The Help window at the page of the tool in use (see [The Help window](#the-help-window)). The small **?** at the top of each panel opens that panel's page. |
| `Ctrl+S` | **Save to world** (a saved world), **Apply live** (live), **Save blueprint** (the Workshop). |

## Choose a tool

| Key | Tool |
|---|---|
| `1` `2` `3` `4` `5` | Raise, Lower, Flatten, Smooth, Restore |
| `0` | Naturalize |
| `O` (the letter) | Erode |
| `6` `7` `8` `9` | The **Paint** brush with Dirt, Cultivate, Paved, Clear paint |
| `B` | Region |
| `P` | Path |
| `T` | Place (**Build** in the Workshop) |
| `U` | Build |
| `G` | Landform |
| `E` | Select |
| `M` | Measure |
| `H` or `Esc` | View (`Esc` only when it has nothing else to cancel, see [How Esc works](#how-esc-works)) |

The digits of the number pad work too. **Dungeon**, **Script** and **Blueprints** have no
key: click them on the rail. In the Workshop only `U` or `T`, `E` and `H` or `Esc` choose a tool.

## Undo, copy and paste

| Key | Action |
|---|---|
| `Ctrl+Z` | Undo the last change. |
| `Ctrl+Y` or `Ctrl+Shift+Z` | Redo it. |
| `Ctrl+C` | Copy the Region box or polygon, or the selected objects (Region and Select tools). |
| `Ctrl+X` | Cut: the same copy, then the objects leave the world as one step (the ground stays). |
| `Ctrl+V` | Start pasting the clipboard, from any tool. |
| `[` / `]` | Brush size 1 m smaller / bigger (1 to 30 m). |

## Select objects

These work in the **Select** tool (`E`). A click in the **View** tool picks objects the same way.

| Key or mouse | Action |
|---|---|
| Click | Select that object alone. Click again at the same spot: the next object behind it. |
| `Ctrl`+click | Add the object, or take it out when it was selected. |
| `Shift`+click | Take the object out of the selection. |
| `Ctrl+Shift`+click | Add every shown object in a row, from the last one clicked to this one. |
| Double-click | A building piece: the whole building. A part of a ship or another linked group: the whole group. Any other object (a chest, a sign, a tree): that object, with its Inspector open. In the View tool, a double-click on an object switches to Select. |
| Drag on empty ground | Select what is inside the box (or the outline on the ground, with **A drag draws** set so). `Ctrl`: add to the selection. `Shift`: take out. |
| `Alt` + drag | The same, starting over objects too (objects are not hit). |
| `Ctrl+A` / `Alt+A` | Select every shown object / clear the selection. |
| `Ctrl+I` | Invert the selection. |
| `Ctrl+H` / `Alt+H` | Hide the selected objects in the view / show them again. |
| `Alt+Z` | **Select through** on or off: a box also takes what is hidden behind others. |
| `Shift+F` or number pad `.` | Frame the selection. |

## Per tool

### Sculpt and paint brushes

| Key or mouse | Action |
|---|---|
| Left drag | Paint the brush on the ground. |
| `Alt`+click | Take the ground's height there as Flatten's height, and switch to **Flatten**. |
| `Alt+Shift`+click | Set the [Mask](../masks.md)'s height range to 2 m above and below that spot (with the Path tool too). |

### Region

| Key or mouse | Action |
|---|---|
| Drag (Box) | Draw the box. |
| Click (Polygon) | Add a point. Double-click: close the polygon. |
| `Enter` | Close the polygon being drawn; with the selection done, do the chosen action. |
| `Enter`, then `Del` | With **Objects** chosen: select the objects inside, then delete them in the Select tool. |
| `Backspace` | Remove the polygon's last point. |
| `Esc` | Clear the selection (the box or polygon). |

### Path

| Key or mouse | Action |
|---|---|
| Click | Add a point. |
| Drag the line | Add a point there and move it. |
| Drag a point | Move it. |
| `Ctrl`+click a point | Remove it. |
| `Alt`+click | Take the ground's height there as the path's height (Ramp: its start). |
| `Ctrl+Alt`+click | Ramp: take the height as its end. |
| `Alt+Shift`+click | Set the [Mask](../masks.md)'s height range, as with a brush. |
| `Enter` | Apply the path. |
| `Backspace` | Remove the last point. |
| `Esc` | Clear the line. |

### Place

| Key or mouse | Action |
|---|---|
| Left drag (Brush) | Place objects under the brush. `Shift` + drag: take them away. |
| Click (Line, Polygon) | Add a point. Drag the outline: add a point there. Drag a point: move it. `Ctrl`+click a point: remove it. |
| Double-click the last point | Place along the shape. |
| Drag (Grid, line shapes) | Draw the grid's box, or the shape from one end to the other. |
| `Alt`+click | Take the height there as the **Elevation**. |
| `Enter` | Place along the line, circle or rectangle, or in the grid or polygon. |
| `Backspace` | Remove the line's or polygon's last point. |
| `Esc` | Clear the shape being drawn, or cancel **Pick from world**. |
| `N` | A new random layout. |
| `R` | Turn the preview, a drawn grid or a polygon a quarter turn. |
| `,` / `.` or `Alt` + wheel | Turn the preview, a drawn grid or a polygon by 15° (`Shift`, finer: 1°). |
| `PgUp` / `PgDn` or `Ctrl` + wheel | Raise or lower what you place by 0.5 m (`Shift`, finer: 0.1 m). |
| Click an object (**Pick from world**) | Take its kind. `Shift`+click adds it to the kinds chosen. |

### Build, Paste and moving a selection: the building bar's steps

[Build](../build.md), Paste and Select turn and lift by the same keys, by the steps set on
[the building bar](../build.md#the-building-bar) (**Turn by**, 22.5° at first; **Lift**, 0.5 m at first). `Shift`
always makes the step finer.

| Key or mouse | Action |
|---|---|
| `,` / `.` or `Alt` + wheel | Turn the piece, the paste or the selection by the **Turn by** step (`Shift`: 1°). |
| `PgUp` / `PgDn` or `Ctrl` + wheel | Lift or lower it by the **Lift** step (`Shift`: a fifth of it). |

### Build (worlds, the Workshop, and building in a dungeon)

| Key or mouse | Action |
|---|---|
| Click | Put the piece down (**Lay**: **One piece**), or add a point (**Line**). |
| Drag | A row of the piece (**One piece**); the circle, rectangle or grid (**Lay**). |
| Hold `Alt` | Place the piece by a quarter metre, without snapping. |
| `R` | Turn the piece a quarter turn. |
| `Q` / `E` | With **Snap** on **Manual**: the piece's previous / next snap point. |
| `Shift` + right click | Take away the piece under the pointer. |
| Middle click | Build with the kind of the piece under the pointer. |
| `Enter` | Put down the line, circle, rectangle or grid. |
| `Backspace` | Remove the line's last point. |
| `Esc` | Clear the line, figure or grid being drawn. |

### Paste

| Key or mouse | Action |
|---|---|
| Click | Put the paste down. Click again for more copies. |
| `R` | Turn a quarter turn. |
| `X` | Mirror. |
| `Esc` | Stop pasting, back to the Region tool. |

### Move the selection (Select)

| Key or mouse | Action |
|---|---|
| Drag a selected object | Move the selection. |
| Drag an arrow or the ring | Move along that axis, or turn. |
| `R` | Turn the selection a quarter turn. |
| `End` | Drop it onto the ground. |
| `Del` | Delete the selected objects. |
| `I` | Open the Inspector of the selected object (one object selected). |
| `Esc` | Cancel a move under way, or a box or outline being drawn. |

### Other tools

| Tool | Key or mouse | Action |
|---|---|---|
| Landform | Click | Put the landform there: the formula's shape, the mountain or the picture. |
| Measure | Click two points | Measure between them. `Esc` clears the measure. |
| Dungeon | `R` | Turn the room to add: it joins by its next opening. |
| | `Del` | Delete the selected room. |
| Script | `Ctrl+Enter` | Run the script (in the code box). |
| Inspector | `Esc` | Close it. |
| A mod's tool | Its own keys | A tool under **MODS** on the rail takes the keys it uses first; the others work as usual. |

## How Esc works

`Esc` does the first of these that applies, then stops:

1. Cancels picking a kind with **Pick from world**.
2. Stops pasting (back to the Region tool).
3. Closes the Inspector.
4. Cancels what the tool is doing: a move in Select, a box or outline being drawn, a Place shape or a line laid with
   Build, a Region box or polygon, the Path's points, a measure.
5. Switches to the View tool.

## The world map and a new world

| Mouse | Action |
|---|---|
| Drag | Slide the map. |
| Wheel | Zoom toward the mouse. |
| Click | Pick a spot (the zone for **Edit in 3D**). |
| Double-click (a new world's map) | Show the whole world. |
| `Enter` in a search box | Search. |

## The Help window

| Key | Action |
|---|---|
| `F1` | Open the help at what you are using: the tool, the Inspector, the map, the start page… (any page). |
| `Ctrl+F` | Search the help. |
| `Alt+Left` / `Alt+Right` | Back / forward. |
| `Esc` | Close an enlarged picture, else empty the search. |
| `Enter` in the search box | Open the first page found. |

## Related

- [The editor window](../editor-basics.md): the top bar, the rail, the camera and the panels.
- [Select](../select.md): selecting and moving objects.
- [Place](../place.md), [Build](../build.md), [Path](../path.md), [Region, copy and paste](../area.md): what each
  tool's keys do.
- [Glossary](glossary.md): the words the editor uses.
