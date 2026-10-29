using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// The script's Objects.Place and the number and area checks it shares with Move.
public static partial class ScriptHost
{
    /// <summary>
    /// A script's Objects.Place: the new object noted (y NaN: on the ground as the script leaves it; scale 0: the
    /// prefab's own size).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A prefab the editor cannot make, a place or turn that is not a number (NaN, infinite), a place outside the
    /// area, or a scale below 0.
    /// </exception>
    internal static void Place(Snapshot snap, Changes ch, string name, float x, float z, float yaw, float scale, float y, float pitch, float roll, bool snapIt)
    {
        int prefab = StableHash.Of(name);
        if (!snap.CanCreate(prefab))
        {
            throw new ArgumentException($"the editor cannot make a “{name}” (a prefab name, like Beech1 or rock4_forest)");
        }
        Number("x", x);
        Number("z", z);
        InArea(snap, x, z);
        if (!float.IsNaN(y))
        {
            Number("y", y);
        }
        Number("yaw", yaw);
        Number("pitch", pitch);
        Number("roll", roll);
        if (!float.IsFinite(scale) || scale < 0)
        {
            throw new ArgumentException($"a scale of {scale}: give a size above 0 (1 or 0: the prefab's own)");
        }
        ch.Add.Add((prefab, name, new Vector3(x, y, z), new Vector3(pitch, yaw, roll), scale, snapIt));
    }

    /// <summary>Throws unless a script's value is a number: not NaN, not infinite.</summary>
    /// <param name="what">The argument's name, as the script gives it.</param>
    /// <param name="v">Its value.</param>
    /// <exception cref="ArgumentException">It is NaN or infinite.</exception>
    private static void Number(string what, float v)
    {
        if (!float.IsFinite(v))
        {
            throw new ArgumentException($"{what} is {v}: give a number (not NaN or infinite)");
        }
    }

    /// <summary>
    /// Throws unless world point (x, z) is within the area: its first to its last grid point (Area.MinX to MaxX, MinZ
    /// to MaxZ), the edge included, so every point the scripts' Area.Inside takes (the edge left out) is in. A
    /// coordinate that is not a number is not.
    /// </summary>
    /// <param name="snap">The area.</param>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="what">The point as the error names it (null: its coordinates).</param>
    /// <exception cref="ArgumentException">It is outside the area.</exception>
    private static void InArea(Snapshot snap, float x, float z, string? what = null)
    {
        float maxX = snap.Ox + snap.W - 1, maxZ = snap.Oz + snap.H - 1;
        if (!(x >= snap.Ox && x <= maxX && z >= snap.Oz && z <= maxZ))
        {
            throw new ArgumentException($"{what ?? $"({x}, {z})"} is outside the area (x {snap.Ox} to {maxX}, z {snap.Oz} to {maxZ}): keep to Area.Inside");
        }
    }

    /// <summary>
    /// The whole-metre offsets from c, within reach of it, that can land within min..max: a short range of integers to
    /// count, however large reach is, and never beyond 2^24 m (where whole metres stop counting). Empty (From above
    /// To) when reach is not a size or none of them lands within min..max.
    /// </summary>
    /// <param name="c">The middle, on one axis.</param>
    /// <param name="reach">How far from it to go.</param>
    /// <param name="min">The lowest place that counts.</param>
    /// <param name="max">The highest place that counts.</param>
    internal static (int From, int To) Offsets(float c, float reach, float min, float max)
    {
        if (!(reach >= 0) || !float.IsFinite(c))
        {
            return (0, -1);
        }
        // Offsets past 2^24 m are left out: those that can still be counted are kept.
        double from = Math.Max(Math.Max(-Math.Ceiling(reach), Math.Floor(min - (double)c)), -(1 << 24));
        double to = Math.Min(Math.Min(Math.Floor(reach), Math.Ceiling(max - (double)c)), 1 << 24);
        return from <= to ? ((int)from, (int)to) : (0, -1);
    }
}
