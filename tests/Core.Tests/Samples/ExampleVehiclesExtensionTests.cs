using System.Globalization;
using System.Numerics;
using ExampleVehicles.WorldEditor;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SDK's sample extension (samples/ExampleVehicles.WorldEditor), through the editor's hooks: packed ids read as
/// the mod keeps them and shown in words; with it running, a pasted vehicle's packed ids follow the copy's objects
/// (instead of being left out); sails drawn from their three or four corners in their colour; a cable drawn from a
/// post to the post it names, found through the world; the ground left undrawn round a pit, as wide as it says. Its
/// worldeditor.json links as the tests' example mod does.
/// Runs alone (the extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ExampleVehiclesExtensionTests : IDisposable
{
    private const int Root = 10, PostA = 13, PostB = 14;

    public ExampleVehiclesExtensionTests()
    {
        ModExtensions.UseForTests(("ExampleVehicles", new Extension()));
    }

    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An int value as data text.</summary>
    private static ObjectField Int(string key, int value) => new("ints", StableHash.Of(key), value.ToString(CultureInfo.InvariantCulture));

    /// <summary>An object as an extension sees it, from its data.</summary>
    private static ObjectView View(string prefab, Vector3 at, Vector3 turn, Action<ZdoData>? fill = null, float scale = 1)
    {
        var z = new ZdoData { Prefab = StableHash.Of(prefab) };
        fill?.Invoke(z);
        return new ObjectView(prefab, at, turn, z, scale);
    }

    [Fact]
    public void PackedIdsAreReadAndWrittenAsTheModKeepsThemAndShownInWords()
    {
        byte[] bytes = PackedIds.Write([PostA, -5]);
        Assert.Equal(9, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal([PostA, -5], PackedIds.Read(bytes)!);
        // Another version, or damaged bytes, are not read.
        Assert.Null(PackedIds.Read([2, 0, 0, 0, 0]));
        Assert.Null(PackedIds.Read([1, 0, 0]));
        Assert.Null(PackedIds.Read([]));

        var view = View("example_post", default, default);
        Assert.Equal("Linked to 2 object(s): 13, -5", ModExtensions.Decode(StableHash.Of(Keys.Packed), view, bytes));
        Assert.Equal("Linked to nothing.", ModExtensions.Decode(StableHash.Of(Keys.Packed), view, PackedIds.Write([])));
        Assert.Null(ModExtensions.Decode(StableHash.Of(Keys.Packed), view, [9]));
    }

    // Two posts on a vehicle, A's packed ids naming B and an object not copied: the copy's A names the copy's B only.
    [Fact]
    public void APastedVehiclesPackedIdsFollowItsObjects()
    {
        static NewObject Made(params ObjectField[] data) => new(0, StableHash.Of("wood_floor"), Vector3.Zero, Vector3.Zero, 0, Data: data);
        var copy = ObjectLinks.Remap(
        [
            Made(Int(ExampleMod.IdKey, Root)),
            Made(Int(ExampleMod.VehicleKey, Root), Int(ExampleMod.IdKey, PostA), new ObjectField("bytes", StableHash.Of(Keys.Packed), Convert.ToBase64String(PackedIds.Write([PostB, 999])))),
            Made(Int(ExampleMod.VehicleKey, Root), Int(ExampleMod.IdKey, PostB)),
        ], new HashSet<int> { Root, PostA, PostB, 999 });

        int b = int.Parse(copy[2].Data!.Single(f => f.Key == StableHash.Of(ExampleMod.IdKey)).Value!, CultureInfo.InvariantCulture);
        Assert.NotEqual(PostB, b);
        string packed = copy[1].Data!.Single(f => f.Key == StableHash.Of(Keys.Packed)).Value!;
        Assert.Equal([b], PackedIds.Read(Convert.FromBase64String(packed))!);
    }

    [Fact]
    public void ASailIsDrawnFromItsCornersInItsColour()
    {
        static Action<ZdoData> Sail(int count, params Vector3[] corners) => z =>
        {
            z.IntList.Add((StableHash.Of(Keys.Corners), count));
            for (int i = 0; i < corners.Length; i++)
            {
                z.Vec3List.Add((StableHash.Of(Keys.Corner + (i + 1)), corners[i]));
            }
        };
        int prefab = StableHash.Of(Keys.Sail);
        var four = View(Keys.Sail, default, default, z =>
        {
            Sail(4, Vector3.Zero, new Vector3(0, 4, 0), new Vector3(3, 4, 0), new Vector3(3, 0, 0))(z);
            z.ByteList.Add((StableHash.Of(Keys.Colour), [255, 0, 0, 255]));
        });

        var shape = Assert.Single(ModExtensions.ShapesOf(prefab, four, ModExtensions.NoWorld));

        Assert.Equal(4, shape.Vertices.Length);
        Assert.Equal([0, 1, 2, 0, 2, 3], shape.Triangles);
        Assert.Equal(new Vector4(1, 0, 0, 1), shape.Color);
        Assert.True(shape.BothSides);
        Assert.False(shape.WorldSpace);
        Assert.Equal(3, Assert.Single(ModExtensions.ShapesOf(prefab, View(Keys.Sail, default, default, Sail(3, Vector3.Zero, Vector3.UnitY, Vector3.UnitX)), ModExtensions.NoWorld)).Triangles.Length);
        // A sail missing a corner, or not woven yet, draws nothing.
        Assert.Empty(ModExtensions.ShapesOf(prefab, View(Keys.Sail, default, default, Sail(4, Vector3.Zero, Vector3.UnitY)), ModExtensions.NoWorld));
        Assert.Empty(ModExtensions.ShapesOf(prefab, View(Keys.Sail, default, default), ModExtensions.NoWorld));
    }

    /// <summary>A world of the posts given, by id.</summary>
    private sealed class Posts(Dictionary<int, IObjectView> posts) : IWorldView
    {
        public bool TryFind(string idKey, int id, out IObjectView obj)
        {
            obj = idKey == Keys.Id && posts.TryGetValue(id, out var p) ? p : null!;
            return obj != null;
        }
    }

    // A post at (10, 30, 10) turned 90° (its tie 2 m up), a cable to a post at (20, 30, 10) twice as tall: a ribbon
    // from tie to tie in world space; none to a post not in the world.
    [Fact]
    public void ACableRunsFromAPostToTheOneItNames()
    {
        var a = View(Keys.Post, new Vector3(10, 30, 10), new Vector3(0, 90, 0), z => z.IntList.Add((StableHash.Of(Keys.Target), PostB)));
        var b = View(Keys.Post, new Vector3(20, 30, 10), Vector3.Zero, z => z.IntList.Add((StableHash.Of(Keys.Id), PostB)), scale: 2);
        var world = new Posts(new() { [PostB] = b });

        var cable = Assert.Single(ModExtensions.ShapesOf(StableHash.Of(Keys.Post), a, world));

        Assert.True(cable.WorldSpace);
        Assert.Equal(4, cable.Vertices.Length);
        Assert.All(cable.Vertices.Take(2), v => Assert.True(Vector3.Distance(v, new Vector3(10, 32, 10)) < 0.05f, v.ToString()));
        Assert.All(cable.Vertices.Skip(2), v => Assert.True(Vector3.Distance(v, new Vector3(20, 34, 10)) < 0.05f, v.ToString()));
        Assert.Empty(ModExtensions.ShapesOf(StableHash.Of(Keys.Post), a, ModExtensions.NoWorld));
    }

    [Fact]
    public void TheSamplesManifestLinksAsTheTestsExampleModDoes()
    {
        var sample = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "worldeditor.json")))!;
        Assert.Equal("ExampleVehicles.WorldEditor.dll", (string?)sample["extension"]);
        var expected = System.Text.Json.Nodes.JsonNode.Parse(ExampleMod.Manifest)!["links"];
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(expected, sample["links"]));
    }

    [Fact]
    public void ThePitsOutlineIsAsWideAsItSays()
    {
        int prefab = StableHash.Of(Keys.Pit);
        var cut = Assert.Single(ModExtensions.CutsOf(prefab, View(Keys.Pit, default, default, z => z.FloatList.Add((StableHash.Of(Keys.Radius), 3))), ModExtensions.NoWorld));
        Assert.False(cut.WorldSpace);
        Assert.Equal(16, cut.Outline.Length);
        Assert.All(cut.Outline, p => Assert.Equal(3, p.Length(), 3));
        // No radius kept: the default one.
        Assert.All(Assert.Single(ModExtensions.CutsOf(prefab, View(Keys.Pit, default, default), ModExtensions.NoWorld)).Outline, p => Assert.Equal(1.5f, p.Length(), 3));
    }
}
