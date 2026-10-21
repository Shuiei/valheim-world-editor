using System.Globalization;
using System.Security.Cryptography;
using TerrainEditor.Tunnelheim;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Tunnelheim's rock as the game builds it: records and a checkpoint the mod made on a local dedicated server (every
/// kind of dig, fills, planes, hoe and cultivator strokes on rock), replayed by the editor, give the columns, chunks,
/// densities, paints and meshes the game built from them after a reload, to the bit (tests/fixtures/tunnelheim-reference.txt).
/// </summary>
public class RockGridReferenceTests
{
    private sealed class Reference
    {
        public Dictionary<(int, int), List<ColumnRecord>> Records { get; } = [];
        public Dictionary<(int, int), Dictionary<int, ColumnCheckpoint>> Checkpoints { get; } = [];
        public Dictionary<(int, int), float> Heights { get; } = [];
        public HashSet<(int, int)> Columns { get; } = [];
        public Dictionary<(int, int, int), string> Chunks { get; } = [];
        public Dictionary<(int, int, int), (int Vertices, int Triangles, string Hash)> Meshes { get; } = [];
    }

    private static Reference Read()
    {
        var r = new Reference();
        foreach (string line in File.ReadLines(Path.Combine(Fixtures.Root, "tunnelheim-reference.txt")).Where(l => !l.StartsWith('#')))
        {
            string[] t = line.Split(' ');
            int I(int i) => int.Parse(t[i], CultureInfo.InvariantCulture);
            switch (t[0])
            {
                case "R":
                    (r.Records.TryGetValue((I(1), I(2)), out var list) ? list : r.Records[(I(1), I(2))] = []).Add(ColumnRecord.Parse(Convert.FromBase64String(t[3])));
                    break;
                case "P":
                    (r.Checkpoints.TryGetValue((I(1), I(2)), out var byId) ? byId : r.Checkpoints[(I(1), I(2))] = [])[I(3)] = ColumnCheckpoint.Parse(Convert.FromBase64String(t[4]));
                    break;
                case "G":
                    r.Heights[(I(1), I(2))] = BitConverter.Int32BitsToSingle(int.Parse(t[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    break;
                case "V":
                    r.Columns.UnionWith(t.Skip(1).Select(c => c.Split(':')).Select(c => (int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[1], CultureInfo.InvariantCulture))));
                    break;
                case "C":
                    r.Chunks[(I(1), I(2), I(3))] = t[4];
                    break;
                case "M":
                    r.Meshes[(I(1), I(2), I(3))] = (I(4), I(5), t[6]);
                    break;
            }
        }
        return r;
    }

    private static RockGrid Built(Reference r)
    {
        var hoe = TerrainOps.Read(File.ReadAllText(Path.Combine(Fixtures.Root, "terrain-ops.json")));
        var grid = new RockGrid((x, z) => r.Heights[((int)MathF.Round(x / 0.5f), (int)MathF.Round(z / 0.5f))], new RockRules(64f, hoe, null));
        grid.Build(r.Records.Select(kv => new ColumnRecords(kv.Key.Item1, kv.Key.Item2, kv.Value, r.Checkpoints.GetValueOrDefault(kv.Key) ?? [])));
        return grid;
    }

    private static string Hash(IEnumerable<byte> bytes) => Convert.ToHexString(SHA256.HashData([.. bytes])).ToLowerInvariant();

    [Fact]
    public void TheColumnsTheGameVoxelisedAreTheEditors()
    {
        var r = Read();
        Assert.Equal(r.Columns.Order(), Built(r).Columns.Order());
    }

    [Fact]
    public void EveryChunksDensityAndPaintAreTheGamesToTheBit()
    {
        var r = Read();
        var grid = Built(r);
        Assert.True(r.Chunks.Count > 20);
        Assert.All(r.Chunks, kv =>
        {
            var c = grid.Chunk(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
            Assert.NotNull(c);
            var bytes = Enumerable.Range(0, RockGrid.ChunkSamples).SelectMany(i => BitConverter.GetBytes(c.Raw(i)))
                .Concat(Enumerable.Range(0, RockGrid.ChunkSamples).Select(c.PaintAt));
            Assert.Equal(kv.Value, Hash(bytes));
        });
    }

    [Fact]
    public void EveryChunksMeshIsTheGamesToTheBit()
    {
        var r = Read();
        var grid = Built(r);
        Assert.Contains(r.Meshes.Values, m => m.Triangles > 1000);
        Assert.All(r.Meshes, kv =>
        {
            var m = RockMesher.Build(grid, kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
            var bytes = m.Positions.Concat(m.Normals).SelectMany(v => new[] { v.X, v.Y, v.Z }).SelectMany(BitConverter.GetBytes)
                .Concat(m.Triangles.SelectMany(BitConverter.GetBytes));
            Assert.Equal(kv.Value, (m.Positions.Length, m.Triangles.Length / 3, Hash(bytes)));
        });
    }

    [Fact]
    public void TheReferenceHoldsACheckpointAndEveryKindOfEdit()
    {
        var r = Read();
        Assert.Single(r.Checkpoints.SelectMany(c => c.Value));
        var kinds = r.Records.Values.SelectMany(l => l).SelectMany(x => x.Ops).Select(o => o.Kind).ToHashSet();
        Assert.Superset(new HashSet<DigKind> { DigKind.Sphere, DigKind.Hoe, DigKind.Box, DigKind.Fill, DigKind.Plane, DigKind.Stroke, DigKind.Chip, DigKind.Stairs, DigKind.ChipFloor }, kinds);
    }
}
