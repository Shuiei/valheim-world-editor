using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ModelStore
{
    /// <summary>
    /// A mesh by id, read once (with its winding turned around for the right-handed view) and kept until <c>Forget</c>;
    /// null when there is none or it could not be read.
    /// </summary>
    public MeshData? LoadMesh(string id) => _meshes.GetOrAdd(id, i => new Lazy<MeshData?>(() => ReadMesh(i))).Value;

    /// <summary>
    /// Reads one mesh from the source, turns its winding around and remembers its box.
    /// </summary>
    /// <param name="id">The mesh's id.</param>
    /// <returns>The mesh, or null when there is none or it could not be read.</returns>
    internal MeshData? ReadMesh(string id)
    {
        MeshData? md;
        try
        {
            md = _source.Mesh(id);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.CaughtBrief(e, $"reading the mesh {id}");
            return null;
        }
        if (md == null)
        {
            return null;
        }
        // Mirroring z (left- to right-handed) turns Unity's front faces into back faces: swap two corners.
        foreach (uint[] idx in md.Submeshes)
        {
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                (idx[t + 1], idx[t + 2]) = (idx[t + 2], idx[t + 1]);
            }
        }
        _bounds[id] = md.Bounds;
        return md;
    }

    /// <summary>
    /// A mesh's box, if it was read before (null: read it with LoadMesh).
    /// </summary>
    public (Vector3 Min, Vector3 Max)? BoundsOf(string id) => _bounds.TryGetValue(id, out var b) ? b : null;

    /// <summary>
    /// The mesh's vertices are not needed any more (they are on the graphics card): let go, its box kept. Read again if
    /// asked again.
    /// </summary>
    public void Forget(string id) => _meshes.TryRemove(id, out _);
}
