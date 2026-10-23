using System.Numerics;
using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

/// <summary>The add-on on what the real mod saved (fixtures/valheimraft-reference.txt).</summary>
public sealed class ReferenceTests
{
    private readonly Reference _ship = Reference.Read();

    private Reference.Piece Piece(string prefab, int id) => _ship.Pieces.Single(p => p.Prefab == prefab && p.Id == id);

    [Fact]
    public void TheAnchorsRopesNameWhatTheModTiedThemTo()
    {
        int chest = _ship.Pieces.Single(p => p.Prefab == "piece_chest_wood").Id;
        var first = _ship.Pieces.Single(p => p.Prefab == Keys.RopeAnchor && p.Id != 0);
        var second = Piece(Keys.RopeAnchor, 0);

        Assert.Equal([(chest, (byte)0)], RopeList.Read(first.Values.Bytes[Keys.Ropes])!.Select(r => (r.Target, r.Point)));
        Assert.Equal([(first.Id, (byte)0)], RopeList.Read(second.Values.Bytes[Keys.Ropes])!.Select(r => (r.Target, r.Point)));
        // Written back the mod's way, byte for byte.
        Assert.Equal(first.Values.Bytes[Keys.Ropes], RopeList.Write(RopeList.Read(first.Values.Bytes[Keys.Ropes])!));
    }

    [Fact]
    public void TheRopeTiePointsAreTheModsPrefabs()
    {
        Assert.Equal(_ship.Ties[Keys.RopeAnchor], RopeLines.AnchorTie);
        Assert.Equal(_ship.Ties[Keys.DockAnchor], RopeLines.DockTie);
    }

    [Fact]
    public void TheSailIsWovenBetweenTheCornersTheModSaved()
    {
        var sail = Piece(Keys.Sail, 0).Values;

        var shape = Assert.Single(new SailCloth().ShapesOf(sail, new FakeWorld()));

        Assert.Equal([Vector3.Zero, new(0, -3, 0), new(-4, -3, 0.5f), new(-4, 0, 0.5f)], shape.Vertices);
        Assert.Equal("#FFFFFF, 100% opaque", new SailCloth().Describe(sail, sail.Bytes[Keys.SailColour]));
    }
}
