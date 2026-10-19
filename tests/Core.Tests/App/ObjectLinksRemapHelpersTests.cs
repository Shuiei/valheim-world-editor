using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The steps of remapping a pasted group's links: the new ids of a copy, the top of a part's chain, the mirrored
/// placement, the placement a detached part loses, and the values a rule leaves out of copies.
/// </summary>
public class ObjectLinksRemapHelpersTests
{
    private static readonly LinkRule Rule = new("group", 1, [2], 3, 4, 5, [6], [31, 33], true);

    private static ObjectLink Link(int id, params int[] parents) => new(Rule, id, parents, Vector3.Zero, null, null);

    [Fact]
    public void EachIdOfACopyIsGivenANewOneAndTheFirstObjectHoldingItIsKept()
    {
        var taken = new HashSet<int> { 1 };
        var links = new List<ObjectLink?> { Link(7), Link(7), null };
        var (at, ids) = ((Dictionary<int, int> At, Dictionary<int, int> Ids))PrivateMember.Call(typeof(ObjectLinks), "NewCopyIds", null, links, taken, new Random(1))!;
        Assert.Equal(new Dictionary<int, int> { [7] = 0 }, at);
        Assert.Equal(7, Assert.Single(ids.Keys));
        Assert.NotEqual(1, ids[7]);
        Assert.Contains(ids[7], taken);
        Assert.Equal(2, taken.Count);
    }

    [Fact]
    public void AnObjectWithoutAnIdGetsNoNewOne()
    {
        var taken = new HashSet<int> { 1 };
        var links = new List<ObjectLink?> { null, Link(0) };
        var (at, ids) = ((Dictionary<int, int> At, Dictionary<int, int> Ids))PrivateMember.Call(typeof(ObjectLinks), "NewCopyIds", null, links, taken, new Random(1))!;
        Assert.Empty(at);
        Assert.Empty(ids);
        Assert.Single(taken);
    }

    [Fact]
    public void TheTopOfAChainIsTheRootItHangsFrom()
    {
        var links = new List<ObjectLink?> { Link(5), Link(6, 5), Link(7, 6) };
        var at = new Dictionary<int, int> { [5] = 0, [6] = 1, [7] = 2 };
        Assert.Equal(0, PrivateMember.Call(typeof(ObjectLinks), "TopOfPart", null, links, at, 2));
        Assert.Equal(0, PrivateMember.Call(typeof(ObjectLinks), "TopOfPart", null, links, at, 0));
    }

    [Fact]
    public void AChainThatLoopsStopsAtThePartItReachesFirstTwice()
    {
        // 5 hangs on 6 and 6 on 5: the walk stops where it would go round again, at 6.
        var links = new List<ObjectLink?> { Link(5, 6), Link(6, 5) };
        var at = new Dictionary<int, int> { [5] = 0, [6] = 1 };
        Assert.Equal(1, PrivateMember.Call(typeof(ObjectLinks), "TopOfPart", null, links, at, 0));
    }

    [Fact]
    public void AMirroredPartHasItsOffsetAndTurnsMirroredInItsParentsFrame()
    {
        var rule = new LinkRule("group", 1, [2], 11, 12, 13, [], [], true);
        var link = new ObjectLink(rule, 7, [], new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Quaternion(0.1f, 0.2f, 0.3f, 0.4f));
        var data = new List<ObjectField>();
        PrivateMember.Call(typeof(ObjectLinks), "MirrorPlacement", null, data, rule, link);
        Assert.Equal("-1 2 3", data.Find(f => f.Section == "vec3" && f.Key == 11)!.Value);
        Assert.Equal("4 -5 -6", data.Find(f => f.Section == "vec3" && f.Key == 12)!.Value);
        Assert.Equal("0.1 -0.2 -0.3 0.4", data.Find(f => f.Section == "quats" && f.Key == 13)!.Value);
    }

    [Fact]
    public void ADetachedPartLosesItsParentAndItsPlacement()
    {
        var rule = new LinkRule("group", 1, [21], 11, 12, 13, [], [], true);
        var data = new List<ObjectField>
        {
            new("ints", 21, "5"),
            new("vec3", 11, "1 2 3"),
            new("vec3", 12, "1 1 1"),
            new("quats", 13, "0 0 0 1"),
            new("ints", 99, "keep"),
        };
        PrivateMember.Call(typeof(ObjectLinks), "ClearPlacement", null, data, rule);
        Assert.DoesNotContain(data, f => f.Key is 21 or 11 or 12 or 13 && f.Value != null);
        Assert.Equal("keep", data.Find(f => f.Key == 99)!.Value);
    }

    [Fact]
    public void ValuesARuleLeavesOutOfCopiesAreTakenAwayUnlessAnExtensionRewritesThem()
    {
        var data = new List<ObjectField> { new("ints", 31, "a"), new("ints", 32, "b"), new("ints", 33, "c") };
        PrivateMember.Call(typeof(ObjectLinks), "DropUnkept", null, data, Rule, new HashSet<int> { 33 });
        Assert.Null(data[0].Value);
        Assert.Equal("b", data[1].Value);
        Assert.Equal("c", data[2].Value);
    }
}
