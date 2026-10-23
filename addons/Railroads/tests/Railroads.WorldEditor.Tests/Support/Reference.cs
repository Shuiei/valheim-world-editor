using System.Globalization;
using System.Numerics;
using System.Text;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The objects Railroads made on a local server running it (fixtures/railroads-reference.txt): each with its prefab,
/// the game's object id, its place and turn and every value of its data by type, as the game kept them.
/// </summary>
internal static class Reference
{
    /// <summary>The recorded objects, in the file's order.</summary>
    public static IReadOnlyList<Recorded> Objects { get; } = Read(Path.Combine(AppContext.BaseDirectory, "fixtures", "railroads-reference.txt"));

    /// <summary>
    /// The objects of a second record (fixtures/railroads-supports.txt): the same railway, then track and an old station
    /// built by a player's placement, with their prices and the support poles the mod built under them.
    /// </summary>
    public static IReadOnlyList<Recorded> Supports { get; } = Read(Path.Combine(AppContext.BaseDirectory, "fixtures", "railroads-supports.txt"));

    /// <summary>The recorded objects of a prefab.</summary>
    public static IEnumerable<Recorded> Of(string prefab) => Objects.Where(o => o.Object.Prefab == prefab);

    private static List<Recorded> Read(string path)
    {
        var objects = new List<Recorded>();
        FakeObject? current = null;
        (long, uint) id = default;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            string[] p = line.Split(' ');
            switch (p[0])
            {
                case "object":
                    string[] uid = p[2].Split(':');
                    id = (long.Parse(uid[0], CultureInfo.InvariantCulture), uint.Parse(uid[1], CultureInfo.InvariantCulture));
                    current = new FakeObject(p[1]);
                    break;
                case "pos":
                    current!.Position = new Vector3(F(p[1]), F(p[2]), F(p[3]));
                    break;
                case "rot":
                    current!.Orientation = new Quaternion(F(p[1]), F(p[2]), F(p[3]), F(p[4]));
                    break;
                case "int":
                    current!.Ints[p[1]] = int.Parse(p[2], CultureInfo.InvariantCulture);
                    break;
                case "long":
                    current!.Longs[p[1]] = long.Parse(p[2], CultureInfo.InvariantCulture);
                    break;
                case "float":
                    current!.Floats[p[1]] = F(p[2]);
                    break;
                case "vec3":
                    current!.Vectors[p[1]] = new Vector3(F(p[2]), F(p[3]), F(p[4]));
                    break;
                case "quat":
                    current!.Quaternions[p[1]] = new Quaternion(F(p[2]), F(p[3]), F(p[4]), F(p[5]));
                    break;
                case "string":
                    current!.Strings[p[1]] = Encoding.UTF8.GetString(Convert.FromBase64String(p.Length > 2 ? p[2] : ""));
                    break;
                case "bytes":
                    current!.Bytes[p[1]] = Convert.FromBase64String(p.Length > 2 ? p[2] : "");
                    break;
                case "end":
                    objects.Add(new Recorded(current!, id));
                    current = null;
                    break;
                default:
                    throw new InvalidDataException("unknown line in the reference: " + line);
            }
        }
        return objects;
    }

    private static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}

/// <summary>One recorded object and the game's id of it.</summary>
/// <param name="Object">The object, its data as recorded.</param>
/// <param name="Id">The game's object id: its user and its number.</param>
internal sealed record Recorded(FakeObject Object, (long User, uint Id) Id);
