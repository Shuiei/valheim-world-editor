using TerrainEditor.Save;

namespace WorldEditor.Tests;

// The test world: tests/fixtures/CITest, a fresh world made by a dedicated server and filled live
// through the editor (ground edits, trees, rocks, bushes, pickables, a crop, floors, walls,
// chests). Every test works on its own copy, so saves never touch the fixture.
public sealed class TempWorld : IDisposable
{
	public string Dir { get; }

	public TempWorld()
	{
		Dir = Path.Combine(Path.GetTempPath(), "vwe-test-" + Guid.NewGuid().ToString("N")[..10], "CITest");
		CopyDir(Fixtures.World, Dir);
	}

	public WorldSave Load() => WorldSave.Load(Dir);

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
		}
		catch
		{
		}
	}

	public static void CopyDir(string from, string to)
	{
		Directory.CreateDirectory(to);
		foreach (string f in Directory.GetFiles(from))
		{
			File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
		}
	}
}

public static class Fixtures
{
	public static string Root
	{
		get
		{
			for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
			{
				string f = Path.Combine(d, "tests", "fixtures");
				if (Directory.Exists(f))
				{
					return f;
				}
			}
			throw new DirectoryNotFoundException("tests/fixtures not found above " + AppContext.BaseDirectory);
		}
	}

	public static string World => Path.Combine(Root, "CITest");

	// The game's location rules for the kinds laid out first (start, bosses, traders), as
	// `ValheimWorldEditor --export-locations` reads them from Valheim 0.221: CI has no game.
	public static void UseLocations() =>
		ValheimGen.SeedLocations.Use(ValheimGen.SeedLocations.RuleSet.FromJson(File.ReadAllText(Path.Combine(Root, "locations.json"))));

	// The same world as the running game sends it (WorldEditorBridge /snapshot, gzip).
	public static string Snapshot => Path.Combine(Root, "CITest.snapshot");

	public static int Hash(string name) => StableHash.Of(name);
}
