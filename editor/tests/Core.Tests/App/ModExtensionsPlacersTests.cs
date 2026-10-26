using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The running extensions' placers (ModExtensions.Place): each handed the placed objects of its own prefabs only, all at
/// once, and known by the prefabs it places; a placer that throws turns its extension off, the others go on. Runs alone
/// (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsPlacersTests : IDisposable
{
    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>A placer of the prefabs given, noting what it is handed, or throwing.</summary>
    private sealed class Placer(bool fail, params string[] prefabs) : IWorldEditorExtension, IObjectPlacer
    {
        public List<string> Handed { get; } = [];

        public void Register(IExtensionRegistry registry) => registry.AddPlacer(this, prefabs);

        public void Place(IReadOnlyList<ICopy> placed, IAreaView area)
        {
            if (fail)
            {
                throw new InvalidOperationException("no");
            }
            Handed.AddRange(placed.Select(p => p.Prefab));
        }
    }

    private static (int, ICopy) Placed(string prefab) => (StableHash.Of(prefab), new CopyView(prefab, Vector3.Zero, Vector3.Zero, []));

    [Fact]
    public void EachPlacerIsHandedItsOwnPrefabsOnly()
    {
        var carts = new Placer(false, "cart", "rail");
        var boats = new Placer(false, "boat");
        ModExtensions.UseForTests(("Carts", carts), ("Boats", boats));

        Assert.True(ModExtensions.HasPlacer(StableHash.Of("rail")));
        Assert.False(ModExtensions.HasPlacer(StableHash.Of("tree")));
        Assert.Equal("cart", ModExtensions.PlacedName(StableHash.Of("cart")));
        Assert.Null(ModExtensions.PlacedName(StableHash.Of("tree")));

        ModExtensions.Place([Placed("rail"), Placed("tree"), Placed("cart"), Placed("boat")], new PlainArea(-8, -8, 8, 8, 30));

        Assert.Equal(["rail", "cart"], carts.Handed);
        Assert.Equal(["boat"], boats.Handed);
    }

    [Fact]
    public void APlacerThatThrowsTurnsItsExtensionOff()
    {
        var fine = new Placer(false, "rail");
        ModExtensions.UseForTests(("Broken", new Placer(true, "rail")), ("Fine", fine));

        ModExtensions.Place([Placed("rail")], new PlainArea(-8, -8, 8, 8, 30));

        Assert.Equal(["rail"], fine.Handed);
        Assert.Contains(ModExtensions.Offered, e => e.Mod == "Broken" && e.State == ModExtensions.Status.Failed);
    }
}
