
namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Joins rooms that are far apart on the way but close in space: through a shared wall, or by a
    /// short straight corridor. How many follows the settings' Loops and the level's rooms.
    /// </summary>
    private void AddLoops(DungeonPlan.Level l)
    {
        int want = (int)MathF.Round(_s.Loops * l.Spaces.Count(s => s.IsRoom) / 5f);
        for (int made = 0, tries = 0; made < want && tries < want * 3 + 3; tries++)
        {
            var options = new List<(float Score, Action Make)>();
            // Joins change the ways between rooms, so the distances are worked out again each try: the stairs once,
            // and each room's only when it has a room to join.
            var stairs = StairLinks(_plan.Spaces);
            foreach (var a in l.Spaces.Where(Loopable))
            {
                Dictionary<DungeonPlan.Space, int>? dist = null;
                foreach (var dir in Dirs)
                {
                    var side = SideTiles(a, dir);
                    for (int at = 0; at + 1 < side.Count; at++)
                    {
                        var (t1, t2) = (side[at], side[at + 1]);
                        if (!IsSlot(t1, t2, a, dir))
                        {
                            continue;
                        }
                        for (int len = 0; len <= 6; len++)
                        {
                            var n1 = Add(t1, dir, len + 1);
                            var n2 = Add(t2, dir, len + 1);
                            var b = l.Owner.GetValueOrDefault(n1);
                            if (b != null && b == l.Owner.GetValueOrDefault(n2) && b != a && Loopable(b))
                            {
                                dist ??= Distances(a, stairs);
                                int d = dist.GetValueOrDefault(b, 99);
                                var path = Enumerable.Range(1, len).SelectMany(t => new[] { Add(t1, dir, t), Add(t2, dir, t) }).ToList();
                                if (d >= 3 && path.All(t => Free(l, t)) && SlotAllowed(b, n1, n2, (-dir.X, -dir.Z)))
                                {
                                    var (aa, bb, tt1, tt2, dd, ll, pp) = (a, b, t1, t2, dir, len, path);
                                    void MakeLoop()
                                    {
                                        if (ll == 0)
                                        {
                                            Connect(aa, bb, tt1, tt2, dd, loop: true);
                                        }
                                        else
                                        {
                                            var c = NewCorridor(l, pp, dd);
                                            Connect(aa, c, tt1, tt2, dd, loop: true);
                                            Connect(c, bb, Add(tt1, dd, ll), Add(tt2, dd, ll), dd, loop: true);
                                        }
                                    }
                                    options.Add((d - len * 0.3f + (float)_rnd.NextDouble(), MakeLoop));
                                }
                                break;
                            }
                            if (b != null || !Free(l, n1) || !Free(l, n2))
                            {
                                break;
                            }
                        }
                    }
                }
            }
            if (options.Count == 0)
            {
                break;
            }
            options.OrderByDescending(o => o.Score).First().Make();
            made++;
        }
    }
}
