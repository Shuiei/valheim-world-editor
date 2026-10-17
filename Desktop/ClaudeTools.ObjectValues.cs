using System.Globalization;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// The values an object holds in the save, as (section, key hash, value as text): from its saved bytes in a world,
    /// from its values on the Workshop's plot (which has no world files behind it); null when it cannot be read.
    /// </summary>
    private List<(string Section, int Key, string Value)>? Values(EditSession s, WorldScene.Thing t)
    {
        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        if (_w.InWorkshop || s.Scene.World is not { } world)
        {
            return [.. (s.Edits.FindAdded(t.Id)?.Data ?? []).Where(f => f.Value != null).Select(f => (f.Section, f.Key, f.Value!))];
        }
        byte[]? bytes;
        try
        {
            bytes = ObjectData.Bytes(world, s.Edits, t.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading an object's data for Claude (none is given)");
            bytes = null;
        }
        if (bytes == null)
        {
            return null;
        }
        var z = Save.ZdoData.Parse(bytes);
        return
        [
            .. z.FloatList.Select(v => ("floats", v.Key, F(v.Value))),
            .. z.Vec3List.Select(v => ("vec3", v.Key, $"{F(v.Value.X)} {F(v.Value.Y)} {F(v.Value.Z)}")),
            .. z.QuatList.Select(v => ("quats", v.Key, $"{F(v.Value.X)} {F(v.Value.Y)} {F(v.Value.Z)} {F(v.Value.W)}")),
            .. z.IntList.Select(v => ("ints", v.Key, v.Value.ToString(CultureInfo.InvariantCulture))),
            .. z.LongList.Select(v => ("longs", v.Key, v.Value.ToString(CultureInfo.InvariantCulture))),
            .. z.StringList.Select(v => ("strings", v.Key, v.Value)),
            .. z.ByteList.Select(v => ("bytes", v.Key, Convert.ToBase64String(v.Value))),
        ];
    }

    /// <summary>An object's values as get_object gives them: the byte values by size (contents are given as items), the others by value, with what a number names.</summary>
    /// <param name="values">The values, as <see cref="Values"/> reads them.</param>
    internal static List<object> DataOf(List<(string Section, int Key, string Value)> values)
    {
        var data = new List<object>();
        foreach (var (section, key, value) in values)
        {
            string? name = Terrain.ZdoKeys.NameOf(key);
            if (section == "bytes")
            {
                // Contents are given as items below; other byte values only by size.
                if (key != ObjectData.ItemsKey)
                {
                    data.Add(new { section, key = name ?? key.ToString(CultureInfo.InvariantCulture), bytes = Convert.FromBase64String(value).Length });
                }
                continue;
            }
            string? note = section == "ints" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ? Terrain.PrefabCatalog.NameOf(h) : null;
            data.Add(new { section, key = name ?? key.ToString(CultureInfo.InvariantCulture), value = value.Length > LongestText ? value[..LongestText] + "…" : value, note });
        }
        return data;
    }
}