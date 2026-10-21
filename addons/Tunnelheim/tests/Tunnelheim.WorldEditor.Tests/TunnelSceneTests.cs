using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The tunnels drawn in an area (TunnelScene, TunnelArea): each built column cut out of the editor's ground and its rock
/// meshed in the world, with the ground's biome colour and the mod's paint; nothing without records; a record the mod
/// cannot read told and left out.
/// </summary>
public class TunnelSceneTests
{
    /// <summary>The editor as the scene sees it: no game components (no hoe strokes).</summary>
    private sealed class Host : IEditorHost, IGameData
    {
        public ILiveGame? Live => null;

        public IGameData Game => this;

        public void Redraw()
        {
        }

        public IReadOnlyList<GameComponent> Components(string scriptClass) => [];
    }

    private static ColumnRecord Dug(Vector3 at) => new(ColumnRecord.Plain, null, 0, [new DigOp(DigKind.Sphere, at, 2f)]);

    [Fact]
    public void ADugColumnIsCutOutAndItsRockDrawnInTheWorld()
    {
        var log = new List<string>();
        var area = new FakeArea(40, FakeObject.Record(Dug(new Vector3(24, 40, 8)), 24, 8));
        var layer = new TunnelScene(new Host(), log.Add).Build(area)!;

        var hole = Assert.Single(layer.Holes);
        Assert.Equal((16, 0, 32, 16), (hole.X0, hole.Z0, hole.X1, hole.Z1));
        Assert.NotEmpty(layer.Meshes);
        var all = layer.Meshes.SelectMany(m => m.Positions).ToList();
        Assert.All(all, p => Assert.InRange(p.X, 15f, 33f));
        // The pit goes down two metres at its middle, and the flat ground around is drawn at 40 m.
        Assert.InRange(all.Min(p => p.Y), 37.9f, 38.2f);
        Assert.Contains(all, p => Math.Abs(p.Y - 40) < 0.01f);
        Assert.All(layer.Meshes, m => Assert.All(m.BiomeColors!, c => Assert.Equal(0x01020304u, c)));
        // Dug rock is drawn as dirt, the untouched ground around it as it is.
        var uvs = layer.Meshes.SelectMany(m => m.MaskUvs!).ToList();
        Assert.Contains(new Vector2(0.75f, 0.25f), uvs);
        Assert.Contains(new Vector2(0.25f, 0.25f), uvs);
        Assert.Empty(log);
    }

    [Fact]
    public void AnAreaWithoutRecordsDrawsNothing() => Assert.Null(new TunnelScene(new Host(), _ => { }).Build(new FakeArea(40)));

    [Fact]
    public void ARecordTheModCannotReadIsToldAndLeftOut()
    {
        var log = new List<string>();
        var bad = new FakeObject(TunnelheimMod.RecordPrefabName, new Vector3(8, 0, 8), new Dictionary<string, byte[]> { [TunnelheimMod.RecordKeyName] = [1, 2, 3] });
        var layer = new TunnelScene(new Host(), log.Add).Build(new FakeArea(40, bad))!;
        Assert.Empty(layer.Holes);
        Assert.StartsWith("TunnelColumnData at (8, 8) left out", Assert.Single(log));
    }

    [Fact]
    public void ACheckpointIsFoundInItsRecordsColumnByItsId()
    {
        var log = new List<string>();
        var checkpoint = new ColumnCheckpoint(ColumnCheckpoint.Current, [new CheckpointChunk(2, new short[RockGrid.ChunkSamples], null)]);
        var record = FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 77, []), 8, 8);
        var held = new FakeObject(TunnelheimMod.CheckpointPrefabName, new Vector3(8, 0, 8),
            new Dictionary<string, byte[]> { [TunnelheimMod.CheckpointKeyName] = checkpoint.Encode() },
            new Dictionary<string, int> { [TunnelheimMod.CheckpointIdKeyName] = 77 });
        var column = Assert.Single(TunnelArea.Columns(new FakeArea(40, record, held), log.Add));
        Assert.Equal((0, 0), (column.X, column.Z));
        Assert.Equal(77, Assert.Single(column.Records).CheckpointId);
        Assert.Equal(2, Assert.Single(column.Checkpoints[77].Chunks).Y);
        Assert.Empty(log);
    }

    [Fact]
    public void ARecordInWordsSaysWhatItHolds()
    {
        var obj = FakeObject.Record(Dug(new Vector3(8, 40, 8)), 8, 8);
        obj.TryGetBytes(TunnelheimMod.RecordKeyName, out byte[] bytes);
        Assert.Equal("Tunnel record (version 5): 1 edit(s).", new RecordDecoder().Describe(obj, bytes));
        Assert.StartsWith("Not a record Tunnelheim 1.0.0 reads: ", new RecordDecoder().Describe(obj, [0]));
    }
}
