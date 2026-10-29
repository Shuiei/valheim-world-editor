using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// The lines of track in a place as the mod lays them out while the game runs: each track piece's centre lines (from
/// its model), placed and turned as the piece stands. The mod names a line by its two ends and finds a vehicle's or a
/// stop's place on it by the nearest point.
/// </summary>
public sealed class TrackNetwork
{
    private readonly List<TrackLine> _lines = [];

    /// <summary>The lines.</summary>
    public IReadOnlyList<TrackLine> Lines => _lines;

    /// <summary>The lines of track pieces (others are passed over).</summary>
    /// <param name="pieces">The objects that may be track.</param>
    /// <param name="models">The mod's models.</param>
    public static TrackNetwork Of(IEnumerable<IObjectView> pieces, ModelLibrary models)
    {
        var network = new TrackNetwork();
        int piece = 0;
        foreach (var obj in pieces)
        {
            if (RailroadsMod.Pieces.FirstOrDefault(p => p.Prefab == obj.Prefab && p.Kind == RailKind.Track) is not { } track
                || models.Get(track.Model) is not { } model)
            {
                continue;
            }
            foreach (var line in model.Lines)
            {
                network._lines.Add(new TrackLine(piece, track.Model, [.. line.Select(p => obj.Position + Vector3.Transform(p, obj.Orientation))]));
            }
            piece++;
        }
        return network;
    }

    /// <summary>
    /// The nearest point of the lines to a point, strictly within a distance, among the lines a filter keeps; null when
    /// none is that near.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="within">The distance, in metres.</param>
    /// <param name="keep">Which lines count, and at which point of them (all when null).</param>
    public (TrackLine Line, float At, Vector3 Point)? Nearest(Vector3 point, float within, Func<TrackLine, Vector3, bool>? keep = null)
    {
        (TrackLine, float, Vector3)? best = null;
        float most = within * within;
        foreach (var line in _lines)
        {
            for (int i = 0; i + 1 < line.Points.Length; i++)
            {
                Vector3 a = line.Points[i], b = line.Points[i + 1];
                float t = Math.Min(1, Math.Max(0, Vector3.Dot(point - a, b - a) / Math.Max((b - a).LengthSquared(), 1e-6f)));
                var on = Vector3.Lerp(a, b, t);
                float d = (on - point).LengthSquared();
                if (d < most && (keep == null || keep(line, on)))
                {
                    most = d;
                    best = (line, line.Along[i] + (t * (line.Along[i + 1] - line.Along[i])), on);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Whether trains stop for a station sign at a point of a line: on straight track, or on a loop's circle away from
    /// where its other lines join it (3 m), as the mod has it.
    /// </summary>
    public bool StopsAt(TrackLine line, Vector3 point) =>
        line.Model.StartsWith("track_straight", StringComparison.Ordinal)
        || (line.Model == "track_loop" && !_lines.Any(o => o.Piece == line.Piece && o != line && o.Distance(point) < 3));
}

/// <summary>One line of track in the world: the piece it is on, that piece's model and its points in order.</summary>
public sealed class TrackLine
{
    /// <summary>A line.</summary>
    /// <param name="piece">The piece it is on, numbered in its network.</param>
    /// <param name="model">That piece's model.</param>
    /// <param name="points">Its points in the world, in order.</param>
    public TrackLine(int piece, string model, Vector3[] points)
    {
        Piece = piece;
        Model = model;
        Points = points;
        Along = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            Along[i] = Along[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }
    }

    /// <summary>The piece it is on, numbered in its network.</summary>
    public int Piece { get; }

    /// <summary>That piece's model.</summary>
    public string Model { get; }

    /// <summary>Its points in the world, in order.</summary>
    public Vector3[] Points { get; }

    /// <summary>How far along it each point is, in metres.</summary>
    public float[] Along { get; }

    /// <summary>Its first end, which with <see cref="B"/> names it.</summary>
    public Vector3 A => Points[0];

    /// <summary>Its last end.</summary>
    public Vector3 B => Points[Points.Length - 1];

    /// <summary>Its way forward at a distance along it (the segment there).</summary>
    public Vector3 ForwardAt(float at)
    {
        int i = 1;
        while (i < Points.Length - 1 && Along[i] < at)
        {
            i++;
        }
        var d = Points[i] - Points[i - 1];
        return d.LengthSquared() > 0 ? Vector3.Normalize(d) : Vector3.Zero;
    }

    /// <summary>How far a point is from it.</summary>
    public float Distance(Vector3 point)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < Points.Length; i++)
        {
            Vector3 a = Points[i], b = Points[i + 1];
            float t = Math.Min(1, Math.Max(0, Vector3.Dot(point - a, b - a) / Math.Max((b - a).LengthSquared(), 1e-6f)));
            best = Math.Min(best, Vector3.Distance(Vector3.Lerp(a, b, t), point));
        }
        return best;
    }
}
