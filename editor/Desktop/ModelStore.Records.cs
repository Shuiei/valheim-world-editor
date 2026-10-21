using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class ModelStore
{
    /// <summary>
    /// One part of a model: a submesh drawn with a material.
    /// </summary>
    /// <param name="Mesh">The mesh's id.</param>
    /// <param name="Sub">Which of the mesh's submeshes.</param>
    /// <param name="Material">The material's id.</param>
    /// <param name="Matrix">The part's placement in the model, in the view's right-handed space.</param>
    public sealed record Part(string Mesh, int Sub, string Material, Matrix4x4 Matrix);
    /// <summary>
    /// A kind's model: its parts and its root's scale.
    /// </summary>
    /// <param name="Name">The model name it was asked by.</param>
    /// <param name="Parts">Its parts, never empty when loaded through the store.</param>
    /// <param name="RootScale">The prefab root's scale; used when a placed object has no scale of its own.</param>
    public sealed record Model(string Name, List<Part> Parts, Vector3 RootScale);
    /// <summary>
    /// A mesh's vertices and triangles.
    /// </summary>
    /// <param name="Vertices">Interleaved position, normal, uv: 8 floats per vertex.</param>
    /// <param name="Submeshes">Each submesh's triangle indices, three per triangle.</param>
    public sealed record MeshData(float[] Vertices, uint[][] Submeshes)
    {
        /// <summary>
        /// Every triangle is there wound both ways (Unity's primitives, made to show whatever their material culls):
        /// drawn with back faces culled, it still shows from both sides, each side once (twice, a see-through material
        /// would look twice as thick).
        /// </summary>
        public bool BothWays { get; init; }

        /// <summary>
        /// The mesh's box (its vertices' smallest and largest coordinates).
        /// </summary>
        public (Vector3 Min, Vector3 Max) Bounds { get; } = BoundsOf(Vertices);

        /// <summary>
        /// The smallest and largest vertex positions; zero for a mesh without a whole vertex.
        /// </summary>
        private static (Vector3, Vector3) BoundsOf(float[] v)
        {
            if (v.Length < 8)
            {
                return (Vector3.Zero, Vector3.Zero);
            }
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            for (int i = 0; i + 2 < v.Length; i += 8)
            {
                var p = new Vector3(v[i], v[i + 1], v[i + 2]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return (min, max);
        }
    }
    /// <summary>
    /// How a part is coloured.
    /// </summary>
    /// <param name="Color">RGBA, the colour in linear space (alpha as is).</param>
    /// <param name="Map">The texture's id, or null for none.</param>
    /// <param name="Cutoff">Alpha below which pixels are dropped; 0 draws them all.</param>
    /// <param name="DoubleSided">Draw both faces.</param>
    /// <param name="UvTransform">Texture scale (x, y) then offset (z, w), applied as uv * scale + offset.</param>
    /// <param name="Transparent">See-through (glass, water): blended with what is behind it by its alpha (the colour's
    /// times the texture's), after the opaque objects; false: opaque, or cut out at <paramref name="Cutoff"/>.</param>
    /// <param name="Foliage">Drawn with the game's vegetation or grass shaders (leaves, bushes, grass, cloth that sways like them):
    /// a click counts it only where nothing solid is behind it along the ray.</param>
    public sealed record MaterialData(Vector4 Color, string? Map, float Cutoff, bool DoubleSided, Vector4 UvTransform, bool Transparent = false, bool Foliage = false);
    /// <summary>
    /// A texture's pixels.
    /// </summary>
    /// <param name="Width">In pixels.</param>
    /// <param name="Height">In pixels.</param>
    /// <param name="Rgba">Four bytes per pixel, row by row.</param>
    public sealed record ImageData(int Width, int Height, byte[] Rgba);
}
