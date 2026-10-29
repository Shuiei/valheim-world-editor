using System.Globalization;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Writing a link into an object's saved values, and adding a link to a copy of an object's data.
/// </summary>
public static partial class ObjectLinks
{
    /// <summary>
    /// A link with values set over it (as a new object's data sets them over what it is made from): null when it ends
    /// up with none. Values that do not parse are left out.
    /// </summary>
    /// <param name="start">The link before (null: none).</param>
    /// <param name="data">The values set, if any.</param>
    public static ObjectLink? With(ObjectLink? start, IReadOnlyList<ObjectField>? data)
    {
        var set = Current;
        if (data == null || !data.Any(f => set.IsLinkKey(f.Key)))
        {
            return start;
        }
        var z = new ZdoData();
        if (start != null)
        {
            Write(start, z);
        }
        foreach (var f in data.Where(f => set.IsLinkKey(f.Key)))
        {
            try
            {
                z.Set(f.Section, f.Key, f.Value);
            }
            catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
            {
                // A damaged value counts as none.
                Log.CaughtBrief(e, $"reading the link value {f.Key} (damaged: counts as none)");
            }
        }
        return Read(set, z.IntList, z.Vec3List, z.QuatList);
    }

    /// <summary>Puts a link's values in an object's data.</summary>
    private static void Write(ObjectLink link, ZdoData z)
    {
        var rule = link.Rule;
        if (link.Id != 0)
        {
            z.IntList.Add((rule.IdKey, link.Id));
        }
        for (int k = 0; k < rule.ParentKeys.Length; k++)
        {
            if (link.Parents[k] != 0)
            {
                z.IntList.Add((rule.ParentKeys[k], link.Parents[k]));
            }
        }
        // Under the keys of the parent it hangs on (a carried part has none).
        var place = link.Placement;
        if (place.OffsetKey != 0)
        {
            z.Vec3List.Add((place.OffsetKey, link.Offset));
        }
        if (link.Euler is { } e && place.EulerKey != 0)
        {
            z.Vec3List.Add((place.EulerKey, e));
        }
        if (link.Quat is { } q && place.QuatKey != 0)
        {
            z.QuatList.Add((place.QuatKey, q));
        }
    }

    /// <summary>A vector as data text.</summary>
    private static string Vec(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:R} {v.Y:R} {v.Z:R}");

    /// <summary>An int value of data (0 when it has none or it does not parse).</summary>
    private static int IntIn(List<ObjectField> data, int key) =>
        data.Find(f => f.Key == key && f.Section == "ints") is { Value: { } v } && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    /// <summary>Sets a value of data (null: takes it away), in place of the one it had; nothing for key 0 (none).</summary>
    private static void Put(List<ObjectField> data, string section, int key, string? value)
    {
        if (key != 0)
        {
            ObjectField.Put(data, section, key, value);
        }
    }
}
