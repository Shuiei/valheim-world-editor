using System.Numerics;
using TerrainEditor.Editing;
using ValheimGen;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Save;

// A blueprint location's terrain snapshots written to the ground, as Expand World Data applies them.
public static partial class WorldCreator
{
    /// <summary>The most zones one location's snapshots may change; the mod applies none beyond.</summary>
    private const int MaxSnapshotZones = 64;

    /// <summary>
    /// A blueprint location's terrain snapshots laid on the zones they reach (Terrain.ApplyBlueprint, after the file's
    /// levelling and paint): each vertex of a zone takes the nearest node's value, a height as a level change from the
    /// zone's ground as generated to that height above the blueprint's spot, a paint as it is. Zones that get a value
    /// are added to, or changed in, <paramref name="grounds"/>.
    /// </summary>
    /// <param name="grounds">The changed zones' ground, by zone.</param>
    /// <param name="contents">What the file changes (the blueprint's snapshots).</param>
    /// <param name="at">Where the blueprint is placed (with the file's offset).</param>
    /// <param name="turn">How it is turned.</param>
    /// <param name="generator">The world's generator (the zones' ground as generated).</param>
    internal static void ApplySnapshots(Dictionary<(int X, int Z), ZoneEdit> grounds, LocationContents contents, Vector3 at, Quaternion turn, WorldGenerator generator)
    {
        var (heights, paints) = (contents.TerrainHeights, contents.TerrainPaints);
        if (SnapshotZones(heights, paints, at) is not var (x0, z0, x1, z1))
        {
            return;
        }
        for (int zz = z0; zz <= z1; zz++)
        {
            for (int zx = x0; zx <= x1; zx++)
            {
                var edit = grounds.GetValueOrDefault((zx, zz)) ?? new ZoneEdit(zx, zz) { Changed = true };
                if (SnapshotZone(edit, heights, paints, at, turn, generator))
                {
                    grounds[(zx, zz)] = edit;
                }
            }
        }
    }

    /// <summary>
    /// The zones the snapshots may reach (the farther one's reach from the spot, and a margin of a node or a metre); null
    /// for no snapshot, or more zones than the mod changes.
    /// </summary>
    private static (int X0, int Z0, int X1, int Z1)? SnapshotZones(TerrainSnapshot? heights, TerrainSnapshot? paints, Vector3 at)
    {
        float reach = MathF.Max(Reach(heights), Reach(paints)) + MathF.Max(1f, MathF.Max(Margin(heights), Margin(paints)));
        var (x0, z0) = ChunkMath.SectorOf(at.X - reach, at.Z - reach);
        var (x1, z1) = ChunkMath.SectorOf(at.X + reach, at.Z + reach);
        bool none = heights == null && paints == null;
        return none || (x1 - x0 + 1) * (z1 - z0 + 1) > MaxSnapshotZones ? null : (x0, z0, x1, z1);
    }

    /// <summary>How far a snapshot reaches; 0 for none.</summary>
    private static float Reach(TerrainSnapshot? s) => s == null ? 0f : s.Reach();

    /// <summary>A snapshot's node spacing, at least a millimetre; 0 for none.</summary>
    private static float Margin(TerrainSnapshot? s) => s == null ? 0f : MathF.Max(0.001f, s.Spacing);

    /// <summary>One zone under the snapshots: each vertex's level and paint from the nearest nodes; false when none reaches it.</summary>
    private static bool SnapshotZone(ZoneEdit edit, TerrainSnapshot? heights, TerrainSnapshot? paints, Vector3 at, Quaternion turn, WorldGenerator generator)
    {
        float[]? generated = null;
        bool reached = false;
        for (int z = 0; z < EditStore.Grid; z++)
        {
            for (int x = 0; x < EditStore.Grid; x++)
            {
                int i = z * EditStore.Grid + x;
                var node = new Vector3(edit.ZoneX * 64f + (x - 32), 0f, edit.ZoneZ * 64f + (z - 32));
                if (heights?.Nearest(node, at, turn) is { } h)
                {
                    generated ??= BaseTerrain.BuildZone(generator, edit.ZoneX, edit.ZoneZ, null);
                    // SetHeight: the level that brings the ground there, no smoothing; a node saved as changed unless both are 0.
                    edit.Level[i] = h.X + at.Y - generated[i];
                    edit.Smooth[i] = 0f;
                    edit.Modified[i] = edit.Level[i] != 0f;
                    reached = true;
                }
                if (paints?.Nearest(node, at, turn) is { } c)
                {
                    // SetPaint: the snapshot's final paint, written even where it is the unpainted one.
                    (edit.Paint[i * 4], edit.Paint[i * 4 + 1], edit.Paint[i * 4 + 2], edit.Paint[i * 4 + 3]) = (c.X, c.Y, c.Z, c.W);
                    edit.PaintModified[i] = true;
                    reached = true;
                }
            }
        }
        return reached;
    }
}
