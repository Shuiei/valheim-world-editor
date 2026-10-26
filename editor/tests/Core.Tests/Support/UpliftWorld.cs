using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using TerrainService = ValheimGen.TerrainService;

namespace WorldEditor.Tests;

/// <summary>
/// A world with its terrain (with modifiers) and edits: the ground as the editor shows it, lifted, and saved. The uplift
/// tests lift the ground in it and check the ground the saved world makes.
/// </summary>
public sealed class UpliftWorld
{
    /// <summary>The world as the editor read it.</summary>
    public required WorldSave World { get; init; }

    /// <summary>The terrain of the world, with its modifiers.</summary>
    public required TerrainService Terrain { get; init; }

    /// <summary>The edits made on the world.</summary>
    public required EditStore Edits { get; init; }

    private int _next = -1;

    /// <summary>The next id for an object the uplift makes, counting down from -1.</summary>
    public int NextId() => _next--;

    /// <summary>A world opened from its save, with its terrain and an empty set of edits.</summary>
    public static UpliftWorld Of(WorldSave world)
    {
        var mods = new TerrainModifiers(world);
        return new UpliftWorld { World = world, Terrain = new TerrainService(world, mods), Edits = new EditStore(world) };
    }

    /// <summary>The ground as the editor shows it (edits clamped to ±8 m, plus any lift not saved yet).</summary>
    public float Height(float x, float z)
    {
        int zx = (int)MathF.Floor((x + 32) / 64), zz = (int)MathF.Floor((z + 32) / 64);
        int i = ((int)MathF.Round(z) - (zz * 64 - 32)) * 65 + (int)MathF.Round(x) - (zx * 64 - 32);
        float b = Terrain.BaseZone(zx, zz)[i];
        var e = Edits.Get(zx, zz);
        return b + (e != null && e.Modified[i] ? Math.Clamp(e.Level[i] + e.Smooth[i], -8, 8) : 0) + (e?.Lift[i] ?? 0);
    }

    /// <summary>Lifts the ground by f(x, z) (No limit) over the zones around (cx, cz).</summary>
    public void Lift(float cx, float cz, Func<float, float, float> f)
    {
        int zc = (int)MathF.Floor((cx + 32) / 64), zr = (int)MathF.Floor((cz + 32) / 64);
        for (int zz = zr - 2; zz <= zr + 2; zz++)
        {
            for (int zx = zc - 2; zx <= zc + 2; zx++)
            {
                var e = Edits.Get(zx, zz) ?? new ZoneEdit(zx, zz);
                for (int i = 0; i < EditStore.Cells; i++)
                {
                    e.Lift[i] += f(zx * 64 - 32 + i % 65, zz * 64 - 32 + i / 65);
                }
                Edits.Put(e);
            }
        }
    }

    /// <summary>Plans and applies the uplift, saves the world, and checks the save wrote everything it was given.</summary>
    public Uplift.Plan Save()
    {
        var plan = Uplift.Make(World, Terrain, Edits, NextId)!;
        Uplift.Apply(plan, Edits);
        var r = WorldWriter.Save(World, [.. Edits.All().Where(e => e.Changed)], Edits.Deleted, Edits.Added, Edits.Resets);
        Assert.True(r.Saved, r.Message);
        Assert.True(r.Skipped.Count == 0, string.Join("; ", r.Skipped.Take(3)));
        return plan;
    }

    /// <summary>A smooth hill (or hollow) of the given height and radius.</summary>
    public static Func<float, float, float> Bell(float cx, float cz, float height, float radius) => (x, z) =>
    {
        float d = MathF.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
        return d >= 1 ? 0 : height * (1 - d * d) * (1 - d * d);
    };
}
