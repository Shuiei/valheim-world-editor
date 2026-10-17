using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The lairs of the goblin and dvergr rooms (DungeonDresser.Lairs and Lairs.Quarters), each dressed from its own seed: a
/// totem is five poles around a brazier or nothing, a den has one trash pile when it has its bedding, and the quarters
/// have one wardrobe in either arrangement.
/// </summary>
public class DungeonDresserLairsTests
{
    [Fact]
    public void ATotemIsFivePolesOrNothingAndFivePolesOccurOnSomeSeed()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var poles = new List<int>();
        for (int seed = 1; seed <= 20; seed++)
        {
            var (dresser, o) = DungeonDresserInstance.Of(room, seed);
            PrivateCall.Instance(dresser, "Totem", room.Frame);
            poles.Add(o.Items.Count(i => i.Prefab == "goblin_totempole"));
        }

        Assert.All(poles, n => Assert.True(n is 0 or 5, $"a totem with {n} poles"));
        Assert.Contains(5, poles);
    }

    [Fact]
    public void ADenWithItsBeddingHasOneTrashPile()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var piles = new List<int>();
        for (int seed = 1; seed <= 20; seed++)
        {
            var (dresser, o) = DungeonDresserInstance.Of(room, seed);
            PrivateCall.Instance(dresser, "Den", room.Frame);
            piles.Add(o.Items.Count(i => i.Prefab == "goblin_trashpile"));
        }

        Assert.All(piles, n => Assert.True(n <= 1, $"a den with {n} trash piles"));
        Assert.Contains(1, piles);
    }

    [Fact]
    public void TheQuartersHaveOneWardrobeInEitherArrangement()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        for (int seed = 1; seed <= 5; seed++)
        {
            var (dresser, o) = DungeonDresserInstance.Of(room, seed);
            PrivateCall.Instance(dresser, "Quarters", room.Frame);

            Assert.Single(o.Items, i => i.Prefab == "prop_chest_warderobe");
        }
    }
}
