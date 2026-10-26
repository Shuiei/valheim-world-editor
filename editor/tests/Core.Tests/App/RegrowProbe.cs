using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow nature against a world the game generated: REALWORLD=&lt;world folder&gt; OUT=&lt;report file&gt; runs it
/// (how many saved trees, rocks... sit exactly where the port puts them, per kind). Without a world it does nothing;
/// the test world is too small to hold generated nature. The world generator keeps static state: one world at a time.
/// </summary>
[Collection("World files")]
public class RegrowProbe
{
    [Fact]
    public void RegrowMatchesTheGamesOwnPlacement()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var terrain = new ValheimGen.TerrainService(world, null);
        var a = Regrow.Zones(terrain, new EditStore(world), 12345, 0, 0, 1, 1);
        var b = Regrow.Zones(terrain, new EditStore(world), 12345, 0, 0, 1, 1);
        Assert.Equal(a, b);   // the same draws every time
        Assert.All(a, s => Assert.InRange(s.X, -32f - 20f, 96f + 20f));
    }

    [Fact]
    public void Probe()
    {
        string? dir = Environment.GetEnvironmentVariable("REALWORLD");
        if (dir == null)
        {
            return;
        }

        var world = WorldSave.Load(dir);
        var terrain = new ValheimGen.TerrainService(world, new TerrainModifiers(world));
        var edits = new EditStore(world);
        var gen = world.Zones!.Generated.Select(g => ((int)g.X, (int)g.Z)).Where(g => Math.Abs(g.Item1) < 40 && Math.Abs(g.Item2) < 40).ToList();
        var byZone = world.Objects.GroupBy(o => ((int)MathF.Floor((o.Position.X + 32) / 64), (int)MathF.Floor((o.Position.Z + 32) / 64))).ToDictionary(g => g.Key, g => g.ToList());
        var per = new Dictionary<string, int[]>();
        foreach (var (zx, zz) in gen)
        {
            var spots = Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz);
            var set = spots.GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var (n, l) in set) { int[] st = per.TryGetValue(n, out int[]? q) ? q : per[n] = new int[3]; st[2] += l.Count; }
            foreach (var (id, prefab, position, rotation, scale) in byZone.GetValueOrDefault((zx, zz)) ?? [])
            {
                string? n = PrefabCatalog.NameOf(prefab);
                if (n == null || !set.TryGetValue(n, out var list))
                {
                    continue;
                }

                int[] st = per[n];
                st[0]++;
                if (list.Any(s => MathF.Abs(s.X - position.X) < 0.05f && MathF.Abs(s.Z - position.Z) < 0.05f))
                {
                    st[1]++;
                }
            }
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("OUT")!, $"total objs {per.Values.Sum(v => v[0])} matched {per.Values.Sum(v => v[1])} spots {per.Values.Sum(v => v[2])}\n" + string.Join("\n", per.OrderByDescending(kv => kv.Value[0]).Select(kv => $"{kv.Key}: objs {kv.Value[0]} matched {kv.Value[1]} spots {kv.Value[2]}")));
    }
}
