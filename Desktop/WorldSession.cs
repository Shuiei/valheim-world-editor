using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using TerrainService = ValheimGen.TerrainService;

namespace TerrainEditor.Desktop;

// One open world, for as long as it is open: the save (or the running game's world, live), the edits
// waiting to be saved, and the world generator's ground. Every area opened from the map shares it, so
// edits stay pending while you move around the world (like the web editor's session).
public sealed class WorldSession
{
	public required WorldSave World { get; set; }
	public required EditStore Edits { get; init; }
	public required TerrainModifiers Modifiers { get; init; }
	public required TerrainService Terrain { get; init; }
	// Live: the running game (through WorldEditorBridge), and which object changes it already has.
	public LiveBridge? Live { get; init; }
	public LiveSync LiveSync { get; } = new();
	public bool IsLive => Live != null;
	public string Label { get; init; } = "";

	// The history while no area is open, or another one (see EditSession.Export); gone when the world
	// is saved, discarded or read again.
	public EditSession.Kept? History { get; set; }

	// Ids for objects added in this session: negative, like the web editor's, unique across every area.
	private int _nextId = -1;
	public int NextId() => Interlocked.Decrement(ref _nextId) + 1;

	private static WorldSession From(WorldSave world, LiveBridge? live, string label)
	{
		var modifiers = new TerrainModifiers(world);
		return new WorldSession
		{
			World = world, Edits = new EditStore(world), Modifiers = modifiers, Terrain = new TerrainService(world, modifiers), Live = live, Label = label,
		};
	}

	// A world on this computer (its folder).
	public static WorldSession Open(string dir)
	{
		WorldSave.ModifierPrefabs = TerrainModifiers.NetworkPrefabHashes.ToHashSet();
		var world = WorldSave.Load(dir);
		WorldSave.Builder = Builders.Default(world);
		return From(world, null, world.Name);
	}

	// The world of a running game or server (live).
	public static async Task<WorldSession> OpenLive(LiveBridge live, string label)
	{
		WorldSave.ModifierPrefabs = TerrainModifiers.NetworkPrefabHashes.ToHashSet();
		var world = await live.LoadWorld();
		WorldSave.Builder = Builders.Default(world);
		return From(world, live, label);
	}

	// What is waiting to be saved (live: to be applied to the game).
	public (int Zones, int Deleted, int Added, int Resets) Pending
	{
		get
		{
			if (IsLive)
			{
				var (d, a) = LiveSync.Pending(Edits);
				return (Edits.ChangedZoneCount, d, a, Edits.ResetCount);
			}
			return (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);
		}
	}

	public sealed record Outcome(bool Done, string Message, bool Reloaded, WorldWriter.Result? Saved = null);

	// Writes everything into the world's files (a backup first), then reads the world again.
	public Outcome Save()
	{
		var changed = Edits.All().Where(e => e.Changed).ToList();
		var result = WorldWriter.Save(World, changed, Edits.Deleted, Edits.Added, Edits.Resets);
		if (result.Saved)
		{
			World = WorldSave.Load(World.Directory);
			Edits.ResetFrom(World);
			_nextId = -1;
			History = null;
		}
		return new Outcome(result.Saved, result.Message, result.Saved, result);
	}

	// Live: the changed zones' ground and the object changes go into the running game; zone resets last
	// (the game regenerates them), after which the world is read again from the game.
	public async Task<Outcome> ApplyLive()
	{
		var live = Live!;
		var done = new List<string>();
		bool reloaded = false;
		try
		{
			var zones = Edits.All().Where(e => e.Changed).ToList();
			if (zones.Count > 0)
			{
				await live.ApplyTerrain(zones.Select(e => (e.ZoneX, e.ZoneZ, WorldWriter.EncodeTerrain(e))).ToList());
				Edits.MarkApplied(zones.Select(e => (e.ZoneX, e.ZoneZ)));
				done.Add($"{zones.Count} zone(s) of ground");
			}
			string objects = await LiveSync.Apply(World, Edits, live);
			if (objects != "")
			{
				done.Add(objects);
			}
			if (Edits.ResetCount > 0)
			{
				var resets = Edits.Resets;
				await live.ResetZones(resets);
				done.Add($"{resets.Count} zone(s) reset");
				World = await live.LoadWorld();
				Edits.ResetFrom(World);
				LiveSync.Reset();
				_nextId = -1;
				History = null;
				reloaded = true;
			}
			return new Outcome(done.Count > 0, done.Count > 0 ? $"Applied to the running game: {string.Join("; ", done)}." : "Nothing to apply.", reloaded);
		}
		catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or InvalidDataException)
		{
			return new Outcome(false, "Could not apply live: " + ex.Message, false);
		}
	}

	// Live: the world read again from the game (what players changed since); pending edits are dropped.
	public async Task Reload()
	{
		World = await Live!.LoadWorld();
		Edits.ResetFrom(World);
		LiveSync.Reset();
		History = null;
	}

	// Drops every pending change (offline: the world is read again from its files).
	public void Discard()
	{
		if (!IsLive)
		{
			World = WorldSave.Load(World.Directory);
		}
		Edits.ResetFrom(World);
		LiveSync.Reset();
		History = null;
	}
}
