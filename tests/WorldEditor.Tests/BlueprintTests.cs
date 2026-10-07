using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// Saved copies (blueprints): files in a folder, listed with their size and content.
public sealed class BlueprintTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..10]);

	public void Dispose()
	{
		try
		{
			Directory.Delete(_dir, recursive: true);
		}
		catch
		{
		}
	}

	private static JsonObject Clip(int objects, bool ground) => new()
	{
		["w"] = 3,
		["h"] = 2,
		["rel"] = new JsonArray(Enumerable.Range(0, 6).Select(i => (JsonNode)(ground && i == 2 ? 150 : -32768)).ToArray()),
		["objects"] = new JsonArray(Enumerable.Range(0, objects).Select(i => (JsonNode)new JsonObject { ["name"] = "Beech1", ["dx"] = i, ["dz"] = 0, ["dy"] = 0 }).ToArray()),
	};

	[Fact]
	public void SavedBlueprintsAreListedAndReadBack()
	{
		var store = new BlueprintStore(_dir);
		string id = store.Save("My hall", "CITest", "data:image/png;base64,AAAA", Clip(3, ground: true));
		var b = Assert.Single(store.List());
		Assert.Equal(id, b.Id);
		Assert.Equal("My hall", b.Name);
		Assert.Equal("CITest", b.World);
		Assert.Equal(3, b.Objects);
		Assert.True(b.Ground);
		Assert.Equal((3, 2), (b.W, b.H));
		JsonNode doc = JsonNode.Parse(store.Read(id)!)!;
		Assert.Equal(3, doc["clip"]!["objects"]!.AsArray().Count);
	}

	[Fact]
	public void SameNameReplacesAndDeleteRemoves()
	{
		var store = new BlueprintStore(_dir);
		store.Save("Gate", null, null, Clip(1, ground: false));
		string id = store.Save("Gate", null, null, Clip(5, ground: false));
		var b = Assert.Single(store.List());
		Assert.Equal(5, b.Objects);
		Assert.False(b.Ground);
		Assert.True(store.Exists("Gate"));
		Assert.True(store.Delete(id));
		Assert.Empty(store.List());
	}

	[Fact]
	public void NamesCannotLeaveTheFolder()
	{
		var store = new BlueprintStore(_dir);
		string id = store.Save("../../evil/name", null, null, Clip(1, ground: false));
		Assert.DoesNotContain("/", id);
		Assert.True(File.Exists(Path.Combine(_dir, id + ".json")));
		Assert.Null(store.Read("../" + id));
		Assert.False(store.Delete("../" + id));
	}

	[Fact]
	public void SomethingThatIsNotACopyIsRefused()
	{
		var store = new BlueprintStore(_dir);
		Assert.Throws<ArgumentException>(() => store.Save("x", null, null, new JsonObject { ["w"] = 1 }));
		Assert.Throws<ArgumentException>(() => store.Save("  ", null, null, Clip(1, false)));
	}
}
