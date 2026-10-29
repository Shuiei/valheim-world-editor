using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;
using TerrainService = ValheimGen.TerrainService;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Restore under saved No limit ground: once saved, the ground past the game's ±8 m is ground discs the editor reads as
/// generated ground (Base), but Restore (the brush and the Area tool's) still takes the ground back to the ground as the
/// world generated it, and the next save takes the discs away.
/// </summary>
public class RestoreDiscGroundTests
{
    /// <summary>The zone in the middle of the hill (its middle at x = z = 320, inside the test world's saved zones).</summary>
    private const int Zone = 5;

    /// <summary>The middle of the 3 × 3 zone block read round <see cref="Zone"/>: zone 5's point 32 both ways.</summary>
    private const int Mid = 64 + 32;

    [Fact]
    public void RestoreTakesSavedNoLimitGroundBackToTheGeneratedGround()
    {
        using var w = new TempWorld();
        var (world, terrain, edits) = Open(w);
        float[] generated = (float[])terrain.BaseZone(Zone, Zone).Clone();
        Assert.Null(terrain.BareZone(Zone, Zone));
        LiftHill(edits);
        Assert.True(Save(world, terrain, edits).Discs > 0);

        // Opened again: the hill is generated ground for the game now, not for Restore.
        (world, terrain, edits) = Open(w);
        Assert.NotEmpty(world.Discs);
        Assert.True(terrain.BaseZone(Zone, Zone)[32 * 65 + 32] - generated[32 * 65 + 32] > 15);
        Assert.NotNull(terrain.BareZone(Zone, Zone));
        Assert.Null(terrain.BareZone(Zone + 6, Zone + 6));

        // The brush: everything back to the ground as generated.
        var brushed = Ground.Read(terrain, edits, Zone - 1, Zone - 1, 3, 3);
        for (int p = 0; p < brushed.Base.Length; p++)
        {
            Sculpt.RestorePoint(brushed, p, 1, 1);
        }
        AssertGenerated(brushed, generated);

        // The Area tool's Restore does the same.
        var area = Ground.Read(terrain, edits, Zone - 1, Zone - 1, 3, 3);
        var restore = new AreaTool.CellChange(area, null!, AreaTool.GroundAction.Restore, 0f, 0f, new float[4], AreaTool.HeightSnapshot.Take(area, null!, -1), 0);
        for (int p = 0; p < area.Base.Length; p++)
        {
            restore.Change(p, 1);
        }
        AssertGenerated(area, generated);

        // Saved: no disc is needed any more, and the world's ground is the generated one again.
        for (int zz = 0; zz < 3; zz++)
        {
            for (int zx = 0; zx < 3; zx++)
            {
                edits.Put(brushed.ZoneEdit(zx, zz));
            }
        }
        Save(world, terrain, edits);
        (world, terrain, _) = Open(w);
        Assert.Empty(world.Discs);
        Assert.InRange(terrain.BaseZone(Zone, Zone)[32 * 65 + 32] - generated[32 * 65 + 32], -1, 1);
    }

    /// <summary>The ground of <see cref="Zone"/> is the generated ground: Restore's lift cancels the discs exactly.</summary>
    private static void AssertGenerated(Ground g, float[] generated)
    {
        Assert.True(MathF.Abs(g.Lift[Mid * g.W + Mid]) > 15);
        for (int k = 0; k < 65; k++)
        {
            for (int l = 0; l < 65; l++)
            {
                int p = (64 + k) * g.W + 64 + l;
                Assert.Equal(0, g.Mod[p]);
                Assert.Equal(generated[k * 65 + l], g.HeightOf(p), 2);
            }
        }
    }

    /// <summary>The test world as the editor opens it: the save, its terrain with the modifiers, no edits.</summary>
    private static (WorldSave World, TerrainService Terrain, EditStore Edits) Open(TempWorld w)
    {
        var world = w.Load();
        return (world, new TerrainService(world, new TerrainModifiers(world)), new EditStore(world));
    }

    /// <summary>A hill 25 m high (past the game's ±8 m) on <see cref="Zone"/>'s middle, as No limit lift.</summary>
    private static void LiftHill(EditStore edits)
    {
        for (int zz = Zone - 1; zz <= Zone + 1; zz++)
        {
            for (int zx = Zone - 1; zx <= Zone + 1; zx++)
            {
                var e = edits.Get(zx, zz) ?? new ZoneEdit(zx, zz);
                for (int i = 0; i < EditStore.Cells; i++)
                {
                    float x = zx * 64 - 32 + i % 65 - Zone * 64, z = zz * 64 - 32 + i / 65 - Zone * 64;
                    float d = MathF.Sqrt(x * x + z * z) / 50;
                    e.Lift[i] += d >= 1 ? 0 : 25 * (1 - d * d) * (1 - d * d);
                }
                edits.Put(e);
            }
        }
    }

    /// <summary>Turns the lift into ground discs and saves the world, as saving does.</summary>
    private static Uplift.Plan Save(WorldSave world, TerrainService terrain, EditStore edits)
    {
        int next = -1;
        var plan = Uplift.Make(world, terrain, edits, () => next--)!;
        Uplift.Apply(plan, edits);
        var r = WorldWriter.Save(world, [.. edits.All().Where(e => e.Changed)], edits.Deleted, edits.Added, edits.Resets);
        Assert.True(r.Saved, r.Message);
        return plan;
    }
}
