using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The drawing thread's upload of a read model: its textures, then its batches.
public sealed partial class GlView
{
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

    /// <summary>
    /// A loaded model's batches (or the stand-in box when there is no model, unless a mod's extension draws its things),
    /// then its instances.
    /// </summary>
    private unsafe void UploadModel(ReadyModel r)
    {
        var g = r.Group;
        if (r.Model == null)
        {
            // No model copied for this kind: a box stands in, as in the web editor (spawn points and nests in their
            // markers' colours), unless a mod's extension draws its things.
            var tint = g.Kind switch
            {
                ObjectKind.SpawnPoints => SpawnColour(false),
                ObjectKind.Nests => NestColour,
                _ => new Vector4(0.35f, 0.3f, 0.25f, 1),
            };
            if (!g.DrawnByShapes)
            {
                AddBatch(g, BoxMesh(), 0, new ModelStore.MaterialData(tint, null, 0, false, new Vector4(1, 1, 0, 0)),
                    Matrix4x4.CreateScale(1, 2, 1) * Matrix4x4.CreateTranslation(0, 1, 0));
            }
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
        // A selected object of this model, boxed while it had none, is outlined now.
        _selectionDirty |= Selected.Count > 0;
        Interlocked.Decrement(ref _pending);
    }
}
