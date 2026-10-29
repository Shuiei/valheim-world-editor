using System.Globalization;
using System.Numerics;

namespace ValheimRAFT.WorldEditor.Tests;

/// <summary>
/// The second ship the real mod saved and built again (fixtures/valheimraft-swivels.txt): each object where it was
/// saved, with its data, and where the mod built it.
/// </summary>
internal static class SwivelShip
{
    /// <summary>An object: as saved (its values set on it), and where the mod built it.</summary>
    public sealed record Built(FakeObject Saved, Vector3 At, Quaternion Turn);

    /// <summary>The fixture, read.</summary>
    public static List<Built> Read()
    {
        var all = new List<Built>();
        var byTag = new Dictionary<string, FakeObject>();
        foreach (string[] t in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "valheimraft-swivels.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split(' ')))
        {
            float F(int i) => float.Parse(t[i], CultureInfo.InvariantCulture);
            if (t[0] == "O")
            {
                var o = new FakeObject(t[2], new Vector3(F(3), F(4), F(5)), new Quaternion(F(6), F(7), F(8), F(9)));
                byTag[t[1]] = o;
                all.Add(new Built(o, new Vector3(F(10), F(11), F(12)), new Quaternion(F(13), F(14), F(15), F(16))));
                continue;
            }
            var obj = byTag[t[1]];
            switch (t[0])
            {
                case "I":
                    obj.Ints[t[2]] = int.Parse(t[3], CultureInfo.InvariantCulture);
                    break;
                case "F":
                    obj.Floats[t[2]] = F(3);
                    break;
                case "V":
                    obj.Vectors[t[2]] = new Vector3(F(3), F(4), F(5));
                    break;
                default:
                    throw new InvalidDataException($"valheimraft-swivels.txt: unknown line kind '{t[0]}'.");
            }
        }
        return all;
    }

    /// <summary>The object holding an id (PersistentID).</summary>
    public static Built Holding(this List<Built> all, int id) => all.Single(b => b.Saved.TryGetInt(Keys.Id, out int held) && held == id);
}
