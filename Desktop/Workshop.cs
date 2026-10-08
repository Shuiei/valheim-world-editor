using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Workshop: a blank, flat meadow plot that is no world, to build a building on (Place and Select,
// as in a world) and keep it as a Homestead blueprint (BlueprintsPanel). Blueprints open in it to be
// changed. Its middle at ground level is the blueprint's anchor, as Homestead measures it.
public static class Workshop
{
	public const float Ground = 34f;
	public const int Zones = 3;

	// The plot, with nothing on it.
	public static WorldScene Create(string title)
	{
		int w = Zones * 64 + 1;
		var scene = new WorldScene
		{
			World = new WorldSave { Directory = "", SaveNumber = 0 },
			Name = title, X0 = -(Zones / 2), Z0 = -(Zones / 2), Size = Zones, W = w, H = w,
			Heights = Enumerable.Repeat(Ground, w * w).ToArray(),
			// Meadows everywhere (biome 1: the terrain shader's plain grass).
			Biomes = Enumerable.Repeat((int)ValheimGen.Heightmap.Biome.Meadows, w * w).ToArray(),
			Things = new(), BiomeColor = new byte[w * w * 4], Mask = new byte[w * w * 4], OceanDepth = new float[w * w], Limit = new float[w * w],
			Cx = 0, Cz = 0,
			LoadInfo = "The Workshop: a blank plot to build on. Save blueprint keeps the building pieces as a Homestead blueprint.",
		};
		var ground = new Ground(w, w, scene.X0, scene.Z0, Zones);
		Array.Fill(ground.Base, Ground);
		scene.Session = new EditSession(scene, ground, new EditStore(scene.World));
		return scene;
	}

	// The middle of the plot at ground level: the blueprint's anchor.
	public static Vector3 Anchor(WorldScene s) => new(s.Cx, Ground, s.Cz);

	// A blueprint file's pieces onto the plot (one undo step), its anchor at the plot's middle.
	public static (int Placed, List<string> Unknown, float Lift) Open(EditSession s, string path, string? text = null) => Add(s, path, text, null);

	// A blueprint file's pieces added to what is on the plot (one undo step), its anchor at world point
	// at (x, z; on the ground there), or the plot's middle, its lowest point on the ground. Homestead's are
	// measured from their anchor; others (PlanBuild, .vbuild) from a corner, so they are centred.
	// Returns how many were put, the kinds the editor cannot make, and how far it was lifted (saving it
	// back takes that off: Homestead's anchor keeps its height).
	public static (int Placed, List<string> Unknown, float Lift) Add(EditSession s, string path, string? text, Vector2? at)
	{
		text ??= File.ReadAllText(path);
		var parsed = BlueprintFormats.Parse(Path.GetFileName(path), text);
		bool homestead = text.Contains("#HomesteadVersion:", StringComparison.OrdinalIgnoreCase);
		var known = parsed.Pieces.Where(p => s.Scene.World.CanCreate(StableHash.Of(p.Name))).ToList();
		var unknown = parsed.Pieces.Select(p => p.Name).Where(n => !s.Scene.World.CanCreate(StableHash.Of(n))).Distinct().OrderBy(n => n).ToList();
		// The building as it is, moved as one: its lowest point (the pieces' colliders, as the game has
		// them) on the ground; across, others than Homestead's (measured from a corner) centred.
		Vector3 shift = Vector3.Zero;
		if (known.Count > 0)
		{
			float low = known.Min(p => p.Position.Y + Hammer.Bottom(p.Name, BlueprintFormats.FromEuler(p.Euler), p.Scale > 0 ? p.Scale : 1));
			shift = homestead ? new Vector3(0, -low, 0)
				: new Vector3(-(known.Min(p => p.Position.X) + known.Max(p => p.Position.X)) / 2, -low, -(known.Min(p => p.Position.Z) + known.Max(p => p.Position.Z)) / 2);
		}
		var anchor = at is { } w ? new Vector3(w.X, Ground, w.Y) : Anchor(s.Scene);
		var adds = known.Select(p => (new NewObject(0, StableHash.Of(p.Name), anchor + p.Position + shift, p.Euler, MathF.Abs(p.Scale - 1) < 1e-3f ? 0 : p.Scale),
			PieceCatalog.Get(StableHash.Of(p.Name))?.Tool != null)).ToList();
		s.Commit($"{(at == null ? "Opened" : "Added")} {parsed.Name}", null, Array.Empty<int>(), adds);
		return (adds.Count, unknown, shift.Y);
	}

	// The building on the plot as a copy (the clipboard format, for BlueprintsPanel): every building
	// piece standing, measured from the anchor. Anything else (trees, rocks, items) is left out.
	// lift: how far the blueprint opened was lifted (its anchor that much below the ground).
	public static JsonObject Building(WorldScene s, string name, float lift = 0)
	{
		var anchor = Anchor(s) + new Vector3(0, lift, 0);
		var objects = new JsonArray();
		lock (s.Things)
		{
			foreach (var t in s.Things.Where(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null))
			{
				objects.Add(new JsonObject
				{
					["name"] = PrefabCatalog.NameOf(t.Prefab), ["dx"] = R(t.Position.X - anchor.X), ["dz"] = R(t.Position.Z - anchor.Z), ["dy"] = R(t.Position.Y - anchor.Y),
					["rx"] = R(t.Rotation.X), ["ry"] = R(t.Rotation.Y), ["rz"] = R(t.Rotation.Z), ["scale"] = R(t.Scale), ["sourceId"] = null,
				});
			}
		}
		float half = objects.Count == 0 ? 1 : objects.OfType<JsonObject>().Max(o => MathF.Max(MathF.Abs((float)o["dx"]!), MathF.Abs((float)o["dz"]!))) + 2;
		return new JsonObject
		{
			["w"] = 1, ["h"] = 1, ["rel"] = new JsonArray(-32768), ["wt"] = new JsonArray(0), ["pnt"] = new JsonArray(-255, -255, -255, -255),
			["objects"] = objects,
			["poly"] = new JsonArray(P(-half, -half), P(half, -half), P(half, half), P(-half, half)),
			["name"] = name,
		};
		static JsonObject P(float x, float z) => new() { ["gx"] = x, ["gz"] = z };
	}

	// How many of each kind of building piece stand on the plot (prefab name → count).
	public static Dictionary<string, int> Kinds(WorldScene s)
	{
		lock (s.Things)
		{
			return s.Things.Where(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null).GroupBy(t => PrefabCatalog.NameOf(t.Prefab) ?? "?").ToDictionary(g => g.Key, g => g.Count());
		}
	}

	// How many building pieces stand on the plot.
	public static int Pieces(WorldScene s)
	{
		lock (s.Things)
		{
			return s.Things.Count(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null);
		}
	}

	private static float R(float v) => MathF.Round(v * 1000f) / 1000f;
}
