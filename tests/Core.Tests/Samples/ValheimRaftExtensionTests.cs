using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimRAFT.WorldEditor;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SDK's sample extension for ValheimRAFT (samples/ValheimRAFT.WorldEditor), through the editor's hooks: ropes
/// read as the mod keeps them and shown in words; with it running, a pasted ship's ropes follow the copy's anchors
/// (instead of being left out); custom sails drawn from their three or four corners in their colour. Runs alone (the
/// extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ValheimRaftExtensionTests : IDisposable
{
    private const int Root = 10, AnchorA = 13, AnchorB = 14;

    public ValheimRaftExtensionTests()
    {
        ModExtensions.UseForTests(("ValheimRAFT", new Extension()));
    }

    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An int value as data text.</summary>
    private static ObjectField Int(string key, int value) => new("ints", StableHash.Of(key), value.ToString(CultureInfo.InvariantCulture));

    [Fact]
    public void RopesAreReadAndWrittenAsTheModKeepsThemAndShownInWords()
    {
        byte[] bytes = Ropes.Write([(AnchorA, 2), (-5, 0)]);
        Assert.Equal(11, bytes.Length);
        Assert.Equal(2, bytes[0]);
        Assert.Equal([(AnchorA, 2), (-5, 0)], Ropes.Read(bytes)!);
        // An older list (anchors by ZDOID) or damaged bytes are not read.
        Assert.Null(Ropes.Read([1, 0, 0]));
        Assert.Null(Ropes.Read([2, 0, 0]));
        Assert.Null(Ropes.Read([]));

        var view = new ObjectView("MBRopeAnchor", default, default, new ZdoData());
        Assert.Equal("2 rope(s): to anchor 13 (point 2), to anchor -5 (point 0)", ModExtensions.Decode(StableHash.Of(Ropes.Key), view, bytes));
        Assert.Equal("No ropes.", ModExtensions.Decode(StableHash.Of(Ropes.Key), view, Ropes.Write([])));
        Assert.Null(ModExtensions.Decode(StableHash.Of(Ropes.Key), view, [9]));
    }

    // Two anchors on a ship, A's ropes going to B and to an anchor not copied: the copy's A goes to the copy's B only.
    [Fact]
    public void APastedShipsRopesFollowItsAnchors()
    {
        static NewObject Made(params ObjectField[] data) => new(0, StableHash.Of("wood_floor"), Vector3.Zero, Vector3.Zero, 0, Data: data);
        var copy = ObjectLinks.Remap(
        [
            Made(Int("PersistentID", Root)),
            Made(Int("MBParentId", Root), Int("PersistentID", AnchorA), new ObjectField("bytes", StableHash.Of(Ropes.Key), Convert.ToBase64String(Ropes.Write([(AnchorB, 1), (999, 0)])))),
            Made(Int("MBParentId", Root), Int("PersistentID", AnchorB)),
        ], new HashSet<int> { Root, AnchorA, AnchorB, 999 });

        int b = int.Parse(copy[2].Data!.Single(f => f.Key == StableHash.Of("PersistentID")).Value!, CultureInfo.InvariantCulture);
        Assert.NotEqual(AnchorB, b);
        string ropes = copy[1].Data!.Single(f => f.Key == StableHash.Of(Ropes.Key)).Value!;
        Assert.Equal([(b, 1)], Ropes.Read(Convert.FromBase64String(ropes))!);
    }

    [Fact]
    public void ACustomSailIsDrawnFromItsCornersInItsColour()
    {
        static ZdoData Sail(int count, params Vector3[] corners)
        {
            var z = new ZdoData { Prefab = StableHash.Of(Sails.Prefab) };
            z.IntList.Add((StableHash.Of("m_sailCornersCountHash"), count));
            for (int i = 0; i < corners.Length; i++)
            {
                z.Vec3List.Add((StableHash.Of($"m_sailCorner{i + 1}Hash"), corners[i]));
            }
            return z;
        }
        int prefab = StableHash.Of(Sails.Prefab);
        var four = Sail(4, Vector3.Zero, new Vector3(0, 4, 0), new Vector3(3, 4, 0), new Vector3(3, 0, 0));
        four.ByteList.Add((StableHash.Of("m_mainColor"), [255, 0, 0, 255]));

        var shape = Assert.Single(ModExtensions.ShapesOf(prefab, new ObjectView(Sails.Prefab, default, default, four)));

        Assert.Equal(4, shape.Vertices.Length);
        Assert.Equal([0, 1, 2, 0, 2, 3], shape.Triangles);
        Assert.Equal(new Vector4(1, 0, 0, 1), shape.Color);
        Assert.True(shape.BothSides);
        Assert.Equal(3, Assert.Single(ModExtensions.ShapesOf(prefab, new ObjectView(Sails.Prefab, default, default, Sail(3, Vector3.Zero, Vector3.UnitY, Vector3.UnitX)))).Triangles.Length);
        // A sail missing a corner, or not woven yet, draws nothing.
        Assert.Empty(ModExtensions.ShapesOf(prefab, new ObjectView(Sails.Prefab, default, default, Sail(4, Vector3.Zero, Vector3.UnitY))));
        Assert.Empty(ModExtensions.ShapesOf(prefab, new ObjectView(Sails.Prefab, default, default, new ZdoData())));
    }
}
