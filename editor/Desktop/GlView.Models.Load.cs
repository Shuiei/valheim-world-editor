using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Reading a group's model on a worker thread. The drawing thread sends it to the graphics card once it is read.
public sealed partial class GlView
{
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
                ReadGroupModel(g, images, claimed);
            }
            catch (Exception ex)
            {
                FallBackToBox(g, ex, images, claimed);
            }
            finally
            {
                LoadGate.Release();
            }
            Wake();
        });
    }

    /// <summary>
    /// Reads a group's model (its parts' meshes, materials and textures), gives the group its box and the root scale,
    /// and hands the model to the drawing thread.
    /// </summary>
    /// <param name="g">The group.</param>
    /// <param name="images">The textures read by this loader, by file.</param>
    /// <param name="claimed">The textures this loader claimed, to be let go if it fails.</param>
    private void ReadGroupModel(Group g, Dictionary<string, ModelStore.ImageData?> images, List<string> claimed)
    {
        string? name = NameOf(g.Key.Prefab);
        var model = name != null && Models != null && _gameLookOn ? Models.LoadModel(name) : null;
        var meshes = new Dictionary<string, ModelStore.MeshData>();
        var mats = new Dictionary<string, ModelStore.MaterialData>();
        var (lo, hi) = model != null ? ReadParts(model, meshes, mats, images, claimed) : (new Vector3(float.MaxValue), new Vector3(float.MinValue));
        var shapes = lo.X > hi.X ? ShapesBox(g.Key.Prefab) : null;
        if (lo.X > hi.X)
        {
            // No model: the box of what a mod's extension draws of it, else the stand-in box.
            (lo, hi) = shapes ?? StandInBox;
        }
        lock (_groups)
        {
            g.RootScale = model?.RootScale ?? Vector3.One;
            g.HasModel = model != null;
            g.DrawnByShapes = shapes != null;
            g.Box = (lo, hi);
        }
        _ready.Enqueue(new ReadyModel(g, model, meshes, mats, images));
    }

    /// <summary>
    /// The box, in its own frame, of the shapes the running extensions draw for a thing of a kind with none of its data
    /// (ModExtensions.ShapesOf); null when they draw none (or only in the world's frame, reaching other objects).
    /// </summary>
    private static (Vector3 Min, Vector3 Max)? ShapesBox(int prefab)
    {
        if (!ModExtensions.HasShapes(prefab))
        {
            return null;
        }
        var view = new ObjectView(Terrain.PrefabCatalog.NameOf(prefab) ?? "", Vector3.Zero, Vector3.Zero, new Save.ZdoData { Prefab = prefab });
        // In the frame models are read in: z mirrored, as the game's meshes are (Unity is left-handed).
        var corners = ModExtensions.ShapesOf(prefab, view, ModExtensions.NoWorld).Where(s => !s.WorldSpace).SelectMany(s => s.Vertices)
            .Select(v => v with { Z = -v.Z }).ToList();
        return corners.Count == 0 ? null
            : (corners.Aggregate(Vector3.Min), corners.Aggregate(Vector3.Max));
    }

    /// <summary>
    /// A model that could not be read (the store turns a mesh, material or texture that cannot be read into none;
    /// what still fails is said): the kind's things get the stand-in box rather than nothing at all, so they are
    /// drawn, picked and their ghosts shown. The textures read go up with it; those claimed and not read are let go,
    /// for another loader to read.
    /// </summary>
    /// <param name="g">The group.</param>
    /// <param name="ex">What failed.</param>
    /// <param name="images">The textures read, by file.</param>
    /// <param name="claimed">The textures this loader claimed.</param>
    private void FallBackToBox(Group g, Exception ex, Dictionary<string, ModelStore.ImageData?> images, List<string> claimed)
    {
        Log.Caught(ex, $"loading the model of {g.Key.Prefab} (drawn as a box)");
        Status?.Invoke(Said.Of("world.msg.modelFailed", g.Key.Prefab, ex.Message));
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
}
