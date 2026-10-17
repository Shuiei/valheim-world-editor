using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class Growth
{
    /// <summary>
    /// The farthest something standing can stop a spot from: a building within 4 m.
    /// </summary>
    private const float Farthest = 4;

    /// <summary>
    /// The cell of a coordinate for the neighbour tests: cells a metre wider than <see cref="Farthest"/>, so whatever
    /// is close enough is in the same cell or the next one.
    /// </summary>
    private static int Cell(float v) => (int)MathF.Floor(v / (Farthest + 1));

    /// <summary>The spots as new objects, in one undo step.</summary>
    public static void Commit(EditSession session, string label, IEnumerable<Regrow.Spot> spots) =>
        session.Commit(label, null, [], spots.Select(o => (new NewObject(0, StableHash.Of(o.Name), new Vector3(o.X, o.Y, o.Z), new Vector3(o.Rx, o.Ry, o.Rz), MathF.Abs(o.Scale - 1) < 1e-4f ? 0 : o.Scale), false)).ToList());
}
