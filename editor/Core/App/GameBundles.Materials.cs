namespace TerrainEditor.App;

/// <summary>
/// Loading a material: its shader, textures, floats and colours.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A material's shader and saved properties, null when there is no such object.
    /// </summary>
    public Material? LoadMaterial(Where at)
    {
        lock (_sync)
        {
            try
            {
                if (Read(at.Bundle, at.PathId) is not { } m)
                {
                    return null;
                }
                string shader = "";
                var shaderAt = Resolve(at.Bundle, m["m_Shader"]);
                if (shaderAt is Where sh && Read(sh.Bundle, sh.PathId) is { } shf && Has(shf, "m_ParsedForm"))
                {
                    shader = shf["m_ParsedForm.m_Name"].AsString;
                }
                var props = m["m_SavedProperties"];
                var texs = new Dictionary<string, TexEnv>();
                foreach (var te in props["m_TexEnvs.Array"].Children)
                {
                    var s = te["second"];
                    texs[te["first"].AsString] = new TexEnv(Resolve(at.Bundle, s["m_Texture"]), s["m_Scale.x"].AsFloat, s["m_Scale.y"].AsFloat, s["m_Offset.x"].AsFloat, s["m_Offset.y"].AsFloat);
                }
                var floats = new Dictionary<string, float>();
                foreach (var f in props["m_Floats.Array"].Children)
                {
                    floats[f["first"].AsString] = f["second"].AsFloat;
                }
                var colors = new Dictionary<string, float[]>();
                foreach (var c in props["m_Colors.Array"].Children)
                {
                    var v = c["second"];
                    colors[c["first"].AsString] = [v["r"].AsFloat, v["g"].AsFloat, v["b"].AsFloat, v["a"].AsFloat];
                }
                return new Material(m["m_Name"].AsString, shader, shaderAt, texs, floats, colors);
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// Bytes per 4 x 4 block (block formats) or per pixel, by Unity TextureFormat; 0: not read here.
    /// </summary>
    public static (int Bytes, bool Block) FormatLayout(int format) => format switch
    {
        10 or 26 => (8, true),             // DXT1, BC4
        12 or 25 or 27 or 24 => (16, true), // DXT5, BC7, BC5, BC6H
        1 or 63 => (1, false),             // Alpha8, R8
        3 => (3, false),                   // RGB24
        4 or 5 or 14 => (4, false),        // RGBA32, ARGB32, BGRA32
        _ => (0, false),
    };

    /// <summary>
    /// The bytes of one mip level of a texture of this format and size (block formats: whole 4 x 4 blocks).
    /// </summary>
    public static int LevelSize(int format, int w, int h)
    {
        var (bytes, block) = FormatLayout(format);
        return block ? Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * bytes : w * h * bytes;
    }
}
