using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The prefab names in the game's own prefab list: the keys of its JSON object, each once.
/// </summary>
public class GameDataPrefabNamesTests
{
    [Fact]
    public void EachKeyOfThePrefabListIsAPrefabName()
    {
        var names = (HashSet<string>)PrivateCall.Static(typeof(GameData), "PrefabNames", """{"Wood":{"a":1},"Stone":{}}""")!;

        Assert.Equal(new[] { "Stone", "Wood" }, names.Order().ToArray());
    }

    [Fact]
    public void AnEmptyPrefabListHasNoNames()
    {
        var names = (HashSet<string>)PrivateCall.Static(typeof(GameData), "PrefabNames", "{}")!;

        Assert.Empty(names);
    }
}
