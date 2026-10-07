# Path (`P`)

![A planned path with its soft edges](images/path.jpg)

Draw a line, then change the ground along it: a level road, a ramp up a hill, a raised causeway, a
ditch, or a painted track.

## How to use it

1. Choose **Path** (`P`).
2. **Click points** along the route, or **hold and drag** to draw it freely. The red line is the
   centre; the thin lines show the full width and the soft edges.
3. Pick an **Action** and its settings.
4. Press **Apply** (`Enter`). The line stays, so you can apply a second action to it (for example
   flatten, then paint paved).
5. **Clear** (`Esc`) removes the line. **Backspace** removes the last point.

## Controls

| Control | What it does |
|---|---|
| **Action** | What happens along the line: **Flatten to height**, **Ramp (start → end)**, **Raise by**, **Lower by**, **Smooth**, **River / canal**, **Paint dirt / paved / cultivated**, **Clear paint**. |
| **Width** | Width of the path (m). |
| **Soft edge** | Width of the blend on each side (m), so the path joins the ground around it smoothly. |
| **Height** | Flatten to height: the height of the path (m). **Alt + click** the ground picks it. |
| **Amount** | Raise by / Lower by: how much (m). |
| **Start**, **End** | Ramp: the height at the first and at the last point (m). **Alt + click** picks the start, **Alt + Shift + click** the end. The ramp rises evenly along the length of the line. |
| **Depth** | River / canal: how deep the bed is below sea level in its middle (m), so it fills with water. The bed rises to the waterline at the path's **Width**, and the **Soft edge** makes the banks. It only digs: ground already lower stays as it is. The ±8 m limit applies, so a river through high ground stops at it (the status bar says so); bring the ground down near the sea first, or follow low ground. |
| **Smooth curve through the points** | On: a smooth curve through your points. Off: straight lines between them. |
| **Natural look** | Ragged edges, a width that varies along the way, and small bumps, so the path looks worn rather than tool-made (uses the Naturalize settings). |
| **Apply** (`Enter`) | Does the action along the line. One undo step. |
| **Clear** (`Esc`) | Removes the line. |

The panel shows how many points the line has and how long it is. The [Mask](masks.md) applies.
