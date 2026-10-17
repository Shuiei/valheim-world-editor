using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Drawing the objects beyond the batches in cells: the Place and Paste tools' ghosts, the dungeons' rooms and
// the locations' parts as solids, materials and see-through glass, where an object goes, and the support check's
// tints.
public sealed partial class GlView
{
    /// <summary>The buffer of the ghosts' and rooms' instances, made at first use.</summary>
    private uint _ghostVbo;
    /// <summary>The kinds the paste's ghost drew this frame (their posts are left out).</summary>
    private HashSet<int>? _pasteGhosts;
    /// <summary>
    /// The Place tool's preview, or the Paste tool's (the building where it would land), drawn with the
    /// models, see-through. Kinds whose model is not there yet keep their posts (DrawPlace, the paste's
    /// outline). Returns the prefabs drawn.
    /// </summary>
    private unsafe HashSet<int> DrawGhosts(WorldScene s)
    {
        var drawn = new HashSet<int>();
        List<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown;
        if (Mode == ToolMode.Place && Place is { } pl && pl.Shown.Length > 0)
        {
            shown = [.. pl.Shown.Select(o => (Save.StableHash.Of(o.Name), o.Position, o.Rotation, o.Scale))];
        }
        else if (Mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
        {
            shown = Paste.Ghosts(pat, GridHeight, s.OriginX, s.OriginZ);
        }
        else
        {
            return drawn;
        }
        _pasteGhosts = Mode == ToolMode.Paste ? drawn : null;
        DrawGhostInstances(s, shown, drawn);
        return drawn;
    }

    /// <summary>
    /// The Place and Paste previews: models at places that are not things of the scene, see-through, one draw per model
    /// part. Kinds whose model is not read yet, or that have none, are skipped (and left out of
    /// <paramref name="drawn"/>).
    /// </summary>
    private unsafe void DrawGhostInstances(WorldScene s, IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown, HashSet<int>? drawn = null)
    {
        if (_ghostVbo == 0)
        {
            _ghostVbo = _own.Buffer();
        }
        _gl.UseProgram(_objectProg);
        _gl.Uniform1(_u.At(_objectProg, "uGhost"), 1f);
        var u = MaterialUniforms.Of(_u, _objectProg);
        // Ghosts are see-through whole.
        GlBlend.Over(_gl);
        _gl.DepthMask(false);
        foreach (var byKind in shown.GroupBy(o => o.Prefab))
        {
            int prefab = byKind.Key;
            var (piece, g) = PieceAndGroup(prefab);
            if (!g.Ready || g.Batches.Count == 0)
            {
                continue;
            }
            drawn?.Add(prefab);
            var placements = byKind.Select(o => Placement(s, new WorldScene.Thing(0, prefab, o.Position, o.Rotation, o.Scale, piece), g.RootScale)).ToList();
            foreach (var (b, pre) in g.Batches)
            {
                var data = placements.Select(m => pre * m).ToArray();
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _ghostVbo);
                fixed (Matrix4x4* p = data)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.DynamicDraw);
                }
                DrawFrom(b, _ghostVbo, data.Length, null, u);
            }
        }
        _gl.Uniform1(_u.At(_objectProg, "uGhost"), 0f);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Points the bound vertex array's instance matrix (attributes 3 to 6, one 64-byte matrix each) into the bound
    /// buffer from this byte on.
    /// </summary>
    /// <param name="offset">The first matrix's byte in the buffer.</param>
    private unsafe void PointInstances(long offset)
    {
        for (uint c = 0; c < 4; c++)
        {
            _gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)(offset + c * 16));
        }
    }

    /// <summary>
    /// Sets a new vertex array's instance matrix up: attributes 3 to 6 on, one step per instance, pointed at the start
    /// of the bound buffer.
    /// </summary>
    private void SetUpInstances()
    {
        for (uint c = 0; c < 4; c++)
        {
            _gl.EnableVertexAttribArray(3 + c);
            _gl.VertexAttribDivisor(3 + c, 1);
        }
        PointInstances(0);
    }

    /// <summary>
    /// Draws a model part's instances from another buffer than its own (its material set up), then points its vertex
    /// array back at its own instances. With <paramref name="runs"/>, only the runs whose cell is in view are drawn
    /// (nothing at all, not even the material, when none is).
    /// </summary>
    /// <param name="b">The model part.</param>
    /// <param name="vbo">The buffer of the instances' matrices.</param>
    /// <param name="count">How many instances it holds.</param>
    /// <param name="runs">Its instances by area cell (see <see cref="Runs"/>), or null to draw them all.</param>
    /// <param name="u">The object shader's material uniforms.</param>
    private unsafe void DrawFrom(Batch b, uint vbo, int count, (int Cell, int Start, int Count)[]? runs, MaterialUniforms u)
    {
        if (runs != null && !runs.Any(r => Shows(r.Cell, float.MaxValue)))
        {
            return;
        }
        _gl.BindVertexArray(b.Vao);
        UseMaterial(b, u);
        if (runs == null)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            PointInstances(0);
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
        }
        else
        {
            // However far: these are not left out for being small and far away, only for being out of view.
            DrawRuns(b, vbo, count, runs, float.MaxValue, out _);
        }
        // The batch's own instances again.
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        PointInstances(0);
        b.PointedAt = 0;
    }

    /// <summary>
    /// Whether a prefab is a building piece (not a plain object), and its group of drawn things (see KindGroup).
    /// </summary>
    /// <param name="prefab">The prefab's id.</param>
    private (bool Piece, Group G) PieceAndGroup(int prefab)
    {
        bool piece = Terrain.PieceCatalog.Get(prefab)?.Tool != null;
        return (piece, KindGroup(prefab, piece));
    }

    /// <summary>
    /// The instances of models drawn solid at places that are not things of the scene (dungeon rooms, locations'
    /// parts), on the graphics card: made from one list and kept while that list and its kinds' models stay the same,
    /// so a frame only draws them.
    /// </summary>
    private sealed class Solid
    {
        /// <summary>The list they were made from (compared by reference: the caches give the same list until it changes).</summary>
        public object? Source;
        /// <summary>The area they were made for (its middle places them).</summary>
        public WorldScene? Scene;
        /// <summary>Each kind in the list: its group then, and whether it was drawn (its model read).</summary>
        public readonly List<(int Prefab, bool Piece, Group Group, bool Drawn)> Kinds = [];
        /// <summary>
        /// One draw per model part of each kind drawn: its batch, the buffer of its instances, how many, and their runs by
        /// area cell (<see cref="Runs"/>, in the list's order).
        /// </summary>
        public readonly List<(Batch Batch, uint Vbo, int Count, (int Cell, int Start, int Count)[] Runs)> Draws = [];
        /// <summary>The cells (<c>_cellsMade</c>) whose boxes were grown by these instances' boxes; -1 for none.</summary>
        public int Cells = -1;
        /// <summary>The buffers made so far, used again when the instances are made again.</summary>
        public readonly List<uint> Buffers = [];

        /// <summary>Forgets everything (the graphics context went, and its buffers with it).</summary>
        public void Forget()
        {
            Source = null;
            Scene = null;
            Cells = -1;
            Kinds.Clear();
            Draws.Clear();
            Buffers.Clear();
        }
    }

    /// <summary>The dungeon rooms' instances (DrawRooms).</summary>
    private readonly Solid _roomsMade = new();
    /// <summary>The locations' parts' instances (DrawLocations).</summary>
    private readonly Solid _locationsMade = new();

    /// <summary>Whether a kind's model is drawn solid: read, with parts.</summary>
    private static bool DrawnSolid(Group g) => g.Ready && g.Batches.Count > 0 && g.HasModel;

    /// <summary>
    /// Draws models solid at places that are not things of the scene: opaque parts first, then see-through ones
    /// (glass) blended over them, only in the area's cells in view. Their instances are made again (<c>MakeSolid</c>)
    /// only when <paramref name="source"/> is another list, the area or its cells changed, or one of its kinds' models
    /// was read or let go since; kinds whose model is not read yet, or that have none, are skipped.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="source">The list the places come from, the same object until they change.</param>
    /// <param name="shown">The places, taken from <paramref name="source"/> only when the instances are made again.</param>
    /// <param name="made">The instances made last from this kind of list.</param>
    private void DrawSolid(WorldScene s, object source, Func<IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation)>> shown, Solid made)
    {
        EnsureCells(s);
        bool stale = made.Source != source || made.Scene != s || made.Cells != _cellsMade;
        for (int k = 0; k < made.Kinds.Count && !stale; k++)
        {
            var (prefab, piece, group, drawn) = made.Kinds[k];
            var g = KindGroup(prefab, piece);
            stale = g != group || DrawnSolid(g) != drawn;
        }
        if (stale)
        {
            MakeSolid(s, source, shown(), made);
            // The cells' boxes grew (or are new): which are in view is seen again, so no part in view is left out.
            SeeCells(_lastViewProj, _lastEye);
        }
        if (made.Draws.Count == 0)
        {
            return;
        }
        _gl.UseProgram(_objectProg);
        _gl.Uniform1(_u.At(_objectProg, "uGhost"), 0f);
        var u = MaterialUniforms.Of(_u, _objectProg);
        foreach (bool glass in (ReadOnlySpan<bool>)[false, true])
        {
            if (glass)
            {
                GlBlend.Over(_gl);
                _gl.DepthMask(false);
            }
            foreach (var (b, vbo, count, runs) in made.Draws)
            {
                if (b.Material.Transparent == glass)
                {
                    DrawFrom(b, vbo, count, runs, u);
                }
            }
        }
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Makes the instances of <c>DrawSolid</c> again: one buffer of matrices per model part of each kind whose model is
    /// read, the buffers made before used again. Each instance's placed model box grows the box of the area cell it
    /// stands in, so the cell shows whenever any of it is in view.
    /// </summary>
    private unsafe void MakeSolid(WorldScene s, object source, IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation)> shown, Solid made)
    {
        made.Source = source;
        made.Scene = s;
        made.Cells = _cellsMade;
        made.Kinds.Clear();
        made.Draws.Clear();
        int used = 0;
        foreach (var byKind in shown.GroupBy(o => o.Prefab))
        {
            int prefab = byKind.Key;
            var (piece, g) = PieceAndGroup(prefab);
            bool drawn = DrawnSolid(g);
            made.Kinds.Add((prefab, piece, g, drawn));
            if (!drawn)
            {
                continue;
            }
            var placements = byKind.Select(o => Placement(s, new WorldScene.Thing(0, prefab, o.Position, o.Rotation, 0f, piece), g.RootScale)).ToList();
            (Vector3 Min, Vector3 Max)? box;
            lock (_groups)
            {
                box = g.Box;
            }
            int[] cells = new int[placements.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = CellOf(s, placements[i].Translation);
                Grow(_cellBounds, cells[i], PlacedBox(box, placements[i]));
            }
            var runs = Runs(cells);
            var data = new Matrix4x4[placements.Count];
            foreach (var (b, pre) in g.Batches)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = pre * placements[i];
                }
                if (used == made.Buffers.Count)
                {
                    made.Buffers.Add(_own.Buffer());
                }
                uint vbo = made.Buffers[used++];
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
                fixed (Matrix4x4* p = data)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.StaticDraw);
                }
                made.Draws.Add((b, vbo, data.Length, runs));
            }
        }
    }

    /// <summary>The object shader's uniforms a batch's material sets (their locations in the program).</summary>
    /// <param name="Color">The colour (uColor).</param>
    /// <param name="Cutoff">The alpha cutoff (uCutoff).</param>
    /// <param name="HasMap">Whether there is a texture (uHasMap).</param>
    /// <param name="Uv">The texture's scale and offset (uUv).</param>
    /// <param name="Blend">Whether the material is see-through (uBlend).</param>
    private readonly record struct MaterialUniforms(int Color, int Cutoff, int HasMap, int Uv, int Blend)
    {
        /// <summary>The locations in this program.</summary>
        public static MaterialUniforms Of(GlUniforms u, uint program) => new(u.At(program, "uColor"), u.At(program, "uCutoff"),
            u.At(program, "uHasMap"), u.At(program, "uUv"), u.At(program, "uBlend"));
    }

    /// <summary>
    /// Sets the object shader up for a batch's material: its texture (looked up again until it is on the graphics card),
    /// its back faces culled or not (always for a mesh wound both ways), its colour, cutoff and uv, and whether it is
    /// blended (see-through).
    /// </summary>
    private void UseMaterial(Batch b, MaterialUniforms u)
    {
        var m = b.Material;
        if (b.Texture == 0 && m.Map != null)
        {
            lock (_textures)
            {
                _textures.TryGetValue(m.Map, out b.Texture);
            }
        }
        if (m.DoubleSided && !b.BothWays)
        {
            _gl.Disable(EnableCap.CullFace);
        }
        else
        {
            _gl.Enable(EnableCap.CullFace);
        }
        _gl.Uniform4(u.Color, m.Color.X, m.Color.Y, m.Color.Z, m.Color.W);
        _gl.Uniform1(u.Cutoff, m.Cutoff);
        _gl.Uniform1(u.HasMap, b.Texture != 0 ? 1 : 0);
        _gl.Uniform4(u.Uv, m.UvTransform.X, m.UvTransform.Y, m.UvTransform.Z, m.UvTransform.W);
        _gl.Uniform1(u.Blend, m.Transparent ? 1f : 0f);
        _gl.BindTexture(TextureTarget.Texture2D, b.Texture);
    }

    /// <summary>
    /// The things' see-through materials (glass), last of the objects: blended over what is drawn, behind them too, and
    /// not hiding what is drawn after. Buildings drawn see-through (View) had theirs drawn with them. Returns the
    /// instances drawn.
    /// </summary>
    private int DrawGlass(MaterialUniforms u, bool seeThrough)
    {
        int drawn = 0;
        bool started = false;
        foreach (var b in _batches)
        {
            if (!b.Material.Transparent || !_shown[(int)b.Kind] || seeThrough && b.Kind == ObjectKind.Buildings)
            {
                continue;
            }
            if (!started)
            {
                started = true;
                _gl.UseProgram(_objectProg);
                GlBlend.Over(_gl);
                _gl.DepthMask(false);
            }
            UseMaterial(b, u);
            _gl.BindVertexArray(b.Vao);
            drawn += DrawCells(b);
        }
        if (started)
        {
            _gl.DepthMask(true);
            _gl.Disable(EnableCap.Blend);
            _gl.BindVertexArray(0);
        }
        return drawn;
    }

    /// <summary>
    /// The rooms of the area's dungeons (not objects of the save: entries in their dungeon's data, see
    /// DungeonRooms), drawn like objects; a room shown elsewhere by the Dungeon tool (moving, adding) too.
    /// </summary>
    private void DrawRooms(WorldScene s)
    {
        var dungeons = DungeonRooms.Shown(s, Wake);
        DrawSolid(s, dungeons, () => dungeons.SelectMany(d => d.Rooms).Select(r => (r.Hash, r.Position, BlueprintFormats.ToEuler(r.Rotation))), _roomsMade);
    }

    /// <summary>
    /// The parts of the area's locations that are not saved objects (see LocationParts), drawn like objects with the
    /// Ruins &amp; structures kind; they cannot be picked.
    /// </summary>
    private void DrawLocations(WorldScene s)
    {
        var shown = LocationParts.Of(s);
        DrawSolid(s, shown, () => shown.Select(l => (l.Location, l.Position, l.Rotation)), _locationsMade);
    }

    /// <summary>
    /// Where an object goes, in view space: Unity's rotation (Euler degrees, applied z, x, then y)
    /// mirrored in z, like the web editor's objects.js.
    /// </summary>
    private static Matrix4x4 Placement(WorldScene s, WorldScene.Thing t, Vector3 rootScale)
    {
        const float D = MathF.PI / 180f;
        var q = Quaternion.CreateFromYawPitchRoll(t.Rotation.Y * D, t.Rotation.X * D, t.Rotation.Z * D);
        q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
        var scale = t.Scale > 0 ? new Vector3(t.Scale) : rootScale;
        var pos = ToView(s, t.Position);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(pos);
    }

    /// <summary>
    /// The support check (the Workshop): each building piece tinted as the game's build mode shows its
    /// support (SupportTint). Per thing index: -1 full, 0..1, or -2 breaks. Null: off.
    /// </summary>
    private volatile Dictionary<int, float>? _support;

    /// <summary>Turns the support check on (values per thing index, see <c>_support</c>) or off (null).</summary>
    public void ShowSupport(Dictionary<int, float>? support)
    {
        _support = support;
        // The tints are in the instances: built again.
        lock (_groups)
        {
            foreach (var g in _groups.Values)
            {
                MarkDirty(g);
            }
        }
        Wake();
    }

    /// <summary>The support values shown, null when the check is off.</summary>
    internal Dictionary<int, float>? Support => _support;

    /// <summary>
    /// The game's colour for a support value (WearNTear.Highlight): light blue at full support (on the
    /// ground), else red to green as it gets stronger, more saturated and brighter the weaker; one that
    /// breaks as the weakest (red).
    /// </summary>
    internal static Vector3 SupportTint(float v)
    {
        if (v is > (-1.5f) and < 0)
        {
            return new Vector3(0.6f, 0.8f, 1f);
        }
        float t = Math.Clamp(v < 0 ? 0 : v, 0, 1);
        // Lerp(red, green, t) has hue 0..1/3 with full saturation and value: its HSV, then the game's S and V.
        float h = new Vector3(1 - t, t, 0) is var c && c.X >= c.Y ? c.Y / c.X / 6f : (2 - c.X / c.Y) / 6f;
        return HsvToRgb(h, 1 - 0.5f * t, 1.2f - 0.3f * t);
    }

    /// <summary>A colour from hue (0..1), saturation and value (value may go above 1).</summary>
    private static Vector3 HsvToRgb(float h, float s, float v)
    {
        float r = Math.Clamp(MathF.Abs(h * 6 - 3) - 1, 0, 1), g = Math.Clamp(2 - MathF.Abs(h * 6 - 2), 0, 1), b = Math.Clamp(2 - MathF.Abs(h * 6 - 4), 0, 1);
        return new Vector3((1 - s + s * r) * v, (1 - s + s * g) * v, (1 - s + s * b) * v);
    }
}
