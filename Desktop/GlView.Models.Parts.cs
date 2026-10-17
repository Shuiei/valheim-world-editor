using System.Numerics;

namespace TerrainEditor.Desktop;

// Reading the parts of a model on a worker thread: each part's mesh (unless it is on the graphics card), its
// material and its textures, and the box round them.
public sealed partial class GlView
{
    /// <summary>
    /// The model's parts' meshes (those not on the graphics card yet), materials and textures (the textures this
    /// loader may read are claimed first), and the box round all of them.
    /// </summary>
    /// <param name="model">The model read.</param>
    /// <param name="meshes">The meshes read, by name.</param>
    /// <param name="mats">The materials read, by name.</param>
    /// <param name="images">The textures read, by file.</param>
    /// <param name="claimed">The textures claimed, by file.</param>
    /// <returns>The box of the model's parts, in the model's frame.</returns>
    private (Vector3 Min, Vector3 Max) ReadParts(ModelStore.Model model, Dictionary<string, ModelStore.MeshData> meshes,
        Dictionary<string, ModelStore.MaterialData> mats, Dictionary<string, ModelStore.ImageData?> images, List<string> claimed)
    {
        Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
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
                if (mat.Map != null && !images.ContainsKey(mat.Map) && ClaimTexture(mat.Map))
                {
                    claimed.Add(mat.Map);
                    images[mat.Map] = Models.LoadTexture(mat.Map);
                }
            }
        }
        return (lo, hi);
    }
}
