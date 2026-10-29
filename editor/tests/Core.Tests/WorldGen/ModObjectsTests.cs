using ValheimGen;
using Xunit;
using Rnd = ValheimGen.UnityEngine.Random;

namespace WorldEditor.Tests;

/// <summary>
/// How Expand World Data swaps an object and gives it data (ModObjects): a weighted pick that draws only between
/// several choices, a removal, data for "all" then for the prefab.
/// </summary>
public class ModObjectsTests
{
    [Fact]
    public void APickDrawsOnlyBetweenSeveralChoices()
    {
        Rnd.InitState(3);
        var start = Rnd.State;
        Assert.Equal("a", ModObjects.Pick<string>([new(1f, "a")]));
        Assert.Equal("", ModObjects.Pick<string>([]));
        Assert.Equal(start, Rnd.State);
        // Several: one draw taken down by each weight; past them all, the last.
        float roll = Rnd.Value;
        Rnd.State = start;
        Assert.Equal(roll <= 0.5f ? "a" : "b", ModObjects.Pick<string>([new(0.5f, "a"), new(0.25f, "b")]));
        Assert.NotEqual(start, Rnd.State);
    }

    [Fact]
    public void ASwapReplacesOrRemovesAPrefab()
    {
        var swaps = new Dictionary<string, IReadOnlyList<Weighted<string>>> { ["Rock"] = [new(1f, "Bush")], ["Gone"] = [new(1f, "")] };
        Assert.Equal(("Bush", "", "Other"), (ModObjects.Swapped("Rock", swaps), ModObjects.Swapped("Gone", swaps), ModObjects.Swapped("Other", swaps)));
    }

    [Fact]
    public void DataForAllComesBeforeThePrefabs()
    {
        ContentField a = new("ints", 1, "1"), b = new("ints", 2, "2");
        var data = new Dictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> { ["Rock"] = [new(1f, [b])], ["all"] = [new(1f, [a])], ["Bush"] = [new(1f, null)] };
        List<ContentField> fields = [];
        ModObjects.AddData(fields, data, "Rock");
        Assert.Equal([a, b], fields);
        fields.Clear();
        ModObjects.AddData(fields, data, "Bush");
        Assert.Equal([a], fields);
    }
}
