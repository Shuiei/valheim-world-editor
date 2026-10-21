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

    /// <summary>The spots as new objects, in one undo step.</summary>
    public static void Commit(EditSession session, string label, IEnumerable<Regrow.Spot> spots) =>
        session.Commit(label, null, [], spots.Select(o => (new NewObject(0, StableHash.Of(o.Name), new Vector3(o.X, o.Y, o.Z), new Vector3(o.Rx, o.Ry, o.Rz), MathF.Abs(o.Scale - 1) < 1e-4f ? 0 : o.Scale, Data: DataOf(o)), false)).ToList());

    /// <summary>A spot's data: its scale by axis, then what Expand World Data gives it; null for none.</summary>
    internal static IReadOnlyList<ObjectField>? DataOf(Regrow.Spot o)
    {
        List<ObjectField> fields = [.. AxesData(o) ?? [], .. (o.Data ?? []).Select(f => new ObjectField(f.Section, f.Key, f.Value))];
        return fields.Count > 0 ? fields : null;
    }

    /// <summary>
    /// The scale on each axis of a spot whose rule scales by axis (Expand World Data), as the object's "scale" data;
    /// null for a uniform scale (the object's scale then says it).
    /// </summary>
    internal static IReadOnlyList<ObjectField>? AxesData(Regrow.Spot o)
    {
        if (o.Axes == null)
        {
            return null;
        }
        var s = o.Scale3;
        return [new ObjectField("vec3", ZdoData.ScaleKey, FormattableString.Invariant($"{s.X},{s.Y},{s.Z}"))];
    }
}
