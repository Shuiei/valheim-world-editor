using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Loading a texture: its levels, in the formats the game uses.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Unity's crunched TextureFormats, DXT1Crunched and DXT5Crunched: read with <see cref="Crunch"/>, given as DXT1
    /// and DXT5 levels.
    /// </summary>
    private const int Dxt1Crunched = 28, Dxt5Crunched = 29;

    /// <summary>
    /// The largest mip level no wider or taller than maxSize (the smallest there is when none is). Null when the object
    /// is not a Texture2D or its format is not one <c>FormatLayout</c> reads or a crunched one (DXT1 or DXT5 then).
    /// </summary>
    public TextureLevel? LoadTexture(Where at, int maxSize)
    {
        lock (_sync)
        {
            try
            {
                if (Read(at.Bundle, at.PathId) is not { } t || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Texture2D)
                {
                    return null;
                }
                int format = t["m_TextureFormat"].AsInt, w = t["m_Width"].AsInt, h = t["m_Height"].AsInt, mips = Math.Max(1, t["m_MipCount"].AsInt);
                if (format is Dxt1Crunched or Dxt5Crunched)
                {
                    return LoadCrunched(at.Bundle, t, maxSize);
                }
                if (FormatLayout(format).Bytes == 0 || w <= 0 || h <= 0)
                {
                    return null;
                }
                long skip = 0;
                int level = 0;
                while (level < mips - 1 && Math.Max(w, h) > maxSize)
                {
                    skip += LevelSize(format, w, h);
                    w = Math.Max(1, w / 2);
                    h = Math.Max(1, h / 2);
                    level++;
                }
                int size = LevelSize(format, w, h);
                byte[] data;
                if (t["m_StreamData.size"].AsUInt > 0)
                {
                    data = StreamBytes(at.Bundle, t["m_StreamData"], skip, size);
                }
                else
                {
                    byte[] all = t["image data"].AsByteArray;
                    if (skip + size > all.Length)
                    {
                        return null;
                    }
                    data = all.AsSpan((int)skip, size).ToArray();
                }
                return data.Length == size ? new TextureLevel(t["m_Name"].AsString, format, w, h, data) : null;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }
}
