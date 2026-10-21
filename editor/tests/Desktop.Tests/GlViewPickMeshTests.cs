using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A click decided by the models' triangles (GlView.NearestMeshHit, headless with each thing's model set): a cut-out
/// piece such as a fur bed is solid, so on a floor it is picked before the floor; foliage counts only where nothing
/// solid is behind it along the ray.
/// </summary>
public class GlViewPickMeshTests
{
    /// <summary>A floor's stone: opaque.</summary>
    private static readonly ModelStore.MaterialData Stone = new(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
    /// <summary>A bed's fur: cut out, as the game's bed is, but no foliage.</summary>
    private static readonly ModelStore.MaterialData Fur = new(Vector4.One, null, 0.541f, false, new Vector4(1, 1, 0, 0));
    /// <summary>A tree's leaves: cut out and foliage.</summary>
    private static readonly ModelStore.MaterialData Leaves = new(Vector4.One, null, 0.5f, true, new Vector4(1, 1, 0, 0), Foliage: true);

    /// <summary>A thing of its own kind, a piece, this far east of the origin.</summary>
    private static WorldScene.Thing Piece(int id, string prefab, float x) => new(id, StableHash.Of(prefab), new Vector3(x, 0, 0), Vector3.Zero, 0, true);

    /// <summary>A flat square at this height, half this wide: two triangles, 8 floats a vertex as the models' meshes.</summary>
    private static ModelStore.MeshData Quad(float y, float half)
    {
        float[] v = [-half, y, -half, 0, 1, 0, 0, 0, half, y, -half, 0, 1, 0, 1, 0, half, y, half, 0, 1, 0, 1, 1, -half, y, half, 0, 1, 0, 0, 1];
        return new ModelStore.MeshData(v, [[0, 1, 2, 0, 2, 3]]);
    }

    /// <summary>
    /// A view on a flat area with a floor slab (thing 0, 2 m wide), a fur bed on it (thing 1, 1.2 m wide) and a leaf
    /// 2 m above them, 2 m east (thing 2, 3 m wide: over the east halves of the two), each with its model and box;
    /// every kind shown. Also the point 5 m above the floor's middle, in view space.
    /// </summary>
    private static (GlView View, WorldScene Scene, Vector3 Above) BedOnFloorUnderLeaves()
    {
        var s = TestScenes.Flat(2, Piece(1, "Floor_Test", 0), Piece(2, "Bed_Test", 0), Piece(3, "Leaf_Test", 2));
        var v = new GlView();
        v.Show(s.Scene, null);
        foreach (var k in ObjectKinds.All)
        {
            v.SetShown(k, true);
        }
        var at = new Vector3(-s.Scene.Cx, 0, s.Scene.Cz);
        ModelStore.MeshData[] meshes = [Quad(0, 1), Quad(0.4f, 0.6f), Quad(2, 1.5f)];
        ModelStore.MaterialData[] materials = [Stone, Fur, Leaves];
        for (int i = 0; i < 3; i++)
        {
            v.SetModel(i, (meshes[i], materials[i]));
        }
        v.SetBoxes(meshes.Select((m, i) => (i, at + m.Bounds.Min + s.Scene.Things[i].Position, at + m.Bounds.Max + s.Scene.Things[i].Position)));
        return (v, s.Scene, at + new Vector3(0, 5, 0));
    }

    [AvaloniaFact]
    public void ACutOutBedOnAFloorIsPickedBeforeTheFloor()
    {
        var (v, s, above) = BedOnFloorUnderLeaves();
        var d = new Vector3(0, -1, 0);
        // Straight down on the bed's middle, clear of the leaf: the bed's fur is cut out, not foliage, so its top is
        // the first surface met, 4.6 m down.
        var (hit, best) = v.NearestMeshHit(s, above, d, v.BoxesHitBy(above, d));
        Assert.Equal(1, hit);
        Assert.Equal(4.6f, best, 3);
    }

    [AvaloniaFact]
    public void FoliageCountsOnlyWhereNothingSolidIsBehindIt()
    {
        var (v, s, above) = BedOnFloorUnderLeaves();
        var d = new Vector3(0, -1, 0);
        // Through the leaf on to the bed's east edge: the bed. Through the leaf alone, past the floor's edge: the leaf, 3 m down.
        var onBed = above + new Vector3(0.55f, 0, 0);
        Assert.Equal(1, v.NearestMeshHit(s, onBed, d, v.BoxesHitBy(onBed, d)).Hit);
        var beside = above + new Vector3(1.2f, 0, 0);
        var (hit, best) = v.NearestMeshHit(s, beside, d, v.BoxesHitBy(beside, d));
        Assert.Equal(2, hit);
        Assert.Equal(3f, best, 3);
    }
}
