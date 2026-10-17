using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop;

/// <summary>
/// A blueprint's picture: its building seen from above at an angle, drawn on the CPU (no window needed)
/// with a depth buffer. Pieces are the game's models with their textures when the game's look has been
/// copied (ModelStore, placed as GlView places them), else their colliders' boxes in their material's
/// colour (Stability's shapes), else a stand-in box. Transparent around the building; drawn twice the
/// size, then halved, for smooth edges. Homestead shows the .png in its hammer tab.
/// </summary>
public static partial class BuildingPicture
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
    internal struct Tri : IEquatable<Tri>
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
}
