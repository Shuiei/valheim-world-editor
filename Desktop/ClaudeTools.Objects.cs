namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    // ---- Objects already there.

    /// <summary>The longest text value get_object gives whole; longer ones are cut.</summary>
    private const int LongestText = 2000;

    /// <summary>The smallest and largest scale set_transform takes.</summary>
    private const float MinScale = 0.05f, MaxScale = 20f;

    /// <summary>The open area's bounds in world metres.</summary>
    internal static (float MinX, float MinZ, float MaxX, float MaxZ) Bounds(WorldScene scene)
    {
        float minX = scene.OriginX, minZ = scene.OriginZ;
        return (minX, minZ, minX + scene.ZonesX * 64f, minZ + scene.ZonesZ * 64f);
    }

    /// <summary>The label of a change's undo step: Claude's own label as given, else the Claude-prefixed one.</summary>
    /// <param name="label">The label given, which starts with Claude or is made to.</param>
    internal static string ClaudesLabel(string label) => label.StartsWith("Claude", StringComparison.Ordinal) ? label : ClaudesStep(label);

    /// <summary>An object after a change: its new id and where it is now.</summary>
    private object Moved(WorldScene scene, int was, int id)
    {
        var t = scene.Things[id];
        return new
        {
            was,
            id,
            name = _w.NameOfPrefab(t.Prefab),
            x = MathF.Round(t.Position.X, 3),
            y = MathF.Round(t.Position.Y, 3),
            z = MathF.Round(t.Position.Z, 3),
            yaw = MathF.Round(t.Rotation.Y, 1),
        };
    }
    /// <summary>The start of a change to objects by their ids, as <see cref="StartObjectsChange"/> checks it.</summary>
    /// <param name="Scene">The open area the ids are of.</param>
    /// <param name="Reads">Its read count when the ids were checked.</param>
    /// <param name="Usable">The ids that can change (indices into the scene's things).</param>
    /// <param name="Problems">Why the other ids cannot.</param>
    internal sealed record ObjectsStart(WorldScene Scene, int Reads, List<int> Usable, List<string> Problems);

    /// <summary>
    /// The checks a change to objects by their ids starts with, in this order: why none may be made now (see
    /// <see cref="EditStart"/>), the pivot given with only one coordinate, and no object that can change at all; else
    /// the start of the change.
    /// </summary>
    /// <param name="ids">The objects' ids.</param>
    /// <param name="pivotX">The pivot's x, or null.</param>
    /// <param name="pivotZ">The pivot's z, or null.</param>
    /// <param name="nothing">How the answer begins when nothing can change ("Nothing moved").</param>
    /// <returns>The start, or why there is none.</returns>
    private (ObjectsStart? Start, string? Problem) StartObjectsChange(IEnumerable<int> ids, float? pivotX, float? pivotZ, string nothing)
    {
        if (EditStart() is string why)
        {
            return (null, why);
        }
        var scene = _w.Session!.Scene;
        int reads = scene.Reads;
        if (LoneCoordinate(pivotX, pivotZ, "pivotX", "pivotZ") is string lone)
        {
            return (null, lone);
        }
        var (usable, problems) = Usable(scene, ids);
        if (usable.Count == 0)
        {
            return (null, $"{nothing}: " + string.Join("; ", problems));
        }
        return (new ObjectsStart(scene, reads, usable, problems), null);
    }
}
