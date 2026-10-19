using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public static partial class Workshop
{
    /// <summary>A terrain contact's point from its first three fields; false when one is not a number.</summary>
    internal static bool TryPoint(string[] f, out Vector3 p)
    {
        var n = System.Globalization.NumberStyles.Float;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        if (float.TryParse(f[0], n, c, out float x) && float.TryParse(f[1], n, c, out float y) && float.TryParse(f[2], n, c, out float z))
        {
            p = new Vector3(x, y, z);
            return true;
        }
        p = default;
        return false;
    }

    /// <summary>
    /// Puts a planned blueprint on the plot as one undo step (the plot takes its ruin flag when its anchor is at the
    /// middle), and returns what <c>Add</c> does.
    /// </summary>
    /// <param name="s">The edit session <c>Plan</c> was given.</param>
    /// <param name="plan">What to put down.</param>
    /// <param name="label">The history's name for the step; null: "Opened" or "Added" and the blueprint's name.</param>
    public static (int Placed, List<string> Unknown, float Lift, Dictionary<(int, int), float> Terrain, int Stood) Put(EditSession s, Planned plan, string? label = null)
    {
        if (plan.AtMiddle)
        {
            s.Scene.Ruin = plan.Ruin;
        }
        s.Commit(label ?? (plan.AtMiddle ? Lang.T("workshop.undo.opened", plan.Name) : Lang.T("workshop.undo.added", plan.Name)), null, [], plan.Adds);
        return (plan.Adds.Count, plan.Unknown, plan.Lift, plan.Terrain, plan.Stood);
    }

    /// <summary>Pieces players build: the hammer's, the cultivator's, the serving tray's (not the hoe's rocks).</summary>
    public static bool Buildable(string prefab) => PieceCatalog.Get(StableHash.Of(prefab))?.IsBuildPiece == true;

    /// <summary>
    /// The ground the support check counts at a world point: the plot's, or (higher) the ground a
    /// blueprint stood on in game at that metre (Add's terrain).
    /// </summary>
    public static float GroundAt(IReadOnlyDictionary<(int, int), float> terrain, float x, float z) =>
        terrain.TryGetValue(((int)MathF.Floor(x), (int)MathF.Floor(z)), out float y) ? MathF.Max(Ground, y) : Ground;

    /// <summary>Rounds to the millimetre (or thousandth) for the copy's numbers.</summary>
    private static float R(float v) => MathF.Round(v * 1000f) / 1000f;
}
