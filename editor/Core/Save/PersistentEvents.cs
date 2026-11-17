using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Save;

/// <summary>An active persistent event, as the world's save holds it.</summary>
/// <param name="Source">Which event: its place in the game's list of events.</param>
/// <param name="Id">Its own id, counted up by the game.</param>
/// <param name="Position">Its middle.</param>
/// <param name="Radius">Its radius, in metres.</param>
/// <param name="StartTime">The game's time when it started, in seconds (0 for one that does not end).</param>
/// <param name="Duration">How long it lasts, in seconds (-1: until it is stopped).</param>
public sealed record ActiveEvent(int Source, int Id, Vector3 Position, float Radius, double StartTime, double Duration);

/// <summary>
/// The active persistent events at the end of _main.&lt;n&gt;.db2: after the random events' state (a timer, the running
/// event's name, its time and its place), a length and brotli-compressed JSON, {"list":[{"sourceEventId":0,"eventId":3,
/// "position":{"x":1,"y":2,"z":3},"radius":150,"startTime":0,"duration":-1}]} (read from worlds the game saved).
/// </summary>
public static class PersistentEvents
{
    /// <summary>The largest list read, uncompressed.</summary>
    private const int MaxJson = 16 << 20;

    /// <summary>
    /// The events of a .db2 file's tail, and the bytes before their block; null when the tail is not laid out as
    /// above (an older game's save, which has no events) or its list cannot be read (logged).
    /// </summary>
    /// <param name="tail">The file's bytes after the ZoneSystem package.</param>
    public static (byte[] Head, List<ActiveEvent> Events)? Read(byte[] tail)
    {
        try
        {
            using BinaryReader r = new(new MemoryStream(tail));
            r.ReadSingle();
            r.ReadCheckedString();
            r.ReadSingle();
            r.ReadSingle(); r.ReadSingle(); r.ReadSingle();
            int head = (int)r.BaseStream.Position;
            if (r.Remaining() < 4)
            {
                return null;
            }
            int length = r.ReadInt32();
            if (length <= 0 || length != r.Remaining())
            {
                return null;
            }
            byte[] json = CheckedReads.Unbrotli(r.ReadExactBytes(length), MaxJson);
            return (tail[..head], Parse(json));
        }
        catch (Exception ex) when (CheckedReads.Damaged(ex) || ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Log.Caught(ex, "reading the world's persistent events (they cannot be edited)");
            return null;
        }
    }

    /// <summary>A .db2 file's tail with these events: the bytes before their block, then the block.</summary>
    /// <param name="head">The tail's bytes before the block, as <see cref="Read"/> gave them.</param>
    /// <param name="events">The events.</param>
    public static byte[] Write(byte[] head, IReadOnlyList<ActiveEvent> events)
    {
        using MemoryStream packed = new();
        using (BrotliStream b = new(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            b.Write(Json(events));
        }
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(head);
            w.Write((int)packed.Length);
            w.Write(packed.ToArray());
        }
        return ms.ToArray();
    }

    private static List<ActiveEvent> Parse(byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        List<ActiveEvent> events = [];
        foreach (JsonElement e in doc.RootElement.GetProperty("list").EnumerateArray())
        {
            JsonElement p = e.GetProperty("position");
            events.Add(new ActiveEvent(e.GetProperty("sourceEventId").GetInt32(), e.GetProperty("eventId").GetInt32(),
                new Vector3(p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle(), p.GetProperty("z").GetSingle()),
                e.GetProperty("radius").GetSingle(), e.GetProperty("startTime").GetDouble(), e.GetProperty("duration").GetDouble()));
        }
        return events;
    }

    private static byte[] Json(IReadOnlyList<ActiveEvent> events)
    {
        using MemoryStream ms = new();
        using (Utf8JsonWriter w = new(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("list");
            foreach (ActiveEvent e in events)
            {
                w.WriteStartObject();
                w.WriteNumber("sourceEventId", e.Source);
                w.WriteNumber("eventId", e.Id);
                w.WriteStartObject("position");
                w.WriteNumber("x", e.Position.X);
                w.WriteNumber("y", e.Position.Y);
                w.WriteNumber("z", e.Position.Z);
                w.WriteEndObject();
                w.WriteNumber("radius", e.Radius);
                w.WriteNumber("startTime", e.StartTime);
                w.WriteNumber("duration", e.Duration);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }
}
