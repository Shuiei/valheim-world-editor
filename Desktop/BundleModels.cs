using System.Collections.Concurrent;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The models of ModelStore read from the game's own files (GameBundles), as the exporter used to copy
// them: z mirrored (Unity is left-handed, the view right-handed), textures no larger than 1024 pixels,
// cut-out textures bled at their edges. Ids: a mesh or material is "<bundle>:<path id>", a texture the
// same with "|cut" for a cut-out one.
internal sealed class BundleModels : ModelStore.ISource
{
	// Textures are drawn no larger than this (the mip level at or under it).
	public const int TextureMax = 1024;

	// Creatures players tame (and their young), drawn in the Tamed animals kind; Deer and Neck are tamed
	// by server mods.
	private static readonly HashSet<string> Tameable = new(StringComparer.Ordinal)
	{
		"Boar", "Boar_piggy", "Wolf", "Wolf_cub", "Lox", "Lox_Calf", "Hen", "Chicken", "Asksvin", "Asksvin_hatchling", "Deer", "Neck",
	};

	private readonly GameBundles _game;
	private readonly ConcurrentDictionary<int, string?> _objectNames = new();

	internal BundleModels(GameBundles game)
	{
		_game = game;
	}

	public static string IndexFile => Path.Combine(AppSettings.UserDataDir, "game-index.json");

	// The models of this Valheim game folder (its bundles indexed now, or read from the kept index),
	// or null when it is not one.
	public static BundleModels? ForGame(string? valheim)
	{
		if (GameBundles.ForGame(valheim, IndexFile) is not { } game)
		{
			return null;
		}
		try
		{
			return game.Index.Count > 0 ? new BundleModels(game) : null;
		}
		catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			Console.WriteLine($"game files: could not read the bundles in {valheim}: {e.Message}");
			return null;
		}
	}

	// World objects with a model of their own: saved (networked) objects that are not build pieces,
	// creatures, items or ragdolls; also runestones (their location's model) and tameable creatures.
	public string? ObjectName(int prefab) => _objectNames.GetOrAdd(prefab, p =>
	{
		if (PieceCatalog.Get(p) != null || _game.NameOf(p) is not string name || _game.KindOf(name) is not { } kind)
		{
			return null;
		}
		bool extra = name.StartsWith("Runestone_", StringComparison.Ordinal) || kind.Creature && Tameable.Contains(name);
		return extra || kind.Networked && !kind.Creature && !kind.Item && !name.EndsWith("_ragdoll", StringComparison.Ordinal) ? name : null;
	});

	public ModelStore.Model? Model(string name)
	{
		if (_game.LoadModel(name, room: TerrainEditor.Editing.Dungeons.RoomOf(name) != null) is not { } m)
		{
			return null;
		}
		var parts = m.Parts.Select(p => new ModelStore.Part(p.Mesh.Key, p.Sub, p.Material?.Key ?? "", ViewMatrix(p.Matrix))).ToList();
		return new ModelStore.Model(name, parts, new Vector3(m.RootScale[0], m.RootScale[1], m.RootScale[2]));
	}

	// Unity's matrix (row-major, column vectors, left-handed) in the view's space (z mirrored on both
	// sides) as System.Numerics keeps it (row vectors: the transpose).
	internal static Matrix4x4 ViewMatrix(float[] u)
	{
		float T(int r, int c) => u[r * 4 + c] * (r == 2 ? -1 : 1) * (c == 2 ? -1 : 1);
		return new Matrix4x4(
			T(0, 0), T(1, 0), T(2, 0), T(3, 0),
			T(0, 1), T(1, 1), T(2, 1), T(3, 1),
			T(0, 2), T(1, 2), T(2, 2), T(3, 2),
			T(0, 3), T(1, 3), T(2, 3), T(3, 3));
	}

	public ModelStore.MeshData? Mesh(string id)
	{
		if (GameBundles.Where.FromKey(id) is not { } at || _game.LoadMesh(at) is not { } m)
		{
			return null;
		}
		return ViewMesh(m);
	}

	// Interleaved position, normal, uv (8 floats a vertex), z mirrored; indices as Unity has them.
	internal static ModelStore.MeshData ViewMesh(GameBundles.Mesh m)
	{
		int n = m.VertexCount;
		var v = new float[n * 8];
		for (int i = 0; i < n; i++)
		{
			v[i * 8] = m.Positions[i * 3];
			v[i * 8 + 1] = m.Positions[i * 3 + 1];
			v[i * 8 + 2] = -m.Positions[i * 3 + 2];
			if (m.Normals != null)
			{
				v[i * 8 + 3] = m.Normals[i * 3];
				v[i * 8 + 4] = m.Normals[i * 3 + 1];
				v[i * 8 + 5] = -m.Normals[i * 3 + 2];
			}
			else
			{
				v[i * 8 + 4] = 1;
			}
			if (m.Uv != null)
			{
				v[i * 8 + 6] = m.Uv[i * 2];
				v[i * 8 + 7] = m.Uv[i * 2 + 1];
			}
		}
		var subs = m.Submeshes.Select(s => s.Select(i => (uint)i).ToArray()).ToArray();
		return new ModelStore.MeshData(v, subs);
	}

	public ModelStore.MaterialData Material(string id)
	{
		var none = new ModelStore.MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
		if (GameBundles.Where.FromKey(id) is not { } at || _game.LoadMaterial(at) is not { } m)
		{
			return none;
		}
		return ViewMaterial(m);
	}

	// As the exporter judged a material: Unity Standard-style blend modes (_Mode 1: cut out; 2, 3:
	// transparent, drawn cut out at half), vegetation always cut out and seen from both sides.
	internal static ModelStore.MaterialData ViewMaterial(GameBundles.Material m)
	{
		bool vegetation = m.Shader.Contains("Vegetation", StringComparison.Ordinal);
		float mode = m.Floats.GetValueOrDefault("_Mode", 0);
		bool cut = mode == 1 || vegetation || m.Shader.Contains("Grass", StringComparison.Ordinal);
		bool transparent = mode is 2 or 3 || m.Shader.Contains("transparent", StringComparison.OrdinalIgnoreCase);
		float cutoff = cut ? (m.Floats.GetValueOrDefault("_Cutoff", 0.5f) is float c and not 0 ? MathF.Round(c, 3) : 0.5f) : transparent ? 0.5f : 0f;
		bool doubleSided = m.Floats.GetValueOrDefault("_Cull", 2) == 0 || vegetation;
		var col = m.Colors.GetValueOrDefault("_Color") ?? new[] { 1f, 1f, 1f, 1f };
		var color = new Vector4(ModelStore.Linear(col[0]), ModelStore.Linear(col[1]), ModelStore.Linear(col[2]), col[3]);
		string? map = null;
		var uv = new Vector4(1, 1, 0, 0);
		if (m.Textures.GetValueOrDefault("_MainTex") is { Texture: { } tex } main)
		{
			map = tex.Key + (cut || transparent ? "|cut" : "");
			uv = new Vector4(main.ScaleX, main.ScaleY, main.OffsetX, main.OffsetY);
		}
		return new ModelStore.MaterialData(color, map, cutoff, doubleSided, uv);
	}

	public ModelStore.ImageData? Texture(string id)
	{
		bool cut = id.EndsWith("|cut", StringComparison.Ordinal);
		if (GameBundles.Where.FromKey(cut ? id[..^4] : id) is not { } at || _game.LoadTexture(at, TextureMax) is not { } t || GameTextures.Rgba(t) is not { } rgba)
		{
			return null;
		}
		if (cut)
		{
			GameTextures.Bleed(rgba, t.Width, t.Height);
		}
		return new ModelStore.ImageData(t.Width, t.Height, rgba);
	}
}
