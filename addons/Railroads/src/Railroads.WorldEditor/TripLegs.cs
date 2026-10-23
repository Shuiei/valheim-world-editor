using System.Globalization;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// The legs of a trip a sent train still has after the one under way (<see cref="RailroadsMod.Keys.Legs"/>), as the
/// game's packages write them (little-endian): their count, then each leg's way (int: 1 forward, -1 backward), the
/// distance along it where it stops (float), its lines and the lines past the stop its wagons need (each a count and
/// two places a line, see <see cref="TrackLines"/>). Shown in the inspector.
/// </summary>
public sealed class TripLegs : IDataDecoder
{
    /// <summary>The most legs or lines a value holds: more is taken for damage.</summary>
    private const int Most = 1 << 20;

    /// <summary>The legs in a value; null when the bytes are not legs.</summary>
    public static List<Leg>? Read(byte[] bytes)
    {
        int at = 0;
        if (!Int(bytes, ref at, out int count) || count < 0 || count > Most)
        {
            return null;
        }
        var legs = new List<Leg>();
        for (int k = 0; k < count; k++)
        {
            if (!Int(bytes, ref at, out int way) || !Float(bytes, ref at, out float stop) || Lines(bytes, ref at) is not { } lines
                || Lines(bytes, ref at) is not { } after)
            {
                return null;
            }
            legs.Add(new Leg(way, stop, lines, after));
        }
        return at == bytes.Length ? legs : null;
    }

    /// <summary>Legs as the mod keeps them.</summary>
    public static byte[] Write(IReadOnlyList<Leg> legs)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write(legs.Count);
        foreach (var leg in legs)
        {
            w.Write(leg.Way);
            w.Write(leg.Stop);
            w.Write(leg.Lines.Count);
            w.Write(TrackLines.Write(leg.Lines));
            w.Write(leg.After.Count);
            w.Write(TrackLines.Write(leg.After));
        }
        w.Flush();
        return stream.ToArray();
    }

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value) => Read(value) switch
    {
        null => null,
        { Count: 0 } => "No further leg.",
        var legs => string.Format(CultureInfo.InvariantCulture, "{0} more leg(s): {1}", legs.Count, string.Join("; ", legs.Select(l =>
            string.Format(CultureInfo.InvariantCulture, "{0} on {1} line(s), stopping {2:0.#} m along", l.Way < 0 ? "backward" : "forward", l.Lines.Count, l.Stop)))),
    };

    /// <summary>A count of lines and the lines; null when the bytes end first or the count is not one.</summary>
    private static List<(Vector3 A, Vector3 B)>? Lines(byte[] bytes, ref int at)
    {
        if (!Int(bytes, ref at, out int n) || n < 0 || n > Most || (long)n * TrackLines.LineBytes > bytes.Length - at)
        {
            return null;
        }
        var lines = new List<(Vector3, Vector3)>(n);
        for (int i = 0; i < n; i++, at += TrackLines.LineBytes)
        {
            lines.Add((TrackLines.At(bytes, at), TrackLines.At(bytes, at + 12)));
        }
        return lines;
    }

    /// <summary>The next four bytes as an int; false when the bytes end first.</summary>
    private static bool Int(byte[] bytes, ref int at, out int value)
    {
        value = 0;
        if (bytes.Length - at < 4)
        {
            return false;
        }
        value = BitConverter.ToInt32(bytes, at);
        at += 4;
        return true;
    }

    /// <summary>The next four bytes as a float; false when the bytes end first.</summary>
    private static bool Float(byte[] bytes, ref int at, out float value)
    {
        value = 0;
        if (bytes.Length - at < 4)
        {
            return false;
        }
        value = BitConverter.ToSingle(bytes, at);
        at += 4;
        return true;
    }

    /// <summary>One leg of a trip.</summary>
    /// <param name="Way">1 forward, -1 backward.</param>
    /// <param name="Stop">How far along the leg's lines the train stops, in metres.</param>
    /// <param name="Lines">The lines it runs on.</param>
    /// <param name="After">The lines past the stop its wagons need.</param>
    public sealed record Leg(int Way, float Stop, IReadOnlyList<(Vector3 A, Vector3 B)> Lines, IReadOnlyList<(Vector3 A, Vector3 B)> After);
}
