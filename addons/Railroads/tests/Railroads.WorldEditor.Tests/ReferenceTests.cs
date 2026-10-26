using System.Numerics;
using WorldEditor.Tests;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The add-on against what the real Railroads wrote on a server running it (fixtures/railroads-reference.txt: track
/// built with its hammer's code, a station sign, a train of a locomotive, a wagon and a flatbed with a floor on its deck,
/// sent to the station, and a hand cart): every value it keeps as bytes is read; its wagons are named by the game's ids
/// as the add-on reads them; and the add-on puts its vehicles on the same lines at the same points, and the sign's stop
/// at the same place, as the mod did.
/// </summary>
public class ReferenceTests
{
    /// <summary>The real mod's models, downloaded for the tests.</summary>
    private static readonly ModelLibrary Models = new(
        new FakeHost { Plugin = Path.Combine(TestMods.Folder, "VladasZ-Railroads", "plugins", "Railroads", RailroadsMod.DllName) }, Assert.Fail);

    private static readonly string[] LineKeys = [RailroadsMod.Keys.Line, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route, RailroadsMod.Keys.After];

    [Fact]
    public void EveryValueKeptAsBytesIsRead()
    {
        int read = 0;
        foreach (var o in Reference.Objects.Select(r => r.Object))
        {
            foreach (var (key, bytes) in o.Bytes)
            {
                string? text = key switch
                {
                    RailroadsMod.Keys.Wagons or RailroadsMod.Keys.Self => new ObjectIds().Describe(o, bytes),
                    RailroadsMod.Keys.Legs => new TripLegs().Describe(o, bytes),
                    _ when LineKeys.Contains(key) => new TrackLines().Describe(o, bytes),
                    _ => throw new InvalidDataException($"{o.Prefab} keeps {key} as bytes, which the add-on does not read"),
                };
                Assert.NotNull(text);
                read++;
            }
        }
        Assert.Equal(11, read);
    }

    [Fact]
    public void TheTrainNamesItsWagonsByTheGamesIds()
    {
        var train = Assert.Single(Reference.Of(RailroadsMod.Locomotive));
        var wagons = ObjectIds.Read(train.Object.Bytes[RailroadsMod.Keys.Wagons]);

        Assert.NotNull(wagons);
        var named = wagons.Select(id => Assert.Single(Reference.Objects, r => r.Id == id)).ToList();
        Assert.Equal(["bf_wagon", "bf_wagon_flat"], named.Select(w => w.Object.Prefab));
        Assert.All(named, w => Assert.Equal([w.Id], ObjectIds.Read(w.Object.Bytes[RailroadsMod.Keys.Self])!));
    }

    [Fact]
    public void ATrainOnItsWayKeepsItsTrip()
    {
        var train = Assert.Single(Reference.Of(RailroadsMod.Locomotive)).Object;

        Assert.Single(TrackLines.Read(train.Bytes[RailroadsMod.Keys.Route])!);
        Assert.Equal(2, TrackLines.Read(train.Bytes[RailroadsMod.Keys.Trail])!.Count);
        Assert.Empty(TripLegs.Read(train.Bytes[RailroadsMod.Keys.Legs])!);
        Assert.Equal("Fixture Halt", train.Strings[RailroadsMod.Keys.Destination]);
    }

    public static TheoryData<string> Vehicles() => [RailroadsMod.Locomotive, "bf_wagon", "bf_wagon_flat", RailroadsMod.Handcart];

    [Theory]
    [MemberData(nameof(Vehicles))]
    public void AVehicleIsPutOnTheLineTheModPutItOn(string prefab)
    {
        var recorded = Assert.Single(Reference.Of(prefab)).Object;
        var placed = Placed(recorded);

        var line = Assert.Single(TrackLines.Read(recorded.Bytes[RailroadsMod.Keys.Line])!);
        var (a, b) = Assert.Single(TrackLines.Read(placed.Bytes[RailroadsMod.Keys.Line])!);
        Assert.True(Vector3.Distance(line.A, a) < 0.01f && Vector3.Distance(line.B, b) < 0.01f, $"{a} {b} is not {line}");
        Assert.Equal(recorded.Floats[RailroadsMod.Keys.At], placed.Floats[RailroadsMod.Keys.At], 0.01f);
        Assert.Equal(recorded.Ints.GetValueOrDefault(RailroadsMod.Keys.Dir, 1), placed.Ints.GetValueOrDefault(RailroadsMod.Keys.Dir, 1));
        // Put somewhere, a vehicle stops: its trip and the lines its wagons stood on go.
        Assert.DoesNotContain(RailroadsMod.Keys.Route, placed.Keys);
        Assert.DoesNotContain(RailroadsMod.Keys.Trail, placed.Keys);
        Assert.DoesNotContain(RailroadsMod.Keys.Moving, placed.Keys);
    }

    [Fact]
    public void TheSignStopsTrainsWhereTheModStopsThem()
    {
        var recorded = Assert.Single(Reference.Of(RailroadsMod.Sign)).Object;

        var placed = Placed(recorded);

        Assert.True(Vector3.Distance(recorded.Vectors[RailroadsMod.Keys.Stop], placed.Vectors[RailroadsMod.Keys.Stop]) < 0.01f,
            $"{placed.Vectors[RailroadsMod.Keys.Stop]} is not {recorded.Vectors[RailroadsMod.Keys.Stop]}");
    }

    [Fact]
    public void TrackKeepsTheTurnTheModKeeps()
    {
        foreach (var track in Reference.Objects.Select(r => r.Object).Where(o => o.Quaternions.ContainsKey(RailroadsMod.Keys.Turn)))
        {
            var placed = Placed(track);
            var turn = placed.Quaternions[RailroadsMod.Keys.Turn];
            Assert.True(Math.Abs(Quaternion.Dot(turn, track.Quaternions[RailroadsMod.Keys.Turn])) > 0.99999f, track.Prefab);
        }
    }

    [Fact]
    public void PastedCopiesKeepNoTrackOrTripOfTheOriginals()
    {
        foreach (var o in Reference.Objects.Select(r => Copy(r.Object)))
        {
            new PastedCopies().Rewrite(o, new NoIds());
            Assert.DoesNotContain(o.Keys, k => k is RailroadsMod.Keys.Line or RailroadsMod.Keys.Trail or RailroadsMod.Keys.Route or RailroadsMod.Keys.Stop
                or RailroadsMod.Keys.Moving or RailroadsMod.Keys.Legs);
        }
    }

    /// <summary>
    /// A copy of a recorded object put where it stands by the add-on, among the rest of the recorded objects (its
    /// original left out, as when the editor moves it).
    /// </summary>
    private static FakeObject Placed(FakeObject original)
    {
        var copy = Copy(original);
        var rest = Reference.Objects.Select(r => r.Object).Where(o => o != original).Cast<ValheimWorldEditor.Sdk.IObjectView>().ToArray();
        new RailPlacer(Models).Place([copy], new ObjectsArea(rest));
        return copy;
    }

    /// <summary>A copy of an object with all its values.</summary>
    private static FakeObject Copy(FakeObject o)
    {
        var c = new FakeObject(o.Prefab) { Position = o.Position, Orientation = o.Orientation };
        foreach (var v in o.Ints)
        {
            c.Ints[v.Key] = v.Value;
        }
        foreach (var v in o.Longs)
        {
            c.Longs[v.Key] = v.Value;
        }
        foreach (var v in o.Floats)
        {
            c.Floats[v.Key] = v.Value;
        }
        foreach (var v in o.Vectors)
        {
            c.Vectors[v.Key] = v.Value;
        }
        foreach (var v in o.Quaternions)
        {
            c.Quaternions[v.Key] = v.Value;
        }
        foreach (var v in o.Strings)
        {
            c.Strings[v.Key] = v.Value;
        }
        foreach (var v in o.Bytes)
        {
            c.Bytes[v.Key] = v.Value;
        }
        return c;
    }
}
