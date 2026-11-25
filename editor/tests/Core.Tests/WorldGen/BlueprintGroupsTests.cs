using ValheimGen;
using Xunit;
using NVector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Rnd = ValheimGen.UnityEngine.Random;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data 1.81's blueprint groups: one draw per group picks at most one of its parts by their weights, the
/// draws taken before any part's own chance.
/// </summary>
public class BlueprintGroupsTests
{
    private static BlueprintPart Part(string prefab, float chance, string? group = null) =>
        new(prefab, NVector3.Zero, Quaternion.Identity, NVector3.One, null, chance, group);

    [Fact]
    public void OneOfAGroupIsMadeWhenItsWeightsAddUpToOne()
    {
        BlueprintPart a = Part("A", 0.5f, "a"), b = Part("B", 0.5f, "a");
        for (int seed = 0; seed < 50; seed++)
        {
            Rnd.InitState(seed);
            var picked = BlueprintGroups.Pick([a, b]);
            Assert.Equal(1, new[] { a, b }.Count(p => BlueprintGroups.Made(p, picked)));
        }
    }

    [Fact]
    public void AGroupDrawsOnceThenItsPickFollowsTheWeights()
    {
        BlueprintPart a = Part("A", 0.25f, "g"), b = Part("B", 0.5f, "g"), plain = Part("C", 0.5f);
        Rnd.InitState(7);
        float draw = Rnd.Value, next = Rnd.Value;
        Rnd.InitState(7);
        var picked = BlueprintGroups.Pick([a, plain, b]);
        // Weights below 1 in all: the draw is not stretched, and above their total nothing is picked.
        var expected = draw < 0.25f ? a : draw < 0.75f ? b : null;
        Assert.Same(expected, picked["g"]);
        // The part of no group draws its own chance next.
        Assert.Equal(next <= 0.5f, BlueprintGroups.Made(plain, picked));
    }

    [Fact]
    public void WeightsOverOneAreScaledDownAndNoNumberIsOne()
    {
        BlueprintPart a = Part("A", 1f, "g"), b = Part("B", 3f, "g");
        Rnd.InitState(11);
        float draw = Rnd.Value;
        Rnd.InitState(11);
        Assert.Same(draw * 4f < 1f ? a : b, BlueprintGroups.Pick([a, b])["g"]);
    }
}
