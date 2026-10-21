using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// The objects inside the selection to bring back from a backup and the current ones to remove: an object unchanged
    /// since the backup (same kind, place and facing) is kept as it is. Runestones are left as they are on both sides
    /// (a location proxy cannot be copied back).
    /// </summary>
    /// <param name="session">The session whose scene holds the current objects.</param>
    /// <param name="bw">The backup's world.</param>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <returns>The current objects to remove (indices), and the backup's objects to add.</returns>
    private (List<int> Remove, List<(NewObject, bool)> Adds) RestoreObjects(EditSession session, WorldSave bw, List<Vector2> poly)
    {
        var s = session.Scene;
        float ox = s.OriginX, oz = s.OriginZ;
        // Both sides are picked alike (where the Mask lets through too): an unchanged object picked on one side only
        // would be brought back on top of itself.
        var maskAt = MaskTest();
        var wanted = WorldScene.ReadThings(bw, s.X0, s.Z0, s.ZonesX, s.ZonesZ, new HashSet<int>())
            .Where(o => !o.Runestone && AreaTool.Inside(poly, o.Position.X - ox, o.Position.Z - oz) && Area.Kinds.Contains(KindOf(o)) && maskAt(o.Position.X - ox, o.Position.Z - oz)).ToList();
        var current = ThingsInside(poly, chosenKindsOnly: true, includeHidden: true, maskAt).Where(i => !s.Things[i].Runestone).ToList();
        var kept = new HashSet<int>();
        var adds = MatchBackupObjects(bw, s, wanted, current, kept);
        var remove = current.Where(i => !kept.Contains(i)).ToList();
        return (remove, adds);
    }

    /// <summary>Whether a current object is the backup's: same prefab, place within a centimetre and turned within 0.6°.</summary>
    /// <param name="r">The current object.</param>
    /// <param name="o">The backup's object.</param>
    internal static bool SameObject(WorldScene.Thing r, WorldScene.Thing o) => r.Prefab == o.Prefab && MathF.Abs(r.Position.X - o.Position.X) < 0.01f
        && MathF.Abs(r.Position.Y - o.Position.Y) < 0.01f && MathF.Abs(r.Position.Z - o.Position.Z) < 0.01f
        && MathF.Abs(((r.Rotation.Y - o.Rotation.Y) % 360 + 540) % 360 - 180) < 0.6f;

    /// <summary>An object's prefab and the metre cell it stands in.</summary>
    /// <param name="t">The object.</param>
    internal static (int, int, int) CellOf(WorldScene.Thing t) => (t.Prefab, (int)MathF.Floor(t.Position.X), (int)MathF.Floor(t.Position.Z));
}
