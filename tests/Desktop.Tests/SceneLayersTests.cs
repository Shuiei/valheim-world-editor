using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Mods' extensions' layers over an area (SceneLayers): the ground cells their holes leave undrawn (clipped to the area),
/// found under a view point; their meshes picked where they are nearer than the ground, and the ground picked through a
/// hole no more; the area they are given (its ground along the game's triangles, its terrain colours and paint, its
/// objects with the changes not saved yet). Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class SceneLayersTests : IDisposable
{
    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An extension drawing what it is given, for every area.</summary>
    private sealed class Drawing(SceneLayer layer) : IWorldEditorExtension, ISceneProvider
    {
        public void Register(IExtensionRegistry registry) => registry.AddScene(this);

        public SceneLayer? Build(IAreaView area) => layer;
    }

    /// <summary>
    /// A layer with a square of ground 4 m a side round the area's middle left undrawn, and a floor over it 2 m below the
    /// flat scene's ground (35 m).
    /// </summary>
    private static SceneLayer Floor()
    {
        var layer = new SceneLayer();
        layer.Holes.Add(new GroundHole(-2, -2, 2, 2));
        Vector3[] corners = [new(-2, 33, -2), new(-2, 33, 2), new(2, 33, 2), new(2, 33, -2)];
        layer.Meshes.Add(new SceneMesh(corners, [Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY], [0, 1, 2, 0, 2, 3]));
        return layer;
    }

    [Fact]
    public void HolesLeaveTheCellsInsideThemUndrawnWithinTheArea()
    {
        var s = TestScenes.FlatScene();
        var layer = new SceneLayer();
        layer.Holes.Add(new GroundHole(-32, -32, -30, -31));
        layer.Holes.Add(new GroundHole(30, 30, 40, 40));
        // Wholly east of the area, or south of it: nothing.
        layer.Holes.Add(new GroundHole(100, 0, 110, 40));
        layer.Holes.Add(new GroundHole(0, -100, 10, -90));
        byte[] cells = SceneLayers.HoleCells(s, [layer]);
        Assert.Equal(64 * 64, cells.Length);
        Assert.Equal(2 + 4, cells.Count(c => c != 0));
        Assert.Equal([1, 1, 0], cells[..3]);
        Assert.Equal(1, cells[63 * 64 + 63]);
        Assert.Empty(SceneLayers.HoleCells(s, [new SceneLayer()]));
        // The cell under a view point (x, -z from the area's middle).
        Assert.True(SceneLayers.HoleAt(s, cells, -31.5f, 31.5f));
        Assert.False(SceneLayers.HoleAt(s, cells, -29.5f, 31.5f));
        Assert.False(SceneLayers.HoleAt(s, cells, -40f, 31.5f));
        Assert.False(SceneLayers.HoleAt(s, [], -31.5f, 31.5f));
    }

    [Fact]
    public void ALayersMeshIsPickedAndTheGroundUnderItsHoleIsNot()
    {
        var s = TestScenes.FlatScene();
        s.World = new WorldSave { Directory = "none", SaveNumber = 1 };
        ModExtensions.UseForTests(("Floors", new Drawing(Floor())));
        var built = SceneLayers.Refresh(s);
        Assert.Equal(16, built.Holes.Count(c => c != 0));
        Assert.True(built.Picks.Any);
        Assert.Same(built, SceneLayers.Last(s));
        var down = new Vector3(0, -1, 0);
        // Over the hole: the floor, 7 m down from 40 m; elsewhere the ground, 5 m down.
        Assert.Equal(7f, Picking.HitGround(s, new Vector3(0.5f, 40, 0.5f), down)!.Value, 2);
        Assert.Equal(5f, Picking.HitGround(s, new Vector3(10.5f, 40, 10.5f), down)!.Value, 2);
        // A mesh nearer than the ground is hit first, even outside a hole.
        Assert.Null(built.Picks.Hit(new Vector3(10.5f, 40, 10.5f), down, 100));
    }

    [Fact]
    public void WithoutExtensionsThereIsNothing()
    {
        var s = TestScenes.FlatScene();
        Assert.Same(SceneLayers.Built.None, SceneLayers.Last(s));
        Assert.Same(SceneLayers.Built.None, SceneLayers.Shown(s, () => { }));
    }

    [Fact]
    public void TheAreaGivesItsGroundAlongTheGamesTriangles()
    {
        var s = TestScenes.FlatScene();
        s.World = new WorldSave { Directory = "none", SaveNumber = 1 };
        // The first cell's corners: 0 at (-32, -32), 1 east of it, 2 north of it, 4 north-east.
        (s.Heights[0], s.Heights[1], s.Heights[65], s.Heights[66]) = (0, 1, 2, 4);
        s.BiomeColor[4 * 66] = 9;
        s.Mask[4 * 66 + 3] = 255;
        var area = new SceneLayers.AreaView(s);
        Assert.Equal((-32f, -32f, 32f, 32f), (area.MinX, area.MinZ, area.MaxX, area.MaxZ));
        Assert.Equal(0.75f, area.GroundHeight(-31.75f, -31.75f), 4);
        Assert.Equal(2.75f, area.GroundHeight(-31.25f, -31.25f), 4);
        Assert.Equal(0f, area.GroundHeight(-100, -100));
        Assert.Equal(35f, area.GroundHeight(100, 100));
        Assert.Equal(9u, area.BiomeColor(-31, -31));
        Assert.Equal(0xFF000000u, area.GroundMask(-31, -31));
        Assert.Equal(0, area.Biome(0, 0));
        Assert.False(area.IsDeepNorth(0, 0) || area.IsAshlands(0, 0));
    }

    [Fact]
    public void TheAreaGivesItsObjectsWithTheChangesNotSavedYet()
    {
        using var world = new TempWorld();
        int canal = StableHash.Of("example_canal");
        static NewObject Canal(float x, byte tag)
        {
            var z = new ZdoData { Prefab = StableHash.Of("example_canal") };
            z.ByteList.Add((StableHash.Of("ExampleVehicles_Path"), [tag]));
            return new NewObject(0, z.Prefab, new Vector3(x, 46, 4), Vector3.Zero, 1, Fresh: false, Raw: z.Serialize());
        }
        Assert.True(WorldWriter.Save(world.Load(), [], added: [Canal(4, 1), Canal(8, 2)]).Saved);
        var s = WorldScene.Load(world.Dir, 0, 0, 1);
        var session = s.Session!;
        int first = s.World.Objects.First(o => o.Prefab == canal).Id;
        session.Edits.SetDeleted([first], true);
        session.Commit("A canal", null, [], [(Canal(-4, 3), false)]);
        var found = new SceneLayers.AreaView(s).ObjectsOf("example_canal");
        var tags = found.Select(o => o.TryGetBytes("ExampleVehicles_Path", out byte[] b) ? b[0] : 0).Order().ToList();
        Assert.Equal(2, tags.Count);
        Assert.Contains(3, tags);
        Assert.Empty(new SceneLayers.AreaView(s).ObjectsOf("example_hull"));
    }
}
