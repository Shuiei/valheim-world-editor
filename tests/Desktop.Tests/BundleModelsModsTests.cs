using System.Numerics;
using TerrainEditor.App;
using WorldEditor.Tests;
using WorldEditor.Tests.Unity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A mod prefab's model (BundleModels), which no bundle holds whole: drawn as the game reported it (ModCatalogue),
/// each part's mesh and material found by name in the mods' bundles first (ids with "+"), then the game's; of
/// several of a name, the mesh with the bounds and vertex count the game had and the material in the mesh's bundle;
/// Unity's own primitives when no bundle has a mesh that fits; parts whose mesh is nowhere left out; the game's
/// materials for a mod's placeholders. The mods' meshes, materials and textures are read from their bundles by those
/// ids. Runs alone (the mods' catalogue is shared), in a data folder of its own.
/// </summary>
[Collection("DataDir")]
public sealed class BundleModelsModsTests : IDisposable
{
    private static readonly float[] Expected1 = [0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f];
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-modmodels-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly TestGame _game = new(), _mods = new();
    private readonly long _gameBeam, _gamePlank, _wood, _modBeam, _modPlank, _fineWood, _otherFineWood, _fineTexture;
    private readonly long _gamePost, _gameTallPost, _modPost, _modStone;

    /// <summary>
    /// The posts' bounds (centre, extent), as the game reports them: a post 2 m high, and one 4 m high.
    /// </summary>
    private const string Post = "0,1,0,0.1,1,0.1", TallPost = "0,2,0,0.2,2,0.2";

    /// <summary>
    /// RGBA32.
    /// </summary>
    private const int Rgba32 = 4;

    public BundleModelsModsTests()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
        var g = _game.Bundle("game_parts");
        _gameBeam = Triangle("beam").Add(g);
        _gamePlank = Quad("plank").Add(g);
        _wood = TestAssets.Material(g, "wood");
        _gamePost = Quad("post", ((0, 1, 0), (0.1, 1, 0.1))).Add(g);
        _gameTallPost = Triangle("post", ((0, 2, 0), (0.2, 2, 0.2))).Add(g);
        TestAssets.Material(g, "stone");
        _game.Save();
        // Another mod's bundle, read first: a material of the same name as the fine mod's own.
        var other = _mods.Bundle("a_other");
        _otherFineWood = TestAssets.Material(other, "fine_wood");
        var m = _mods.Bundle("finemod");
        _modBeam = Triangle("beam").Add(m);
        _modPlank = Triangle("plank").Add(m);
        _fineTexture = TestAssets.Texture(m, "fine_wood_tex", 2, 1, Rgba32, 1, [1, 2, 3, 4, 5, 6, 7, 8]);
        _fineWood = TestAssets.Material(m, "fine_wood", textures: [("_MainTex", Fields.Ref(_fineTexture), (1, 1), (0, 0))]);
        _modPost = Triangle("post", ((0, 1, 0), (0.1, 1, 0.1))).Add(m);
        // A stand-in for the game's wood (Jötunn's mock shader), and a stone of the mod's own.
        long mock = TestAssets.Shader(m, "JVLmock_Custom/Piece", new Dictionary<int, byte[]>());
        TestAssets.Material(m, "wood", Fields.Ref(mock));
        _modStone = TestAssets.Material(m, "stone");
        _mods.Save();
        ModCatalogue.Take("""{"prefabs":{"bfp_finewall":{"p":1,"d":0,"t":2}}}""", new HashSet<string>());
    }

    /// <summary>
    /// Forgets the mods, puts the data folder and the tests' copies back, and deletes the folders.
    /// </summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        _game.Dispose();
        _mods.Dispose();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>
    /// A one-triangle mesh (3 vertices), with these bounds (none: zero).
    /// </summary>
    private static TestMesh Triangle(string name, ((double, double, double), (double, double, double)) bounds = default) =>
        new() { Name = name, Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = bounds };

    /// <summary>
    /// A two-triangle quad mesh (4 vertices), with these bounds (none: zero).
    /// </summary>
    private static TestMesh Quad(string name, ((double, double, double), (double, double, double)) bounds = default) =>
        new() { Name = name, Positions = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0], Submeshes = [[0, 1, 2, 0, 2, 3]], Bounds = bounds };

    /// <summary>
    /// The models of the test game, with the test mods' bundles (or none).
    /// </summary>
    private BundleModels Models(bool mods = true)
    {
        var game = _game.Bundles();
        var modBundles = mods ? _mods.Bundles() : null;
        return new BundleModels(game, () => modBundles);
    }

    /// <summary>
    /// Reports the fine wall's model: these parts (mesh, vertices, submesh, material or null, Unity's move), scale 2.
    /// </summary>
    private static void Report(params (string Mesh, int Vertices, int Sub, string? Material, (int X, int Y, int Z) Move)[] parts)
    {
        static string Part((string Mesh, int Vertices, int Sub, string? Material, (int X, int Y, int Z) Move) p) =>
            $$"""{"mesh":"{{p.Mesh}}","v":{{p.Vertices}},"sub":{{p.Sub}},{{(p.Material == null ? "" : $"\"mat\":\"{p.Material}\",")}}"m":[1,0,0,{{p.Move.X}},0,1,0,{{p.Move.Y}},0,0,1,{{p.Move.Z}},0,0,0,1]}""";
        Assert.Equal(1, ModCatalogue.TakeModels($$$"""{"bfp_finewall":{"s":[2,2,2],"p":[{{{string.Join(",", parts.Select(Part))}}}]}}"""));
    }

    /// <summary>
    /// Reports the fine wall's model: parts with their mesh's bounds (mesh, vertices, bounds), no material, in place.
    /// </summary>
    private static void ReportBounded(params (string Mesh, int Vertices, string Bounds)[] parts)
    {
        static string Part((string Mesh, int Vertices, string Bounds) p) =>
            $$"""{"mesh":"{{p.Mesh}}","v":{{p.Vertices}},"sub":0,"b":[{{p.Bounds}}],"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}""";
        Assert.Equal(1, ModCatalogue.TakeModels($$$"""{"bfp_finewall":{"s":[1,1,1],"p":[{{{string.Join(",", parts.Select(Part))}}}]}}"""));
    }

    [Fact]
    public void AModPrefabIsDrawnAsTheGameReportedIt()
    {
        Report(
            ("beam Instance", 3, 0, "fine_wood (Instance)", (0, 1, 2)),
            ("plank", 4, 1, "wood", (0, 0, 0)),
            ("made_while_the_game_runs", 12, 0, "wood", (0, 0, 0)),
            ("plank", 3, 0, null, (0, 0, 0)));

        var model = Models().Model("bfp_finewall")!;

        Assert.Equal("bfp_finewall", model.Name);
        Assert.Equal(new Vector3(2, 2, 2), model.RootScale);
        Assert.Equal(3, model.Parts.Count);
        // In both: the mod's; its material the one in its own bundle, though another mod's comes first.
        var beam = model.Parts[0];
        Assert.Equal($"+finemod:{_modBeam}", beam.Mesh);
        Assert.Equal($"+finemod:{_fineWood}", beam.Material);
        Assert.Equal(new Vector3(0, 1, -2), Vector3.Transform(Vector3.Zero, beam.Matrix));
        // Two planks: the one with the vertex count the game had, here the game's, with the game's material.
        var plank = model.Parts[1];
        Assert.Equal(($"game_parts:{_gamePlank}", 1, $"game_parts:{_wood}"), (plank.Mesh, plank.Sub, plank.Material));
        // The other plank's count, no material.
        Assert.Equal(($"+finemod:{_modPlank}", ""), (model.Parts[2].Mesh, model.Parts[2].Material));
        Assert.Null(Models().Model("nothing_known"));
    }

    [Fact]
    public void WithBoundsOnlyAMeshThatFitsOrUnitysOwnIsTaken()
    {
        ReportBounded(
            ("beam", 3, "0,0,0,0,0,0"),
            ("post", 4, Post),
            ("post", 3, "0,1.005,0,0.1,1,0.1"),
            ("post", 3, TallPost),
            ("post", 3, "0,3,0,0.3,3,0.3"),
            ("Cube", 24, "0,0,0,0.5,0.5,0.5"),
            ("Quad", 4, "0,0,0,0.5,0.5,0"),
            ("Plane", 121, "0,0,0,5.04,0,5"),
            ("Cylinder", 88, "0,0,0,0.5,1,0.5"),
            ("Cube", 24, "0,0,0,1,1,1"));

        var parts = Models().Model("bfp_finewall")!.Parts;

        Assert.Equal(
            [
                // Both fit, with the same count: the mod's.
                $"+finemod:{_modBeam}",
                // Both fit: the one with the vertex count, the game's here, the mod's (a centimetre's difference) there.
                $"game_parts:{_gamePost}",
                $"+finemod:{_modPost}",
                // Only the game's taller post fits, though the mod's has the count.
                $"game_parts:{_gameTallPost}",
                // None fits: no part. Unity's own, to a hundredth of their size; not a cube of another size.
                "~Cube",
                "~Quad",
                "~Plane",
                "~Cylinder",
            ],
            parts.Select(p => p.Mesh));
    }

    [Fact]
    public void AModsPlaceholderMaterialsAreTheGamesOwn()
    {
        Report(
            ("beam", 3, 0, "JVLmock_wood", (0, 0, 0)),
            ("beam", 3, 0, "_REPLACE_wood", (0, 0, 0)),
            ("beam", 3, 0, "wood", (0, 0, 0)),
            ("beam", 3, 0, "stone", (0, 0, 0)),
            ("beam", 3, 0, "JVLmock_nothing", (0, 0, 0)));

        var parts = Models().Model("bfp_finewall")!.Parts;

        Assert.Equal(
            [
                // Named for the game's, or the mod's of the game's name with a mock shader: the game's.
                $"game_parts:{_wood}",
                $"game_parts:{_wood}",
                $"game_parts:{_wood}",
                // A mod's own of the game's name: the mod's.
                $"+finemod:{_modStone}",
                "",
            ],
            parts.Select(p => p.Material));
    }

    [Fact]
    public void UnitysPrimitivesAreDrawnAsMadeHere()
    {
        var models = Models();

        var cube = models.Mesh("~Cube")!;
        Assert.Equal(24 * 8, cube.Vertices.Length);
        // Each triangle wound both ways, which the view knows.
        Assert.True(cube.BothWays);
        Assert.False(models.Mesh($"+finemod:{_modBeam}")!.BothWays);
        Assert.Null(models.Mesh("~Torus"));
    }

    [Fact]
    public void AMaterialOfAnotherBundleIsTakenWhenTheMeshsHasNone()
    {
        Report(("plank", 4, 0, "fine_wood", (0, 0, 0)));

        var part = Assert.Single(Models().Model("bfp_finewall")!.Parts);

        // The mesh is the game's: the mods' bundles are still looked in first, the first so named taken.
        Assert.Equal($"game_parts:{_gamePlank}", part.Mesh);
        Assert.Equal($"+a_other:{_otherFineWood}", part.Material);
    }

    [Fact]
    public void AModsMeshMaterialAndTextureAreReadFromItsBundles()
    {
        var models = Models();

        var mesh = models.Mesh($"+finemod:{_modBeam}")!;
        Assert.Equal(Expected1, mesh.Vertices[..8]);
        Assert.Equal(new uint[] { 0, 1, 2 }, mesh.Submeshes[0]);
        // Its texture in the mod's bundles too.
        Assert.Equal($"+finemod:{_fineTexture}", models.Material($"+finemod:{_fineWood}").Map);
        var texture = models.Texture($"+finemod:{_fineTexture}")!;
        Assert.Equal((2, 1), (texture.Width, texture.Height));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], texture.Rgba);
        Assert.Null(models.Mesh("+finemod:4242"));
    }

    [Fact]
    public void WithoutModBundlesTheGamesAreUsedAndModIdsAreNothing()
    {
        Report(("beam", 3, 0, "fine_wood", (0, 0, 0)));
        var models = Models(mods: false);

        var part = Assert.Single(models.Model("bfp_finewall")!.Parts);

        Assert.Equal(($"game_parts:{_gameBeam}", ""), (part.Mesh, part.Material));
        Assert.Null(models.Mesh($"+finemod:{_modBeam}"));
        Assert.Null(models.Material($"+finemod:{_fineWood}").Map);
        Assert.Equal(Vector4.One, models.Material($"+finemod:{_fineWood}").Color);
        Assert.Null(models.Texture($"+finemod:{_fineTexture}"));
    }
}
