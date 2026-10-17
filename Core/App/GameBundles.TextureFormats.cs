using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Crunched textures: how the game's compressed texture formats are recognised and decoded.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A crunched texture's level for <c>LoadTexture</c>: its data, whole, is one crn file holding every level; the
    /// level is picked from the crn file's own size and levels and unpacked to DXT1 or DXT5 blocks. Null when the crn
    /// file is broken, or made by a Unity older than 2017.3 (crunch's original format, not read).
    /// </summary>
    private TextureLevel? LoadCrunched(string bundle, AssetTypeValueField t, int maxSize)
    {
        byte[] crn = t["m_StreamData.size"].AsUInt > 0 ? StreamBytes(bundle, t["m_StreamData"]) : t["image data"].AsByteArray;
        if (!UnityCrunchFormat(Open(bundle).Assets.file.Metadata.UnityVersion))
        {
            return null;
        }
        // The level picked from the header read once (its CRCs cover the whole file).
        int w = 0, h = 0;
        int Pick(Crunch.Texture info)
        {
            int level = 0;
            (w, h) = (info.Width, info.Height);
            while (level < info.Levels - 1 && Math.Max(w, h) > maxSize)
            {
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
                level++;
            }
            return level;
        }
        return Crunch.Unpack(crn, Pick) is { } got ? new TextureLevel(t["m_Name"].AsString, got.Info.Dxt5 ? 12 : 10, w, h, got.Blocks) : null;
    }

    /// <summary>
    /// Whether a file of this Unity version crunches in Unity's format (2017.3 and later): true too when the version
    /// is not given (stripped bundles say 0.0.0).
    /// </summary>
    internal static bool UnityCrunchFormat(string unityVersion)
    {
        string[] parts = unityVersion.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor) || major == 0)
        {
            return true;
        }
        return major > 2017 || (major == 2017 && minor >= 3);
    }

    /// <summary>
    /// A Texture2DArray's GraphicsFormat as a TextureFormat (the compressed ones read here, and RGBA8); 0 (not read) for
    /// any other: the two enums number formats differently (GraphicsFormat 5, R8, is TextureFormat 5, ARGB32).
    /// </summary>
    private static int FromGraphicsFormat(int gf) => gf switch
    {
        96 or 97 => 10,
        98 or 99 => 11,
        100 or 101 => 12,
        102 or 103 => 26,
        104 or 105 => 27,
        106 or 107 => 24,
        108 or 109 => 25,
        8 or 4 => 4,
        _ => 0,
    };
}
