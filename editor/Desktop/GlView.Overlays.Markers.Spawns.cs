using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The spawn points' and nests' markers: found from the things, drawn as a disc with a line down to the object.
public sealed partial class GlView
{
    /// <summary>How far from the camera spawn points and nests get a marker, metres.</summary>
    internal const float SpawnMarkerRange = 250;

    /// <summary>The marker colour of spawn points: orange for one that spawns once, red for one whose creature comes back.</summary>
    internal static Vector4 SpawnColour(bool respawns) => respawns ? new Vector4(0.95f, 0.2f, 0.25f, 1) : new Vector4(1f, 0.6f, 0.15f, 1);

    /// <summary>The marker colour of nests: purple.</summary>
    internal static readonly Vector4 NestColour = new(0.7f, 0.4f, 0.95f, 1);

    /// <summary>
    /// Spawn points and nests within <see cref="SpawnMarkerRange"/> of the camera, for the kinds shown: where their
    /// marker goes (above the stand-in box) and its colour (<see cref="SpawnColour"/>, <see cref="NestColour"/>).
    /// They have no model in the game (spawn points are invisible), so the marker shows where they are through
    /// anything in front. Each frame looks only through <c>_spawns</c>, found again when the things change.
    /// </summary>
    internal List<(Vector3 At, Vector4 Colour)> SpawnMarkers(WorldScene s)
    {
        var list = new List<(Vector3, Vector4)>();
        bool points = _shown[(int)ObjectKind.SpawnPoints], nests = _shown[(int)ObjectKind.Nests];
        if (!points && !nests)
        {
            return list;
        }
        if (_spawnsDirty)
        {
            _spawnsDirty = false;
            _spawns = FindSpawns(s);
            SpawnFinds++;
        }
        foreach (var (kind, at, colour) in _spawns)
        {
            if ((kind == ObjectKind.SpawnPoints ? points : nests) && Vector3.DistanceSquared(at, _lastEye) <= SpawnMarkerRange * SpawnMarkerRange)
            {
                list.Add((at, colour));
            }
        }
        return list;
    }

    /// <summary>
    /// The spawn points and nests still there, whatever is shown or where the camera is: <c>SpawnMarkers</c> picks
    /// from these at each frame. Found again (<c>FindSpawns</c>) when <c>_spawnsDirty</c> is set.
    /// </summary>
    private (ObjectKind Kind, Vector3 At, Vector4 Colour)[] _spawns = [];

    /// <summary>
    /// The spawn points and nests are found again at the next frame: the things changed (appeared, went, came back,
    /// moved, read again) or their kinds were worked out again (<c>StartModels</c>).
    /// </summary>
    private volatile bool _spawnsDirty = true;

    /// <summary>How many times the things were looked through for spawn points and nests (tests).</summary>
    internal int SpawnFinds { get; private set; }

    /// <summary>The spawn markers on the graphics card, one buffer per colour.</summary>
    private readonly Dictionary<Vector4, (uint Vao, uint Vbo)> _spawnMarkGl = [];

    /// <summary>
    /// The spawn points' and nests' markers (<c>SpawnMarkers</c>): a disc of the same size on screen above each, with
    /// a line down to the object, drawn over everything.
    /// </summary>
    private void DrawSpawnMarkers(WorldScene s, Matrix4x4 vp)
    {
        foreach (var byColour in SpawnMarkers(s).GroupBy(m => m.Colour))
        {
            var tri = new List<float>();
            var lines = new List<float>();
            foreach (var (c, _) in byColour)
            {
                Disc(tri, c, 0.008f);
                lines.AddRange([c.X, c.Y, c.Z, c.X, c.Y - 2.4f, c.Z]);
            }
            var (vao, vbo) = _spawnMarkGl.GetValueOrDefault(byColour.Key);
            DrawLines(ref vao, ref vbo, [.. tri], vp, byColour.Key, PrimitiveType.Triangles);
            DrawLines(ref vao, ref vbo, [.. lines], vp, byColour.Key, width: 2);
            _spawnMarkGl[byColour.Key] = (vao, vbo);
        }
    }

    /// <summary>
    /// Adds a little disc facing the camera to a triangle list, the same size on screen at any distance (its radius
    /// this much of the distance).
    /// </summary>
    private void Disc(List<float> tri, Vector3 c, float size)
    {
        var f = Vector3.Normalize(c - _lastEye);
        var right = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY) is var x && x.LengthSquared() > 1e-6f ? x : Vector3.UnitX);
        var up = Vector3.Cross(right, f);
        float r = Vector3.Distance(_lastEye, c) * size;
        for (int k = 0; k < 8; k++)
        {
            float a0 = k * MathF.Tau / 8, a1 = (k + 1) * MathF.Tau / 8;
            foreach (var v in new[] { c, c + r * (MathF.Cos(a0) * right + MathF.Sin(a0) * up), c + r * (MathF.Cos(a1) * right + MathF.Sin(a1) * up) })
            {
                tri.Add(v.X);
                tri.Add(v.Y);
                tri.Add(v.Z);
            }
        }
    }
}
