# Audit scripts (run with run_script, dryRun: true)

Run after each big phase and before reporting. Fill in the building's volumes at the top of the window
audit (the same numbers the build used). Fix with a non-dry script, then run the audit again until clean.

## 1. Counts, duplicates, orphans

```csharp
var all = Objects.All.Where(o => o.Building).ToList();
var dups = all.GroupBy(o => (o.Prefab, MathF.Round(o.X * 10), MathF.Round(o.Y * 10), MathF.Round(o.Z * 10))).Where(g => g.Count() > 1).ToList();
var arches = all.Where(o => o.Prefab == "Piece_grausten_wall_arch").ToList();
int lone = arches.Count(a => !arches.Any(b => b.Id != a.Id && MathF.Abs(b.Y - a.Y) < 0.05f
	&& MathF.Abs(MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)) - 1) < 0.1f));
int orphanGlass = all.Count(o => (o.Prefab == "crystal_wall_1x1" || o.Prefab == "iron_wall_1x1") &&
	!arches.Any(a => (a.X - o.X) * (a.X - o.X) + (a.Z - o.Z) * (a.Z - o.Z) < 1.2f && a.Y >= o.Y - 0.1f && a.Y - o.Y < 4.5f));
Print($"pieces {all.Count}, duplicate spots {dups.Count} (+{dups.Sum(g => g.Count() - 1)}), windows {arches.Count / 2}, unpaired arches {lone}, orphan glass {orphanGlass}");
foreach (var g in dups.Take(5)) Print($"  {g.Key.Prefab} at {g.Key.Item2 / 10},{g.Key.Item3 / 10},{g.Key.Item4 / 10}");
```

Fix duplicates (keep one of each):

```csharp
int k = 0;
foreach (var g in Objects.All.Where(o => o.Building).GroupBy(o => (o.Prefab, MathF.Round(o.X * 10), MathF.Round(o.Y * 10), MathF.Round(o.Z * 10))).Where(g => g.Count() > 1))
	foreach (var o in g.Skip(1)) { Objects.Remove(o); k++; }
Print(k);
```

## 2. Windows: blocked by a block, a tower or a roof; stones on the opening

Windows are found from their arch-head pair and their sill (darkwood_beam 0.6 m outside, just below):
that gives the centre, the facing and the bottom.

```csharp
const float G = 34;   // ground
// The building's volumes: rectangles (x0, z0, x1, z1, top y), round towers (cx, cz, r, top y),
// pitched roofs (x0, z0, x1, z1, ridge along x?, eave y, half-span).
var rects = new (float x0, float z0, float x1, float z1, float top)[] { /* … */ };
var circles = new (float cx, float cz, float r, float top)[] { /* … */ };
var roofs = new (float x0, float z0, float x1, float z1, bool alongX, float eave, float half)[] { /* … */ };
bool Solid(float x, float z, float y)
{
	const float m = 0.6f;   // wall half-thickness + plinth/cornice
	if (rects.Any(r => x > r.x0 - m && x < r.x1 + m && z > r.z0 - m && z < r.z1 + m && y < r.top)) return true;
	if (circles.Any(c => (x - c.cx) * (x - c.cx) + (z - c.cz) * (z - c.cz) < (c.r + m) * (c.r + m) && y < c.top)) return true;
	foreach (var r in roofs)
		if (x > r.x0 - 0.3f && x < r.x1 + 0.3f && z > r.z0 - 0.3f && z < r.z1 + 0.3f)
		{
			float d = r.alongX ? MathF.Abs(z - (r.z0 + r.z1) / 2) : MathF.Abs(x - (r.x0 + r.x1) / 2);
			if (y < r.eave + r.half - d) return true;
		}
	return false;
}
var all = Objects.All.Where(o => o.Building).ToList();
var arches = all.Where(o => o.Prefab == "Piece_grausten_wall_arch").ToList();
var sills = all.Where(o => o.Prefab == "darkwood_beam").ToList();
var smalls = all.Where(o => o.Prefab == "stone_wall_1x1").ToList();
var used = new HashSet<int>();
int blocked = 0, stones = 0, n = 0;
foreach (var a in arches)
{
	if (used.Contains(a.Id)) continue;
	var b = arches.FirstOrDefault(o => o.Id != a.Id && !used.Contains(o.Id) && MathF.Abs(o.Y - a.Y) < 0.05f && MathF.Abs(MathF.Sqrt((o.X - a.X) * (o.X - a.X) + (o.Z - a.Z) * (o.Z - a.Z)) - 1) < 0.1f);
	if (b == null) continue;
	used.Add(a.Id); used.Add(b.Id); n++;
	float cx = (a.X + b.X) / 2, cz = (a.Z + b.Z) / 2;
	var sill = sills.Where(s => s.Y < a.Y && MathF.Abs(MathF.Sqrt((s.X - cx) * (s.X - cx) + (s.Z - cz) * (s.Z - cz)) - 0.6f) < 0.1f).OrderByDescending(s => s.Y).FirstOrDefault();
	if (sill == null) { Print($"no sill {cx:0.#},{cz:0.#}"); continue; }
	float nx = (sill.X - cx) / 0.6f, nz = (sill.Z - cz) / 0.6f, tx = nz, tz = -nx, bottom = sill.Y + 0.2f;
	int h = (int)MathF.Round(a.Y - bottom) + 1;
	bool hit = false;
	foreach (float s in new[] { -0.9f, 0f, 0.9f }) foreach (float dy in new[] { 0.3f, h - 0.3f })
		if (Solid(cx + nx * 1.2f + tx * s, cz + nz * 1.2f + tz * s, bottom + dy)) hit = true;
	if (hit) { blocked++; Print($"blocked {cx:0.#},{cz:0.#} bottom {bottom - G:0}"); }
	int st = smalls.Count(s => s.Y > bottom && s.Y < a.Y + 1.1f && (s.X - cx) * (s.X - cx) + (s.Z - cz) * (s.Z - cz) < 1.69f && ((s.X - cx) * nx + (s.Z - cz) * nz) > 0.1f);
	if (st > 0) { stones++; Print($"stone on {cx:0.#},{cz:0.#} bottom {bottom - G:0}"); }
}
Print($"windows {n}, blocked {blocked}, with stones on them {stones}");
```

Fix: a blocked window is walled up — remove its parts (arch pair, crystal, iron, grate, decowall, sill
within 0.85 m of the centre and inside its height) and place `stone_wall_2x1` at the centre, yaw φ =
atan2(nx, nz), one per row. Stones on a window: remove them (cornice dentils over window columns,
crown corbels over a tower window). Then dedupe again: walling a shared wall can double a stone.

## 3. Floating and sunk things

```csharp
// Objects whose bottom is far from anything below them (flagpoles above spires, lanterns in the air).
foreach (var p in new[] { "darkwood_pole4", "piece_dvergr_lantern_pole", "piece_banner02" })
	foreach (var o in Objects.All.Where(o => o.Prefab == p))
		Print($"{p} at {o.X:0.#},{o.Y:0.#},{o.Z:0.#} ground {Ground.Height(o.X, o.Z):0.#}");
```

Compare against what should be under each (the spire tip: the highest darkwood_roof_67 of the cone + 3).

## 4. What is near a point (identifying something odd in a screenshot)

```csharp
foreach (var g in Objects.All.Where(o => o.X > X0 && o.X < X1 && o.Z > Z0 && o.Z < Z1 && o.Y > Y0).GroupBy(o => o.Prefab))
	Print($"{g.Key}: {g.Count()}  e.g. {g.First().X:0.##},{g.First().Y:0.##},{g.First().Z:0.##} id {g.First().Id}");
```
