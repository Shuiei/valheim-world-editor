using System.Numerics;
using ExampleVehicles.WorldEditor;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SDK sample's canal copier (samples/ExampleVehicles.WorldEditor/CanalCopies.cs): a canal wholly inside what is
/// copied is kept from the copy's middle and dug again at each pasted copy, turned and mirrored with it, on the ground
/// there; others stay behind; bytes not in its form put nothing down.
/// </summary>
public class ExampleVehiclesCanalCopiesTests
{
    /// <summary>An area of flat ground at 12 m holding the canals given.</summary>
    private sealed class Area(params IObjectView[] canals) : IAreaView
    {
        public float MinX => -64;

        public float MinZ => -64;

        public float MaxX => 64;

        public float MaxZ => 64;

        public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => prefab == Keys.Canal ? canals : [];

        public float GroundHeight(float x, float z) => 12;

        public int Biome(float x, float z) => 1;

        public bool IsDeepNorth(float x, float z) => false;

        public bool IsAshlands(float x, float z) => false;

        public uint BiomeColor(int x, int z) => 0;

        public uint GroundMask(int x, int z) => 0xFF000000;
    }

    /// <summary>A canal along a path, with a width when given.</summary>
    private static ObjectView Canal(float? width, params Vector2[] path)
    {
        var z = new ZdoData { Prefab = StableHash.Of(Keys.Canal) };
        z.ByteList.Add((StableHash.Of(Keys.Path), Canals.WritePath(path)));
        if (width is { } w)
        {
            z.SetFloat(StableHash.Of(Keys.Width), w);
        }
        return new ObjectView(Keys.Canal, new Vector3(path[0].X, 0, path[0].Y), Vector3.Zero, z);
    }

    /// <summary>A pasted canal's data, as the edit sets it.</summary>
    private static DataCopy Made((string Prefab, Vector3 Position, Vector3 Rotation, Action<ICopy>? Data) added)
    {
        var copy = new DataCopy(added.Prefab, added.Position, added.Rotation, new ZdoData());
        added.Data!(copy);
        return copy;
    }

    [Fact]
    public void ACanalInsideIsDugAgainAtEachCopyTurnedAndMirrored()
    {
        var inside = Canal(4, new(10, 10), new(14, 10));
        var crossing = Canal(null, new(10, 10), new(40, 10));
        var region = new CopyRegion(new Vector3(10, 5, 10), [new(0, 0), new(20, 0), new(20, 20), new(0, 20)]);
        var copier = new CanalCopies();
        byte[] kept = copier.Copy(new Area(inside, crossing), region)!;

        var edit = copier.Paste(new Area(), kept, [new CopyPlacement(new Vector3(-20, 30, 0), 90, false), new CopyPlacement(new Vector3(20, 30, 0), 0, true)])!;
        Assert.Equal(2, edit.Added.Count);
        Assert.All(edit.Added, a => Assert.Equal(Keys.Canal, a.Prefab));

        // Turned a quarter: east goes north. It stands on the ground where it lands.
        var turned = Made(edit.Added[0]);
        Assert.Equal(new Vector3(-20, 12, 0), turned.Position);
        Assert.True(turned.TryGetBytes(Keys.Path, out byte[] path));
        var points = Canals.ReadPath(path)!;
        Assert.Equal(new Vector2(-20, 0), points[0]);
        Assert.True(Vector2.Distance(new Vector2(-20, 4), points[1]) < 1e-4f);
        Assert.True(turned.TryGetFloat(Keys.Width, out float w));
        Assert.Equal(4f, w);
        Assert.False(turned.TryGetFloat(Keys.Depth, out _));

        // Mirrored: east goes west.
        Assert.True(Made(edit.Added[1]).TryGetBytes(Keys.Path, out byte[] mirrored));
        Assert.Equal([new Vector2(20, 0), new Vector2(16, 0)], Canals.ReadPath(mirrored)!);
    }

    [Fact]
    public void NothingInsideKeepsNothing() =>
        Assert.Null(new CanalCopies().Copy(new Area(Canal(null, new(50, 50), new(60, 50))), new CopyRegion(Vector3.Zero, [new(0, 0), new(5, 0), new(5, 5)])));

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 2, 1, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 1, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 1 })]
    public void BytesNotInItsFormPutNothingDown(byte[] kept) =>
        Assert.Null(new CanalCopies().Paste(new Area(), kept, [new CopyPlacement(Vector3.Zero, 0, false)]));
}
