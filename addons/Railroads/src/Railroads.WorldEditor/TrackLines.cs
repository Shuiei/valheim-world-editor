using System.Globalization;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// Lines of track as a vehicle keeps them (<see cref="RailroadsMod.Keys.Line"/>, <see cref="RailroadsMod.Keys.Trail"/>,
/// <see cref="RailroadsMod.Keys.Route"/>, <see cref="RailroadsMod.Keys.After"/>): the mod names a line by the world
/// places of its two ends, six floats (little-endian) a line. Shown in the inspector.
/// </summary>
public sealed class TrackLines : IDataDecoder
{
    /// <summary>The bytes of one line.</summary>
    public const int LineBytes = 6 * sizeof(float);

    /// <summary>How many lines the inspector names before it counts the rest.</summary>
    private const int Named = 3;

    /// <summary>The lines in a value, each its two ends; null when the bytes are not whole lines.</summary>
    public static List<(Vector3 A, Vector3 B)>? Read(byte[] bytes)
    {
        if (bytes.Length % LineBytes != 0)
        {
            return null;
        }
        var lines = new List<(Vector3, Vector3)>(bytes.Length / LineBytes);
        for (int i = 0; i < bytes.Length; i += LineBytes)
        {
            lines.Add((At(bytes, i), At(bytes, i + 12)));
        }
        return lines;
    }

    /// <summary>Lines as the mod keeps them.</summary>
    public static byte[] Write(IReadOnlyList<(Vector3 A, Vector3 B)> lines)
    {
        byte[] bytes = new byte[lines.Count * LineBytes];
        for (int k = 0; k < lines.Count; k++)
        {
            Put(bytes, k * LineBytes, lines[k].A);
            Put(bytes, (k * LineBytes) + 12, lines[k].B);
        }
        return bytes;
    }

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value) => Read(value) switch
    {
        null => null,
        { Count: 0 } => "No track line.",
        var lines => Text(lines),
    };

    /// <summary>The lines in words: how many, the first few by their ends.</summary>
    public static string Text(IReadOnlyList<(Vector3 A, Vector3 B)> lines) =>
        string.Format(CultureInfo.InvariantCulture, "{0} track line(s): {1}{2}", lines.Count,
            string.Join("; ", lines.Take(Named).Select(l => Place(l.A) + " to " + Place(l.B))),
            lines.Count > Named ? string.Format(CultureInfo.InvariantCulture, "; and {0} more", lines.Count - Named) : "");

    /// <summary>A world place in words, to the centimetre.</summary>
    public static string Place(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", p.X, p.Y, p.Z);

    /// <summary>Three floats at an offset.</summary>
    internal static Vector3 At(byte[] bytes, int offset) =>
        new(BitConverter.ToSingle(bytes, offset), BitConverter.ToSingle(bytes, offset + 4), BitConverter.ToSingle(bytes, offset + 8));

    /// <summary>Writes three floats at an offset.</summary>
    private static void Put(byte[] bytes, int offset, Vector3 v)
    {
        Array.Copy(BitConverter.GetBytes(v.X), 0, bytes, offset, 4);
        Array.Copy(BitConverter.GetBytes(v.Y), 0, bytes, offset + 4, 4);
        Array.Copy(BitConverter.GetBytes(v.Z), 0, bytes, offset + 8, 4);
    }
}
