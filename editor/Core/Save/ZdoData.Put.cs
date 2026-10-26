
namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>Replaces or adds the value for the key in a section, or removes it when the value is null.</summary>
    private static void Put<T, TIn>(List<(int Key, T Value)> list, int key, TIn? value, Func<TIn, T> parse)
    {
        int i = list.FindIndex(e => e.Key == key);
        if (value == null)
        {
            if (i >= 0)
            {
                list.RemoveAt(i);
            }
            return;
        }
        T v = parse(value);
        if (i >= 0)
        {
            list[i] = (key, v);
        }
        else
        {
            list.Add((key, v));
        }
    }
}
