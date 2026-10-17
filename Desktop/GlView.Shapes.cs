using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The shapes the mods' extensions draw for the area's things, and where they cut the ground.
public sealed partial class GlView
{
    /// <summary>The batches of the mods' extensions' shapes (among <see cref="_batches"/>), made by RebuildShapes.</summary>
    private readonly List<Batch> _shapeBatches = [];
    /// <summary>The things changed: the extensions' shapes are made again at the next frame.</summary>
    private bool _shapesDirty = true;
    /// <summary>The extensions (ModExtensions.Generation) the shapes were made with.</summary>
    private int _shapesGeneration = -1;

    /// <summary>
    /// The scene's cut grid once the extensions' cuts were marked into it (null: none were, it holds zeros): the next
    /// rebuild clears it, and only then.
    /// </summary>
    private float[]? _cutMarked;

    /// <summary>
    /// The shapes the mods' running extensions draw for the area's things (ModExtensions.ShapesOf), made again: in view
    /// space, one batch per kind of object, colour and sidedness, each a single instance the object passes draw like any
    /// model (see-through ones with the glass). And where they cut the ground (ModExtensions.CutsOf): the grid points
    /// inside their outlines marked (WorldScene.Cut) and sent to the terrain. Their data read from the world and this
    /// session's objects.
    /// </summary>
    private unsafe void RebuildShapes(WorldScene s)
    {
        _shapesDirty = false;
        _shapesGeneration = ModExtensions.Generation;
        foreach (var b in _shapeBatches)
        {
            _batches.Remove(b);
            _own.DeleteVertexArray(b.Vao);
            _own.DeleteBuffer(b.InstanceVbo);
            _own.DeleteBuffer(b.OwnVbo);
            _own.DeleteBuffer(b.OwnEbo);
        }
        _shapeBatches.Clear();
        if (s.Cut.Length != s.W * s.H)
        {
            s.Cut = new float[s.W * s.H];
        }
        // Cleared only when cuts were marked into it (no extension cuts the ground: nothing to go over).
        bool wasCut = ReferenceEquals(_cutMarked, s.Cut);
        if (wasCut)
        {
            Array.Clear(s.Cut);
            _cutMarked = null;
        }
        if (s.World is not { } world || s.Session is not { } session || !ModExtensions.AnyShapes && !ModExtensions.AnyCuts)
        {
            SendCuts(s, wasCut);
            return;
        }
        var meshes = new Dictionary<(ObjectKind Kind, Vector4 Color, bool BothSides), (List<float> V, List<uint> I)>();
        List<WorldScene.Thing> all;
        lock (s.Things)
        {
            all = [.. s.Things.Where(t => !t.Gone)];
        }
        var things = all.Where(t => ModExtensions.HasShapes(t.Prefab) || ModExtensions.HasCuts(t.Prefab)).ToList();
        if (things.Count == 0)
        {
            SendCuts(s, wasCut);
            return;
        }
        var around = new ShapeWorld(world, session, all);
        foreach (var t in things)
        {
            if (ShapeWorld.ViewOf(world, session, t) is not { } view)
            {
                continue;
            }
            string name = view.Prefab;
            var q = view.Orientation;
            float scale = view.Scale;
            // A corner in view space: turned and placed as the object (or where it is, for a world-space shape), z
            // mirrored (Unity's winding then faces the view's way).
            Vector3 View(Vector3 v, bool worldSpace)
            {
                var p = worldSpace ? v : t.Position + Vector3.Transform(v * scale, q);
                return ToView(s, p);
            }
            // Grid coordinates of a point on the ground (x, z in the object's frame, or the world's).
            Vector2 Grid(Vector2 p, bool worldSpace)
            {
                var v = worldSpace ? new Vector3(p.X, 0, p.Y) : t.Position + Vector3.Transform(new Vector3(p.X, 0, p.Y) * scale, q);
                return new Vector2(v.X - s.Cx + (s.W - 1) / 2f, v.Z - s.Cz + (s.H - 1) / 2f);
            }
            foreach (var cut in ModExtensions.CutsOf(t.Prefab, view, around))
            {
                GroundCuts.Mark(s.Cut, s.W, s.H, [.. cut.Outline.Select(p => Grid(p, cut.WorldSpace))]);
                _cutMarked = s.Cut;
            }
            foreach (var shape in ModExtensions.ShapesOf(t.Prefab, view, around))
            {
                var key = (ObjectKinds.Of(name, t.Piece), shape.Color, shape.BothSides);
                if (!meshes.TryGetValue(key, out var mesh))
                {
                    meshes[key] = mesh = ([], []);
                }
                for (int k = 0; k + 2 < shape.Triangles.Length; k += 3)
                {
                    var a = View(shape.Vertices[shape.Triangles[k]], shape.WorldSpace);
                    var b = View(shape.Vertices[shape.Triangles[k + 1]], shape.WorldSpace);
                    var c = View(shape.Vertices[shape.Triangles[k + 2]], shape.WorldSpace);
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.LengthSquared() < 1e-12f)
                    {
                        continue;
                    }
                    n = Vector3.Normalize(n);
                    foreach (var p in (Vector3[])[a, b, c])
                    {
                        mesh.I.Add((uint)(mesh.V.Count / 8));
                        mesh.V.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, 0f, 0f]);
                    }
                }
            }
        }
        _gl.BindVertexArray(0);
        foreach (var ((kind, color, both), (v, idx)) in meshes)
        {
            if (idx.Count == 0)
            {
                continue;
            }
            var b = new Batch
            {
                Kind = kind,
                Material = new ModelStore.MaterialData(color, null, 0, both, new Vector4(1, 1, 0, 0), Transparent: color.W < 1),
                IndexCount = idx.Count,
                Instances = 1,
                // A cell past the area's: never culled.
                Cells = [(int.MaxValue, 0, 1)],
                Size = 1000,
                OwnVbo = _own.Buffer(),
                OwnEbo = _own.Buffer(),
                Vao = _own.VertexArray(),
                InstanceVbo = _own.Buffer(),
            };
            _gl.BindVertexArray(b.Vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.OwnVbo);
            float[] verts = [.. v];
            fixed (float* p = verts)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * 4), p, BufferUsageARB.StaticDraw);
            }
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, (void*)12);
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, (void*)24);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, b.OwnEbo);
            uint[] ix = [.. idx];
            fixed (uint* p = ix)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(ix.Length * 4), p, BufferUsageARB.StaticDraw);
            }
            // One instance where it is: the vertices are in view space already (no tint).
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
            var one = Matrix4x4.Identity;
            _gl.BufferData(BufferTargetARB.ArrayBuffer, 64, &one, BufferUsageARB.StaticDraw);
            SetUpInstances();
            _gl.BindVertexArray(0);
            _batches.Add(b);
            _shapeBatches.Add(b);
        }
        SendCuts(s, wasCut);
    }

    /// <summary>Sends the ground's cuts to the terrain, when there are any or there were.</summary>
    /// <param name="s">The scene.</param>
    /// <param name="wasCut">Whether the ground was cut before this rebuild.</param>
    private unsafe void SendCuts(WorldScene s, bool wasCut)
    {
        if (_terrainCutVbo == 0 || !wasCut && !ReferenceEquals(_cutMarked, s.Cut))
        {
            return;
        }
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainCutVbo);
        fixed (float* p = s.Cut)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(s.Cut.Length * 4), p);
        }
    }

    /// <summary>
    /// The world around a thing as an extension's shapes see it (IWorldView): the area's things by the ids linked
    /// objects hold (LinkIndex), each read once.
    /// </summary>
    private sealed class ShapeWorld(Save.WorldSave world, EditSession session, List<WorldScene.Thing> things) : ValheimWorldEditor.Sdk.IWorldView
    {
        /// <summary>The linked objects, read when first needed.</summary>
        private LinkIndex? _links;

        /// <summary>The things not gone, by object id (the first when two have it), made when first needed.</summary>
        private Dictionary<int, WorldScene.Thing>? _byId;

        /// <summary>The views made, by object id.</summary>
        private readonly Dictionary<int, ObjectView?> _views = [];

        /// <summary>A thing as an extension sees it, from its data; null when its data cannot be read.</summary>
        public static ObjectView? ViewOf(Save.WorldSave world, EditSession session, WorldScene.Thing t) =>
            ObjectData.Bytes(world, session.Edits, t.Id) is { } bytes
                ? new ObjectView(Terrain.PrefabCatalog.NameOf(t.Prefab) ?? "", t.Position, t.Rotation, Save.ZdoData.Parse(bytes), t.Scale)
                : null;

        /// <inheritdoc/>
        public bool TryFind(string idKey, int id, out ValheimWorldEditor.Sdk.IObjectView obj)
        {
            obj = null!;
            _links ??= session.ReadLinks();
            if (_links.Holder(Save.StableHash.Of(idKey), id) is not { } holder)
            {
                return false;
            }
            if (_byId == null)
            {
                _byId = [];
                foreach (var t in things)
                {
                    _byId.TryAdd(t.Id, t);
                }
            }
            if (!_byId.TryGetValue(holder.Id, out var thing))
            {
                return false;
            }
            if (!_views.TryGetValue(thing.Id, out var view))
            {
                _views[thing.Id] = view = ViewOf(world, session, thing);
            }
            obj = view!;
            return view != null;
        }
    }
}
