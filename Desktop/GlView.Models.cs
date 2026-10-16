using System.Collections.Concurrent;
using System.Numerics;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The objects' models: grouped by kind, read on worker threads, uploaded by the drawing thread (meshes,
// textures, batches), and let go of when the graphics card's budget is past.
public sealed partial class GlView
{
    /// <summary>How many objects of each kind the area has (once the models' names are known).</summary>
    public event Action<Dictionary<ObjectKind, int>>? KindCounts;

    /// <summary>One draw: a model part (mesh and material) with all its copies, as instances.</summary>
    private sealed class Batch
    {
        /// <summary>The View kind it belongs to (hidden with it).</summary>
        public ObjectKind Kind;
        /// <summary>Its vertex array, and the buffer of its instances' matrices.</summary>
        public uint Vao, InstanceVbo;
        /// <summary>Indices per copy, and how many copies.</summary>
        public int IndexCount, Instances;
        /// <summary>Its texture, 0 for none.</summary>
        public uint Texture;
        /// <summary>Its material (colour, cut-out, two-sided…).</summary>
        public ModelStore.MaterialData Material = null!;
        /// <summary>Its mesh (a key of <c>_pickMeshes</c>) and which of its submeshes: picking tries the triangles.</summary>
        public string MeshId = "";
        /// <summary>See <see cref="MeshId"/>.</summary>
        public int Sub;
        /// <summary>
        /// Its mesh has every triangle wound both ways: back faces are culled whatever the material, each side drawn once.
        /// </summary>
        public bool BothWays;
        /// <summary>
        /// Its instances by area cell (<see cref="CellSize"/>): they are sorted by cell, each cell a run of them, so
        /// only the cells in view are drawn.
        /// </summary>
        public (int Cell, int Start, int Count)[] Cells = [];
        /// <summary>The model's size (its box's diagonal, metres): small ones are not drawn far away.</summary>
        public float Size = 1;
        /// <summary>The instance its vertex array's instance attributes point at now.</summary>
        public int PointedAt;
        /// <summary>Its own vertex and index buffers (a mod extension's shapes, see RebuildShapes); 0 for a model's, shared.</summary>
        public uint OwnVbo, OwnEbo;
    }
    /// <summary>Every batch drawn.</summary>
    private readonly List<Batch> _batches = [];

    /// <summary>
    /// The things of one kind of object (a building piece a player placed apart from the same piece in a
    /// ruin): one model, drawn with one batch per model part. Its batches' instances are rebuilt when one
    /// of its things changes (deleted, moved, added, or shown elsewhere while being moved).
    /// </summary>
    private sealed class Group
    {
        /// <summary>Prefab, piece or not, tamed or not.</summary>
        public required (int Prefab, bool Piece, bool Tamed) Key { get; init; }
        /// <summary>The View kind of its things.</summary>
        public required ObjectKind Kind { get; init; }
        /// <summary>Its things, as indices into the scene's things.</summary>
        public readonly List<int> Things = [];
        /// <summary>
        /// The same, to ask whether one is in (a list's Contains made opening an area with tens of
        /// thousands of one piece take seconds: every object asked it).
        /// </summary>
        public readonly HashSet<int> Members = [];
        /// <summary>The model's root scale, applied to every copy.</summary>
        public Vector3 RootScale = Vector3.One;
        /// <summary>The model's box in its own frame (for picking), known once read.</summary>
        public (Vector3 Min, Vector3 Max)? Box;
        /// <summary>Its batches, each with the part's own matrix within the model.</summary>
        public readonly List<(Batch Batch, Matrix4x4 Pre)> Batches = [];
        /// <summary>Its batches are made (the model read, or the box set up in its place).</summary>
        public bool Ready;
        /// <summary>A model was read (else the batches are the stand-in box).</summary>
        public bool HasModel;
    }
    /// <summary>By prefab, piece or not and tamed or not: tamed creatures are their own View kind.</summary>
    private readonly Dictionary<(int, bool, bool), Group> _groups = [];
    /// <summary>Groups whose instances are built again at the next frame.</summary>
    private readonly HashSet<Group> _dirtyGroups = [];
    /// <summary>Things shown somewhere else than where they are, while a move is being made (Select tool).</summary>
    private Dictionary<int, WorldScene.Thing> _previews = [];
    /// <summary>The things were read again (rebuild every group); the overlays are built again.</summary>
    private volatile bool _thingsReset, _overlaysDirty;
    /// <summary>
    /// Each mesh on the graphics card, by name: its vertex buffer, and an index buffer and count per part.
    /// </summary>
    private readonly Dictionary<string, (uint Vbo, uint[] Ebos, int[] Counts)> _meshGl = [];
    /// <summary>
    /// A mesh kept for picking: its vertex positions (three floats each) and its submeshes' triangles, its box.
    /// </summary>
    private sealed record PickMesh(float[] Positions, uint[][] Submeshes, (Vector3 Min, Vector3 Max) Bounds);
    /// <summary>The meshes on the graphics card, kept in a small form so a click can be tried against their triangles.</summary>
    private readonly ConcurrentDictionary<string, PickMesh> _pickMeshes = [];
    /// <summary>The meshes on the graphics card whose triangles are all there wound both ways (MeshData.BothWays).</summary>
    private readonly HashSet<string> _bothWays = [];
    /// <summary>
    /// Meshes on the graphics card (read by the loaders on worker threads), and about how much the
    /// models and their textures take there. Past the budget, opening another area lets them all go
    /// (that area's are read again): they piled up over a long session, gigabytes with Valheim open.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _onGpu = new();
    /// <summary>About how many bytes the models and their textures take on the graphics card.</summary>
    private long _gpuBytes;
    /// <summary>The graphics card budget in bytes (1 GiB); VWE_GPU_BUDGET (bytes): another budget, for the tests.</summary>
    private static readonly long GpuBudget = long.TryParse(Environment.GetEnvironmentVariable("VWE_GPU_BUDGET"), out long b) ? b : 1L << 30;
    /// <summary>Textures on the graphics card, by name.</summary>
    private readonly Dictionary<string, uint> _textures = [];
    /// <summary>Models read on worker threads, waiting to go to the graphics card (on the drawing thread).</summary>
    private sealed record ReadyModel(Group Group, ModelStore.Model? Model, Dictionary<string, ModelStore.MeshData> Meshes,
        Dictionary<string, ModelStore.MaterialData> Materials, Dictionary<string, ModelStore.ImageData?> Images);
    /// <summary>Models read, waiting for the drawing thread.</summary>
    private readonly ConcurrentQueue<ReadyModel> _ready = new();
    /// <summary>Tests (headless, so nothing goes to the graphics card): the models read and waiting, each by its kind's prefab, whether it has a model (else the stand-in box) and its kind's box.</summary>
    internal (int Prefab, bool HasModel, (Vector3 Min, Vector3 Max)? Box)[] ModelsWaiting
    {
        get
        {
            lock (_groups)
            {
                return [.. _ready.Select(r => (r.Group.Key.Prefab, r.Model != null, r.Group.Box))];
            }
        }
    }
    /// <summary>Tests: the textures a loader has taken to read and not yet handed to the graphics card.</summary>
    internal string[] TexturesClaimed { get { lock (_textures) { return [.. _claimed]; } } }
    /// <summary>The box standing in for a kind without a model (a metre across, two tall), in its own frame.</summary>
    private static readonly (Vector3 Min, Vector3 Max) StandInBox = (new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 2, 0.5f));
    /// <summary>Models still being read.</summary>
    private int _pending;

    // ---- Objects: read each kind's model on worker threads; the drawing thread uploads them.
    /// <summary>A prefab's name: the model store's, else the catalogue's display name; null when unknown.</summary>
    private string? NameOf(int prefab) => Models?.NameOf(prefab) ?? Terrain.PrefabCatalog.DisplayName(prefab);

    /// <summary>
    /// Tests (headless, so nothing is drawn): the shown area's models read on worker threads now, as the first frame
    /// drawn would start them.
    /// </summary>
    internal void ReadModels()
    {
        if (Scene is { } s)
        {
            StartModels(s);
        }
    }

    /// <summary>A new area's objects: every thing in its group, and every group's model read on worker threads.</summary>
    private void StartModels(WorldScene s)
    {
        lock (_groups)
        {
            _groups.Clear();
            lock (_dirtyGroups)
            {
                _dirtyGroups.Clear();
            }
            int n;
            lock (s.Things)
            {
                n = s.Things.Count;
                for (int i = 0; i < n; i++)
                {
                    GroupOf(s, i, start: false);
                }
            }
            _pending = _groups.Count;
            foreach (var g in _groups.Values.ToList())
            {
                Load(g);
            }
        }
        SendKindCounts(s);
        _overlaysDirty = true;
        // The kinds were worked out again (another area, or the things read again).
        _spawnsDirty = true;
    }

    /// <summary>
    /// A thing's group: by prefab, built or not, tamed or not. A ruin's piece joined to a building counts as built (shown
    /// with Your buildings), in a group apart from the same prefab's other ruin pieces.
    /// </summary>
    private static (int Prefab, bool Piece, bool Tamed) KeyOf(WorldScene.Thing t) => (t.Prefab, t.Piece || t.Joined, t.Tamed);

    /// <summary>The group a thing belongs to (made, and its model read, if it is the first of its kind).</summary>
    private Group GroupOf(WorldScene s, int i, bool start = true)
    {
        var t = s.Things[i];
        var key = KeyOf(t);
        EnsureSize(i + 1);
        if (!_groups.TryGetValue(key, out var g))
        {
            g = _groups[key] = new Group { Key = key, Kind = ObjectKinds.Of(NameOf(t.Prefab), t.Piece || t.Joined, t.Tamed) };
            if (start)
            {
                Interlocked.Increment(ref _pending);
                Load(g);
            }
        }
        if (g.Members.Add(i))
        {
            g.Things.Add(i);
        }
        lock (_objLock)
        {
            _kinds[i] = g.Kind;
        }
        return g;
    }

    /// <summary>A kind's group without things yet (the Place tool's ghosts), its model read if new.</summary>
    private Group KindGroup(int prefab, bool piece)
    {
        lock (_groups)
        {
            if (!_groups.TryGetValue((prefab, piece, false), out var g))
            {
                g = _groups[(prefab, piece, false)] = new Group { Key = (prefab, piece, false), Kind = ObjectKinds.Of(NameOf(prefab), piece) };
                Interlocked.Increment(ref _pending);
                Load(g);
            }
            return g;
        }
    }

    /// <summary>Grows the per-thing arrays (boxes, kinds) to hold at least <paramref name="n"/> things.</summary>
    private void EnsureSize(int n)
    {
        lock (_objLock)
        {
            if (_bounds.Length >= n)
            {
                return;
            }
            int size = Math.Max(n, _bounds.Length * 3 / 2 + 16);
            Array.Resize(ref _bounds, size);
            Array.Resize(ref _known, size);
            Array.Resize(ref _boundsAt, size);
            Array.Resize(ref _kinds, size);
        }
    }

    /// <summary>Counts the things still there by kind and sends it (<c>KindCounts</c>, on the UI thread).</summary>
    private void SendKindCounts(WorldScene s)
    {
        var counts = new Dictionary<ObjectKind, int>();
        lock (s.Things)
        {
            // The things may have been read again (after a save) before the view caught up.
            EnsureSize(s.Things.Count);
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count; i++)
                {
                    if (!s.Things[i].Gone)
                    {
                        counts[_kinds[i]] = counts.GetValueOrDefault(_kinds[i]) + 1;
                    }
                }
            }
        }
        Dispatcher.UIThread.Post(() => KindCounts?.Invoke(counts));
    }

    /// <summary>Things appeared, went or came back (EditSession): their groups are drawn again.</summary>
    private void OnThingsChanged(IReadOnlyList<int> indices)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        _shapesDirty = true;
        lock (_groups)
        {
            lock (s.Things)
            {
                foreach (int i in indices)
                {
                    MarkDirty(GroupOf(s, i));
                }
            }
        }
        lock (_selection)
        {
            _selection.RemoveWhere(i => s.Things[i].Gone);
        }
        _selectionDirty = true;
        _overlaysDirty = true;
        _marksDirty = true;
        _spawnsDirty = true;
        SendKindCounts(s);
        Wake();
    }

    /// <summary>Has a group's instances built again at the next frame.</summary>
    private void MarkDirty(Group g)
    {
        lock (_dirtyGroups)
        {
            _dirtyGroups.Add(g);
        }
    }

    /// <summary>Shows things somewhere else than where they are (a move in progress), or (null) where they are.</summary>
    internal void SetPreviews(Dictionary<int, WorldScene.Thing>? previews)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var old = _previews;
        _previews = previews ?? [];
        lock (_groups)
        {
            foreach (int i in old.Keys.Concat(_previews.Keys))
            {
                var t = s.Things[i];
                if (_groups.TryGetValue(KeyOf(t), out var g))
                {
                    MarkDirty(g);
                }
            }
        }
        _selectionDirty = true;
        Wake();
    }

    /// <summary>
    /// Reads a group's model on a worker thread; the drawing thread sends it to the graphics card. Waiting
    /// for its turn holds no thread (hundreds of kinds each blocking one starved the thread pool: the
    /// next area's loading and the map's search waited seconds behind them).
    /// </summary>
    private void Load(Group g)
    {
        Task.Run(async () =>
        {
            await LoadGate.WaitAsync();
            var images = new Dictionary<string, ModelStore.ImageData?>();
            // The textures this loader took to read (released again if not read).
            var claimed = new List<string>();
            try
            {
                // Left already (another area opened while it waited): not read at all.
                if (!Current(g))
                {
                    return;
                }
                string? name = NameOf(g.Key.Prefab);
                var model = name != null && Models != null && _gameLookOn ? Models.LoadModel(name) : null;
                var meshes = new Dictionary<string, ModelStore.MeshData>();
                var mats = new Dictionary<string, ModelStore.MaterialData>();
                Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
                if (model != null)
                {
                    foreach (var part in model.Parts)
                    {
                        // A mesh already on the graphics card is not read again: only its box is needed.
                        var bounds = _onGpu.ContainsKey(part.Mesh) ? Models!.BoundsOf(part.Mesh) : null;
                        if (bounds == null && Models!.LoadMesh(part.Mesh) is { } md)
                        {
                            meshes.TryAdd(part.Mesh, md);
                            bounds = md.Bounds;
                        }
                        if (bounds is { } box)
                        {
                            // The object's box, for picking: the parts' meshes.
                            var (a, b) = Picking.Transform(box.Min, box.Max, part.Matrix);
                            lo = Vector3.Min(lo, a);
                            hi = Vector3.Max(hi, b);
                        }
                        if (!mats.ContainsKey(part.Material))
                        {
                            var mat = Models!.Material(part.Material);
                            mats[part.Material] = mat;
                            if (mat.Map != null && !images.ContainsKey(mat.Map))
                            {
                                lock (_textures)
                                {
                                    if (_textures.ContainsKey(mat.Map) || _claimed.Contains(mat.Map))
                                    {
                                        continue;
                                    }
                                    _claimed.Add(mat.Map);
                                }
                                claimed.Add(mat.Map);
                                images[mat.Map] = Models.LoadTexture(mat.Map);
                            }
                        }
                    }
                }
                if (lo.X > hi.X)
                {
                    // No model: the stand-in box.
                    (lo, hi) = StandInBox;
                }
                lock (_groups)
                {
                    g.RootScale = model?.RootScale ?? Vector3.One;
                    g.HasModel = model != null;
                    g.Box = (lo, hi);
                }
                _ready.Enqueue(new ReadyModel(g, model, meshes, mats, images));
            }
            catch (Exception ex)
            {
                // The store turns a mesh, material or texture that cannot be read into none; what still fails (the
                // memory running out on a texture) is said, and the kind's things get the stand-in box rather than
                // nothing at all: drawn, picked, and their ghosts shown. The textures read go up with it, as any
                // model's; those claimed and not read are let go, for another loader to read.
                Status?.Invoke($"Model of {g.Key.Prefab}: {ex.Message}");
                lock (_textures)
                {
                    foreach (string file in claimed)
                    {
                        if (!images.ContainsKey(file))
                        {
                            _claimed.Remove(file);
                        }
                    }
                }
                lock (_groups)
                {
                    g.RootScale = Vector3.One;
                    g.HasModel = false;
                    g.Box = StandInBox;
                }
                _ready.Enqueue(new ReadyModel(g, null, [], [], images));
            }
            finally
            {
                LoadGate.Release();
            }
            Wake();
        });
    }
    /// <summary>
    /// How many models are read at once (half the processors, at least two). Static: one 3D view lives as long as
    /// the app (and an instance field would make the view disposable).
    /// </summary>
    private static readonly SemaphoreSlim LoadGate = new(Math.Max(2, Environment.ProcessorCount / 2));
    /// <summary>Guards the per-thing arrays (boxes, kinds), which the loaders and the drawing thread both use.</summary>
    private readonly Lock _objLock = new();

    /// <summary>Every model and texture off the graphics card (after DropObjects: no batch uses them).</summary>
    private void DropModels()
    {
        foreach (var (vbo, ebos, _) in _meshGl.Values)
        {
            _own.DeleteBuffer(vbo);
            foreach (uint e in ebos)
            {
                _own.DeleteBuffer(e);
            }
        }
        _meshGl.Clear();
        _pickMeshes.Clear();
        _bothWays.Clear();
        _onGpu.Clear();
        lock (_textures)
        {
            foreach (uint t in _textures.Values)
            {
                _own.DeleteTexture(t);
            }
            _textures.Clear();
            _claimed.Clear();
        }
        Status?.Invoke($"Models let go from the graphics card ({_gpuBytes >> 20} MB): this area's are read again.");
        _gpuBytes = 0;
    }

    /// <summary>Everything read again (after a save): the old batches go.</summary>
    private void DropObjects()
    {
        foreach (var b in _batches)
        {
            _own.DeleteVertexArray(b.Vao);
            _own.DeleteBuffer(b.InstanceVbo);
            if (b.OwnVbo != 0)
            {
                _own.DeleteBuffer(b.OwnVbo);
                _own.DeleteBuffer(b.OwnEbo);
            }
        }
        _batches.Clear();
        _shapeBatches.Clear();
        _shapesDirty = true;
        // The cells' boxes are made again with the objects.
        _cellScene = null;
        while (_ready.TryDequeue(out var dropped))
        {
            UploadTextures(dropped);
        }
        // (The selection went when the area was shown or read again, not here: one made since, as the
        // map's "Edit in 3D" makes at once, is the new area's.)
        _selectionDirty = true;
        lock (_objLock)
        {
            Array.Clear(_known);
        }
    }
    /// <summary>Textures a loader has taken to read (guarded by <c>_textures</c>), so no other reads them too.</summary>
    private readonly HashSet<string> _claimed = [];

    /// <summary>
    /// A loaded model's textures, then its batches. A model read for an area (or a look) left since
    /// keeps only its textures: its group is gone, and its things' indices are another scene's.
    /// </summary>
    private unsafe void Upload(ReadyModel r)
    {
        UploadTextures(r);
        if (Current(r.Group))
        {
            UploadModel(r);
        }
    }

    /// <summary>The group is still the view's (not one of an area left since).</summary>
    private bool Current(Group g)
    {
        lock (_groups)
        {
            return _groups.TryGetValue(g.Key, out var now) && now == g;
        }
    }

    /// <summary>
    /// A loaded model's textures. Also for a model dropped before it was drawn (a new area): its loader
    /// claimed them, so no other loader reads them.
    /// </summary>
    private unsafe void UploadTextures(ReadyModel r)
    {
        foreach (var (file, img) in r.Images)
        {
            if (img == null || _textures.ContainsKey(file))
            {
                continue;
            }
            uint tex = _own.Texture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            fixed (byte* p = img.Rgba)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Srgb8Alpha8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
            _gl.GenerateMipmap(TextureTarget.Texture2D);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            // With its mipmaps, a third more.
            _gpuBytes += (long)img.Width * img.Height * 4 * 4 / 3;
            lock (_textures)
            {
                _textures[file] = tex;
            }
        }
    }

    /// <summary>A loaded model's batches (or the stand-in box when there is no model), then its instances.</summary>
    private unsafe void UploadModel(ReadyModel r)
    {
        var g = r.Group;
        if (r.Model == null)
        {
            // No model copied for this kind: a box stands in, as in the web editor (spawn points and nests in their
            // markers' colours).
            var tint = g.Kind switch
            {
                ObjectKind.SpawnPoints => SpawnColour(false),
                ObjectKind.Nests => NestColour,
                _ => new Vector4(0.35f, 0.3f, 0.25f, 1),
            };
            AddBatch(g, BoxMesh(), 0, new ModelStore.MaterialData(tint, null, 0, false, new Vector4(1, 1, 0, 0)),
                Matrix4x4.CreateScale(1, 2, 1) * Matrix4x4.CreateTranslation(0, 1, 0));
        }
        else
        {
            foreach (var part in r.Model.Parts)
            {
                // The mesh was read with this model, or is already on the graphics card for another.
                var md = r.Meshes.GetValueOrDefault(part.Mesh);
                if (md == null && !_meshGl.ContainsKey(part.Mesh))
                {
                    continue;
                }
                var mesh = MeshGl(part.Mesh, md);
                if (part.Sub >= mesh.Counts.Length)
                {
                    continue;
                }
                AddBatch(g, mesh, part.Sub, r.Materials[part.Material], part.Matrix, _bothWays.Contains(part.Mesh), part.Mesh);
            }
        }
        lock (_groups)
        {
            g.Ready = true;
        }
        if (Scene is { } s)
        {
            Rebuild(s, g);
        }
        Interlocked.Decrement(ref _pending);
    }

    /// <summary>
    /// A mesh on the graphics card: its vertex buffer and one index buffer per part, uploaded the first time
    /// (<paramref name="md"/> may be null only when it is already there). The model store then forgets the mesh.
    /// </summary>
    private unsafe (uint Vbo, uint[] Ebos, int[] Counts) MeshGl(string id, ModelStore.MeshData? md)
    {
        if (_meshGl.TryGetValue(id, out var m))
        {
            return m;
        }
        // No vertex array bound: binding the index buffers below would rewire whichever one is (another
        // mesh's batch then draws with these triangles: shards everywhere).
        _gl.BindVertexArray(0);
        uint vbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = md!.Vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(md.Vertices.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        uint[] ebos = new uint[md.Submeshes.Length];
        int[] counts = new int[md.Submeshes.Length];
        for (int s = 0; s < ebos.Length; s++)
        {
            ebos[s] = _own.Buffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebos[s]);
            fixed (uint* p = md.Submeshes[s])
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(md.Submeshes[s].Length * 4), p, BufferUsageARB.StaticDraw);
            }
            counts[s] = md.Submeshes[s].Length;
        }
        _gpuBytes += md.Vertices.Length * 4L + md.Submeshes.Sum(x => x.Length * 4L);
        _onGpu[id] = true;
        if (md.BothWays)
        {
            _bothWays.Add(id);
        }
        float[] positions = new float[md.Vertices.Length / 8 * 3];
        for (int v = 0; v < positions.Length / 3; v++)
        {
            positions[v * 3] = md.Vertices[v * 8];
            positions[v * 3 + 1] = md.Vertices[v * 8 + 1];
            positions[v * 3 + 2] = md.Vertices[v * 8 + 2];
        }
        _pickMeshes[id] = new PickMesh(positions, md.Submeshes, md.Bounds);
        Models?.Forget(id);
        return _meshGl[id] = (vbo, ebos, counts);
    }

    /// <summary>The stand-in box's mesh: a unit cube centred on 0, flat normals per face.</summary>
    private (uint, uint[], int[]) BoxMesh()
    {
        const string Id = "\0box";
        if (_meshGl.TryGetValue(Id, out var m))
        {
            return m;
        }
        var verts = new List<float>();
        var idx = new List<uint>();
        // A unit cube centred on 0, flat normals per face.
        Vector3[] n = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        foreach (var f in n)
        {
            var u = MathF.Abs(f.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(f, u);
            uint b = (uint)(verts.Count / 8);
            foreach (var (a, c) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var p = (f + u * a + v * c) * 0.5f;
                verts.AddRange([p.X, p.Y, p.Z, f.X, f.Y, f.Z, 0f, 0f]);
            }
            idx.AddRange([b, b + 1, b + 2, b, b + 2, b + 3]);
        }
        return MeshGl(Id, new ModelStore.MeshData([.. verts], [[.. idx]]));
    }

    /// <summary>
    /// A batch for one part of a group's model: the mesh part, its material, and the part's matrix within the model;
    /// <paramref name="bothWays"/>: the mesh has every triangle wound both ways (see <see cref="Batch.BothWays"/>).
    /// </summary>
    private unsafe void AddBatch(Group g, (uint Vbo, uint[] Ebos, int[] Counts) mesh, int sub, ModelStore.MaterialData mat, Matrix4x4 pre, bool bothWays = false, string meshId = "")
    {
        var b = new Batch { Kind = g.Kind, Material = mat, IndexCount = mesh.Counts[sub], Instances = 0, BothWays = bothWays, MeshId = meshId, Sub = sub };
        lock (_textures)
        {
            b.Texture = mat.Map != null && _textures.TryGetValue(mat.Map, out uint t) ? t : 0;
        }
        b.Vao = _own.VertexArray();
        _gl.BindVertexArray(b.Vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, mesh.Vbo);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, (void*)12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, (void*)24);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, mesh.Ebos[sub]);
        b.InstanceVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        SetUpInstances();
        _gl.BindVertexArray(0);
        _batches.Add(b);
        lock (_groups)
        {
            g.Batches.Add((b, pre));
        }
    }
}
