using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The byte arrays the mod keeps, in words in the inspector: track lines by their ends, the game's object ids as it
/// writes them, a trip's later legs; bytes not in the mod's form are left to the editor (no text).
/// </summary>
public class DecoderTests
{
    private static readonly FakeObject Locomotive = new(RailroadsMod.Locomotive);

    [Fact]
    public void TrackLinesAreNamedByTheirEnds()
    {
        (Vector3, Vector3)[] lines = [(new(1, 2, 3), new(1, 2, 11)), (new(1, 2, 11), new(-4.5f, 2, 19.25f))];
        byte[] bytes = TrackLines.Write(lines);

        Assert.Equal(48, bytes.Length);
        Assert.Equal(lines, TrackLines.Read(bytes));
        Assert.Equal("2 track line(s): (1, 2, 3) to (1, 2, 11); (1, 2, 11) to (-4.5, 2, 19.25)", new TrackLines().Describe(Locomotive, bytes));
        Assert.Equal("No track line.", new TrackLines().Describe(Locomotive, []));
        Assert.Null(new TrackLines().Describe(Locomotive, new byte[25]));
    }

    [Fact]
    public void ManyLinesAreCounted()
    {
        var lines = Enumerable.Range(0, 5).Select(i => (new Vector3(i, 0, 0), new Vector3(i + 1, 0, 0))).ToList();

        Assert.EndsWith("(2, 0, 0) to (3, 0, 0); and 2 more", new TrackLines().Describe(Locomotive, TrackLines.Write(lines)), StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectIdsAreWrittenAsTheGameWritesThem()
    {
        (long, uint)[] ids = [(-123456789012L, 42u), (5, uint.MaxValue)];
        byte[] bytes = ObjectIds.Write(ids);

        Assert.Equal(24, bytes.Length);
        Assert.Equal(ids, ObjectIds.Read(bytes));
        Assert.Equal("2 object(s): -123456789012:42, 5:4294967295", new ObjectIds().Describe(Locomotive, bytes));
        Assert.Equal("No object.", new ObjectIds().Describe(Locomotive, []));
        Assert.Null(new ObjectIds().Describe(Locomotive, new byte[13]));
        Assert.EndsWith(" and 2 more", new ObjectIds().Describe(Locomotive, new byte[72]), StringComparison.Ordinal);
    }

    [Fact]
    public void ATripsLaterLegsAreToldInOrder()
    {
        TripLegs.Leg[] legs =
        [
            new(1, 23.5f, [(new(0, 0, 0), new(0, 0, 8)), (new(0, 0, 8), new(0, 0, 16))], []),
            new(-1, 4, [(new(0, 0, 16), new(0, 0, 8))], [(new(0, 0, 8), new(0, 0, 0))]),
        ];
        byte[] bytes = TripLegs.Write(legs);

        var back = TripLegs.Read(bytes);

        Assert.NotNull(back);
        Assert.Equal(2, back.Count);
        Assert.Equal((1, 23.5f, 2, 0), (back[0].Way, back[0].Stop, back[0].Lines.Count, back[0].After.Count));
        Assert.Equal(legs[1].After, back[1].After);
        Assert.Equal("2 more leg(s): forward on 2 line(s), stopping 23.5 m along; backward on 1 line(s), stopping 4 m along",
            new TripLegs().Describe(Locomotive, bytes));
        Assert.Equal("No further leg.", new TripLegs().Describe(Locomotive, TripLegs.Write([])));
    }

    [Fact]
    public void DamagedLegsAreLeftToTheEditor()
    {
        byte[] bytes = TripLegs.Write([new(1, 2, [(Vector3.Zero, Vector3.UnitZ)], [])]);

        Assert.Null(TripLegs.Read(bytes[..^1]));
        Assert.Null(TripLegs.Read([.. bytes, 0]));
        Assert.Null(TripLegs.Read([]));
        // A count of lines far beyond what the bytes hold.
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 12);
        Assert.Null(new TripLegs().Describe(Locomotive, bytes));
        Assert.Null(TripLegs.Read(BitConverter.GetBytes(-1)));
    }
}
