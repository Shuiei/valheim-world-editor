using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// --export-test-data's checks on a pretend home (see GameHome) and how it trims the copies: the copies folder,
/// the game and every copy needed, the game's own entries kept, the piece costs with their pieces and items, the
/// dungeons with their rooms, and the vegetation rules by name and prefab. The tests' copies are in use again
/// afterwards.
/// </summary>
[Collection("DataDir")]
public sealed class GameDataCopyTests : IDisposable
{
    private readonly GameHome _sandbox = new("vwe-gamedata-");

    /// <summary>
    /// Puts the tests' copies back, then the pretend home (its environment, settings and folder).
    /// </summary>
    public void Dispose()
    {
        GameData.UseCopies(Fixtures.Root);
        _sandbox.Dispose();
    }

    [Fact]
    public void ExportNeedsTheCopiesFolderTheGameAndEveryCopy()
    {
        Assert.Equal(2, GameData.Export(null));
        Assert.Equal(2, GameData.Export(Path.Combine(_sandbox.Folder, "no-such-folder")));
        string copies = Path.Combine(_sandbox.Folder, "fixtures");
        Directory.CreateDirectory(copies);
        // No Valheim in the pretend home, nor set in the settings.
        Assert.Equal(1, GameData.Export(copies));
        var (valheim, _) = _sandbox.Game("Valheim", code: false);
        new AppSettings { ValheimPath = valheim }.Save();
        // A copy missing.
        Assert.Equal(1, GameData.Export(copies));
        foreach (string kind in GameData.KindNames)
        {
            File.WriteAllText(Path.Combine(copies, kind + ".json"), GameHome.Copy(kind));
        }
        // The game's data cannot be read (no bundles, no code): the copies are left as they were.
        Assert.Equal(1, GameData.Export(copies));
        Assert.All(GameData.KindNames, k => Assert.Equal(GameHome.Copy(k), File.ReadAllText(Path.Combine(copies, k + ".json"))));
    }

    [Fact]
    public void ACopyKeepsTheGamesEntriesItHad()
    {
        var (text, kept, now) = GameData.Trim("terrain-modifiers", """{"a":[1,2],"b":[3],"c":5,"d":6,"e":7}""", """{"a":0,"c":0,"x":0}""");
        // One entry a line, each written compact.
        Assert.Equal("{\n  \"a\": [1,2],\n  \"c\": 5\n}\n", text);
        Assert.Equal(["a", "c", "x"], kept);
        Assert.Equal(["a", "c"], now);
        // A copy with no entries stays empty.
        Assert.Equal("{}\n", GameData.Trim("prefabs", """{"a":1}""", "{}").Text);
    }

    [Fact]
    public void APieceCostCopyKeepsItsPiecesAndTheItemsTheyTake()
    {
        string game = """
            {"items":{"Wood":"Wood","Stone":"Stone","Flint":"Flint"},"pieces":{"wall":{"n":"Wall","r":[["Wood",2]]},
            "floor":{"n":"Floor","r":[["Wood",1],["Stone",1]]},"beam":{"n":"Beam","r":[["Wood",1]]},"pole":{"n":"Pole","r":[["Wood",1]]},
            "door":{"n":"Door","r":[["Wood",4]]},"gone":{"n":"Gone","r":[["Flint",1]]}}}
            """;
        string copy = """{"items":{"Wood":"Wood"},"pieces":{"wall":{},"floor":{},"beam":{},"pole":{},"door":{},"old":{}}}""";
        var (text, kept, now) = GameData.Trim("piece-cost", game, copy);
        // Its two groups opened, one entry a line.
        Assert.Equal("""
            {
              "items": {
                "Wood": "Wood",
                "Stone": "Stone"
              },
              "pieces": {
                "wall": {"n":"Wall","r":[["Wood",2]]},
                "floor": {"n":"Floor","r":[["Wood",1],["Stone",1]]},
                "beam": {"n":"Beam","r":[["Wood",1]]},
                "pole": {"n":"Pole","r":[["Wood",1]]},
                "door": {"n":"Door","r":[["Wood",4]]}
              }
            }

            """.ReplaceLineEndings("\n"), text);
        Assert.Equal(["items/Wood", "pieces/wall", "pieces/floor", "pieces/beam", "pieces/pole", "pieces/door", "pieces/old"], kept);
        Assert.Equal(["items/Wood", "items/Stone", "pieces/wall", "pieces/floor", "pieces/beam", "pieces/pole", "pieces/door"], now);
    }

    [Fact]
    public void ADungeonCopyKeepsEveryDungeonAndItsRooms()
    {
        string game = """{"dungeons":{"Crypt":{"k":1},"Cave":{"k":2}},"rooms":{"r1":{"s":1},"r2":{"s":2}}}""";
        var (text, kept, now) = GameData.Trim("dungeon-rooms", game, """{"dungeons":{},"rooms":{"r2":{}}}""");
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(2, doc.RootElement.GetProperty("dungeons").EnumerateObject().Count());
        Assert.Equal(["r2"], doc.RootElement.GetProperty("rooms").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["rooms/r2"], kept);
        Assert.Equal(["dungeons/Crypt", "dungeons/Cave", "rooms/r2"], now);
    }

    [Fact]
    public void AVegetationCopyKeepsTheRulesItHadByNameAndPrefab()
    {
        string game = """[{"name":"A","prefab":"p1","x":1},{"name":"A","prefab":"p2"},{"name":"B","prefab":"p1"}]""";
        var (text, kept, now) = GameData.Trim("vegetation", game, """[{"name":"A","prefab":"p1"},{"name":"C","prefab":"p9"}]""");
        Assert.Equal("[\n  {\"name\":\"A\",\"prefab\":\"p1\",\"x\":1}\n]\n", text);
        Assert.Equal(["A/p1", "C/p9"], kept);
        Assert.Equal(["A/p1"], now);
        Assert.Equal("[]\n", GameData.Trim("vegetation", game, "[]").Text);
    }
}
