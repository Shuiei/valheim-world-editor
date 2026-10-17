using System.Globalization;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// One object of a pasted copy as extensions rewrite it (ICopy): its values as data text (ObjectField, as the
/// clipboard keeps them), read and changed in place. A value that does not parse counts as none.
/// </summary>
/// <param name="prefab">Its prefab's name ("" when not known).</param>
/// <param name="position">Where it is pasted.</param>
/// <param name="rotation">Its turn, Unity Euler degrees.</param>
/// <param name="data">Its values; changed in place (a value taken away is kept with a null value, as copies do).</param>
/// <param name="scale">Its size (0 or 1: the prefab's own).</param>
public sealed partial class CopyView(string prefab, Vector3 position, Vector3 rotation, List<ObjectField> data, float scale = 1) : PlacedView(prefab, position, rotation, scale), ICopy
{

    /// <summary>Its values, as changed.</summary>
    public List<ObjectField> Data => data;

    /// <summary>Whether it holds a value of a key (by hash).</summary>
    public bool Holds(int key) => data.Exists(f => f.Key == key && f.Value != null);

    /// <summary>A value's text; null when it has none.</summary>
    private string? Text(string section, string key)
    {
        int h = StableHash.Of(key);
        return data.Find(f => f.Key == h && f.Section == section)?.Value;
    }

    /// <summary>Sets a value's text (null: takes it away).</summary>
    private void Put(string section, string key, string? value) => ObjectField.Put(data, section, StableHash.Of(key), value);

    /// <summary>Numbers of a value's text, exactly <paramref name="n"/> of them (as the save reads them); null otherwise.</summary>
    private float[]? Floats(string section, string key, int n) => ZdoData.Numbers(Text(section, key), n);

    /// <summary>Numbers as a value's text.</summary>
    private static string Join(params float[] v) => string.Join(' ', v.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
}
