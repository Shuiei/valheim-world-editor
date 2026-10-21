using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Loading a texture array: each layer as a texture level.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// The top level of each slice of a texture array (bottom row first). Null when the object is not a Texture2DArray
    /// or its format is not read here.
    /// </summary>
    public List<TextureLevel>? LoadTextureArray(Where at)
    {
        lock (_sync)
        {
            try
            {
                if (Read(at.Bundle, at.PathId) is not { } t || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Texture2DArray)
                {
                    return null;
                }
                int format = FromGraphicsFormat(t["m_Format"].AsInt), w = t["m_Width"].AsInt, h = t["m_Height"].AsInt, depth = t["m_Depth"].AsInt;
                byte[] data = t["image data"].AsByteArray;
                if (data.Length == 0 && t["m_StreamData.size"].AsUInt > 0)
                {
                    data = StreamBytes(at.Bundle, t["m_StreamData"]);
                }
                int size = LevelSize(format, w, h);
                if (depth <= 0 || FormatLayout(format).Bytes == 0 || data.Length / depth < size)
                {
                    return null;
                }
                // Slice after slice, each with its mip levels.
                int per = data.Length / depth;
                return [.. Enumerable.Range(0, depth).Select(i => new TextureLevel($"{t["m_Name"].AsString}[{i}]", format, w, h, data.AsSpan(i * per, size).ToArray()))];
            }
            finally
            {
                _fields.Clear();
            }
        }
    }
}
