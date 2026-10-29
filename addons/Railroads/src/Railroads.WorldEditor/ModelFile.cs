using System.Numerics;
using System.Text;

namespace Railroads.WorldEditor;

/// <summary>
/// The mod's model files (meshes/&lt;model&gt;.bfm beside its plugin), which it builds its pieces' meshes from while the
/// game runs: none of them is in an asset bundle, so the editor cannot find them by itself. Little-endian: "BFM5"; the
/// part count; each part's material slot (a length and UTF-8 text: wood, stone or iron), its corners (position,
/// normal, texture place: eight floats each) and its triangles (indices into the part's own corners); then the boxes
/// the mod collides with (centre, size, forward, up), its snap points, the track's centre lines (corner lists) and the
/// wheels' axles.
/// </summary>
public static class ModelFile
{
    /// <summary>The first four bytes of a model file of the version this reads.</summary>
    public const string Magic = "BFM5";

    /// <summary>The most of anything a file holds (parts, corners, indices, boxes, points): more is taken for damage.</summary>
    private const int Most = 1 << 22;

    /// <summary>Reads a model file.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <exception cref="InvalidDataException">The bytes are not a model file this reads.</exception>
    public static RailModel Read(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes, writable: false), Encoding.UTF8);
        try
        {
            if (Encoding.ASCII.GetString(Bytes(reader, 4)) != Magic)
            {
                throw new InvalidDataException("not a model file of version " + Magic);
            }
            var parts = new List<RailModel.Part>();
            for (int p = Count(reader, "parts"); p > 0; p--)
            {
                string slot = Encoding.UTF8.GetString(Bytes(reader, Count(reader, "slot name's bytes")));
                var positions = new Vector3[Count(reader, "corners")];
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = Vector(reader);
                    Bytes(reader, 5 * sizeof(float));
                }
                int[] triangles = new int[Count(reader, "indices")];
                if (triangles.Length % 3 != 0)
                {
                    throw new InvalidDataException($"part {slot} has {triangles.Length} indices, not whole triangles");
                }
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles[i] = reader.ReadInt32();
                    if (triangles[i] < 0 || triangles[i] >= positions.Length)
                    {
                        throw new InvalidDataException($"part {slot} names corner {triangles[i]} of {positions.Length}");
                    }
                }
                parts.Add(new RailModel.Part(slot, positions, triangles));
            }
            var boxes = new RailModel.Box[Count(reader, "boxes")];
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i] = new RailModel.Box(Vector(reader), Vector(reader));
                // Its forward and up: the mod's boxes the add-on uses stand straight.
                Bytes(reader, 6 * sizeof(float));
            }
            // The snap points: not drawn.
            Bytes(reader, Count(reader, "snap points") * 3 * sizeof(float));
            var lines = new List<Vector3[]>();
            for (int l = Count(reader, "lines"); l > 0; l--)
            {
                lines.Add(Vectors(reader, Count(reader, "line points")));
            }
            var axles = Vectors(reader, Count(reader, "axles"));
            if (reader.BaseStream.Position != bytes.Length)
            {
                throw new InvalidDataException($"{bytes.Length - reader.BaseStream.Position} bytes past its end");
            }
            return new RailModel(parts, boxes, lines, axles);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("it ends early", e);
        }
    }

    /// <summary>A count, checked: never below 0 or above <see cref="Most"/>.</summary>
    private static int Count(BinaryReader reader, string what)
    {
        int n = reader.ReadInt32();
        return n is >= 0 and <= Most ? n : throw new InvalidDataException($"{n} {what}");
    }

    /// <summary>The next bytes, all of them (a file ending before is damaged).</summary>
    private static byte[] Bytes(BinaryReader reader, int n)
    {
        byte[] b = reader.ReadBytes(n);
        return b.Length == n ? b : throw new EndOfStreamException();
    }

    /// <summary>A position: three floats.</summary>
    private static Vector3 Vector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    /// <summary>A list of positions.</summary>
    private static Vector3[] Vectors(BinaryReader reader, int n)
    {
        var v = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            v[i] = Vector(reader);
        }
        return v;
    }
}

/// <summary>A model of the mod, as its file holds it (<see cref="ModelFile"/>), in the frame of the piece it builds.</summary>
/// <param name="Parts">Its parts, one per material slot.</param>
/// <param name="Boxes">The boxes the mod collides with (a vehicle's include where its couplers are).</param>
/// <param name="Lines">The centre lines of its track, each a list of points in order (none for what is no track).</param>
/// <param name="Axles">Where its wheels' axles are (none for what has no wheels).</param>
public sealed record RailModel(IReadOnlyList<RailModel.Part> Parts, IReadOnlyList<RailModel.Box> Boxes, IReadOnlyList<Vector3[]> Lines,
    IReadOnlyList<Vector3> Axles)
{
    /// <summary>One part: what it is made of, its corners and its triangles (Unity's winding).</summary>
    /// <param name="Slot">The material it is drawn with: wood, stone or iron.</param>
    /// <param name="Positions">Its corners' positions.</param>
    /// <param name="Triangles">Three indices into <paramref name="Positions"/> per triangle.</param>
    public sealed record Part(string Slot, Vector3[] Positions, int[] Triangles);

    /// <summary>One box the mod collides with, in the piece's frame.</summary>
    /// <param name="Center">Its centre.</param>
    /// <param name="Size">Its size along each axis.</param>
    public sealed record Box(Vector3 Center, Vector3 Size);
}
