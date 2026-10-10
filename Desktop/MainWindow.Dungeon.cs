using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Building in a dungeon (the Dungeon panel's Build here): the Workshop's Build (one piece at a time,
// where the hammer would put it, snapping) over the dungeon's rooms, whose floors and walls the cursor
// points at. Pieces resting on a room hold in game: its floor counts as ground for their support.
public partial class MainWindow
{
	private Kept? _dungeonKept;

	internal bool BuildingInDungeon => _dungeonKept != null;

	internal void BuildInDungeon(bool on)
	{
		var t = PlaceTool;
		if (on && _dungeonKept == null)
		{
			_dungeonKept = KeepPlaceTool();
			BuildPanel.Start();
			t.CutY = _view.CutY;
			Tools.ChooseMode(ToolMode.Place);
			_message.Text = "Building in the dungeon: pick a piece, then click on a room's floor or on a piece. Pieces on a room's floor hold in game.";
		}
		else if (!on && _dungeonKept is { } k)
		{
			_dungeonKept = null;
			RestorePlaceTool(k);
			t.Notify();
		}
	}

	// A generated dungeon into the world, 5000 m above the ground at the view's centre (the game builds
	// dungeons there, and treats what is above 3000 m as indoors), with a portal on the ground there and
	// its twin at the dungeon's entrance, linked. One step of the history: Ctrl+Z takes it all back.
	internal void PlaceGenerated(DungeonGen.Settings settings, DungeonGen.Result r)
	{
		if (_view.Scene is not { Session: { } session } scene)
		{
			_message.Text = "Open an area of a world first: the dungeon goes above the middle of the view.";
			return;
		}
		var world = scene.World;
		var target = _view.Camera.Target;
		float gx = target.X + scene.Cx, gz = scene.Cz - target.Z;
		int ix = Math.Clamp((int)MathF.Round(gx - scene.Cx + (scene.W - 1) / 2f), 0, scene.W - 1), iz = Math.Clamp((int)MathF.Round(gz - scene.Cz + (scene.H - 1) / 2f), 0, scene.H - 1);
		float ground = Math.Max(scene.Heights[iz * scene.W + ix], scene.Water);
		float HeightAt(float x, float z)
		{
			int i = Math.Clamp((int)MathF.Round(x - scene.Cx + (scene.W - 1) / 2f), 0, scene.W - 1), j = Math.Clamp((int)MathF.Round(z - scene.Cz + (scene.H - 1) / 2f), 0, scene.H - 1);
			return scene.Heights[j * scene.W + i];
		}
		var origin = new Vector3(MathF.Round(gx), MathF.Round(ground) + 5000, MathF.Round(gz));
		// Clear of what is already up there: dungeons laid out, objects, and over the dungeons' locations
		// near here, the space the game will lay theirs out in (higher by 50 m steps).
		List<(int, Vector3)> high;
		lock (scene.Things)
		{
			high = scene.Things.Where(t => !t.Gone && t.Position.Y > 3000).Select(t => (t.Prefab, t.Position)).ToList();
		}
		var near = world.Locations.Where(l => MathF.Abs(l.Position.X - origin.X) < 600 && MathF.Abs(l.Position.Z - origin.Z) < 600);
		origin = DungeonGen.Clear(origin, r, DungeonGen.Taken(high, near));
		var files = new Dictionary<ChunkFile, byte[]>();
		byte[]? Blank(NewObject n) => world.NewObjectBytes(n, m => world.LiveBytes ?? (files.TryGetValue(m.File, out var f) ? f : files[m.File] = File.ReadAllBytes(Path.Combine(world.Directory, m.File.FileName))));
		var adds = new List<(NewObject, bool)>();
		var arrival = origin + r.Arrival;
		if (r.Rooms is { } rooms && r.Kind is { } kind)
		{
			// The game's rooms: a dungeon object holding the list, which the game builds them from.
			var placed = rooms.Select(p => p with { Position = p.Position + origin }).ToList();
			var dg = new NewObject(0, kind.Hash, origin, Vector3.Zero, 0);
			if (Blank(dg) is not { } bytes)
			{
				_message.Text = $"{kind.Name} cannot be made in this world.";
				return;
			}
			var z = ZdoData.Parse(bytes);
			z.SetBytes(Dungeons.RoomDataKey, Dungeons.Write(placed));
			adds.Add((dg with { Raw = z.Serialize(), Fresh = false }, false));
			// The portal on the entrance room's floor, found under its middle.
			var dungeon = new DungeonRooms.Dungeon(-1, new WorldScene.Thing(-1, kind.Hash, origin, Vector3.Zero, 1, false), kind, placed, null);
			var above = placed[0].Position + new Vector3(0, 3, 0);
			arrival = RoomSurfaces.Hit(new[] { dungeon }, _view.Models, above, -Vector3.UnitY) is float down ? above - new Vector3(0, down - 0.05f, 0) : placed[0].Position;
		}
		// Every object with its own data, and no builder: the game takes the dungeon for a ruin, as its
		// own dungeons (monsters do not go for its walls; taking it apart gives back a third).
		int skipped = 0, creator = StableHash.Of("creator");
		foreach (var it in r.Items)
		{
			int prefab = StableHash.Of(it.Prefab);
			// Made fresh (nothing kept of the object it is copied from), then its builder taken off.
			var n = new NewObject(0, prefab, it.Position + origin, Dungeons.ToEuler(it.Rotation), 0);
			{
				if (Blank(n) is not { } bytes)
				{
					skipped++;
					continue;
				}
				var z = ZdoData.Parse(bytes);
				z.Set("longs", creator, null);
				foreach (var (section, key, value) in it.Data ?? Array.Empty<(string, string, string)>())
				{
					if (section == "bytes")
					{
						z.SetBytes(StableHash.Of(key), Convert.FromBase64String(value));
					}
					else
					{
						z.Set(section, StableHash.Of(key), value);
					}
				}
				n = n with { Raw = z.Serialize(), Fresh = false };
			}
			adds.Add((n, TerrainEditor.Terrain.PieceCatalog.Get(prefab)?.Tool != null));
		}
		// Two portals of the same tag, written linked (the game pairs only portals it sees made, or that
		// a save gives linked).
		string tag = $"dg{settings.Seed % 100000}";
		int link = Random.Shared.Next(1, int.MaxValue);
		int portal = StableHash.Of("portal_wood");
		foreach (var (at, yaw, kindByte) in new[] { (arrival, 0f, (byte)0x01), (new Vector3(gx, Math.Max(HeightAt(gx, gz + 3), scene.Water), gz + 3), 180f, (byte)0x11) })
		{
			var n = new NewObject(0, portal, at, new Vector3(0, yaw, 0), 0);
			if (Blank(n) is not { } bytes)
			{
				continue;
			}
			var z = ZdoData.Parse(bytes);
			z.Set("longs", creator, null);
			z.Set("strings", StableHash.Of("tag"), tag);
			z.Connection = new[] { kindByte }.Concat(BitConverter.GetBytes(link)).ToArray();
			adds.Add((n with { Raw = z.Serialize(), Fresh = false }, true));
		}
		session.Commit($"Dungeon: generated {r.Name}", null, Array.Empty<int>(), adds);
		_message.Text = $"Generated {r.Name}: {adds.Count} objects, 5000 m above here. A portal \u201c{tag}\u201d on the ground here leads in. "
			+ (skipped > 0 ? $"{skipped} could not be made in this world. " : "") + "Ctrl+Z takes it back; Save writes it.";
		_view.Orbit(arrival.X, arrival.Z, 30, 55, 60, arrival.Y);
		_view.CutY = arrival.Y + 3.5f;
	}

	// A generated dungeon as a blueprint (in the game's Homestead folder), opened in the Workshop. What
	// its objects hold (the key in its chest, signs, the boss's stars) goes in the editor's own lines of
	// the file, and the building is written as a ruin (no builder): the editor puts them back when the
	// blueprint is placed in a world. Built in game with Homestead, it is only the objects.
	private static readonly string[] DungeonTags = { "dungeon", "generated" };

	internal async Task OpenGeneratedInWorkshop(DungeonGen.Settings settings)
	{
		var r = DungeonGen.Make(settings);
		float mx = (r.Items.Min(i => i.Position.X) + r.Items.Max(i => i.Position.X)) / 2, mz = (r.Items.Min(i => i.Position.Z) + r.Items.Max(i => i.Position.Z)) / 2;
		float my = r.Items.Min(i => i.Position.Y);
		var objects = new System.Text.Json.Nodes.JsonArray();
		foreach (var it in r.Items)
		{
			var e = Dungeons.ToEuler(it.Rotation);
			var data = (it.Data ?? Array.Empty<(string Section, string Key, string Value)>())
				.Select(d => new ObjectField(d.Section, StableHash.Of(d.Key), d.Value)).Append(ObjectField.NoBuilder);
			objects.Add(new System.Text.Json.Nodes.JsonObject
			{
				["name"] = it.Prefab, ["dx"] = it.Position.X - mx, ["dy"] = it.Position.Y - my, ["dz"] = it.Position.Z - mz, ["rx"] = e.X, ["ry"] = e.Y, ["rz"] = e.Z, ["scale"] = 0,
				["data"] = BlueprintFormats.DataJson(data),
			});
		}
		try
		{
			string folder = Homestead.Folder();
			Directory.CreateDirectory(folder);
			// A blueprint of that name already (one changed in the Workshop, perhaps): kept, this one numbered.
			string path = Path.Combine(folder, Homestead.FileName(r.Name));
			for (int n = 2; File.Exists(path); n++)
			{
				path = Path.Combine(folder, Homestead.FileName($"{r.Name} ({n})"));
			}
			await File.WriteAllTextAsync(path, Homestead.Write(new System.Text.Json.Nodes.JsonObject { ["objects"] = objects }, r.Name, "Valheim World Editor", null, DateTime.Now,
				$"A generated dungeon (seed {settings.Seed}): " + string.Join(" ", r.Notes), DungeonTags));
			await OpenWorkshop(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_message.Text = $"Could not write the dungeon's blueprint: {ex.Message}";
		}
	}
}
