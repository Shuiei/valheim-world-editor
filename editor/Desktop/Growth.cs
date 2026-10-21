using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Growing the game's own vegetation (Regrow: the world generator's rules, on the ground as it is now)
/// over a rectangle of the world, for the Area tool's Regrow and the Mountain tool: the spots kept by
/// keep, not where something already stands (a tree still there is not doubled) or near buildings.
/// </summary>
public static partial class Growth
{
    /// <summary>
    /// The spots the game would grow in the rectangle, less those near what already stands (anything within 1 m,
    /// the same object within 3 m, buildings within 4 m) and those <paramref name="keep"/> refuses.
    /// </summary>
    /// <param name="session">The edit session whose world, edits and objects are used.</param>
    /// <param name="x0">The rectangle's lowest X, world metres.</param>
    /// <param name="z0">Its lowest Z, world metres.</param>
    /// <param name="x1">Its highest X, world metres.</param>
    /// <param name="z1">Its highest Z, world metres.</param>
    /// <param name="keep">Which spots to keep (the tool's own shape, for example).</param>
    /// <param name="nameOf">The prefab name of a prefab hash, null when unknown.</param>
    /// <param name="message">Shows a status line (progress, or why it cannot be done), in both languages.</param>
    /// <param name="maxZones">The most zones the rectangle may cover (the work grows with them).</param>
    /// <returns>Null when it cannot be done (a message says why).</returns>
    public static async Task<List<Regrow.Spot>?> Spots(EditSession session, float x0, float z0, float x1, float z1, Func<Regrow.Spot, bool> keep, Func<int, string?> nameOf, Action<Said> message, int maxZones = 16)
    {
        var s = session.Scene;
        if (s.Terrain is not { } terrain || s.World == null)
        {
            message(Said.Of("regrow.msg.noGenerator"));
            return null;
        }
        var (zx0, zz0) = ChunkMath.SectorOf(x0, z0);
        var (zx1, zz1) = ChunkMath.SectorOf(x1, z1);
        if ((zx1 - zx0 + 1) * (zz1 - zz0 + 1) > maxZones)
        {
            int side = (int)Math.Sqrt(maxZones);
            message(Said.Of("regrow.msg.tooBig", maxZones, side * 64));
            return null;
        }
        message(Said.Of("regrow.msg.working"));
        var world = s.World;
        var spots = await Task.Run(() => Regrow.Zones(terrain, session.Edits, world.Seed, zx0, zz0, zx1, zz1, Regrow.ClearAreas.Of(world, terrain.Generator))
            .Where(p => world.CanCreate(StableHash.Of(p.Name))).ToList());
        // The tool's own test first, here (it may read the window's state); the rest away from the window.
        var kept = spots.Where(keep).ToList();
        if (kept.Count == 0)
        {
            return kept;
        }
        var standing = StandingNear(s, kept, nameOf);
        return await Task.Run(() => Unobstructed(kept, standing));
    }
}
