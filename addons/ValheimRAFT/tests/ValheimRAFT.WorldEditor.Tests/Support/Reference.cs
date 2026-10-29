using System.Globalization;
using System.Numerics;

namespace ValheimRAFT.WorldEditor.Tests;

/// <summary>
/// The ship the real mod saved and built again (fixtures/valheimraft-reference.txt): its pieces, with the values the mod
/// saved on them, and the tie points of its rope anchors.
/// </summary>
internal sealed class Reference
{
    /// <summary>A piece of the ship: its prefab, its id (0: none) and the values the mod saved on it.</summary>
    public sealed record Piece(string Prefab, int Id, FakeObject Values);

    /// <summary>The pieces, in the fixture's order, values set on them where the fixture names their prefab and id.</summary>
    public List<Piece> Pieces { get; } = [];

    /// <summary>The rope anchors' tie points, by prefab.</summary>
    public Dictionary<string, Vector3> Ties { get; } = [];

    /// <summary>The fixture, read.</summary>
    public static Reference Read()
    {
        var r = new Reference();
        foreach (string[] t in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "valheimraft-reference.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split(' ')))
        {
            switch (t[0])
            {
                case "T":
                    r.Ties[t[1]] = Vec(t, 2);
                    break;
                case "P":
                    r.Pieces.Add(new Piece(t[1], Int(t[4]), new FakeObject(t[1])));
                    break;
                case "B":
                    r.Holding(t[1], Int(t[2])).Bytes[t[3]] = Convert.FromHexString(t[4]);
                    break;
                case "I":
                    r.Holding(t[1], Int(t[2])).Ints[t[3]] = Int(t[4]);
                    break;
                case "V":
                    r.Holding(t[1], Int(t[2])).Vectors[t[3]] = Vec(t, 4);
                    break;
                case "R":
                    // The vehicle's root: these checks place pieces from their own values.
                    break;
                default:
                    throw new InvalidDataException($"valheimraft-reference.txt: unknown line kind '{t[0]}'.");
            }
        }
        return r;
    }

    /// <summary>The values of the piece of a prefab holding that id (0: none; the fixture has one such piece of each).</summary>
    private FakeObject Holding(string prefab, int id) => Pieces.Single(p => p.Prefab == prefab && p.Id == id).Values;

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static Vector3 Vec(string[] t, int at) =>
        new(float.Parse(t[at], CultureInfo.InvariantCulture), float.Parse(t[at + 1], CultureInfo.InvariantCulture), float.Parse(t[at + 2], CultureInfo.InvariantCulture));
}
