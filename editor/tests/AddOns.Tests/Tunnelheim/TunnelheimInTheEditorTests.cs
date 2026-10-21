using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Tunnelheim add-on in the editor, on records the real mod wrote (its tests' reference fixture): the editor runs
/// its scene layer, keeps every mesh it gives (none fails the editor's checks), cuts the ground over exactly the columns
/// the game rebuilt, and shows a record in words in the inspector.
/// </summary>
[Collection("AddOns")]
public sealed class TunnelheimInTheEditorTests : IDisposable
{
    private readonly AddOnSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    /// <summary>What the fixture holds: the record and checkpoint objects, the ground, the columns and chunk meshes the game built.</summary>
    private sealed class Reference
    {
        public List<IObjectView> Objects { get; } = [];

        public Dictionary<(int, int), float> Heights { get; } = [];

        public HashSet<(int X, int Z)> Columns { get; } = [];

        public int Meshes { get; set; }
    }

    /// <summary>The add-on's reference fixture (addons/Tunnelheim/tests/.../fixtures/tunnelheim-reference.txt).</summary>
    private static Reference Read()
    {
        var r = new Reference();
        string file = Directory.GetFiles(Path.Combine(RepositoryAddOns.Folder, "Tunnelheim", "tests"), "tunnelheim-reference.txt", SearchOption.AllDirectories)
            .First(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        foreach (string[] t in File.ReadLines(file).Where(l => !l.StartsWith('#')).Select(l => l.Split(' ')))
        {
            int I(int i) => int.Parse(t[i], CultureInfo.InvariantCulture);
            switch (t[0])
            {
                case "R":
                    r.Objects.Add(Column("TunnelColumnData", I(1), I(2), z => z.SetBytes(StableHash.Of("Tunneling_Digs"), Convert.FromBase64String(t[3]))));
                    break;
                case "P":
                    r.Objects.Add(Column("TunnelColumnSnapshot", I(1), I(2), z =>
                    {
                        z.SetInt(StableHash.Of("Tunneling_SnapId"), I(3));
                        z.SetBytes(StableHash.Of("Tunneling_SnapData"), Convert.FromBase64String(t[4]));
                    }));
                    break;
                case "G":
                    r.Heights[(I(1), I(2))] = BitConverter.Int32BitsToSingle(int.Parse(t[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    break;
                case "V":
                    r.Columns.UnionWith(t.Skip(1).Select(c => c.Split(':')).Select(c => (int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[1], CultureInfo.InvariantCulture))));
                    break;
                case "M" when I(5) > 0:
                    r.Meshes++;
                    break;
                default:
                    // The chunks' densities and the empty meshes are the add-on's own tests' to compare.
                    break;
            }
        }
        return r;
    }

    /// <summary>An object of the mod's in the middle of a column, as the mod places them, its data set by <paramref name="set"/>.</summary>
    private static ObjectView Column(string prefab, int cx, int cz, Action<ZdoData> set)
    {
        var z = new ZdoData { Prefab = StableHash.Of(prefab) };
        set(z);
        return new ObjectView(prefab, new Vector3((cx + 0.5f) * 16, 0, (cz + 0.5f) * 16), Vector3.Zero, z);
    }

    /// <summary>The area the fixture's records lie in: the ground the game showed, at the half-metre points it gave.</summary>
    private sealed class ReferenceArea(Reference r) : IAreaView
    {
        public float MinX => -4096;

        public float MinZ => -4096;

        public float MaxX => 4096;

        public float MaxZ => 4096;

        public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => [.. r.Objects.Where(o => o.Prefab == prefab)];

        public float GroundHeight(float x, float z) => r.Heights[((int)MathF.Round(x / 0.5f), (int)MathF.Round(z / 0.5f))];

        public int Biome(float x, float z) => 1;

        public bool IsDeepNorth(float x, float z) => false;

        public bool IsAshlands(float x, float z) => false;

        public uint BiomeColor(int x, int z) => 0xFF00FF00;

        public uint GroundMask(int x, int z) => 0xFF000000;
    }

    [Fact]
    public void TheEditorDrawsTheTunnelsTheGameBuilt()
    {
        var r = Read();
        _sandbox.Run("Tunnelheim");

        var layer = Assert.Single(ModExtensions.ScenesOf(new ReferenceArea(r)));
        Assert.Equal(ModExtensions.Status.Running, Assert.Single(ModExtensions.Offered).State);
        Assert.Equal(r.Columns.Order(), layer.Holes.Select(h => (h.X0 / 16, h.Z0 / 16)).Order());
        Assert.All(layer.Holes, h => Assert.Equal((16, 16), (h.X1 - h.X0, h.Z1 - h.Z0)));
        // Every chunk the game meshed is drawn: the editor kept every mesh the add-on gave.
        Assert.Equal(r.Meshes, layer.Meshes.Count);
        Assert.All(layer.Meshes, m => Assert.NotNull(m.BiomeColors));
    }

    [Fact]
    public void TheTunnelToolIsOnTheRailWithItsCard()
    {
        _sandbox.Run("Tunnelheim");
        var tool = Assert.Single(ModExtensions.Tools);
        Assert.Equal(("Tunnelheim/tunnel", "Tunnel"), (tool.Name, tool.Info.Name));
        // Every control the add-on gives is of a form the editor shows (none left out).
        Assert.Equal(["action", "hint", "size", "width", "depth", "height", "length", "steps"], tool.Controls.Select(c => c.Id));
    }

    [Fact]
    public void TheInspectorShowsARecordInWords()
    {
        var r = Read();
        _sandbox.Run("Tunnelheim");
        var record = r.Objects.First(o => o.Prefab == "TunnelColumnData");
        Assert.True(record.TryGetBytes("Tunneling_Digs", out byte[] bytes));
        Assert.StartsWith("Tunnel record (version ", ModExtensions.Decode(StableHash.Of("Tunneling_Digs"), record, bytes));
    }
}
