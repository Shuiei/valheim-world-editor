using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// Homestead's blueprints, the editor's building library: finding Homestead in a BepInEx folder (with
// its version), writing a blueprint the way Homestead writes them (its headers, pieces measured from the
// anchor, rotations as quaternions) that reads back the same, and listing a folder of them.
public class CoreHomesteadTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-hs-" + Guid.NewGuid().ToString("N")[..8]);

	public void Dispose()
	{
		if (Directory.Exists(_dir))
		{
			Directory.Delete(_dir, recursive: true);
		}
	}

	[Fact]
	public void HomesteadIsFoundInABepInExFolderWithItsVersion()
	{
		var settings = new AppSettings();
		string bep = Path.Combine(_dir, "BepInEx");
		Directory.CreateDirectory(Path.Combine(bep, "plugins"));
		settings.BepInExFolders.Add(bep);
		Assert.False(Homestead.Find(settings).Installed);
		// As a mod manager installs it: its own folder with the package's manifest.
		string mod = Path.Combine(bep, "plugins", "sighsorry-Homestead");
		Directory.CreateDirectory(mod);
		File.WriteAllText(Path.Combine(mod, "Homestead.dll"), "");
		File.WriteAllText(Path.Combine(mod, "manifest.json"), """{ "name": "Homestead", "version_number": "1.3.2" }""");
		var found = Homestead.Find(settings);
		Assert.True(found.Installed);
		Assert.Equal("1.3.2", found.Version);
		Assert.Single(found.Where);
		// The tests never use the game's own folder.
		Assert.Equal(Homestead.FolderOverride, found.Folder);
	}

	private static JsonObject Clip(params (string Name, float X, float Y, float Z, float Yaw)[] pieces) => new()
	{
		["w"] = 1, ["h"] = 1, ["rel"] = new JsonArray(-32768), ["wt"] = new JsonArray(0), ["pnt"] = new JsonArray(-255, -255, -255, -255),
		["objects"] = new JsonArray(pieces.Select(p => (JsonNode)new JsonObject
		{
			["name"] = p.Name, ["dx"] = p.X, ["dy"] = p.Y, ["dz"] = p.Z, ["rx"] = 0, ["ry"] = p.Yaw, ["rz"] = 0, ["scale"] = 0,
		}).ToArray()),
		["poly"] = new JsonArray(),
	};

	[Fact]
	public void ABlueprintIsWrittenAsHomesteadWritesThemAndReadsBack()
	{
		string text = Homestead.Write(Clip(("wood_floor", -1, 0, 1, 0), ("woodwall", 2, 1, 0, 90)), "Hut", "Tester", "Midgard", new DateTime(2026, 10, 8, 17, 0, 0));
		Assert.StartsWith("#Name:Hut\n#Creator:Tester\n#Description:\n#Category:Blueprints\n#HomesteadVersion:1\n#HomesteadWorld:Midgard\n#HomesteadSavedAt:2026-10-08 17:00:00 ", text);
		Assert.Contains("#HomesteadRadius:", text);
		Assert.Contains("\n#Pieces\nwood_floor;Building;-1;0;1;0;0;0;1;\"\";1;1;1\n", text);
		// A quarter turn: Homestead's quaternion (Unity's).
		Assert.Contains("woodwall;Building;2;1;0;0;0.707;0;0.707;\"\";1;1;1", text);
		var parsed = BlueprintFormats.Parse("Hut.blueprint", text);
		Assert.Equal("Hut", parsed.Name);
		Assert.Equal(2, parsed.Pieces.Count);
		Assert.Equal(new Vector3(2, 1, 0), parsed.Pieces[1].Position);
		Assert.Equal(90, parsed.Pieces[1].Euler.Y, 1);
	}

	[Fact]
	public void AFolderListsItsBlueprintsWithTheirPictures()
	{
		Directory.CreateDirectory(_dir);
		File.WriteAllText(Path.Combine(_dir, "Hut.blueprint"), Homestead.Write(Clip(("wood_floor", 0, 0, 0, 0), ("woodwall", 0, 1, 1, 0)), "Hut", "Tester", null, DateTime.Now));
		File.WriteAllBytes(Path.Combine(_dir, "Hut.png"), new byte[] { 1 });
		File.WriteAllText(Path.Combine(_dir, "Old.blueprint"), "#Name:Old one\n#Pieces\nwood_floor;Misc;0;0;0;0;0;0;1;\"\";1;1;1\n");
		File.WriteAllText(Path.Combine(_dir, "notes.txt"), "not a blueprint");
		var list = Homestead.List(_dir);
		Assert.Equal(new[] { "Hut", "Old one" }, list.Select(e => e.Name));
		Assert.Equal(2, list[0].Pieces);
		Assert.Equal("Tester", list[0].Creator);
		Assert.NotNull(list[0].Picture);
		Assert.Null(list[1].Picture);
		Assert.Empty(Homestead.List(Path.Combine(_dir, "missing")));
		Assert.Equal("a_b.blueprint", Homestead.FileName("a/b"));
	}

	[Fact]
	public void DescriptionAndTagsAreWrittenReadAndChangedBeforeThePieces()
	{
		Directory.CreateDirectory(_dir);
		string file = Path.Combine(_dir, "Hut.blueprint");
		string text = Homestead.Write(Clip(("wood_floor", 0, 0, 0, 0), ("wood_floor", 2, 0, 0, 0)), "Hut", "Tester", null, DateTime.Now, "A small\nhut", new[] { "house", "wood" });
		// Homestead skips header lines it does not know, as long as they come before "#Pieces".
		Assert.True(text.IndexOf("#Tags:house, wood\n", StringComparison.Ordinal) < text.IndexOf("#Pieces", StringComparison.Ordinal));
		Assert.Contains("#Description:A small hut\n", text);
		File.WriteAllText(file, text);
		var e = Homestead.Read(file)!;
		Assert.Equal(("A small hut", 2), (e.Description, e.Kinds["wood_floor"]));
		Assert.Equal(new[] { "house", "wood" }, e.Tags);
		string changed = Homestead.WithDetails(text, new Homestead.Details("Big hut", "Bigger", new() { "house" }));
		Assert.StartsWith("#Name:Big hut\n#Creator:Tester\n#Description:Bigger\n#Tags:house\n#Category:Blueprints\n", changed);
		Assert.EndsWith(text[text.IndexOf("#Pieces", StringComparison.Ordinal)..], changed);
		Assert.DoesNotContain("#Tags:", Homestead.WithDetails(text, new Homestead.Details("Hut", "", new())));
		Assert.Equal(new[] { "house", "Viking", "stone" }, Homestead.Details.ParseTags("house, Viking,stone ,HOUSE;; "));
	}
}
