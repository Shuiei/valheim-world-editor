namespace TerrainEditor.App;

/// <summary>
/// The game's look besides models, read from its bundles (GameBundles): the terrain's shader and
/// textures (its material Heightmap_basematerial and two texture arrays), and the world map's textures
/// (material "minimap"). What the Python copy made as files (game-look/terrain, game-look/maptex).
/// </summary>
public static class GameLookData
{
    /// <summary>
    /// Pixels, RGBA, rows in the order asked (Unity keeps them bottom first).
    /// </summary>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels (all slices, for a texture array).</param>
    /// <param name="Rgba">Four bytes per pixel.</param>
    public sealed record Picture(int Width, int Height, byte[] Rgba);

    /// <summary>
    /// The terrain: its fragment shader (GLSL) and textures, rows bottom first; a texture array is its slices one after
    /// the other.
    /// </summary>
    /// <param name="Fragment">The fragment shader's GLSL source.</param>
    /// <param name="DiffuseArray">terrain_d_array's slices.</param>
    /// <param name="NormalArray">terrain_n_array's slices.</param>
    /// <param name="Textures">The other textures, by the names of <c>TerrainTextures</c>.</param>
    public sealed record Terrain(string Fragment, Picture DiffuseArray, Picture NormalArray, Dictionary<string, Picture> Textures);

    /// <summary>
    /// The terrain material's textures by property, under the names the terrain draws them by.
    /// </summary>
    public static readonly Dictionary<string, string> TerrainTextures = new()
    {
        ["_CliffNormal"] = "cliff_n",
        ["_ColorVarietyNoise"] = "variety",
        ["_MistlandsCliffNormal"] = "mistcliff_n",
        ["_NoiseTex"] = "noise",
        ["_PavedNormal"] = "paved_n",
        ["_RockNormal"] = "rock_n",
        ["_SnowNormal"] = "snow_n",
    };

    /// <summary>
    /// The world map material's textures by property.
    /// </summary>
    public static readonly Dictionary<string, string> MapTextures = new()
    {
        ["_BackgroundTex"] = "background",
        ["_WaterTex"] = "water",
        ["_MountainTex"] = "mountain",
        ["_ForestTex"] = "forest",
        ["_CloudTex"] = "cloud",
        ["_lavaTex"] = "lava",
        ["_SpaceTex"] = "space",
        ["_FogLayerTex"] = "foglayer",
    };

    /// <summary>
    /// Reads the terrain's shader (from its OpenGL program, else its Vulkan one through SPIR-V) and textures. Throws
    /// InvalidDataException when any of them cannot be read.
    /// </summary>
    /// <param name="game">The game's bundles.</param>
    /// <param name="vulkan">Take the shader from its Vulkan program even when there is an OpenGL one (what Valheim for
    /// Windows gives; for testing).</param>
    public static Terrain ReadTerrain(GameBundles game, bool vulkan = false)
    {
        var matAt = game.Asset("Heightmap_basematerial.mat") ?? throw new InvalidDataException("The game has no terrain material (Heightmap_basematerial).");
        var mat = game.LoadMaterial(matAt) ?? throw new InvalidDataException("The terrain material could not be read.");
        var textures = new Dictionary<string, Picture>();
        foreach (var (prop, name) in TerrainTextures)
        {
            if (mat.Textures.GetValueOrDefault(prop)?.Texture is { } t && Texture(game, t, flip: false) is { } pic)
            {
                textures[name] = pic;
            }
            else
            {
                throw new InvalidDataException($"The terrain's texture {prop} could not be read.");
            }
        }
        var programs = (mat.ShaderAt is { } sh ? game.LoadShader(sh) : null) ?? throw new InvalidDataException("The terrain shader could not be read.");
        string fragment;
        if (programs.Platforms.Contains(GameShader.OpenGlCore) && !vulkan)
        {
            fragment = GameShader.FromOpenGl(programs.Blobs[GameShader.OpenGlCore]);
        }
        else if (programs.Platforms.Contains(GameShader.Vulkan))
        {
            fragment = TerrainShader.FromSpirv(GameShader.FromVulkan(programs.Blobs[GameShader.Vulkan], programs.Deferred));
        }
        else
        {
            throw new InvalidDataException($"The terrain shader has no OpenGL or Vulkan program (platforms {string.Join(", ", programs.Platforms)}): this copy of the game cannot give the editor its terrain shader.");
        }
        return new Terrain(fragment, Array(game, "terrain_d_array.texture2darray"), Array(game, "terrain_n_array.texture2darray"), textures);
    }

    /// <summary>
    /// The world map's textures by name, rows top first (as the map uploads them). Empty when the game has no minimap
    /// material; textures that cannot be read are left out.
    /// </summary>
    public static Dictionary<string, Picture> ReadMap(GameBundles game)
    {
        var res = new Dictionary<string, Picture>();
        if (game.Asset("minimap.mat") is not { } at || game.LoadMaterial(at) is not { } mat)
        {
            return res;
        }
        foreach (var (prop, name) in MapTextures)
        {
            if (mat.Textures.GetValueOrDefault(prop)?.Texture is { } t && Texture(game, t, flip: true) is { } pic)
            {
                res[name] = pic;
            }
        }
        return res;
    }

    /// <summary>
    /// A texture's top mip level as RGBA, rows turned top first with <c>flip</c>; null when it cannot be read or
    /// converted.
    /// </summary>
    private static Picture? Texture(GameBundles game, GameBundles.Where at, bool flip)
    {
        if (game.LoadTexture(at, int.MaxValue) is not { } t || GameTextures.Rgba(t) is not { } rgba)
        {
            return null;
        }
        if (flip)
        {
            Flip(rgba, t.Width, t.Height);
        }
        return new Picture(t.Width, t.Height, rgba);
    }

    /// <summary>
    /// A texture array's slices as one picture, one below the other (rows bottom first). Throws InvalidDataException
    /// when it cannot be read.
    /// </summary>
    private static Picture Array(GameBundles game, string file)
    {
        var slices = (game.Asset(file) is { } at ? game.LoadTextureArray(at) : null) ?? throw new InvalidDataException($"The terrain's texture array {file} could not be read.");
        int w = slices[0].Width, h = slices[0].Height;
        // In long: 4096 × 4096 × 32 slices passes int's range.
        long size = (long)w * h * 4 * slices.Count;
        if (size > System.Array.MaxLength)
        {
            throw new InvalidDataException($"The terrain's texture array {file} is too large to read here ({w} × {h} × {slices.Count}).");
        }
        byte[] all = new byte[size];
        for (int i = 0; i < slices.Count; i++)
        {
            byte[] rgba = GameTextures.Rgba(slices[i]) ?? throw new InvalidDataException($"The terrain's texture array {file} is in a format not read here ({slices[i].Format}).");
            Buffer.BlockCopy(rgba, 0, all, i * w * h * 4, rgba.Length);
        }
        return new Picture(w, h * slices.Count, all);
    }

    /// <summary>
    /// Turns an RGBA picture upside down, in place.
    /// </summary>
    private static void Flip(byte[] rgba, int w, int h)
    {
        int row = w * 4;
        byte[] tmp = new byte[row];
        for (int y = 0; y < h / 2; y++)
        {
            Buffer.BlockCopy(rgba, y * row, tmp, 0, row);
            Buffer.BlockCopy(rgba, (h - 1 - y) * row, rgba, y * row, row);
            Buffer.BlockCopy(tmp, 0, rgba, (h - 1 - y) * row, row);
        }
    }
}
