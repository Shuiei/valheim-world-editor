using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

public sealed class RopeListTests
{
    [Fact]
    public void RopesAreReadAndWrittenAsTheModPacksThem()
    {
        byte[] bytes = RopeList.Write([new Rope(-1234567, 0), new Rope(42, 3)]);

        Assert.Equal(11, bytes.Length);
        Assert.Equal(2, bytes[0]);
        Assert.Equal([0x79, 0x29, 0xED, 0xFF, 0], bytes[1..6]);
        var ropes = RopeList.Read(bytes)!;
        Assert.Equal([(-1234567, (byte)0), (42, (byte)3)], ropes.Select(r => (r.Target, r.Point)));
        Assert.Empty(RopeList.Read([2])!);
    }

    [Fact]
    public void AnotherVersionOrDamagedBytesAreNotRead()
    {
        Assert.Null(RopeList.Read([]));
        Assert.Null(RopeList.Read([3, 1, 0, 0, 0, 0]));
        Assert.Null(RopeList.Read([2, 1, 0, 0, 0]));
        // The old way: an object id (eight bytes of user, four of id) and the tie point, thirteen bytes a rope.
        Assert.Null(RopeList.Read(new byte[1 + 13]));
        Assert.Equal(2, RopeList.OldCount([1, .. new byte[26]]));
        Assert.Null(RopeList.OldCount([1, 0, 0]));
        Assert.Null(RopeList.OldCount([2, .. new byte[13]]));
    }

    [Fact]
    public void RopesAreShownInWords()
    {
        var ropes = new RopeList();
        var anchor = new FakeObject(Keys.RopeAnchor);

        Assert.Equal("1 rope to: 42", ropes.Describe(anchor, RopeList.Write([new Rope(42, 0)])));
        Assert.Equal("2 ropes to: 42, 7 (tie point 2)", ropes.Describe(anchor, RopeList.Write([new Rope(42, 0), new Rope(7, 1)])));
        Assert.Equal("No rope.", ropes.Describe(anchor, [2]));
        Assert.StartsWith("1 rope, kept the old way", ropes.Describe(anchor, [1, .. new byte[13]]));
        Assert.Null(ropes.Describe(anchor, [9, 9]));
    }

    [Fact]
    public void APastedAnchorsRopesFollowTheCopyAndLetGoOfWhatWasNotCopied()
    {
        var anchor = new FakeObject(Keys.RopeAnchor);
        anchor.Bytes[Keys.Ropes] = RopeList.Write([new Rope(10, 0), new Rope(11, 2), new Rope(12, 0)]);

        new RopeList().Rewrite(anchor, new FakeIds(new() { [10] = 100, [11] = 110 }));

        Assert.Equal([(100, (byte)0), (110, (byte)2)], RopeList.Read(anchor.Bytes[Keys.Ropes])!.Select(r => (r.Target, r.Point)));
    }

    [Fact]
    public void APastedAnchorLetsGoOfRopesKeptTheOldWay()
    {
        var anchor = new FakeObject(Keys.RopeAnchor);
        anchor.Bytes[Keys.Ropes] = [1, .. new byte[13]];

        new RopeList().Rewrite(anchor, new FakeIds([]));

        Assert.False(anchor.Bytes.ContainsKey(Keys.Ropes));
    }
}
