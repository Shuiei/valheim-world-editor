using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The materials and textures of the models, as the store hands them to the renderer.
/// </summary>
public sealed partial class ModelStore
{
    /// <summary>
    /// A material by id; plain white and opaque when it could not be read.
    /// </summary>
    public MaterialData Material(string id)
    {
        try
        {
            return _source.Material(id);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.CaughtBrief(e, $"reading the material {id}");
            return new MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
        }
    }

    /// <summary>
    /// A texture's pixels, RGBA, the bottom row first (as Unity keeps them); null when it could not be read.
    /// </summary>
    public ImageData? LoadTexture(string id)
    {
        try
        {
            return _source.Texture(id);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.CaughtBrief(e, $"reading the texture {id}");
            return null;
        }
    }
}
