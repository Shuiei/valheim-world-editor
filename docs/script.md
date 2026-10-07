# Script console

Like MCEdit's filters: a few lines of JavaScript run against the world, for jobs the tools do not
cover (remove every young beech in a valley, put rocks on every steep slope, cut terraces...).
Open it with **Script** in the top bar.

| Control | What it does |
|---|---|
| **Script** | An example, or one of yours. Choosing one puts its code in the box. |
| **Run** (`Ctrl+Enter`) | Runs the code. Everything it changes is **one step in History**: `Ctrl+Z` takes the whole run back. If it stops on an error, what it changed before is kept as a step too. |
| **Keep as…** / **Delete** | Keeps the code under a name (in this browser) / deletes one of yours. |
| Output | What `vwe.log` writes, and errors. |

## The API (`vwe`)

Coordinates are the game's, in metres: `x` east, `z` north, `y` up. Only the area shown in the
editor can be changed.

| Call | What it does |
|---|---|
| `vwe.objects({ name, kind, inSelection })` | The objects (id, name, kind, x, y, z, ry, scale, added). `name`: a name or a pattern (`/^Beech/`); `kind`: trees, rocks, bushes, pickables, ore, ruins, buildings, other. |
| `vwe.add([{ name, x, z, y, ry, rx, rz, scale }])` | New objects (`await` it). `y` defaults to the ground; kinds that cannot be placed are listed and skipped. |
| `vwe.remove(objects or ids)` | Removes them. |
| `vwe.ground(x, z)` / `vwe.original(x, z)` | The height of the ground now / as generated (`null` outside the area). |
| `vwe.setGround(x, z, y)` | Sets the ground point nearest to (x, z) to `y`, within the game's ±8 m limit. |
| `vwe.points({ inSelection, every })` | The ground points (x, z, h, weight), every `every` metres. |
| `vwe.selection()` / `vwe.inside(x, z)` | The Area selection's outline / whether a point is in it. |
| `vwe.kinds()` | The kinds that can be placed. |
| `vwe.select(objects)` | Selects them (Select tool). |
| `vwe.log(...)` | Writes to the output. |

With `inSelection: true` a script works inside the [Area](area.md) selection (with its soft edge
and the Mask); without a selection it stops and says so, so it never runs over the whole area by
accident.

```js
// Young beeches out of the selection, grown ones kept.
const young = vwe.objects({ name: /^Beech_small/, inSelection: true });
vwe.remove(young);
vwe.log(`Removed ${young.length}.`);
```
