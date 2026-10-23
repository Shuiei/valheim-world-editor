using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// Pasted copies of the mod's pieces made pieces of their own: a vehicle leaves the original's track, trip and driver
/// behind and keeps its coal and signs; a station sign leaves the original's stop and keeps its name; the train's and
/// the deck's ids are left to the editor's ties (the manifest's), a piece no longer on a deck leaves its place there.
/// (Where the copy lands sets the rest: RailPlacerTests.)
/// </summary>
public class PastedCopiesTests
{
    private static readonly Key[] TrainState =
    [
        new("bytes", RailroadsMod.Keys.Line), new("float", RailroadsMod.Keys.At), new("int", RailroadsMod.Keys.Dir),
        new("bytes", RailroadsMod.Keys.Trail), new("bytes", RailroadsMod.Keys.Route), new("bytes", RailroadsMod.Keys.After),
        new("bytes", RailroadsMod.Keys.Legs), new("int", RailroadsMod.Keys.Moving),
        new("float", RailroadsMod.Keys.Speed), new("long", RailroadsMod.Keys.Driver), new("string", RailroadsMod.Keys.Destination),
        new("int", RailroadsMod.Keys.Switch), new("int", RailroadsMod.Keys.Gear), new("int", RailroadsMod.Keys.Way),
        new("float", RailroadsMod.Keys.Stop),
    ];

    /// <summary>A value of a type under a key.</summary>
    private sealed record Key(string Type, string Name);

    private static void Rewrite(FakeObject copy) => new PastedCopies().Rewrite(copy, new NoIds());

    [Fact]
    public void APastedTrainLeavesTheOriginalsTrackTripAndWagons()
    {
        var copy = new FakeObject(RailroadsMod.Locomotive);
        foreach (var key in TrainState)
        {
            Put(copy, key);
        }
        copy.Ints[RailroadsMod.Keys.Coal] = 40;
        copy.Ints[RailroadsMod.Keys.Trips] = 3;
        copy.Floats[RailroadsMod.Keys.Odometer] = 12.5f;

        Rewrite(copy);

        Assert.Equal(new[] { RailroadsMod.Keys.Coal, RailroadsMod.Keys.Odometer, RailroadsMod.Keys.Trips }.Order(StringComparer.Ordinal),
            copy.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(40, copy.Ints[RailroadsMod.Keys.Coal]);
    }

    [Fact]
    public void OlderSavesTextTiesGoToo()
    {
        var copy = new FakeObject("bf_wagon");
        copy.Strings[RailroadsMod.Keys.Self] = "123:4";
        copy.Strings[RailroadsMod.Keys.Line] = "1,2,3;1,2,11";
        copy.Strings[RailroadsMod.Keys.Sign] = "Coal for the mine";

        Rewrite(copy);

        Assert.Equal([RailroadsMod.Keys.Sign], copy.Keys);
    }

    [Fact]
    public void AWagonsOwnIdIsLeftToTheTrainsTie()
    {
        var copy = new FakeObject("bf_wagon");
        copy.Bytes[RailroadsMod.Keys.Self] = ObjectIds.Write([(77, 5)]);
        copy.Bytes[RailroadsMod.Keys.Line] = TrackLines.Write([(Vector3.Zero, Vector3.UnitZ)]);
        copy.Strings[RailroadsMod.Keys.Sign] = "Coal";

        Rewrite(copy);

        Assert.Equal(new[] { RailroadsMod.Keys.Self, RailroadsMod.Keys.Sign }.Order(StringComparer.Ordinal), copy.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void APastedHandCartIsHeldByNobody()
    {
        var copy = new FakeObject(RailroadsMod.Handcart);
        copy.Longs[RailroadsMod.Keys.Front] = 1;
        copy.Longs[RailroadsMod.Keys.Back] = 2;
        copy.Floats[RailroadsMod.Keys.Beam] = 0.5f;

        Rewrite(copy);

        Assert.Equal([RailroadsMod.Keys.Beam], copy.Keys);
    }

    [Fact]
    public void APastedStationSignKeepsItsNameNotTheOriginalsStop()
    {
        var copy = new FakeObject("bf_station_sign");
        copy.Vectors[RailroadsMod.Keys.Stop] = new Vector3(10, 30, 12);
        copy.Strings[RailroadsMod.Keys.Name] = "Riverside";

        Rewrite(copy);

        Assert.Equal([RailroadsMod.Keys.Name], copy.Keys);
    }

    [Fact]
    public void APieceOnADeckKeepsItsPlaceThereWhileItsDeckWasCopied()
    {
        var flatbed = new FakeObject("bf_wagon_flat");
        flatbed.Longs[RailroadsMod.Keys.DeckKey] = 99;
        var floor = new FakeObject("wood_floor");
        floor.Longs[RailroadsMod.Keys.Deck] = 99;
        floor.Vectors[RailroadsMod.Keys.DeckPos] = new Vector3(1, 1.5f, 0);
        floor.Quaternions[RailroadsMod.Keys.DeckRot] = Quaternion.Identity;
        // Its flatbed not copied: the editor's tie took its deck's id away.
        var loose = new FakeObject("wood_floor");
        loose.Vectors[RailroadsMod.Keys.DeckPos] = new Vector3(1, 1.5f, 0);
        loose.Quaternions[RailroadsMod.Keys.DeckRot] = Quaternion.Identity;
        loose.Ints["health"] = 400;

        Rewrite(flatbed);
        Rewrite(floor);
        Rewrite(loose);

        Assert.Equal([RailroadsMod.Keys.DeckKey], flatbed.Keys);
        Assert.Equal(3, floor.Keys.Count());
        Assert.Equal(["health"], loose.Keys);
    }

    [Fact]
    public void EveryKeyTakenAwayIsOneTheRewriterHandles()
    {
        var copy = new FakeObject(RailroadsMod.Locomotive);
        foreach (var key in TrainState)
        {
            Put(copy, key);
        }
        foreach (string name in copy.Keys)
        {
            Assert.Contains(name, PastedCopies.Keys);
        }
        Assert.Contains(RailroadsMod.Keys.DeckRot, PastedCopies.Keys);
        // The ties' keys are the editor's to rewrite (the manifest's "ties").
        Assert.DoesNotContain(RailroadsMod.Keys.Self, PastedCopies.Keys);
        Assert.DoesNotContain(RailroadsMod.Keys.Wagons, PastedCopies.Keys);
        Assert.DoesNotContain(RailroadsMod.Keys.DeckKey, PastedCopies.Keys);
        Assert.DoesNotContain(RailroadsMod.Keys.Deck, PastedCopies.Keys);
    }

    /// <summary>Some value of the key's type.</summary>
    private static void Put(FakeObject o, Key key)
    {
        switch (key.Type)
        {
            case "int": o.Ints[key.Name] = 1; break;
            case "long": o.Longs[key.Name] = 2; break;
            case "float": o.Floats[key.Name] = 3; break;
            case "string": o.Strings[key.Name] = "Riverside"; break;
            default: o.Bytes[key.Name] = new byte[24]; break;
        }
    }
}
