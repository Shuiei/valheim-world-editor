using System.Numerics;

namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>
    /// Sets a value (replacing one with the same key in the same section), or removes it (value null).
    /// </summary>
    /// <param name="section">floats, vec3, quats, ints, longs, strings, bytes.</param>
    /// <param name="key">The data key hash.</param>
    /// <param name="value">
    /// Values come as text from the editor: numbers, "x y z" (vec3), "x y z w" (quats), base64 (bytes). Null removes
    /// the value.
    /// </param>
    /// <exception cref="ArgumentException">An unknown section.</exception>
    /// <exception cref="FormatException">The value does not parse for that section, or a number in it is not finite.</exception>
    public void Set(string section, int key, string? value)
    {
        // A number the game can use: NaN or an infinity (typed, or a number too large) would break the object in it.
        static float F(string s)
        {
            float v = float.Parse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            return float.IsFinite(v) ? v : throw new FormatException($"{s} is not a finite number");
        }
        static float[] Fs(string s, int n)
        {
            float[] v = Numbers(s, n) ?? throw new FormatException($"{n} numbers expected");
            return v.All(float.IsFinite) ? v : throw new FormatException($"{s} is not a finite number");
        }
        switch (section)
        {
            case "floats": Put(FloatList, key, value, F); break;
            case "vec3": Put(Vec3List, key, value, s => { float[] v = Fs(s, 3); return new Vector3(v[0], v[1], v[2]); }); break;
            case "quats": Put(QuatList, key, value, s => { float[] v = Fs(s, 4); return new Quaternion(v[0], v[1], v[2], v[3]); }); break;
            case "ints": Put(IntList, key, value, s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture)); break;
            case "longs": Put(LongList, key, value, s => long.Parse(s, System.Globalization.CultureInfo.InvariantCulture)); break;
            case "strings": Put(StringList, key, value, s => s); break;
            case "bytes": Put(ByteList, key, value, Convert.FromBase64String); break;
            default: throw new ArgumentException("unknown section " + section);
        }
    }
}
