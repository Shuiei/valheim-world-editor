using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Quaternion = System.Numerics.Quaternion;
using Rnd = ValheimGen.UnityEngine.Random;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// A blueprint grown in place of an object (Expand World Data's blueprint vegetation): its parts made by chance, turned
/// and scaled with it, given the rule's data with theirs over it, nested blueprints in their place, and the draws of
/// each as the mod and the game make them.
/// </summary>
public class RegrowBlueprintsTests
{
    [Fact]
    public void ABlueprintGrowsItsPartsAsTheModSpawnsThem()
    {
        int mark = StableHash.Of("mark"), own = StableHash.Of("own");
        var veg = new VegetationRule
        {
            prefab = "Hut",
            data = [new ContentField("ints", mark, "1")],
            blueprints = new Dictionary<string, IReadOnlyList<BlueprintPart>>
            {
                ["Hut"] =
                [
                    new("Pickable_Stone", Vector3.Zero, Quaternion.Identity, Vector3.One, null, 0f),
                    new("Bush01", new Vector3(2, 0, 0), Quaternion.Identity, new Vector3(2), [new ContentField("ints", own, "2"), new ContentField("ints", mark, "3")], 1f),
                    new("Inner", new Vector3(0, 0, 3), Quaternion.Identity, Vector3.One, null, 1f),
                ],
                ["Inner"] = [new("wood_fence", new Vector3(1, 0, 0), Quaternion.Identity, Vector3.One, null, 1f)],
            },
        };
        List<Regrow.Spot> spots = [];
        Rnd.InitState(5);
        // Turned a quarter about y: the blueprint's x becomes the world's -z.
        PrivateMember.Call(typeof(Regrow), "PlaceBlueprint", null, veg, veg.blueprints["Hut"], new Vector3(100, 10, 200), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), spots);
        Assert.Equal(["Bush01", "wood_fence"], spots.Select(s => s.Name));
        Assert.True(Vector3.Distance(new Vector3(100, 10, 198), new Vector3(spots[0].X, spots[0].Y, spots[0].Z)) < 1e-4f);
        Assert.True(Vector3.Distance(new Vector3(103, 10, 199), new Vector3(spots[1].X, spots[1].Y, spots[1].Z)) < 1e-4f);
        // The bush keeps its scale; the rule's data first, its own over it.
        Assert.Equal(new Vector3(2), spots[0].Scale3);
        Assert.Equal([new ContentField("ints", mark, "1"), new ContentField("ints", own, "2"), new ContentField("ints", mark, "3")], spots[0].Data!);
        Assert.Equal(Vector3.One, spots[1].Scale3);
        // The draws: the stone's chance, the bush's components, the fence's two timers.
        var after = Rnd.State;
        Rnd.InitState(5);
        int draws = 1 + (Regrow.GameRules.FirstOrDefault(r => r.prefab == "Bush01")?.draws ?? 0) + 2;
        for (int i = 0; i < draws; i++)
        {
            Rnd.NextUInt();
        }
        Assert.Equal(Rnd.State, after);
    }
}
