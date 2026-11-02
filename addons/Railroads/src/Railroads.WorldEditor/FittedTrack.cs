using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// The fitted track of Railroads 0.4.0 (<see cref="RailroadsMod.FittedTrack"/>): one piece joining two free track ends
/// that do not line up, up to 32 m long. Its shape is in its values, not its model: the far end
/// (<see cref="RailroadsMod.Keys.FitEnd"/>, from the piece's place) and the way the track leaves each end
/// (<see cref="RailroadsMod.Keys.FitWayA"/>, <see cref="RailroadsMod.Keys.FitWayB"/>), all along the world's axes: the mod
/// builds the piece unturned. Its line is the curve that leaves the first end along the first way and reaches the far
/// end along the second (a cubic Hermite curve, its ways as long as the ends are apart), laid out again in steps of at
/// most a quarter metre; its rails are the mod's 1 m straight bent along it, a whole number of times.
/// </summary>
public static class FittedTrack
{
    /// <summary>How many steps the curve is first followed in, to measure it.</summary>
    private const int Samples = 256;

    /// <summary>The longest step of the line as the mod lays it out, in metres.</summary>
    private const float Step = 0.25f;

    /// <summary>The model the mod bends along the line: the 1 m straight.</summary>
    public const string RailModelName = "track_straight_1";

    /// <summary>The way an end leaves when the piece does not say: forward (north).</summary>
    private static readonly Vector3 Forward = Vector3.UnitZ;

    /// <summary>The farthest far end the mod lays (since Railroads 0.4.1), in metres: its longest fitted track (32 m) and a metre.</summary>
    public const float MaxReach = 33;

    /// <summary>The shortest way the mod lays (since 0.4.1): its ways are directions, a metre long.</summary>
    public const float MinWay = 0.5f;

    /// <summary>The longest way the mod lays (since 0.4.1).</summary>
    public const float MaxWay = 1.5f;

    /// <summary>
    /// The piece's line from its own place (along the world's axes), as its values give it; null without a far end, or
    /// with values the mod lays no line for (since Railroads 0.4.1, a shape no game builds: a far end past
    /// <see cref="MaxReach"/>, a way shorter than <see cref="MinWay"/> or longer than <see cref="MaxWay"/>, a value that
    /// is no number). The mod then shows the piece's own model and runs no train on it; and a damaged world or blueprint
    /// makes no line of millions of points.
    /// </summary>
    /// <param name="obj">The fitted track.</param>
    public static Vector3[]? LineOf(IObjectView obj)
    {
        if (!obj.TryGetVector3(RailroadsMod.Keys.FitEnd, out var end))
        {
            return null;
        }
        Vector3 wayA = Way(obj, RailroadsMod.Keys.FitWayA), wayB = Way(obj, RailroadsMod.Keys.FitWayB);
        return Within(end, 0, MaxReach) && Within(wayA, MinWay, MaxWay) && Within(wayB, MinWay, MaxWay) ? Line(end, wayA, wayB) : null;
    }

    /// <summary>Whether a vector is a number of a length from one to another.</summary>
    private static bool Within(Vector3 v, float least, float most)
    {
        float length = v.Length();
        return !float.IsNaN(length) && !float.IsInfinity(length) && length >= least && length <= most;
    }

    /// <summary>The way a piece says an end leaves, else forward.</summary>
    private static Vector3 Way(IObjectView obj, string key) => obj.TryGetVector3(key, out var way) ? way : Forward;

    /// <summary>
    /// The line from the first end (the piece's place) to the far end: the curve leaving along <paramref name="wayA"/> and
    /// arriving along <paramref name="wayB"/>, laid out again in equal steps of at most <see cref="Step"/>, its last point
    /// the far end exactly.
    /// </summary>
    /// <param name="end">The far end, from the first.</param>
    /// <param name="wayA">The way the track leaves the first end.</param>
    /// <param name="wayB">The way the track reaches the far end.</param>
    public static Vector3[] Line(Vector3 end, Vector3 wayA, Vector3 wayB)
    {
        float reach = end.Length();
        var curve = new Vector3[Samples + 1];
        for (int i = 0; i <= Samples; i++)
        {
            float t = (float)i / Samples;
            // Hermite's basis: the far end's weight, then the two ways', each way as long as the ends are apart.
            float toEnd = t * t * (3 - (2 * t)), leaving = t * (t - 1) * (t - 1), arriving = t * t * (t - 1);
            curve[i] = (toEnd * end) + (leaving * reach * wayA) + (arriving * reach * wayB);
        }
        float[] along = Lengths(curve);
        int count = Math.Max(2, (int)Math.Ceiling(along[Samples] / Step) + 1);
        var line = new Vector3[count];
        for (int j = 0; j < count; j++)
        {
            line[j] = At(curve, along, along[Samples] * j / (count - 1)).Point;
        }
        line[count - 1] = end;
        return line;
    }

    /// <summary>
    /// The piece's rails as shapes in its own frame: the 1 m straight's parts bent along the line, as many times as the
    /// line is metres long (rounded, once at least), each stretched to share the line evenly. The line is along the
    /// world's axes; <paramref name="turn"/> is the piece's own turn, taken back out so the rails stand where the line is.
    /// </summary>
    /// <param name="line">The line, from the piece's place.</param>
    /// <param name="rail">The 1 m straight.</param>
    /// <param name="turn">The piece's turn as the editor has it.</param>
    public static IEnumerable<Shape> Rails(Vector3[] line, RailModel rail, Quaternion turn)
    {
        float[] along = Lengths(line);
        float length = along[line.Length - 1];
        int pieces = Math.Max(1, (int)Math.Round(length));
        float each = length / pieces;
        var back = Quaternion.Inverse(turn);
        foreach (var part in rail.Parts)
        {
            var corners = new Vector3[part.Positions.Length * pieces];
            int[] triangles = new int[part.Triangles.Length * pieces];
            for (int p = 0; p < pieces; p++)
            {
                for (int v = 0; v < part.Positions.Length; v++)
                {
                    var model = part.Positions[v];
                    var (point, forward) = At(line, along, (p + model.Z) * each);
                    var right = Vector3.Cross(Vector3.UnitY, forward);
                    right = right.LengthSquared() > 0 ? Vector3.Normalize(right) : Vector3.UnitX;
                    var up = Vector3.Cross(forward, right);
                    corners[(p * part.Positions.Length) + v] = Vector3.Transform(point + (right * model.X) + (up * model.Y), back);
                }
                for (int i = 0; i < part.Triangles.Length; i++)
                {
                    triangles[(p * part.Triangles.Length) + i] = part.Triangles[i] + (p * part.Positions.Length);
                }
            }
            yield return new Shape(corners, triangles, PieceShapes.ColourOf(part.Slot));
        }
    }

    /// <summary>How far along a line each of its points is.</summary>
    private static float[] Lengths(Vector3[] points)
    {
        float[] along = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            along[i] = along[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }
        return along;
    }

    /// <summary>
    /// The point at a distance along a line (kept within its ends) and the way it goes there (that step's), forward when
    /// the step has no length.
    /// </summary>
    private static (Vector3 Point, Vector3 Forward) At(Vector3[] points, float[] along, float at)
    {
        at = Math.Max(0, Math.Min(along[along.Length - 1], at));
        int i = 1;
        while (i < points.Length - 1 && along[i] < at)
        {
            i++;
        }
        float span = along[i] - along[i - 1];
        var step = points[i] - points[i - 1];
        float t = span > 0 ? (at - along[i - 1]) / span : 0;
        return (points[i - 1] + (step * t), step.LengthSquared() > 0 ? Vector3.Normalize(step) : Forward);
    }
}
