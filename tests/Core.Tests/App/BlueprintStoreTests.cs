using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Saved copies (blueprints): files in a folder, listed with their size and content.
/// </summary>
public sealed class BlueprintStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..10]);

    /// <summary>
    /// Deletes the temporary blueprint folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// A 3 × 2 clipboard copy holding this many beeches, with ground in one cell or none.
    /// </summary>
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

    // A file that cannot be read is left out, and the others are still listed (the whole list threw).
    [Fact]
    public void AnUnreadableFileIsLeftOutOfTheList()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        var store = new BlueprintStore(_dir);
        store.Save("Kept", null, null, Clip(1, ground: false));
        string locked = store.Save("Locked", null, null, Clip(1, ground: false));
        File.SetUnixFileMode(Path.Combine(_dir, locked + ".json"), UnixFileMode.None);

        Assert.Equal("Kept", Assert.Single(store.List()).Name);
    }

    // Names Windows keeps for devices get a "_" (saving "con" failed there), within the 80 characters, and a cut ending
    // in a dot drops it.
    [Fact]
    public void IdsAvoidWindowsDeviceNamesAndATrailingDot()
    {
        Assert.Equal("_nul", BlueprintStore.IdFor("nul"));
        Assert.Equal("_COM1.old", BlueprintStore.IdFor("COM1.old"));
        Assert.Equal(new string('h', 79), BlueprintStore.IdFor(new string('h', 79) + ".more"));
        var store = new BlueprintStore(_dir);
        Assert.Equal("_con", store.Save("con", null, null, Clip(1, ground: false)));
        Assert.NotNull(store.Read("_con"));
        // A device name cut at 80 characters stays within them with its "_", so it can be read back (it could not).
        string id = store.Save("CON." + new string('a', 76), null, null, Clip(1, ground: false));
        Assert.Equal(80, id.Length);
        Assert.NotNull(store.Read(id));
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
