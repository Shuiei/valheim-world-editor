using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using TerrainService = ValheimGen.TerrainService;

namespace TerrainEditor.Desktop;

// One open world, for as long as it is open: the save (or the running game's world, live), the edits
// waiting to be saved, and the world generator's ground. Every area opened from the map shares it, so
// edits stay pending while you move around the world (like the web editor's session).
public sealed class WorldSession : IDisposable
{
	public required WorldSave World { get; set; }
	public required EditStore Edits { get; init; }
	public required TerrainModifiers Modifiers { get; set; }
	public required TerrainService Terrain { get; init; }
	// Live: the running game (through WorldEditorBridge), and which object changes it already has.
	public LiveBridge? Live { get; init; }
	public LiveSync LiveSync { get; } = new();
	public bool IsLive => Live != null;
	public string Label { get; init; } = "";

	// The history while no area is open, or another one (see EditSession.Export); gone when the world
	// is read again (zone resets, No limit ground). It is kept on disk after each save or Apply live
	// (HistoryFile) and comes back when the world is opened again.
	public EditSession.Kept? History { get; set; }
	// The open area's session, whose history is the world's while it is open.
	public EditSession? Area { get; set; }
	// The history kept from an earlier session of the editor, when the world was opened (null: none).
	public HistoryFile.Restored? Restored { get; private set; }
	// Its steps may not match the world any more (live, or saved by the game since): asked once before
	// undoing or redoing them (Earlier).
	public bool EarlierMayDiffer => Restored?.Changed == true && !EarlierAllowed;
	public bool EarlierAllowed { get; set; }
	// The window said so already.
	public bool RestoredSaid { get; set; }

	// Ids for objects added in this session: negative, like the web editor's, unique across every area.
	private int _nextId = -1;
	public int NextId() => Interlocked.Decrement(ref _nextId) + 1;

	private static WorldSession From(WorldSave world, LiveBridge? live, string label)
	{
		var modifiers = new TerrainModifiers(world);
		var s = new WorldSession
		{
			World = world, Edits = new EditStore(world), Modifiers = modifiers, Terrain = new TerrainService(world, modifiers), Live = live, Label = label,
		};
		s.RestoreHistory();
		return s;
	}

	// The history kept on disk for this world, if any (the first area opened takes it).
	private void RestoreHistory()
	{
		Restored = HistoryFile.Read(this);
		History = Restored?.Kept;
	}

	// After a save or Apply live: the history as it is now (it matches what the world holds) goes to
	// disk. A failure to write it is only logged: the save itself is done.
	private void KeepHistory()
	{
		try
		{
			if ((Area?.Export() ?? History) is { } kept)
			{
				HistoryFile.Write(this, kept);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Console.WriteLine($"History: could not keep it on disk: {ex.Message}");
		}
	}

	// Saved or applied from the map (no area open): the kept steps are in the world now, as an open
	// area's are once it saves (EditSession), so a later Discard does not undo them.
	private void MarkHistoryApplied()
	{
		if (Area == null && History != null)
		{
			// Applied is part of a step's hash: the ids are keyed again once it is set.
			var ids = History.Ids.ToList();
			foreach (var c in History.Undo)
			{
				c.Applied = true;
			}
			History = History with { Ids = ids.ToDictionary(p => p.Key, p => p.Value) };
		}
	}

	// The world was read again: the steps do not match it any more.
	private void ForgetHistory()
	{
		History = null;
		Restored = null;
		HistoryFile.Delete(this);
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
			if (_lastSave == null)
			{
				return (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);
			}
			// Saved already: what differs from that save (an undo after it is pending too).
			var deleted = Edits.Deleted;
			var added = Edits.Added.ToHashSet();
			int del = deleted.Count(i => !_savedDeleted.Contains(i)) + _savedDeleted.Count(i => !deleted.Contains(i));
			int add = added.Count(o => !_savedAdded.Contains(o)) + _savedAdded.Count(o => !added.Contains(o));
			return (Edits.ChangedZoneCount, del, add, Edits.ResetCount);
		}
	}

	// Offline, once saved: the open world saves again from the save it was opened from (kept until the
	// world is left), with every change since; the zones and objects of the last save, the files it
	// wrote (removed by the next one).
	private IReadOnlyList<string>? _lastSave;
	private int? _lastNumber;
	private HashSet<(int, int)> _savedZones = new();
	private HashSet<int> _savedDeleted = new();
	private HashSet<NewObject> _savedAdded = new();

	private void ForgetSaves()
	{
		_lastSave = null;
		_lastNumber = null;
		_savedZones = new();
		_savedDeleted = new();
		_savedAdded = new();
	}

	public sealed record Outcome(bool Done, string Message, bool Reloaded, WorldWriter.Result? Saved = null)
	{
		// No limit ground was turned into ground discs: the ground under the area changed.
		public bool Lifted { get; init; }
	}

	// No limit ground waiting (lifts): turned into ground discs and ordinary edits (Uplift), the ground
	// worked out again with them. Null when there was none.
	public Uplift.Plan? LiftToDiscs()
	{
		var plan = Editing.Uplift.Make(World, Terrain, Edits, NextId);
		if (plan != null)
		{
			Editing.Uplift.Apply(plan, Edits);
			UseModifiers(new TerrainModifiers(Editing.Uplift.PlacedNow(World, Edits)));
		}
		return plan;
	}

	private void UseModifiers(TerrainModifiers modifiers)
	{
		Modifiers = modifiers;
		Terrain.UseModifiers(modifiers);
	}

	// Writes everything into the world's files, like Apply live: the world stays open as it is, with
	// its history (an undo, saved again, is saved). Each save is made from the save the world was opened
	// from, with every change since (so a zone saved before and not changed since is written again),
	// and the previous save of the session goes. After zone resets or when No limit ground became ground
	// discs, the world is read again from what was written (the ground or zones are not the same).
	// A save the game made after the world was read (a play test between two saves) is never thrown
	// away: the save is refused before anything changes, No limit ground included.
	public Outcome Save()
	{
		int known = _lastNumber ?? World.SaveNumber;
		if (WorldWriter.Newer(World.Directory, known) is string newer)
		{
			return new Outcome(false, newer, false, new WorldWriter.Result(false, newer, null, 0, 0, new()));
		}
		var plan = LiftToDiscs();
		var zones = Edits.All().Where(e => e.Changed).Select(e => (e.ZoneX, e.ZoneZ)).ToHashSet();
		zones.UnionWith(_savedZones);
		var changed = Edits.All().Where(e => zones.Contains((e.ZoneX, e.ZoneZ))).ToList();
		bool reread = plan != null || Edits.ResetCount > 0;
		var result = WorldWriter.Save(World, changed, Edits.Deleted, Edits.Added, Edits.Resets, new WorldWriter.Options(KeepBase: !reread, Drop: _lastSave, Latest: known));
		if (result.Saved && reread)
		{
			World = WorldSave.Load(World.Directory);
			WorldWriter.Prune(World.Directory);
			Edits.ResetFrom(World);
			UseModifiers(new TerrainModifiers(World));
			_nextId = -1;
			ForgetHistory();
			ForgetSaves();
		}
		else if (result.Saved)
		{
			_lastSave = result.Files;
			_lastNumber = result.Number;
			_savedZones = zones;
			Edits.MarkApplied(changed.Select(e => (e.ZoneX, e.ZoneZ)));
			_savedDeleted = Edits.Deleted.ToHashSet();
			_savedAdded = Edits.Added.ToHashSet();
			MarkHistoryApplied();
			KeepHistory();
		}
		string message = plan != null ? $"{result.Message} No limit ground: {plan.Describe()}." : result.Message;
		return new Outcome(result.Saved, message, result.Saved && reread, result with { Message = message }) { Lifted = plan != null };
	}

	// Leaving an offline world it saved: the save it was opened from goes (only the latest stays).
	private void PruneSaves()
	{
		if (!IsLive && _lastSave != null && System.IO.Directory.Exists(World.Directory))
		{
			WorldWriter.Prune(World.Directory);
		}
		ForgetSaves();
	}

	// Live: the changed zones' ground and the object changes go into the running game; zone resets last
	// (the game regenerates them), after which the world is read again from the game.
	// One apply at a time: a second one (a double click) waits and sends only what is still pending.
	private readonly SemaphoreSlim _applying = new(1, 1);

	// When the world is left: the live connection closes.
	public void Dispose()
	{
		PruneSaves();
		Live?.Dispose();
		_applying.Dispose();
	}

	public async Task<Outcome> ApplyLive()
	{
		await _applying.WaitAsync();
		try
		{
			return await ApplyLiveOnce();
		}
		finally
		{
			_applying.Release();
		}
	}

	private async Task<Outcome> ApplyLiveOnce()
	{
		var live = Live!;
		var done = new List<string>();
		bool reloaded = false;
		var plan = LiftToDiscs();
		if (plan != null)
		{
			done.Add($"No limit ground: {plan.Describe()}");
		}
		try
		{
			// Copies as sent: edits made while the game answers are not marked as applied.
			var zones = Edits.All().Where(e => e.Changed).Select(e => e.Clone()).ToList();
			if (zones.Count > 0)
			{
				await live.ApplyTerrain(zones.Select(e => (e.ZoneX, e.ZoneZ, WorldWriter.EncodeTerrain(e))).ToList());
				Edits.MarkApplied(zones);
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
				UseModifiers(new TerrainModifiers(World));
				LiveSync.Reset();
				_nextId = -1;
				ForgetHistory();
				reloaded = true;
			}
			else if (done.Count > 0)
			{
				MarkHistoryApplied();
				KeepHistory();
			}
			return new Outcome(done.Count > 0, done.Count > 0 ? $"Applied to the running game: {string.Join("; ", done)}." : "Nothing to apply.", reloaded) { Lifted = plan != null };
		}
		// The game answering with an error (InvalidOperationException from the bridge) is reported too.
		catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
		{
			return new Outcome(false, "Could not apply live: " + ex.Message, false) { Lifted = plan != null };
		}
	}

	// Live: the world read again from the game (what players changed since); pending edits are dropped.
	public async Task Reload()
	{
		World = await Live!.LoadWorld();
		Edits.ResetFrom(World);
		UseModifiers(new TerrainModifiers(World));
		LiveSync.Reset();
		History = null;
	}

	// Drops every pending change (offline: the world is read again from its files).
	public void Discard()
	{
		if (!IsLive)
		{
			PruneSaves();
			World = WorldSave.Load(World.Directory);
		}
		Edits.ResetFrom(World);
		UseModifiers(new TerrainModifiers(World));
		LiveSync.Reset();
		History = null;
		Area = null;
		// Offline the world is as it was last saved: so is the history kept then.
		if (!IsLive)
		{
			_nextId = -1;
			RestoreHistory();
		}
	}
}
