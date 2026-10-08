using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Saved selections, per world (the web editor kept them in the browser): a name and, for each object,
// its kind and place, since the ids change when the world is saved. Picking one finds the objects
// again by kind and place (within 5 cm across, 20 cm up). In selections.json in the data folder.
public static class SavedSelections
{
	public sealed record Item(int Prefab, float X, float Y, float Z);

	public sealed record Saved(string Name, List<Item> Items);

	// Tests: another file, never the user's.
	internal static string? PathOverride { get; set; }

	private static string FilePath => PathOverride ?? Path.Combine(AppSettings.DataDir, "selections.json");

	private static Dictionary<string, List<Saved>> LoadAll()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				var all = JsonSerializer.Deserialize<Dictionary<string, List<Saved>>>(File.ReadAllText(FilePath));
				if (all != null)
				{
					// A hand-edited file may hold nulls: those entries are left out.
					return all.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value.Where(s => s?.Name != null && s.Items != null).ToList());
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
		{
		}
		return new();
	}

	private static bool SaveAll(Dictionary<string, List<Saved>> all)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(all));
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	// This world's saved selections, oldest first.
	public static List<Saved> For(string world) => LoadAll().TryGetValue(world, out var list) ? list : new();

	// Keeps a selection under this name (one of the same name is replaced; the new one goes last).
	// False when the file could not be written.
	public static bool Keep(string world, string name, IEnumerable<Item> items)
	{
		var all = LoadAll();
		var list = all.TryGetValue(world, out var l) ? l : new();
		list.RemoveAll(s => s.Name == name);
		list.Add(new Saved(name, items.Select(i => new Item(i.Prefab, MathF.Round(i.X, 3), MathF.Round(i.Y, 3), MathF.Round(i.Z, 3))).ToList()));
		all[world] = list;
		return SaveAll(all);
	}

	public static bool Forget(string world, string name)
	{
		var all = LoadAll();
		if (!all.TryGetValue(world, out var list) || list.RemoveAll(s => s.Name == name) == 0)
		{
			return true;
		}
		if (list.Count == 0)
		{
			all.Remove(world);
		}
		return SaveAll(all);
	}

	// The objects of a saved selection among these things (the deleted left out), and how many were not found.
	public static (List<int> Ids, int Missing) Find(Saved saved, IReadOnlyList<WorldScene.Thing> things)
	{
		var ids = new List<int>();
		int missing = 0;
		foreach (var it in saved.Items)
		{
			int found = -1;
			for (int i = 0; i < things.Count; i++)
			{
				var t = things[i];
				if (!t.Gone && t.Prefab == it.Prefab && MathF.Abs(t.Position.X - it.X) < 0.05f && MathF.Abs(t.Position.Z - it.Z) < 0.05f && MathF.Abs(t.Position.Y - it.Y) < 0.2f && !ids.Contains(i))
				{
					found = i;
					break;
				}
			}
			if (found >= 0)
			{
				ids.Add(found);
			}
			else
			{
				missing++;
			}
		}
		return (ids, missing);
	}
}
