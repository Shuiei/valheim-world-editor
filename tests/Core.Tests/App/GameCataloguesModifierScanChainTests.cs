using System.Text.Json.Nodes;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The walk from a terrain modifier up to its prefab (GameCatalogues.ModifierScan.Up and Order): a modifier whose objects
/// cannot be followed up to a prefab root is left out, the others still read; a modifier on an object comes before one on
/// its child.
/// </summary>
public class GameCataloguesModifierScanChainTests
{
    [Fact]
    public void AModifierWhoseObjectsCannotBeFollowedUpIsLeftOut()
    {
        using var game = new TestGame();
        var b = game.Bundle("modifiers");
        b.Prefab("Kiln", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("TerrainModifier", new Fields { ["m_sortOrder"] = 5 });
        });
        // On an object that is not in the bundle.
        b.Mono(b.Reserve(), "TerrainModifier", new Fields { ["m_sortOrder"] = 1 });
        // On an object without a Transform.
        Loose(b, "no_transform", null);
        // Under a parent that is not in the bundle.
        Loose(b, "lost_parent", _ => b.Reserve());
        // Its own parent: the walk up never reaches a root.
        Loose(b, "own_parent", tr => tr);
        game.Save();

        var json = JsonNode.Parse(GameCatalogues.ReadAtStart(game.Bundles(), ["terrain-modifiers"])["terrain-modifiers"])!;

        Assert.Equal(["Kiln"], json.AsObject().Select(kv => kv.Key));
    }

    [Fact]
    public void AModifierOnAnObjectComesBeforeOneOnItsChild()
    {
        using var game = new TestGame();
        var b = game.Bundle("modifiers");
        b.Prefab("Tower", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Child("floor", c =>
            {
                // The child's modifier is written, and found, first.
                c.Child("roof", y => y.Mono("TerrainModifier", new Fields { ["m_sortOrder"] = 2 }));
                c.Mono("TerrainModifier", new Fields { ["m_sortOrder"] = 1 });
            });
        });
        game.Save();

        var json = JsonNode.Parse(GameCatalogues.ReadAtStart(game.Bundles(), ["terrain-modifiers"])["terrain-modifiers"])!;

        Assert.Equal([1, 2], json["Tower"]!["modifiers"]!.AsArray().Select(m => m!["sortOrder"]!.GetValue<long>()));
    }

    /// <summary>
    /// A GameObject with a terrain modifier, written by hand: with a Transform whose parent is <paramref name="father"/>
    /// of the Transform's own path id, or without a Transform when it is null.
    /// </summary>
    /// <param name="b">The bundle written to.</param>
    /// <param name="name">The GameObject's name.</param>
    /// <param name="father">The parent Transform's path id, from the Transform's own; null: no Transform.</param>
    private static void Loose(TestBundle b, string name, Func<long, long>? father)
    {
        long go = b.Reserve(), tr = b.Reserve();
        long modifier = b.Mono(go, "TerrainModifier", new Fields { ["m_sortOrder"] = 3 });
        var components = father == null ? [] : new List<Fields> { new() { ["component"] = Fields.Ref(tr) } };
        components.Add(new Fields { ["component"] = Fields.Ref(modifier) });
        b.Assets.Add(UnityClass.GameObject, UnityTypes.GameObject, new Fields
        {
            ["m_Component"] = components,
            ["m_Name"] = name,
            ["m_IsActive"] = true,
        }, pathId: go);
        if (father != null)
        {
            b.Assets.Add(UnityClass.Transform, UnityTypes.Transform, new Fields
            {
                ["m_GameObject"] = Fields.Ref(go),
                ["m_LocalRotation"] = Fields.Quaternion(0, 0, 0, 1),
                ["m_LocalScale"] = Fields.Vector3(1, 1, 1),
                ["m_Father"] = Fields.Ref(father(tr)),
            }, pathId: tr);
        }
    }
}
