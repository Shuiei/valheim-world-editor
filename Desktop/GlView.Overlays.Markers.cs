using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Marks over the objects: the new objects' green dots, the spawn points' and nests' markers, and the players
// online (live).
public sealed partial class GlView
{
    /// <summary>
    /// Live: the players online, in world metres (blue posts with a beam up; their names are labels
    /// the window places over the view).
    /// </summary>
    internal IReadOnlyList<(string Name, Vector3 World)> Players
    {
        get; set
        {
            field = value;
            Wake();
        }
    } = [];

    /// <summary>The players' posts on the graphics card.</summary>
    private uint _playersVao, _playersVbo;

    /// <summary>A player's post: a body 1.9 m tall (rings and sides) and a 40 m beam, in view space.</summary>
    internal static float[] PlayerPosts(WorldScene s, IEnumerable<(string Name, Vector3 World)> players)
    {
        var data = new List<float>();
        void Seg(Vector3 p, Vector3 q) => data.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        foreach (var (_, w) in players)
        {
            var c = ToView(s, w);
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * MathF.Tau / 12, a1 = (i + 1) * MathF.Tau / 12;
                var p0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * 0.45f;
                var p1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * 0.45f;
                Seg(c + p0, c + p1);
                Seg(c + p0 + new Vector3(0, 1.9f, 0), c + p1 + new Vector3(0, 1.9f, 0));
                if (i % 3 == 0)
                {
                    Seg(c + p0, c + p0 + new Vector3(0, 1.9f, 0));
                }
            }
            Seg(c, c + new Vector3(0, 40, 0));
        }
        return [.. data];
    }

    /// <summary>The live players' posts, light blue.</summary>
    private void DrawPlayers(WorldScene s, Matrix4x4 vp)
    {
        if (Players.Count == 0)
        {
            return;
        }
        DrawLines(ref _playersVao, ref _playersVbo, PlayerPosts(s, Players), vp, new Vector4(0.31f, 0.76f, 1, 1));
    }

    /// <summary>
    /// Whether a new object (id &lt; 0) is in the world already (saved, or live: applied); such objects lose their
    /// marker. Call <c>RefreshNewMarkers</c> when that changes.
    /// </summary>
    public Func<int, bool> InGame { get; set; } = _ => false;

    /// <summary>The green marks on new objects can be hidden (View: Unsaved marks).</summary>
    public bool ShowNewMarkers { get; set; } = true;

    /// <summary>
    /// The new objects not saved (or applied) yet, as the web editor marked them: a green dot of the same
    /// size on screen just above each, drawn over everything, for the kinds shown.
    /// </summary>
    internal List<Vector3> NewMarkers(WorldScene s)
    {
        var list = new List<Vector3>();
        lock (s.Things)
        {
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count; i++)
                {
                    var t = s.Things[i];
                    if (t.Id < 0 && !t.Gone && i < _kinds.Length && _shown[(int)_kinds[i]] && !InGame(t.Id))
                    {
                        list.Add(ToView(s, t.Position + new Vector3(0, 0.4f, 0)));
                    }
                }
            }
        }
        return list;
    }

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

    /// <summary>Every spawn point and nest of the scene still there: its kind, where its marker goes and its colour.</summary>
    private (ObjectKind Kind, Vector3 At, Vector4 Colour)[] FindSpawns(WorldScene s)
    {
        var found = new List<(ObjectKind, Vector3, Vector4)>();
        var colours = new Dictionary<int, Vector4>();
        lock (s.Things)
        {
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count && i < _kinds.Length; i++)
                {
                    var k = _kinds[i];
                    var t = s.Things[i];
                    if (k is not (ObjectKind.SpawnPoints or ObjectKind.Nests) || t.Gone)
                    {
                        continue;
                    }
                    if (!colours.TryGetValue(t.Prefab, out var c))
                    {
                        c = colours[t.Prefab] = k == ObjectKind.Nests ? NestColour : SpawnColour(Terrain.PrefabCatalog.SpawnOf(t.Prefab)?.Respawns == true);
                    }
                    found.Add((k, ToView(s, t.Position + new Vector3(0, 2.4f, 0)), c));
                }
            }
        }
        return [.. found];
    }

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

    /// <summary>The new objects' markers on the graphics card, and their program.</summary>
    private uint _markVao, _markVbo, _markProg;
    /// <summary>How many markers the buffer holds.</summary>
    private int _markCount;
    /// <summary>
    /// The markers are found and sent to the graphics card again at the next frame (things changed, a kind shown or
    /// hidden, a save or apply); otherwise the buffer is drawn as it is: a large area can hold millions of new objects.
    /// </summary>
    private volatile bool _marksDirty = true;

    /// <summary>Has the new objects' markers found again at the next frame (after a save or apply, say).</summary>
    public void RefreshNewMarkers()
    {
        _marksDirty = true;
        _spawnsDirty = true;
        Wake();
    }

    /// <summary>
    /// The new objects' green dots (<c>NewMarkers</c>), a few pixels across at any distance (5 at 1080p, 9 at 4K):
    /// one point each, kept on the graphics card until they change.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="vp">The camera's view and projection.</param>
    /// <param name="height">The drawing's height in pixels.</param>
    private unsafe void DrawNewMarkers(WorldScene s, Matrix4x4 vp, int height)
    {
        if (_markProg == 0)
        {
            _markProg = Program(Shaders.MarkVs, Shaders.MarkFs);
            _markVao = _own.VertexArray();
            _markVbo = _own.Buffer();
            _gl.BindVertexArray(_markVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _markVbo);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        }
        if (_marksDirty)
        {
            _marksDirty = false;
            var marks = NewMarkers(s);
            _markCount = marks.Count;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _markVbo);
            fixed (Vector3* p = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(marks))
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(marks.Count * 12), p, BufferUsageARB.StaticDraw);
            }
        }
        if (_markCount == 0)
        {
            return;
        }
        if (!_es)
        {
            // Desktop OpenGL takes the size from the shader only when asked (OpenGL ES always does).
            _gl.Enable(EnableCap.ProgramPointSize);
        }
        _gl.Disable(EnableCap.DepthTest);
        // The dot's soft edge blends over what is under it.
        GlBlend.Over(_gl);
        _gl.UseProgram(_markProg);
        _gl.UniformMatrix4(_u.At(_markProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform4(_u.At(_markProg, "uColor"), 0x5d / 255f, 1, 0x8a / 255f, 1);
        // A small mark, not a blob: 0.43% of the height (5 pixels at 1080p, 9 at 4K), never under 3.
        _gl.Uniform1(_u.At(_markProg, "uSize"), MathF.Max(3, 0.0043f * height));
        _gl.BindVertexArray(_markVao);
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_markCount);
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }
}
