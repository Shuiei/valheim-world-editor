using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Dead ends: a corridor or two off a side room that leads nowhere but a cache at its far end (the chest
    /// corridors of Minecraft's strongholds, the blind passages of old crypts). Often in crypts, catacombs,
    /// prisons and ruins, less in the rest. It touches no other corridor (two corridors open into each other).
    /// </summary>
    private void AddStubs(DungeonPlan.Level l)
    {
        float chance = _style.Name is "Catacombs" or "Crypt" or "Prison" or "Ruins" ? 0.7f : 0.35f;
        int want = _rnd.NextDouble() < chance ? 1 + (l.Spaces.Count(s => s.IsRoom) > 10 ? 1 : 0) : 0;
        for (int made = 0, tries = 0; made < want && tries < 24; tries++)
        {
            var a = l.Spaces.Where(s => Loopable(s) && s.Room is not (Room.Entrance or Room.Antechamber)).OrderBy(_ => _rnd.Next()).FirstOrDefault();
            if (a == null)
            {
                return;
            }
            var dir = Dirs[_rnd.Next(Dirs.Length)];
            var side = SideTiles(a, dir);
            if (side.Count < 2)
            {
                continue;
            }
            int at = _rnd.Next(side.Count - 1);
            var (t1, t2) = (side[at], side[at + 1]);
            if (!IsSlot(t1, t2, a, dir))
            {
                continue;
            }
            // Not beside another doorway: two openings side by side leave the wall between them nothing to hold to.
            if (a.Doors.Any(d => new[] { t1, t2 }.Any(t => Math.Abs(t.Item1 - d.T1.I) + Math.Abs(t.Item2 - d.T1.J) <= 2 || Math.Abs(t.Item1 - d.T2.I) + Math.Abs(t.Item2 - d.T2.J) <= 2)))
            {
                continue;
            }
            int len = _rnd.Next(2, 5);
            var path = Enumerable.Range(1, len).SelectMany(k => new[] { Add(t1, dir, k), Add(t2, dir, k) }).ToList();
            var beyond = path.SelectMany(t => Dirs.Select(d => Add(t, d))).Where(t => !path.Contains(t) && t != t1 && t != t2).ToList();
            if (path.Any(t => !Free(l, t)) || beyond.Any(t => l.Owner.GetValueOrDefault(t) is { } o && (o.Corridor || o != a)))
            {
                continue;
            }
            var c = NewCorridor(l, path, dir);
            c.Parent = a;
            c.DeadEnd = true;
            Connect(a, c, t1, t2, dir);
            made++;
        }
    }
}
