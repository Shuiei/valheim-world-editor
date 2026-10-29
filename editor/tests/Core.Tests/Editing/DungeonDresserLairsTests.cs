using System.Numerics;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The lairs of the goblin and dvergr rooms (DungeonDresser.Lairs and Lairs.Quarters), each dressed from its own seed: a
/// totem is a brazier ringed by five poles or a row of poles along the far wall, a den has one trash pile when it has its
/// bedding, and the quarters have one wardrobe in either arrangement.
/// </summary>
public class DungeonDresserLairsTests
{
    [Fact]
    public void ATotemRingIsFivePolesOrNothingAndFivePolesOccurOnSomeSeed()
    {
        var ring = new List<int>();
        for (int seed = 1; seed <= 20; seed++)
        {
            // A room of its own for each seed: what one dressing keeps taken would block the next one's poles.
            var room = DungeonDresserSupport.ClearRoom("Crypt");
            var (dresser, o) = DungeonDresserInstance.Of(room, seed);
            PrivateCall.Instance(dresser, "Totem", room.Frame);
            // The ring's poles stand 3.2 m from the middle; the row along the far wall stands 4 m or more from it.
            ring.Add(o.Items.Count(i => i.Prefab == "goblin_totempole"
                && Vector2.Distance(new(i.Position.X, i.Position.Z), new(room.Frame.Cx, room.Frame.Cz)) < 3.5f));
        }

        Assert.All(ring, n => Assert.True(n is 0 or 5, $"a totem ring with {n} poles"));
        Assert.Contains(5, ring);
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
        for (int seed = 1; seed <= 5; seed++)
        {
            // A room of its own for each seed, with its far end clear: the wardrobe stands against the far wall.
            var room = DungeonDresserSupport.ClearRoom("Crypt", farEndClear: true);
            var (dresser, o) = DungeonDresserInstance.Of(room, seed);
            PrivateCall.Instance(dresser, "Quarters", room.Frame);

            Assert.Single(o.Items, i => i.Prefab == "prop_chest_warderobe");
        }
    }
}
