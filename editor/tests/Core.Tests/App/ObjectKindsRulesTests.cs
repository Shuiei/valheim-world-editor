using System.Reflection;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The name rules that sort an object into its kind when the game's data does not: the dungeon, spawner, creature and
/// piece rules, and each entry of the ordered rule table, with one name it must take.
/// </summary>
[Collection("World files")]
public class ObjectKindsRulesTests
{
    private static bool IsRule(string helper, string name) =>
        PrivateMember.Call<bool>(typeof(ObjectKinds), helper, null, name);

    [Theory]
    [InlineData("DG_Crypt", true)]
    [InlineData("Rock_4", false)]
    public void ADungeonIsAnObjectNamedDgOrARoomOfOne(string name, bool dungeon) =>
        Assert.Equal(dungeon, IsRule("IsDungeonName", name));

    [Fact]
    public void ASpawnerTheGameDoesNotDescribeHasNoSpawnKind()
    {
        // The kind comes from the game's spawner data; a name it does not know gives no kind here.
        Assert.Null(PrivateMember.Call(typeof(ObjectKinds), "SpawnKindOf", null, "Spawner_Unknown_Thing"));
    }

    [Theory]
    [InlineData("Haldor", true)]
    [InlineData("Rock_4", false)]
    public void ACharacterByNameIsACreature(string name, bool creature) =>
        Assert.Equal(creature, IsRule("IsCreatureName", name));

    [Theory]
    [InlineData("goblin_totem", true)]
    [InlineData("Rock_4", false)]
    public void AStructureOfTheGameByNameIsARuin(string name, bool ruin) =>
        Assert.Equal(ruin, IsRule("IsPieceName", name));

    [Theory]
    [InlineData(0, "Spawner_Greydwarf_Nest", ObjectKind.Nests)]
    [InlineData(1, "Spawner_Greydwarf", ObjectKind.SpawnPoints)]
    [InlineData(2, "Haldor", ObjectKind.Creatures)]
    [InlineData(3, "vfx_fire", ObjectKind.Effects)]
    [InlineData(4, "goblin_totem", ObjectKind.Ruins)]
    [InlineData(5, "Pickable_Mushroom", ObjectKind.Pickables)]
    [InlineData(6, "skeleton_ragdoll", ObjectKind.Remains)]
    [InlineData(7, "chair_wood", ObjectKind.Furniture)]
    [InlineData(8, "beech_tree", ObjectKind.Trees)]
    [InlineData(9, "piece_Charred_wall", ObjectKind.Ruins)]
    [InlineData(10, "tombstone", ObjectKind.Other)]
    [InlineData(11, "copper_ore", ObjectKind.Ore)]
    [InlineData(12, "rock_4", ObjectKind.Rocks)]
    [InlineData(13, "bush_01", ObjectKind.Bushes)]
    public void EachNameRuleTakesItsOwnNamesAndSaysItsKind(int index, string name, ObjectKind kind)
    {
        // The table is read in the order it is checked: entry i holds its matcher and the kind it gives.
        var table = (Array)typeof(ObjectKinds).GetField("NameRules", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        object entry = table.GetValue(index)!;
        var matches = (Func<string, bool>)entry.GetType().GetField("Item1")!.GetValue(entry)!;
        var given = (ObjectKind)entry.GetType().GetField("Item2")!.GetValue(entry)!;
        Assert.Equal(kind, given);
        Assert.True(matches(name), $"rule {index} does not take {name}");
    }
}
