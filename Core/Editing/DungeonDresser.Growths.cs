
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// The biome's growths over the room, after everything else (a stream of the room's own, salt 6: the settings
    /// move nothing else): none Bare, a few Furnished, more Rich; more in corridors' long walls than in small rooms.
    /// Nothing stands where something already does.
    /// </summary>
    private void Growths(Frame f)
    {
        var pool = BiomeGrowths;
        if (pool.Length == 0 || _s.Decor < 1)
        {
            return;
        }
        var rnd = Local(f, 6);
        Growth Pick() => Weighted.Pick(pool, g => g.Weight, rnd.NextDouble());
        int n = (int)MathF.Round(f.Area / 30f * (_s.Decor >= 2 ? 1.6f : 0.8f) + (float)rnd.NextDouble());
        var walls = f.WallSpots(2.2f, 2.6f).OrderBy(_ => rnd.Next()).ToList();
        var corners = f.Corners().Where(c => c.Convex).OrderBy(_ => rnd.Next()).ToList();
        for (int i = 0; i < n; i++)
        {
            var g = Pick();
            switch (g.On)
            {
                case 'w' when walls.Count > 0:
                {
                    var sp = walls[^1];
                    walls.RemoveAt(walls.Count - 1);
                    var (u, v) = In(sp, 0.2f);
                    if (g.R > 0 && !f.Free(u, v, g.R))
                    {
                        break;
                    }
                    _o.Add(g.Prefab, f.W(u, v, f.Top + g.Height + DungeonProps.Foot(g.Prefab)), f.Yaw(sp.Yaw));
                    break;
                }
                case 'f' when walls.Count > 0:
                {
                    var sp = walls[^1];
                    walls.RemoveAt(walls.Count - 1);
                    var (u, v) = In(sp, 0.5f + g.R);
                    Put(f, g.Prefab, u, v, sp.Yaw + (float)rnd.NextDouble() * 60 - 30, g.R, take: false);
                    break;
                }
                case 'c' when corners.Count > 0:
                {
                    var (cu, cv, yaw, _) = corners[^1];
                    corners.RemoveAt(corners.Count - 1);
                    var (u, v) = In((cu, cv, yaw), 0.3f + g.R);
                    Put(f, g.Prefab, u, v, yaw + 180 + (float)rnd.NextDouble() * 40 - 20, g.R, take: false);
                    break;
                }
                case 'r':
                {
                    // Hanging from the roof, where the roof is high enough for it, over a spot that is free below.
                    if (f.H - g.Height < 1.6f)
                    {
                        break;
                    }
                    if (Spot(f, 0.6f, 1.2f, rnd) is var (u, v))
                    {
                        _o.Add(g.Prefab, f.W(u, v, f.Top + f.H - g.Height), f.Yaw((float)rnd.NextDouble() * 360));
                    }
                    break;
                }
                default:
                    break;
            }
        }
    }
}
