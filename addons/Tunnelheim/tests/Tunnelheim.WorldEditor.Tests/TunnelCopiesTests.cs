using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// Tunnels with the editor's copies (TunnelCopies, DigOps.Moved): the edits inside what is copied, each once and in
/// order, kept from the copy's middle; pasted, moved and turned with each copy into the records there, so the same rock
/// comes out where the copy lands; ore marks, edits outside, checkpoints and bytes of another form handled.
/// </summary>
public class TunnelCopiesTests
{
    private static CopyRegion Square(Vector3 middle, float half) =>
        new(middle, [new(middle.X - half, middle.Z - half), new(middle.X + half, middle.Z - half), new(middle.X + half, middle.Z + half), new(middle.X - half, middle.Z + half)],
            (x, z) => Math.Abs(x - middle.X) <= half && Math.Abs(z - middle.Z) <= half);

    private static TunnelCopies Copier(List<string>? log = null) => new(new FakeHost(), (log ?? []).Add);

    [Fact]
    public void AnEditMovedWithACopyIsPlacedAndTurned()
    {
        var turn = new CopyPlacement(new Vector3(100, 20, 50), 90, false);
        var chip = DigOps.Aimed(DigKind.Chip, new Vector3(2, 1, 0), 1.5f, new Vector3(1, -0.5f, 0));
        var moved = DigOps.Moved(chip, turn);
        Assert.Equal(new Vector3(100, 21, 52), moved.Position, new Near());
        Assert.Equal(chip.Code, moved.Code);
        // Its direction turned as the copy turns (east goes north).
        var dir = DigShapes.Direction(chip);
        Assert.Equal(turn.Place(dir) - turn.Place(Vector3.Zero), DigShapes.Direction(moved), new Near());

        var mirror = new CopyPlacement(Vector3.Zero, 0, true);
        Assert.Equal(-chip.Extra, DigOps.Moved(chip, mirror).Extra);
        Assert.Equal(-60f, DigOps.Moved(new DigOp(DigKind.Box, Vector3.Zero, 1, Extra: 30), turn).Extra);
        Assert.Equal(-80f, DigOps.Moved(new DigOp(DigKind.Hoe, Vector3.Zero, 10, Code: 5), turn).Value);
        Assert.Equal(new DigOp(DigKind.Sphere, new Vector3(100, 20, 50), 2), DigOps.Moved(new DigOp(DigKind.Sphere, Vector3.Zero, 2), turn));
    }

    [Fact]
    public void ACopiedTunnelIsDugAgainWhereThePasteLands()
    {
        // A tunnel along column (0, 0) on flat ground, and nothing two columns east.
        var digs = DigOps.Bore(new Vector3(3, 39, 8), new Vector3(13, 37, 8), 1.5f);
        var record = FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, digs), 8, 8);
        var source = new FakeArea(40, record);
        byte[] kept = Copier().Copy(source, Square(new Vector3(8, 40, 8), 8))!;

        var edit = Copier().Paste(new FakeArea(40), kept, [new CopyPlacement(new Vector3(40, 40, 8), 0, false)])!;
        var (_, position, _, data) = Assert.Single(edit.Added);
        Assert.Equal(new Vector3(40, 40, 8), position);
        var pasted = RecordCopy.Of(data!).Record;
        Assert.Equal(digs.Select(d => d.Position + new Vector3(32, 0, 0)), pasted.Ops.Select(o => o.Position));

        // The rock that comes out there is the copied rock, moved two columns.
        var copied = TunnelArea.Build(source, new Dictionary<int, HoeSettings>(), _ => { });
        var landed = TunnelArea.Build(new FakeArea(40, FakeObject.Record(pasted, 40, 8)), new Dictionary<int, HoeSettings>(), _ => { });
        foreach (int cy in copied.ChunksOf(0, 0))
        {
            var a = copied.Chunk(0, cy, 0)!;
            var b = landed.Chunk(2, cy, 0)!;
            Assert.All(Enumerable.Range(0, RockGrid.ChunkSamples), i => Assert.Equal(a.Raw(i), b.Raw(i)));
        }
    }

    [Fact]
    public void EditsKeptByTwoColumnsAreCopiedOnceInTheirOrder()
    {
        var a = new DigOp(DigKind.Sphere, new Vector3(4, 40, 4), 1);
        var shared = new DigOp(DigKind.Sphere, new Vector3(16, 40, 4), 2);
        var b = new DigOp(DigKind.Fill, new Vector3(20, 40, 4), 1);
        var vein = new DigOp(DigKind.Vein, new Vector3(5, 40, 5), 0, Code: 7);
        var outside = new DigOp(DigKind.Sphere, new Vector3(4, 40, 30), 1);
        var area = new FakeArea(40,
            FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, [a, shared, vein, outside]), 8, 8),
            FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, [shared, b]), 24, 8));
        var middle = new Vector3(16, 40, 8);
        byte[] kept = Copier().Copy(area, Square(middle, 12))!;
        Assert.Equal([a, shared, b], ColumnRecord.Parse(kept).Ops.Select(o => o with { Position = o.Position + middle }));
    }

    [Fact]
    public void BakedEditsAreToldAndNothingInsideKeepsNothing()
    {
        var log = new List<string>();
        var area = new FakeArea(40, FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 99, [new DigOp(DigKind.Sphere, new Vector3(8, 40, 8), 1)]), 8, 8));
        Assert.NotNull(Copier(log).Copy(area, Square(new Vector3(8, 40, 8), 8)));
        Assert.Contains("start from a checkpoint", Assert.Single(log));
        Assert.Null(Copier().Copy(area, Square(new Vector3(100, 40, 100), 4)));
    }

    [Fact]
    public void APasteGoesIntoTheRecordsThereAndOddBytesPutNothingDown()
    {
        var there = FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, [new DigOp(DigKind.Sphere, new Vector3(40, 40, 8), 1)]), 40, 8);
        byte[] kept = new ColumnRecord(ColumnRecord.Plain, null, 0, [new DigOp(DigKind.Sphere, Vector3.Zero, 2)]).Encode();
        var edit = Copier().Paste(new FakeArea(40, there), kept, [new CopyPlacement(new Vector3(40, 40, 8), 0, false)])!;
        Assert.Empty(edit.Added);
        Assert.Equal(2, RecordCopy.Of(Assert.Single(edit.Changed).Change).Record.Ops.Count);

        var log = new List<string>();
        Assert.Null(Copier(log).Paste(new FakeArea(40), [1, 2, 3], [new CopyPlacement(Vector3.Zero, 0, false)]));
        Assert.StartsWith("paste: the copy's tunnels are not in a form", Assert.Single(log));
    }

    /// <summary>Points equal to a thousandth.</summary>
    private sealed class Near : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 1e-3f;

        public int GetHashCode(Vector3 v) => 0;
    }
}
