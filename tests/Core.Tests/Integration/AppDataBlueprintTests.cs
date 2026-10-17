using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The blueprint store: saved, listed, read and deleted, its file names, and the refusals.
/// </summary>
[Collection("World files")]
public class AppDataBlueprintTests
{
    private static readonly string[] Expected1 = ["Dated", "My house", "Shed"];
    /// <summary>
    /// A 2 × 1 clipboard copy holding this many beeches, with ground in its first cell or none.
    /// </summary>
    private static JsonObject Clip(int objects = 1, bool ground = false) => new()
    {
        ["w"] = 2,
        ["h"] = 1,
        ["rel"] = new JsonArray(ground ? 120 : -32768, -32768),
        ["objects"] = new JsonArray(Enumerable.Range(0, objects).Select(_ => (JsonNode)new JsonObject { ["name"] = "Beech1" }).ToArray()),
    };

    [Fact]
    public void BlueprintsAreSavedListedReadAndDeleted()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var store = new BlueprintStore(dir);
            Assert.Empty(store.List());
            string id = store.Save("  My house ", "Midgard", "data:thumb", Clip(3, ground: true), "planbuild");
            Assert.Equal("My house", id);
            store.Save("Shed", null, null, Clip());
            // Not blueprints: other JSON, broken JSON, a half-written file.
            File.WriteAllText(Path.Combine(dir, "other.json"), "{\"name\":\"x\"}");
            File.WriteAllText(Path.Combine(dir, "broken.json"), "{ nope");
            File.WriteAllText(Path.Combine(dir, "Shed.json.tmp"), "{");
            // A blueprint with a bad date: the file's own time is used.
            File.WriteAllText(Path.Combine(dir, "Dated.json"), "{\"name\":\"Dated\",\"created\":\"yesterday\",\"clip\":{\"w\":1,\"h\":1,\"objects\":[]}}");
            var list = store.List();
            Assert.Equal(Expected1, list.Select(b => b.Name).ToArray());
            var house = list[1];
            Assert.Equal((3, true, "Midgard", "data:thumb", "planbuild"), (house.Objects, house.Ground, house.World, house.Thumb, house.Source));
            Assert.False(list[2].Ground);
            Assert.True(store.Exists(" My house "));
            Assert.False(store.Exists("Barn"));
            Assert.Contains("\"name\":\"My house\"", store.Read("My house"));
            // Ids that would leave the folder or are not file names are refused.
            Assert.Null(store.Read("../My house"));
            Assert.Null(store.Read(""));
            Assert.False(store.Delete("../x"));
            Assert.False(store.Delete("nothing"));
            Assert.True(store.Delete("Shed"));
            Assert.Null(store.Read("Shed"));
            // The same name again replaces it.
            store.Save("My house", null, null, Clip(5));
            Assert.Equal(5, store.List().Single(b => b.Id == "My house").Objects);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void ABlueprintNeedsANameAndACopy()
    {
        var store = new BlueprintStore(Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..8]));
        Assert.Throws<ArgumentException>(() => store.Save("  ", null, null, Clip()));
        Assert.Throws<ArgumentException>(() => store.Save("x", null, null, new JsonObject { ["w"] = 1 }));
        Assert.Throws<ArgumentException>(() => store.Save("x", null, null, new JsonObject { ["objects"] = new JsonArray(), ["w"] = 1 }));
    }

    [Theory]
    [InlineData("Tower: north/east", "Tower_ north_east")]
    [InlineData("..", "blueprint")]
    [InlineData("  ...  ", "blueprint")]
    [InlineData("a.b-c_d e", "a.b-c_d e")]
    public void BlueprintFileNamesAreSafe(string name, string id) => Assert.Equal(id, BlueprintStore.IdFor(name));

    [Fact]
    public void LongBlueprintNamesAreCut() => Assert.Equal(80, BlueprintStore.IdFor(new string('x', 200)).Length);

    [Fact]
    public void TheDefaultStoreIsInTheDataFolder() => Assert.EndsWith("blueprints", BlueprintStore.Default.Directory);
}
