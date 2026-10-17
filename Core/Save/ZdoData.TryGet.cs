
namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>The first value of a key in a section's list of values; false when the list has none.</summary>
    public static bool TryGet<T>(IReadOnlyList<(int Key, T Value)> list, int key, out T value)
    {
        foreach (var (k, v) in list)
        {
            if (k == key)
            {
                value = v;
                return true;
            }
        }
        value = default!;
        return false;
    }
}
