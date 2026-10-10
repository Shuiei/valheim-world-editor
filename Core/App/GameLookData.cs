namespace TerrainEditor.App;

// The game's look besides models, read from its bundles (GameBundles): the terrain's shader and
// textures (its material Heightmap_basematerial and two texture arrays), and the world map's textures
// (material "minimap"). What the Python copy made as files (game-look/terrain, game-look/maptex).
public static class GameLookData
{
	// Pixels, RGBA, rows in the order asked (Unity keeps them bottom first).
	public sealed record Picture(int Width, int Height, byte[] Rgba);

	// The terrain: its fragment shader (GLSL) and textures, rows bottom first; a texture array is its
	// slices one after the other.
	public sealed record Terrain(string Fragment, Picture DiffuseArray, Picture NormalArray, Dictionary<string, Picture> Textures);

	// The terrain material's textures by property, under the names the terrain draws them by.
	public static readonly Dictionary<string, string> TerrainTextures = new()
	{
		["_CliffNormal"] = "cliff_n", ["_ColorVarietyNoise"] = "variety", ["_MistlandsCliffNormal"] = "mistcliff_n", ["_NoiseTex"] = "noise",
		["_PavedNormal"] = "paved_n", ["_RockNormal"] = "rock_n", ["_SnowNormal"] = "snow_n",
	};

	// The world map material's textures by property.
	public static readonly Dictionary<string, string> MapTextures = new()
	{
		["_BackgroundTex"] = "background", ["_WaterTex"] = "water", ["_MountainTex"] = "mountain", ["_ForestTex"] = "forest",
		["_CloudTex"] = "cloud", ["_lavaTex"] = "lava", ["_SpaceTex"] = "space", ["_FogLayerTex"] = "foglayer",
	};

	// vulkan: take the shader from its Vulkan program even when there is an OpenGL one (what Valheim
	// for Windows gives; for testing).
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

	// The world map's textures by name, rows top first (as the map uploads them).
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

	private static Picture Array(GameBundles game, string file)
	{
		var slices = (game.Asset(file) is { } at ? game.LoadTextureArray(at) : null) ?? throw new InvalidDataException($"The terrain's texture array {file} could not be read.");
		int w = slices[0].Width, h = slices[0].Height;
		var all = new byte[w * h * 4 * slices.Count];
		for (int i = 0; i < slices.Count; i++)
		{
			byte[] rgba = GameTextures.Rgba(slices[i]) ?? throw new InvalidDataException($"The terrain's texture array {file} is in a format not read here ({slices[i].Format}).");
			Buffer.BlockCopy(rgba, 0, all, i * w * h * 4, rgba.Length);
		}
		return new Picture(w, h * slices.Count, all);
	}

	private static void Flip(byte[] rgba, int w, int h)
	{
		int row = w * 4;
		var tmp = new byte[row];
		for (int y = 0; y < h / 2; y++)
		{
			Buffer.BlockCopy(rgba, y * row, tmp, 0, row);
			Buffer.BlockCopy(rgba, (h - 1 - y) * row, rgba, y * row, row);
			Buffer.BlockCopy(tmp, 0, rgba, (h - 1 - y) * row, row);
		}
	}
}
