# Select (`E`)

![A selected rock with its move arrows](images/select-arrows.jpg)

Pick objects (trees, rocks, building pieces, anything shown), then move, turn, lift, drop, copy,
replace or delete them. Only objects that are shown can be picked; switch kinds on in the
[View panel](editor-basics.md#view-panel) first.

## Selecting

| Action | What it does |
|---|---|
| **Click** an object | Selects it (and only it). The yellow box marks selected objects; a blue box shows what is under the cursor. |
| **Shift + click** | Adds an object to the selection, or takes it out. |
| **Drag on empty ground** | Draws a zone; everything shown inside it is selected when you let go. |
| **Alt + drag** | Draws a zone even when you start over an object (useful in forests, where trees cover the ground). |
| **Shift + drag** | Adds what is inside the zone to the selection. |
| **Click on empty ground** / `Esc` | Clears the selection. |

![Drawing a selection zone](images/select-zone-drawing.jpg)
![Everything inside selected](images/select-zone.jpg)

The panel lists what is selected, by kind.

## Moving and turning

| Action | What it does |
|---|---|
| **Drag a selected object** | Moves the whole selection freely. Objects keep their height above the ground. |
| **Drag an arrow** | Moves the selection along one axis only: **red X** (east–west), **green Y** (up–down), **blue Z** (north–south). Hold **Ctrl** to move in 0.5 m steps. X and Z keep the height above the ground; Y raises or sinks. |
| `,` `.` or **Alt + wheel** | Turns the selection around its centre by 1° (Shift: 15°). |
| `PgUp` / `PgDn` | Lifts or lowers by 0.25 m (Shift: 1 m). |
| `End` | **Drops** each object onto what is below it: the top of another object (a floor, a table, a rock), or the ground when there is nothing. Selected objects do not count as surfaces. |
| `Esc` | Cancels a move that is still in progress. |

Moves are combined: everything you do within a moment becomes one undo step. A moved object keeps
all of its data (a moved chest keeps its contents).

## Other actions

| Control | What it does |
|---|---|
| **Delete** (`Del`) | Removes the selected objects. `Ctrl+Z` brings them back. |
| **Deselect** | Clears the selection. |
| **Replace** + **go** | Replaces every selected object by the chosen kind, at the same place and facing. |
| `Ctrl+C` | Copies the selection; `Ctrl+V` pastes it with the [paste tool](area.md#copy-and-paste). Copies are new, independent objects (a copied chest is empty). Pasted objects keep their height above the ground where they land. |
