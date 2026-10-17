using System.Numerics;

namespace TerrainEditor.Desktop;

// The script's Objects.Move: the thing's new place, turn and size noted.
public static partial class ScriptHost
{
    /// <summary>
    /// A script's Objects.Move: the thing's new place, rotation and scale noted (NaN keeps a value: as it was, or as an
    /// earlier Move of the script left it).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No such thing in the area, a place or turn given that is infinite, a place outside the area, or a scale not
    /// above 0.
    /// </exception>
    internal static void Move(Snapshot snap, Changes ch, int index, float x, float z, float y, float yaw, float pitch, float roll, float scale)
    {
        if (!snap.Things.Exists(t => t.Index == index))
        {
            throw new ArgumentException($"no object {index} in the area (Objects.All gives them)");
        }
        foreach (var (what, v) in new[] { ("x", x), ("z", z), ("y", y), ("yaw", yaw), ("pitch", pitch), ("roll", roll) })
        {
            if (!float.IsNaN(v))
            {
                Number(what, v);
            }
        }
        if (!float.IsNaN(scale) && (!float.IsFinite(scale) || scale <= 0))
        {
            throw new ArgumentException($"a scale of {scale}: give a size above 0 (1: the prefab's own)");
        }
        var t = snap.Things.Find(t => t.Index == index);
        var (p, r, sc) = ch.Moves.TryGetValue(index, out var m) ? m : (t.Position, t.Rotation, t.Scale);
        static float Or(float v, float was) => float.IsNaN(v) ? was : v;
        if (!float.IsNaN(x) || !float.IsNaN(z))
        {
            InArea(snap, Or(x, p.X), Or(z, p.Z));
        }
        ch.Moves[index] = (new Vector3(Or(x, p.X), Or(y, p.Y), Or(z, p.Z)), new Vector3(Or(pitch, r.X), Or(yaw, r.Y), Or(roll, r.Z)), Or(scale, sc));
    }
}
