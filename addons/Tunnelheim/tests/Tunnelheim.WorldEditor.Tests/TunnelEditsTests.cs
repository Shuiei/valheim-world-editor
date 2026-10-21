using System.Numerics;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The Tunnel tool's edits made records the mod's way (TunnelEdits): a column dug for the first time gets a record of
/// its own, in its middle, holding the ground it was dug from; an edit across a column's edge goes to both columns; a
/// column with a record has the edits appended to it; one whose record the mod cannot read gets a second one; an edit
/// that changes nothing makes nothing.
/// </summary>
public class TunnelEditsTests
{
    private static TunnelEdits.Result Make(FakeArea area, List<string> log, params DigOp[] ops) =>
        TunnelEdits.Make(area, TunnelArea.Build(area, new Dictionary<int, HoeSettings>(), log.Add), ops, log.Add);

    [Fact]
    public void AColumnDugForTheFirstTimeGetsARecordWithItsGround()
    {
        var log = new List<string>();
        var dig = new DigOp(DigKind.Sphere, new Vector3(8, 40, 8), 2f);
        var result = Make(new FakeArea(40), log, dig);

        Assert.Equal(1, result.Columns);
        var (prefab, position, _, data) = Assert.Single(result.Edit!.Added);
        Assert.Equal((TunnelheimMod.RecordPrefabName, new Vector3(8, 40, 8)), (prefab, position));
        var record = RecordCopy.Of(data!).Written;
        Assert.Equal(dig, Assert.Single(record.Ops));
        Assert.Equal(ColumnRecord.SeedCount, record.Seeds!.Length);
        Assert.All(record.Seeds, s => Assert.Equal(40f, s));
        Assert.Equal(0, record.CheckpointId);
        Assert.Empty(result.Edit.Changed);
        Assert.Empty(log);
    }

    [Fact]
    public void AnEditAcrossAnEdgeGoesToBothColumns()
    {
        var result = Make(new FakeArea(40), [], new DigOp(DigKind.Sphere, new Vector3(16, 40, 8), 2f));
        Assert.Equal(2, result.Columns);
        Assert.Equal([new Vector3(8, 40, 8), new Vector3(24, 40, 8)], result.Edit!.Added.Select(a => a.Position).OrderBy(p => p.X));
    }

    [Fact]
    public void AColumnsRecordHasTheEditsAppended()
    {
        var first = new DigOp(DigKind.Sphere, new Vector3(4, 40, 4), 1f);
        var record = FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, [first]), 4, 4);
        var second = new DigOp(DigKind.Sphere, new Vector3(10, 40, 10), 1f);
        var result = Make(new FakeArea(40, record), [], second);

        Assert.Empty(result.Edit!.Added);
        var (obj, change) = Assert.Single(result.Edit.Changed);
        Assert.Same(record, obj);
        Assert.Equal([first, second], RecordCopy.Of(change).Written.Ops);
    }

    [Fact]
    public void ARecordTheModCannotReadIsLeftAndASecondOneMade()
    {
        var log = new List<string>();
        var broken = new FakeObject(TunnelheimMod.RecordPrefabName, new Vector3(8, 0, 8), new Dictionary<string, byte[]> { [TunnelheimMod.RecordKeyName] = [1, 2] });
        var result = Make(new FakeArea(40, broken), log, new DigOp(DigKind.Sphere, new Vector3(8, 40, 8), 2f));

        Assert.Empty(result.Edit!.Changed);
        Assert.Single(result.Edit.Added);
        Assert.Contains(log, l => l.Contains("new edits go to a record of their own", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEditThatChangesNothingMakesNothing()
    {
        // A sphere high in the air carves no rock.
        var result = Make(new FakeArea(40), [], new DigOp(DigKind.Sphere, new Vector3(8, 90, 8), 2f));
        Assert.Equal((null, 0), (result.Edit, result.Columns));
    }
}
