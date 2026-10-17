namespace TerrainEditor.App;

/// <summary>
/// The records the game's files are read into: where an object is, a prefab's model, meshes, materials and textures.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Where an object is: a bundle and the object's path id in that bundle's assets file.
    /// </summary>
    /// <param name="Bundle">The bundle's file name in the bundles folder.</param>
    /// <param name="PathId">The object's m_PathID in the bundle.</param>
    public sealed record Where(string Bundle, long PathId)
    {
        /// <summary>
        /// "bundle:pathId", as kept in the index file.
        /// </summary>
        public string Key => $"{Bundle}:{PathId}";

        /// <summary>
        /// The reverse of <c>Key</c>; null when the text is not one.
        /// </summary>
        public static Where? FromKey(string key)
        {
            int c = key.LastIndexOf(':');
            return c > 0 && long.TryParse(key.AsSpan(c + 1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out long pid) ? new Where(key[..c], pid) : null;
        }
    }

    /// <summary>
    /// A model's part: a submesh of a mesh with a material, placed by a matrix (row-major 4 x 4, Unity's left-handed
    /// space, relative to the prefab's root).
    /// </summary>
    /// <param name="Mesh">The mesh.</param>
    /// <param name="Sub">The submesh's index in it.</param>
    /// <param name="Material">The renderer's material for that submesh, null when it has none that resolves.</param>
    /// <param name="Matrix">The part's placement (see <c>Matrix</c>).</param>
    public sealed record Part(Where Mesh, int Sub, Where? Material, float[] Matrix);

    /// <summary>
    /// A prefab's model as <c>LoadModel</c> reads it.
    /// </summary>
    /// <param name="Name">The prefab name.</param>
    /// <param name="Parts">What is drawn, relative to the root.</param>
    /// <param name="RootScale">The root's own local scale (x, y, z), left out of the parts' matrices.</param>
    public sealed record Model(string Name, List<Part> Parts, float[] RootScale);

    /// <summary>
    /// A mesh's vertices and triangles, as the game has them (left-handed).
    /// </summary>
    /// <param name="Name">The mesh's name.</param>
    /// <param name="VertexCount">How many vertices there are.</param>
    /// <param name="Positions">Three floats per vertex.</param>
    /// <param name="Normals">Three floats per vertex, null when the mesh has none.</param>
    /// <param name="Uv">Two floats per vertex (the first UV channel), null when the mesh has none.</param>
    /// <param name="Submeshes">Per submesh, its triangles' vertex indices (base vertex added); empty for lines or
    /// points.</param>
    public sealed record Mesh(string Name, int VertexCount, float[] Positions, float[]? Normals, float[]? Uv, int[][] Submeshes);

    /// <summary>
    /// One texture slot of a material.
    /// </summary>
    /// <param name="Texture">The texture, null when the slot is empty.</param>
    /// <param name="ScaleX">The UV tiling along x.</param>
    /// <param name="ScaleY">The UV tiling along y.</param>
    /// <param name="OffsetX">The UV offset along x.</param>
    /// <param name="OffsetY">The UV offset along y.</param>
    public sealed record TexEnv(Where? Texture, float ScaleX, float ScaleY, float OffsetX, float OffsetY);

    /// <summary>
    /// A material's saved properties.
    /// </summary>
    /// <param name="Name">The material's name.</param>
    /// <param name="Shader">The shader's name (its parsed form's), empty when it cannot be read.</param>
    /// <param name="ShaderAt">Where the shader is, for <c>LoadShader</c>.</param>
    /// <param name="Textures">Texture slots by property name (_MainTex...).</param>
    /// <param name="Floats">Float properties by name.</param>
    /// <param name="Colors">Colour properties by name, as r, g, b, a.</param>
    public sealed record Material(string Name, string Shader, Where? ShaderAt, Dictionary<string, TexEnv> Textures, Dictionary<string, float> Floats, Dictionary<string, float[]> Colors);

    /// <summary>
    /// One mip level of a texture, as stored (bottom row first).
    /// </summary>
    /// <param name="Name">The texture's name ("name[i]" for slice i of a texture array).</param>
    /// <param name="Format">Unity's TextureFormat (see <c>FormatLayout</c>).</param>
    /// <param name="Width">The level's width in pixels.</param>
    /// <param name="Height">The level's height in pixels.</param>
    /// <param name="Data">The level's bytes, <c>LevelSize</c> of them.</param>
    public sealed record TextureLevel(string Name, int Format, int Width, int Height, byte[] Data);

    /// <summary>
    /// What a prefab's root has.
    /// </summary>
    /// <param name="Networked">A ZNetView: a saved, networked object.</param>
    /// <param name="Creature">A Character: a creature.</param>
    /// <param name="Item">An ItemDrop: an item.</param>
    /// <param name="Location">A Location: a place the world generator puts (a ruin, a camp, an altar...).</param>
    public sealed record Kind(bool Networked, bool Creature, bool Item, bool Location = false);
}
