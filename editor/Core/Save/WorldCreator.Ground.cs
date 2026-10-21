using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using ValheimGen;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Save;

// The ground Expand World Data levels and paints round a location as it spawns it.
public static partial class WorldCreator
{
    /// <summary>The paint mask of unpainted ground (Heightmap.m_paintMaskNothing).</summary>
    private static readonly Vector4 NoPaint = new(0f, 0f, 0f, 1f);

    /// <summary>
    /// The location's zone as the mod changes its ground before spawning it (LocationSpawning.HandleTerrain, on that
    /// zone's terrain only): levelled to the spot, then painted round it; null when the file changes neither. The
    /// values are the mod's as it writes them (the game holds a level to 8 m when it applies it).
    /// </summary>
    /// <param name="contents">What the file changes.</param>
    /// <param name="surface">The location's spot on the ground (before the file's offset).</param>
    /// <param name="zone">Its zone.</param>
    /// <param name="generator">The world's generator (the zone's ground as generated).</param>
    internal static ZoneEdit? GroundOf(LocationContents contents, Vector3 surface, (int X, int Z) zone, WorldGenerator generator)
    {
        if (contents.Level == null && contents.Paint == null)
        {
            return null;
        }
        var edit = new ZoneEdit(zone.X, zone.Z) { Changed = true };
        if (contents.Level is { } level)
        {
            Level(edit, BaseTerrain.BuildZone(generator, zone.X, zone.Z, null), surface, level);
        }
        if (contents.Paint is { } paint)
        {
            Paint(edit, surface, paint);
        }
        return edit;
    }

    /// <summary>
    /// Terrain.Level: each height of the zone but its edge, within the radius and border of the spot, moved towards
    /// the spot's height (fully within the radius, then less and less), from the ground as generated; a move of 0
    /// changes nothing.
    /// </summary>
    private static void Level(ZoneEdit edit, float[] generated, Vector3 surface, LevelArea level)
    {
        float reach = level.Radius + level.Border;
        if (reach == 0f)
        {
            return;
        }
        float smooth = level.Border / reach;
        for (int z = 1; z < EditStore.Grid - 1; z++)
        {
            for (int x = 1; x < EditStore.Grid - 1; x++)
            {
                if (Reach(edit, surface, x, z, 0f, reach) is not { } d)
                {
                    continue;
                }
                int i = z * EditStore.Grid + x;
                float move = (float)(Blend(smooth, d) * ((double)surface.Y - generated[i]));
                edit.Level[i] = move;
                edit.Modified[i] = move != 0f;
            }
        }
    }

    /// <summary>
    /// Terrain.Paint: each of the zone's 64 × 64 paint cells (measured from its corner) within the radius and border
    /// of the spot blended from unpainted towards the paint, its a the paint's; written as the game reads the mod's
    /// 64 × 64 paint into its 65 × 65 grid (the last row and column repeated).
    /// </summary>
    private static void Paint(ZoneEdit edit, Vector3 surface, PaintArea paint)
    {
        float reach = paint.Radius + paint.Border;
        if (reach == 0f)
        {
            return;
        }
        float smooth = paint.Border / reach;
        const int Cells = EditStore.Grid - 1;
        var painted = new Vector4?[Cells * Cells];
        var color = new Vector4(paint.Color, paint.Alpha);
        for (int z = 0; z < Cells; z++)
        {
            for (int x = 0; x < Cells; x++)
            {
                if (Reach(edit, surface, x, z, 0.5f, reach) is { } d)
                {
                    var blended = Lerp(NoPaint, color, Math.Clamp(Blend(smooth, d), 0f, 1f)) with { W = paint.Alpha };
                    // Unity's == on colours: the same within a squared distance of 1e-10.
                    painted[z * Cells + x] = Vector4.DistanceSquared(blended, NoPaint) < 9.99999944E-11f ? null : blended;
                }
            }
        }
        for (int i = 0; i < EditStore.Cells; i++)
        {
            if (painted[Math.Min(i / EditStore.Grid, Cells - 1) * Cells + Math.Min(i % EditStore.Grid, Cells - 1)] is { } c)
            {
                edit.PaintModified[i] = true;
                (edit.Paint[i * 4], edit.Paint[i * 4 + 1], edit.Paint[i * 4 + 2], edit.Paint[i * 4 + 3]) = (c.X, c.Y, c.Z, c.W);
            }
        }
    }

    /// <summary>
    /// How far a point of the zone's grid is from the spot as a fraction of the reach (the mod's VertexToWorld: half a
    /// metre off the vertex, plus <paramref name="corner"/>), measured on the ground; null beyond the reach.
    /// </summary>
    private static float? Reach(ZoneEdit edit, Vector3 surface, int x, int z, float corner, float reach)
    {
        float px = edit.ZoneX * 64f + (x - 32 + 0.5f) + corner, pz = edit.ZoneZ * 64f + (z - 32 + 0.5f) + corner;
        double dx = (double)surface.X - px, dz = (double)surface.Z - pz;
        float d = (float)Math.Sqrt(dx * dx + dz * dz);
        return d > reach ? null : d / reach;
    }

    /// <summary>Unity's Color.Lerp: each part a + (b - a) * t (in double, rounded once).</summary>
    private static Vector4 Lerp(Vector4 a, Vector4 b, float t) => new(
        (float)(a.X + ((double)b.X - a.X) * t), (float)(a.Y + ((double)b.Y - a.Y) * t), (float)(a.Z + ((double)b.Z - a.Z) * t), (float)(a.W + ((double)b.W - a.W) * t));

    /// <summary>The mod's CalculateSmooth: 1 within the radius, then falling to 0 over the border's share.</summary>
    private static float Blend(float smooth, float d) => 1f - d >= smooth ? 1f : (1f - d) / smooth;

    /// <summary>
    /// A location's zone as the game's terrain mesh has it when it snaps the file's objects after levelling it: the mod
    /// rebuilds the zone's terrain then, so its heights are the zone's as generated, then the location's own terrain
    /// modifiers (spawned just before), then the level (Heightmap.ApplyModifiers, TerrainComp.ApplyToHeightmap: held to
    /// 8 m of the modified ground).
    /// </summary>
    /// <param name="ZoneX">The zone's column.</param>
    /// <param name="ZoneZ">The zone's row.</param>
    /// <param name="Heights">Its 65 × 65 heights, row = z.</param>
    internal sealed record LevelledZone(int ZoneX, int ZoneZ, float[] Heights);

    /// <summary>The <see cref="LevelledZone"/> of a location the file levels; null for one it does not.</summary>
    /// <param name="edit">Its zone's ground as the file changes it; null for none.</param>
    /// <param name="terrain">The world's terrain.</param>
    /// <param name="location">The game location it spawns (its terrain modifiers).</param>
    /// <param name="at">Where the location stands (with the file's offset).</param>
    /// <param name="yaw">Its turn, degrees.</param>
    internal static LevelledZone? LevelledOf(ZoneEdit? edit, TerrainService terrain, int location, Vector3 at, float yaw)
    {
        if (edit == null)
        {
            return null;
        }
        float[] heights = [.. terrain.BaseZone(edit.ZoneX, edit.ZoneZ)];
        float[] held = new Terrain.TerrainModifiers([new PlacedObject(0, location, at, new Vector3(0f, yaw, 0f), 0)]).Apply(edit.ZoneX, edit.ZoneZ, heights);
        for (int i = 0; i < heights.Length; i++)
        {
            if (edit.Modified[i])
            {
                heights[i] = Math.Clamp((float)((double)heights[i] + edit.Level[i] + edit.Smooth[i]), held[i] - EditStore.MaxLevel, held[i] + EditStore.MaxLevel);
            }
        }
        return new LevelledZone(edit.ZoneX, edit.ZoneZ, heights);
    }

    /// <summary>
    /// The ground under an object the file snaps to it: on the location's levelled zone, that zone as the game's mesh
    /// has it then; elsewhere the zone's as generated (<see cref="GroundHeight"/>).
    /// </summary>
    private static float SnapHeight(LevelledZone? levelled, TerrainService terrain, Vector3 at) =>
        levelled == null || ChunkMath.SectorOf(at.X, at.Z) != (levelled.ZoneX, levelled.ZoneZ)
            ? GroundHeight(terrain, at)
            : Regrow.GridHeight(levelled.Heights, EditStore.Grid, levelled.ZoneX * 64f - 32f, levelled.ZoneZ * 64f - 32f, at.X, at.Z);

    /// <summary>A zone's terrain compiler holding its ground changes (none: an empty one), as the game keeps it.</summary>
    internal static byte[] TerrainObject(ZoneEdit edit)
    {
        ushort flags = Terrain.PrefabCatalog.Get(TerrainCompiler)?.Flags ?? 0x0D00;
        var z = ZdoData.Parse(ZdoBuilder.Blank(TerrainCompiler, flags, new Vector3(edit.ZoneX * 64f, 0f, edit.ZoneZ * 64f), Vector3.Zero, 0f));
        z.SmallPosition = true;
        z.SetBytes(StableHash.Of("TCData"), WorldWriter.EncodeTerrain(edit));
        return z.Serialize();
    }
}
