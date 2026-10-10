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
	public static (int Placed, List<string> Unknown, float Lift, Dictionary<(int, int), float> Terrain) Open(EditSession s, string path, string? text = null) => Add(s, path, text, null);

	// A blueprint file's pieces added to what is on the plot (one undo step), its anchor at world point
	// at (x, z; on the ground there), or the plot's middle, its lowest point on the ground. Homestead's are
	// measured from their anchor; others (PlanBuild, .vbuild) from a corner, so they are centred.
	// Returns how many were put, the kinds the editor cannot make, how far it was lifted (saving it back
	// takes that off: Homestead's anchor keeps its height), and the ground it stood on (GroundAt).
	public static (int Placed, List<string> Unknown, float Lift, Dictionary<(int, int), float> Terrain) Add(EditSession s, string path, string? text, Vector2? at)
	{
		text ??= File.ReadAllText(path);
		var parsed = BlueprintFormats.Parse(Path.GetFileName(path), text);
		bool homestead = text.Contains("#HomesteadVersion:", StringComparison.OrdinalIgnoreCase);
		var known = parsed.Pieces.Where(p => s.Scene.World.CanCreate(StableHash.Of(p.Name))).ToList();
		var unknown = parsed.Pieces.Select(p => p.Name).Where(n => !s.Scene.World.CanCreate(StableHash.Of(n))).Distinct().OrderBy(n => n).ToList();
		// The building as it is, moved as one: its lowest buildable piece (the colliders, as the game has
		// them) on the ground; across, others than Homestead's (measured from a corner) centred. Rocks
		// and the like (the hoe's) do not count: they are not the building.
		var built = known.Where(p => Buildable(p.Name)).ToList();
		Vector3 shift = Vector3.Zero;
		if (known.Count > 0)
		{
			float low = (built.Count > 0 ? built : known).Min(p => p.Position.Y + Hammer.Bottom(p.Name, BlueprintFormats.FromEuler(p.Euler), p.Scale > 0 ? p.Scale : 1));
			shift = homestead ? new Vector3(0, -low, 0)
				: new Vector3(-(known.Min(p => p.Position.X) + known.Max(p => p.Position.X)) / 2, -low, -(known.Min(p => p.Position.Z) + known.Max(p => p.Position.Z)) / 2);
		}
		var anchor = at is { } w ? new Vector3(w.X, Ground, w.Y) : Anchor(s.Scene);
		if (at == null)
		{
			s.Scene.Ruin = parsed.Ruin;
		}
		var adds = known.Select(p => (new NewObject(0, StableHash.Of(p.Name), anchor + p.Position + shift, p.Euler, MathF.Abs(p.Scale - 1) < 1e-3f ? 0 : p.Scale, Data: p.Data),
			PieceCatalog.Get(StableHash.Of(p.Name))?.Tool != null)).ToList();
		s.Commit($"{(at == null ? "Opened" : "Added")} {parsed.Name}", null, Array.Empty<int>(), adds);
		// The ground it stood on in game, for the support check: Homestead's terrain contacts when the file
		// has them, else the bottom of its lowest piece at each spot (the terrain reached each post).
		var contacts = text.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("#HomesteadTerrainContact:", StringComparison.OrdinalIgnoreCase))
			.Select(l => l[25..].Split(';')).Where(f => f.Length >= 3)
			.Select(f => (Ok: float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
				& float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
				& float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z), P: new Vector3(x, y, z)))
			.Where(c => c.Ok).Select(c => anchor + c.P + shift).ToList();
		var terrain = new Dictionary<(int, int), float>();
		void Lowest(int cx, int cz, float y) => terrain[(cx, cz)] = terrain.TryGetValue((cx, cz), out float was) ? MathF.Min(was, y) : y;
		if (contacts.Count > 0)
		{
			foreach (var c in contacts)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						Lowest((int)MathF.Floor(c.X) + dx, (int)MathF.Floor(c.Z) + dz, c.Y);
					}
				}
			}
		}
		else
		{
			foreach (var p in built)
			{
				var rot = BlueprintFormats.FromEuler(p.Euler);
				var pts = Hammer.Outline(p.Name, rot, p.Scale > 0 ? p.Scale : 1).ToList();
				if (pts.Count == 0)
				{
					continue;
				}
				var o = anchor + p.Position + shift;
				float bottom = o.Y + pts.Min(v => v.Y);
				int x0 = (int)MathF.Floor(o.X + pts.Min(v => v.X)), x1 = (int)MathF.Floor(o.X + pts.Max(v => v.X));
				int z0 = (int)MathF.Floor(o.Z + pts.Min(v => v.Z)), z1 = (int)MathF.Floor(o.Z + pts.Max(v => v.Z));
				for (int cx = x0; cx <= x1; cx++)
				{
					for (int cz = z0; cz <= z1; cz++)
					{
						Lowest(cx, cz, bottom);
					}
				}
			}
		}
		return (adds.Count, unknown, shift.Y, terrain);
	}

	// Pieces players build: the hammer's, the cultivator's, the serving tray's (not the hoe's rocks).
	public static bool Buildable(string prefab) => PieceCatalog.Get(StableHash.Of(prefab))?.Tool is "hammer" or "cultivator" or "feaster";

	// The ground the support check counts at a world point: the plot's, or (higher) the ground a
	// blueprint stood on in game at that metre (Add's terrain).
	public static float GroundAt(IReadOnlyDictionary<(int, int), float> terrain, float x, float z) =>
		terrain.TryGetValue(((int)MathF.Floor(x), (int)MathF.Floor(z)), out float y) ? MathF.Max(Ground, y) : Ground;

	// What is on the plot as a copy (the clipboard format, for BlueprintsPanel): every object standing
	// (the plot starts bare: all on it was put there), measured from the anchor, with what it holds (a
	// chest's contents, a sign's text, a creature's stars...).
	// lift: how far the blueprint opened was lifted (its anchor that much below the ground).
	public static JsonObject Building(WorldScene s, string name, float lift = 0)
	{
		var anchor = Anchor(s) + new Vector3(0, lift, 0);
		var objects = new JsonArray();
		lock (s.Things)
		{
			foreach (var t in s.Things.Where(t => !t.Gone && PrefabCatalog.Details(t.Prefab) != null))
			{
				var o = new JsonObject
				{
					["name"] = PrefabCatalog.NameOf(t.Prefab), ["dx"] = R(t.Position.X - anchor.X), ["dz"] = R(t.Position.Z - anchor.Z), ["dy"] = R(t.Position.Y - anchor.Y),
					["rx"] = R(t.Rotation.X), ["ry"] = R(t.Rotation.Y), ["rz"] = R(t.Rotation.Z), ["scale"] = R(t.Scale), ["sourceId"] = null,
				};
				var data = DataOf(s, t);
				if (data.Count > 0)
				{
					o["data"] = BlueprintFormats.DataJson(data);
				}
				objects.Add(o);
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

	// What an object of the plot holds: every value of its data but its builder and scale (a blueprint
	// has its own), and no builder at all on a ruin's: the plot opened from one, or a building piece
	// without a builder (added from one: the others got the chosen builder when they were put).
	private static List<ObjectField> DataOf(WorldScene s, WorldScene.Thing t)
	{
		var list = new List<ObjectField>();
		bool ruin = s.Ruin;
		if (s.Session is { } session && ObjectData.Bytes(s.World, session.Edits, t.Id) is { } bytes)
		{
			var z = ZdoData.Parse(bytes);
			ruin |= WorldSave.Builder != 0 && PieceCatalog.Get(t.Prefab)?.Tool != null && !z.LongList.Any(f => f.Key == ObjectField.CreatorKey);
			var ci = System.Globalization.CultureInfo.InvariantCulture;
			static string S(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
			list.AddRange(z.FloatList.Where(f => f.Key != StableHash.Of("scaleScalar")).Select(f => new ObjectField("floats", f.Key, S(f.Value))));
			list.AddRange(z.Vec3List.Where(f => f.Key != StableHash.Of("scale")).Select(f => new ObjectField("vec3", f.Key, $"{S(f.Value.X)} {S(f.Value.Y)} {S(f.Value.Z)}")));
			list.AddRange(z.QuatList.Select(f => new ObjectField("quats", f.Key, $"{S(f.Value.X)} {S(f.Value.Y)} {S(f.Value.Z)} {S(f.Value.W)}")));
			list.AddRange(z.IntList.Select(f => new ObjectField("ints", f.Key, f.Value.ToString(ci))));
			list.AddRange(z.LongList.Where(f => f.Key != ObjectField.CreatorKey).Select(f => new ObjectField("longs", f.Key, f.Value.ToString(ci))));
			list.AddRange(z.StringList.Select(f => new ObjectField("strings", f.Key, f.Value)));
			list.AddRange(z.ByteList.Select(f => new ObjectField("bytes", f.Key, Convert.ToBase64String(f.Value))));
		}
		if (ruin)
		{
			list.Add(ObjectField.NoBuilder);
		}
		return list;
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
