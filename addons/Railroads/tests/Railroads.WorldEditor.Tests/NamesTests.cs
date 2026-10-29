using System.Numerics;
using System.Reflection;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// Train names against what Railroads 0.2.9 did on a server running it (fixtures/railroads-names.txt): the server numbers
/// every train it has not numbered, keeps the last number on its register at the world's centre, and never numbers a
/// train again, so a copy keeping the original's number goes by it too for good. A train pasted as the add-on pastes it
/// keeps its name and gets a number of its own. Every value the mod keeps there is one the add-on names.
/// </summary>
public class NamesTests
{
    private static readonly List<FakeObject> Trains = [.. Reference.Names.Select(r => r.Object).Where(o => o.Prefab == RailroadsMod.Locomotive)];

    /// <summary>The recorded train, the one the world had (the others were made beside it, along x).</summary>
    private static FakeObject Original => Trains.MinBy(t => t.Position.X)!;

    /// <summary>The locomotive made so many metres along x from the recorded train.</summary>
    private static FakeObject MadeAt(float x) => Assert.Single(Trains, t => Math.Abs(t.Position.X - Original.Position.X - x) < 0.01f);

    private static int Number(FakeObject train) => train.Ints[RailroadsMod.Keys.TrainNumber];

    [Fact]
    public void EveryTrainIsNumberedAndTheRegisterKeepsTheLastNumber()
    {
        var register = Assert.Single(Reference.Names, r => r.Object.Prefab == RailroadsMod.Register).Object;
        var handcart = Assert.Single(Reference.Names, r => r.Object.Prefab == RailroadsMod.Handcart).Object;

        Assert.Equal(4, Trains.Count);
        Assert.All(Trains, t => Assert.True(Number(t) > 0));
        Assert.Equal(Vector3.Zero, register.Position);
        Assert.Equal(Trains.Max(Number), register.Ints[RailroadsMod.Keys.TrainCount]);
        // The hand cart is no train.
        Assert.DoesNotContain(RailroadsMod.Keys.TrainNumber, handcart.Keys);
    }

    [Fact]
    public void ATrainPastedAsTheAddOnPastesItKeepsItsNameAndGetsANumberOfItsOwn()
    {
        var copy = new FakeObject(RailroadsMod.Locomotive);
        copy.Strings[RailroadsMod.Keys.TrainName] = Original.Strings[RailroadsMod.Keys.TrainName];
        copy.Ints[RailroadsMod.Keys.TrainNumber] = Number(Original);

        new PastedCopies().Rewrite(copy, new NoIds());

        // Made so on the server (+20 m), it was numbered anew; one keeping the number (+30 m) goes by the original's.
        var pasted = MadeAt(20);
        Assert.DoesNotContain(RailroadsMod.Keys.TrainNumber, copy.Keys);
        Assert.Equal(Original.Strings[RailroadsMod.Keys.TrainName], copy.Strings[RailroadsMod.Keys.TrainName]);
        Assert.Equal(copy.Strings[RailroadsMod.Keys.TrainName], pasted.Strings[RailroadsMod.Keys.TrainName]);
        Assert.Single(Trains, t => Number(t) == Number(pasted));
        Assert.Equal(Number(Original), Number(MadeAt(30)));
    }

    [Fact]
    public void EveryValueTheModKeepsOnTrainsIsOneTheAddOnNames()
    {
        var named = typeof(RailroadsMod.Keys).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!)
            .Concat(["creator", "creatorIndex"]).ToHashSet();

        Assert.All(Reference.Names.SelectMany(r => r.Object.Keys), k => Assert.Contains(k, named));
    }
}
