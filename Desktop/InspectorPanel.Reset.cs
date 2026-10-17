using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>Back to the object's data as opened: its values, contents and stand, nothing changed.</summary>
    private void Reset()
    {
        var z = _z!;
        FieldList = FieldsOf(z);
        Added.Clear();
        LoadContents(z);
        LoadStand(z);
        _standAtOpen = StandKey();
        _itemsAtOpen = ItemsKey();
        _slot = null;
        _moving = false;
        Render();
    }

    /// <summary>
    /// The object's values as the panel lists them, by kind, each with its name and its text.
    /// </summary>
    /// <param name="z">The object's data.</param>
    /// <returns>The fields, in the order the kinds are listed.</returns>
    internal static List<Field> FieldsOf(ZdoData z)
    {
        var list = new List<Field>();
        void Add(string section, int key, string value, string? note = null) => list.Add(new Field { Section = section, Key = key, Name = ZdoKeys.NameOf(key), Value = value, Now = value, Note = note });
        foreach (var (k, v) in z.FloatList)
        {
            Add("floats", k, S(v));
        }

        foreach (var (k, v) in z.Vec3List)
        {
            Add("vec3", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)}");
        }

        foreach (var (k, v) in z.QuatList)
        {
            Add("quats", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)} {S(v.W)}");
        }
        // Ints that are prefab or item hashes (item stands, cooking stations…) get the name beside them.
        foreach (var (k, v) in z.IntList)
        {
            Add("ints", k, v.ToString(CultureInfo.InvariantCulture), PrefabCatalog.NameOf(v));
        }

        foreach (var (k, v) in z.LongList)
        {
            Add("longs", k, v.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var (k, v) in z.StringList)
        {
            Add("strings", k, v);
        }

        foreach (var (k, v) in z.ByteList)
        {
            Add("bytes", k, Convert.ToBase64String(v), Lang.T("inspect.bytes", v.Length));
        }

        return list;
    }
}
