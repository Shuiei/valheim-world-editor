using System.Collections.Concurrent;
using System.Numerics;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The objects' models: grouped by kind, read on worker threads, uploaded by the drawing thread (meshes,
// textures, batches), and let go of when the graphics card's budget is past. The steps are in GlView.Models.*.cs.
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

    /// <summary>
    /// How many models are read at once (half the processors, at least two). Static: one 3D view lives as long as
    /// the app (and an instance field would make the view disposable).
    /// </summary>
    private static readonly SemaphoreSlim LoadGate = new(Math.Max(2, Environment.ProcessorCount / 2));
    /// <summary>Guards the per-thing arrays (boxes, kinds), which the loaders and the drawing thread both use.</summary>
    private readonly Lock _objLock = new();

    /// <summary>Textures a loader has taken to read (guarded by <c>_textures</c>), so no other reads them too.</summary>
    private readonly HashSet<string> _claimed = [];
}
