using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// A blueprint's picture: its building seen from above at an angle, drawn on the CPU (no window needed)
/// with a depth buffer. Pieces are the game's models with their textures when the game's look has been
/// copied (ModelStore, placed as GlView places them), else their colliders' boxes in their material's
/// colour (Stability's shapes), else a stand-in box. Transparent around the building; drawn twice the
/// size, then halved, for smooth edges. Homestead shows the .png in its hammer tab.
/// </summary>
public static class BuildingPicture
{
    /// <summary>One piece to draw.</summary>
    /// <param name="Name">The prefab name.</param>
    /// <param name="Position">Where it stands, in metres (any origin: the picture is fitted around the pieces).</param>
    /// <param name="Euler">Its rotation, Unity's Euler degrees.</param>
    /// <param name="Scale">Its scale; 0 or less: the model's own (1 for boxes).</param>
    public sealed record Piece(string Name, Vector3 Position, Vector3 Euler, float Scale);

    /// <summary>The pieces of a copy (clipboard format: dx, dy, dz from the anchor; Unity's Euler degrees).</summary>
    public static List<Piece> FromClip(JsonObject clip) => [.. (clip["objects"] as JsonArray ?? []).OfType<JsonObject>()
        .Select(o => new Piece((string?)o["name"] ?? "", new Vector3(F(o["dx"]), F(o["dy"]), F(o["dz"])), new Vector3(F(o["rx"]), F(o["ry"]), F(o["rz"])), F(o["scale"])))
        .Where(p => p.Name.Length > 0)];

    /// <summary>A JSON number as a float, 0 when missing.</summary>
    private static float F(JsonNode? n) => n == null ? 0 : float.Parse(n.ToJsonString(), CultureInfo.InvariantCulture);

    /// <summary>Colours (sRGB, 0 to 255) of WearNTear's materials: wood, stone, iron, hardwood, marble, ashstone, ancient, ice, timber.</summary>
    private static readonly Vector3[] MaterialColours =
    [
        new(152, 110, 70), new(140, 138, 132), new(92, 92, 98), new(118, 84, 54), new(205, 205, 210),
        new(82, 76, 76), new(112, 100, 88), new(170, 210, 230), new(140, 100, 64),
    ];

    /// <summary>A triangle to draw, in view space.</summary>
    private struct Tri : IEquatable<Tri>
    {
        /// <summary>The corners.</summary>
        public Vector3 A, B, C;
        /// <summary>The corners' texture coordinates (only used with a texture).</summary>
        public Vector2 Ua, Ub, Uc;
        /// <summary>The colour, sRGB 0 to 255; multiplied by the texture when there is one.</summary>
        public Vector3 Colour;
        /// <summary>The material's texture, null for a plain colour.</summary>
        public ModelStore.ImageData? Texture;
        /// <summary>The material's texture scale (x, y) and offset (z, w).</summary>
        public Vector4 UvTransform;
        /// <summary>The alpha cutoff, 0 to 1: texels less opaque are not drawn (0: none).</summary>
        public float Cutoff;
        /// <summary>
        /// A see-through material's alpha (times its texture's), blended over what is drawn before it; 0: opaque.
        /// </summary>
        public float Alpha;

        /// <summary>Equal when every field is equal (the texture by its own equality).</summary>
        public readonly bool Equals(Tri other) => A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C) && Ua.Equals(other.Ua) && Ub.Equals(other.Ub) && Uc.Equals(other.Uc) && Colour.Equals(other.Colour) && EqualityComparer<ModelStore.ImageData?>.Default.Equals(Texture, other.Texture) && UvTransform.Equals(other.UvTransform) && Cutoff.Equals(other.Cutoff) && Alpha.Equals(other.Alpha);

        /// <summary>Equal to another triangle with the same fields.</summary>
        public override readonly bool Equals(object? obj) => obj is Tri t && Equals(t);

        /// <summary>A hash of the fields, equal for equal triangles.</summary>
        public override readonly int GetHashCode() => HashCode.Combine(HashCode.Combine(A, B, C, Ua), HashCode.Combine(Ub, Uc, Colour, Texture), HashCode.Combine(UvTransform, Cutoff, Alpha));
    }

    /// <summary>A PNG of the pieces, size × size pixels.</summary>
    /// <param name="pieces">The pieces.</param>
    /// <param name="models">The game's models, null: boxes only.</param>
    /// <param name="size">The picture's width and height in pixels.</param>
    public static byte[] Draw(IReadOnlyList<Piece> pieces, ModelStore? models, int size = 256)
    {
        var tris = new List<Tri>();
        var textures = new Dictionary<string, ModelStore.ImageData?>();
        foreach (var p in pieces)
        {
            if (!(models != null && AddModel(tris, p, models, textures)))
            {
                AddBoxes(tris, p);
            }
        }
        return Render(tris, size);
    }

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

    /// <summary>The piece's colliders as boxes in its material's colour (a stand-in box when the game has none).</summary>
    private static void AddBoxes(List<Tri> tris, Piece p)
    {
        var (material, boxes) = Stability.Shape(p.Name);
        var colour = material >= 0 && material < MaterialColours.Length ? MaterialColours[material] : new Vector3(130, 130, 130);
        if (boxes.Count == 0)
        {
            boxes.Add((new Vector3(0, 1, 0), new Vector3(0.5f, 1, 0.5f), Quaternion.Identity));
        }
        var rot = BlueprintFormats.FromEuler(p.Euler);
        float s = p.Scale > 0 ? p.Scale : 1;
        foreach (var (c, h, q) in boxes)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var local = c + Vector3.Transform(new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z), q);
                var w = p.Position + Vector3.Transform(local * s, rot);
                corners[i] = new Vector3(w.X, w.Y, -w.Z);
            }
            // The six faces, two triangles each.
            int[][] faces = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];
            foreach (int[] f in faces)
            {
                tris.Add(new Tri { A = corners[f[0]], B = corners[f[1]], C = corners[f[2]], Colour = colour });
                tris.Add(new Tri { A = corners[f[0]], B = corners[f[2]], C = corners[f[3]], Colour = colour });
            }
        }
    }

    /// <summary>
    /// Draws the triangles at twice the size, seen from the south-east, orthographic and fitted to the picture, with
    /// simple lighting, then halves it into a PNG.
    /// </summary>
    private static byte[] Render(List<Tri> tris, int size)
    {
        int n = size * 2;
        byte[] rgba = new byte[n * n * 4];
        if (tris.Count > 0)
        {
            // Seen from the south-east, 35° above (view space), orthographic.
            var eye = Vector3.Normalize(new Vector3(1f, 0.8f, 1.2f));
            var view = Matrix4x4.CreateLookAt(eye * 1000f, Vector3.Zero, Vector3.UnitY);
            var light = Vector3.Normalize(new Vector3(0.5f, 1f, 0.8f));
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            var cam = new (Vector3 A, Vector3 B, Vector3 C)[tris.Count];
            for (int i = 0; i < tris.Count; i++)
            {
                var t = tris[i];
                cam[i] = (Vector3.Transform(t.A, view), Vector3.Transform(t.B, view), Vector3.Transform(t.C, view));
                foreach (var v in new[] { cam[i].A, cam[i].B, cam[i].C })
                {
                    x0 = MathF.Min(x0, v.X); x1 = MathF.Max(x1, v.X); y0 = MathF.Min(y0, v.Y); y1 = MathF.Max(y1, v.Y);
                }
            }
            float scale = n * 0.92f / MathF.Max(MathF.Max(x1 - x0, y1 - y0), 0.5f), mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            Vector3 Screen(Vector3 c) => new(n / 2f + (c.X - mx) * scale, n / 2f - (c.Y - my) * scale, -c.Z);
            float[] depth = new float[n * n];
            Array.Fill(depth, float.MaxValue);
            // Opaque triangles first, then the see-through ones over them.
            foreach (int i in Enumerable.Range(0, tris.Count).OrderBy(i => tris[i].Alpha > 0))
            {
                var t = tris[i];
                var normal = Vector3.Cross(t.B - t.A, t.C - t.A);
                if (normal.LengthSquared() < 1e-12f)
                {
                    continue;
                }
                normal = Vector3.Normalize(normal);
                // Either side lit as its side facing the eye.
                if (Vector3.Dot(normal, eye) < 0)
                {
                    normal = -normal;
                }
                float shade = 0.62f + 0.68f * MathF.Max(0, Vector3.Dot(normal, light));
                Fill(Screen(cam[i].A), Screen(cam[i].B), Screen(cam[i].C), t, shade, n, depth, rgba);
            }
        }
        using var big = new SKBitmap(new SKImageInfo(n, n, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, big.GetPixels(), rgba.Length);
        using var small = big.Resize(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var image = SKImage.FromBitmap(small);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    /// <summary>
    /// Rasterises one triangle (screen space, z the depth) into the picture, with the depth test, the texture and
    /// its cutoff, times the shade; a see-through one blended over what is there, leaving the depth as it is.
    /// </summary>
    private static void Fill(Vector3 a, Vector3 b, Vector3 c, Tri t, float shade, int n, float[] depth, byte[] rgba)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (MathF.Abs(area) < 1e-6f)
        {
            return;
        }
        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int maxX = Math.Min(n - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int maxY = Math.Min(n - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        var tex = t.Texture;
        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float wa = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / area;
                float wb = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / area;
                float wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0)
                {
                    continue;
                }
                float z = wa * a.Z + wb * b.Z + wc * c.Z;
                int at = y * n + x;
                if (z >= depth[at])
                {
                    continue;
                }
                var colour = t.Colour;
                float alpha = t.Alpha;
                if (tex != null)
                {
                    var uv = wa * t.Ua + wb * t.Ub + wc * t.Uc;
                    float u = uv.X * t.UvTransform.X + t.UvTransform.Z, v = uv.Y * t.UvTransform.Y + t.UvTransform.W;
                    int tx = (int)((u - MathF.Floor(u)) * tex.Width) % tex.Width, ty = (int)((v - MathF.Floor(v)) * tex.Height) % tex.Height;
                    int ti = (ty * tex.Width + tx) * 4;
                    if (t.Cutoff > 0 && tex.Rgba[ti + 3] < t.Cutoff * 255)
                    {
                        continue;
                    }
                    colour = new Vector3(tex.Rgba[ti] * colour.X, tex.Rgba[ti + 1] * colour.Y, tex.Rgba[ti + 2] * colour.Z) / 255f;
                    alpha *= tex.Rgba[ti + 3] / 255f;
                }
                colour *= shade;
                if (t.Alpha > 0)
                {
                    // Over what is there (the background is clear): the alphas added up, the colours mixed by them.
                    float below = rgba[at * 4 + 3] / 255f * (1 - alpha), total = alpha + below;
                    if (total <= 0)
                    {
                        continue;
                    }
                    var under = new Vector3(rgba[at * 4], rgba[at * 4 + 1], rgba[at * 4 + 2]);
                    colour = (colour * alpha + under * below) / total;
                    rgba[at * 4] = (byte)Math.Clamp(colour.X, 0, 255);
                    rgba[at * 4 + 1] = (byte)Math.Clamp(colour.Y, 0, 255);
                    rgba[at * 4 + 2] = (byte)Math.Clamp(colour.Z, 0, 255);
                    rgba[at * 4 + 3] = (byte)Math.Clamp(total * 255, 0, 255);
                    continue;
                }
                depth[at] = z;
                rgba[at * 4] = (byte)Math.Clamp(colour.X, 0, 255);
                rgba[at * 4 + 1] = (byte)Math.Clamp(colour.Y, 0, 255);
                rgba[at * 4 + 2] = (byte)Math.Clamp(colour.Z, 0, 255);
                rgba[at * 4 + 3] = 255;
            }
        }
    }
}
