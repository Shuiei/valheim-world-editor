using System.Numerics;

namespace TerrainEditor.Desktop;

public static partial class BuildingPicture
{
    /// <summary>The game's model, placed as GlView.Placement does (view space: z mirrored).</summary>
    /// <returns>Whether the model was found and gave triangles (otherwise boxes are drawn).</returns>
    private static bool AddModel(List<Tri> tris, Piece p, ModelStore models, Dictionary<string, ModelStore.ImageData?> textures)
    {
        if (models.LoadModel(p.Name) is not { } model)
        {
            return false;
        }
        const float D = MathF.PI / 180f;
        var q = Quaternion.CreateFromYawPitchRoll(p.Euler.Y * D, p.Euler.X * D, p.Euler.Z * D);
        q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
        var scale = p.Scale > 0 ? new Vector3(p.Scale) : model.RootScale;
        var place = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(p.Position.X, p.Position.Y, -p.Position.Z);
        int before = tris.Count;
        foreach (var part in model.Parts)
        {
            if (models.LoadMesh(part.Mesh) is not { } mesh || part.Sub >= mesh.Submeshes.Length)
            {
                continue;
            }
            var mat = models.Material(part.Material);
            ModelStore.ImageData? tex = null;
            if (mat.Map != null && !textures.TryGetValue(mat.Map, out tex))
            {
                textures[mat.Map] = tex = models.LoadTexture(mat.Map);
            }
            var m = part.Matrix * place;
            var colour = new Vector3(MathF.Pow(mat.Color.X, 1 / 2.2f), MathF.Pow(mat.Color.Y, 1 / 2.2f), MathF.Pow(mat.Color.Z, 1 / 2.2f)) * 255f;
            float[] v = mesh.Vertices;
            uint[] idx = mesh.Submeshes[part.Sub];
            Vector3 P(uint i) => Vector3.Transform(new Vector3(v[i * 8], v[i * 8 + 1], v[i * 8 + 2]), m);
            Vector2 U(uint i) => new(v[i * 8 + 6], v[i * 8 + 7]);
            // A mesh wound both ways has each triangle twice: once is enough (no faces are culled here), and see-through
            // twice would look twice as thick.
            var once = new HashSet<(uint, uint, uint)>();
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                if (mesh.BothWays && !once.Add(Corners(idx[t], idx[t + 1], idx[t + 2])))
                {
                    continue;
                }
                tris.Add(new Tri
                {
                    A = P(idx[t]),
                    B = P(idx[t + 1]),
                    C = P(idx[t + 2]),
                    Ua = U(idx[t]),
                    Ub = U(idx[t + 1]),
                    Uc = U(idx[t + 2]),
                    Colour = colour,
                    Texture = tex,
                    UvTransform = mat.UvTransform,
                    Cutoff = mat.Cutoff,
                    // As the view draws it: never fully clear (a material file may say 0).
                    Alpha = mat.Transparent ? Math.Max(mat.Color.W, BundleModels.LeastAlpha) : 0,
                });
            }
        }
        return tris.Count > before;
    }

    /// <summary>A triangle's corners in order, whichever way it is wound.</summary>
    private static (uint, uint, uint) Corners(uint a, uint b, uint c) =>
        (Math.Min(a, Math.Min(b, c)), Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c)), Math.Max(a, Math.Max(b, c)));
}
