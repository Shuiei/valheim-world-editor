using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// What a stand draws of an item (GameBundles.LoadHeld: the item's attach child by use, its transform and the item's
/// placing facts) and where a stand hangs it (LoadStandPoints: an item stand's attach point, an armour stand's slots
/// and joints), read from a made-up game.
/// </summary>
public class GameBundlesStandTests
{
    /// <summary>
    /// A game with a shield (attach, attach_back and equipoffset children), a chest piece (attach_skin only), a wall
    /// stand whose attach point hangs under a child, and an armour stand with a hand, a head and a chest slot.
    /// </summary>
    private static GameBundles Game()
    {
        var game = new TestGame();
        var b = game.Bundle("stands");
        long quad = Quad("quad").Add(b);
        long mat = TestAssets.Material(b, "paint");
        void Draw(PrefabNode c) => c.Renderer(Fields.Ref(quad), [Fields.Ref(mat)]);
        long Skinned(PrefabNode c) => c.Component(UnityClass.SkinnedMeshRenderer, UnityTypes.SkinnedMeshRenderer, new Fields { ["m_Enabled"] = true, ["m_Materials"] = new List<Fields> { Fields.Ref(mat) }, ["m_Mesh"] = Fields.Ref(quad) });
        Items(b, Draw, Skinned);
        b.Prefab("Stone");
        ItemStand(b);
        ArmourStand(b, Skinned);
        return SavedGameBundles.Of(game);
    }

    /// <summary>The shield and the chest piece.</summary>
    private static void Items(TestBundle b, Action<PrefabNode> draw, Func<PrefabNode, long> skinned)
    {
        b.Prefab("Shield", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("ItemDrop", new Fields
            {
                ["m_itemData"] = new Fields
                {
                    ["m_shared"] = new Fields
                    {
                        ["m_itemType"] = 5,
                        ["m_scaleByQuality"] = 0.1,
                        ["m_itemStandOffsets"] = new List<Fields> { new() { ["m_position"] = Fields.Vector3(0, 0.05, 0), ["m_rotation"] = Fields.Vector3(0, 90, 0), ["m_scale"] = Fields.Vector3(1, 1, 1), ["m_orientations"] = 2 } },
                    },
                },
            });
            r.Child("attach", c =>
            {
                c.Active = false;
                c.Position = (0, -0.05, 0.1);
                c.Scale = (2, 2, 2);
                c.Child("model", m =>
                {
                    m.Position = (0, 0.5, 0);
                    draw(m);
                });
            });
            r.Child("attach_back", c => c.Child("back", draw));
            r.Child("equipoffset", c => c.Position = (0, 0.1, 0));
        });
        b.Prefab("Chest", r =>
        {
            r.Mono("ItemDrop", new Fields { ["m_itemData"] = new Fields { ["m_shared"] = new Fields { ["m_itemType"] = 7 } } });
            r.Child("attach_skin", c =>
            {
                c.Active = false;
                c.Child("body", m => skinned(m));
            });
        });
    }

    /// <summary>The wall stand: its attach point under a child, the root placed away from the origin.</summary>
    private static void ItemStand(TestBundle b)
    {
        b.Prefab("itemstand", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Position = (0, 50, 0);
            long attachTr = 0;
            r.Child("New", n =>
            {
                n.Position = (0, 1, 0);
                attachTr = n.Child("attach_other", a => a.Position = (0, 0, 0.147)).Tr;
            });
            r.Mono("ItemStand", new Fields { ["m_supportedTypes"] = new List<object> { 5 }, ["m_attachOther"] = Fields.Ref(attachTr), ["m_orientationType"] = 2 });
        });
    }

    /// <summary>The armour stand: its joints down a scaled chain, its body's skinned renderer, three slots.</summary>
    private static void ArmourStand(TestBundle b, Func<PrefabNode, long> skinned)
    {
        b.Prefab("ArmorStand", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            long hand = 0, head = 0, body = 0, vis = 0;
            r.Child("Player Pose", p =>
            {
                p.Position = (0, 0.1, 0);
                p.Child("Visual", v =>
                {
                    v.Scale = (0.5, 0.5, 0.5);
                    v.Child("Armature", a =>
                    {
                        a.Scale = (100, 100, 100);
                        hand = a.Child("LeftHand_Attach", h =>
                        {
                            h.Position = (0, 0.01, 0);
                            h.Scale = (0.01, 0.01, 0.01);
                        }).Tr;
                        head = a.Child("Helmet_attach", h => h.Position = (0, 0.02, 0)).Tr;
                    });
                    v.Child("body", m => body = skinned(m));
                });
                vis = p.Mono("VisEquipment", new Fields { ["m_bodyModel"] = Fields.Ref(body), ["m_helmet"] = Fields.Ref(head), ["m_leftHand"] = Fields.Ref(hand) });
            });
            r.Mono("ArmorStand", new Fields
            {
                ["m_slots"] = new List<Fields> { new() { ["m_slot"] = 0, ["m_supportedTypes"] = new List<object> { 5 } }, new() { ["m_slot"] = 6 }, new() { ["m_slot"] = 4 } },
                ["m_visEquipment"] = Fields.Ref(vis),
                ["m_poseCount"] = 3,
            });
        });
    }

    [Fact]
    public void AnItemsAttachChildIsWhatAStandDrawsOfIt()
    {
        var bundles = Game();
        var held = bundles.LoadHeld("Shield", GameBundles.HeldUse.Stand)!;
        Assert.Equal("Shield/stand", held.Model.Name);
        // The attach child's part, relative to the child (its own transform kept apart), though the child is inactive.
        var part = Assert.Single(held.Model.Parts);
        Assert.Equal(0.5f, part.Matrix[7]);
        Assert.Equal([1f, 1f, 1f], held.Model.RootScale);
        Assert.Equal([0f, -0.05f, 0.1f], held.Position);
        Assert.Equal([0f, 0f, 0f, 1f], held.Rotation);
        Assert.Equal([2f, 2f, 2f], held.Scale);
        Assert.False(held.Skin);
        Assert.Equal(0.1f, held.ScaleByQuality);
        var offset = Assert.Single(held.Offsets);
        Assert.Equal((2, 0.05f, 90f), (offset.Orientations, offset.Position[1], offset.Rotation[1]));
        Assert.Equal([0f, 0.1f, 0f], held.EquipOffset!.Value.Position);
        // Asked again: kept.
        Assert.Same(held, bundles.LoadHeld("Shield", GameBundles.HeldUse.Stand));
        // In a hand: the same child; on the back: attach_back; on the body: nothing (no attach_skin).
        Assert.Equal([0f, -0.05f, 0.1f], bundles.LoadHeld("Shield", GameBundles.HeldUse.Hand)!.Position);
        Assert.Equal("Shield/back", bundles.LoadHeld("Shield", GameBundles.HeldUse.Back)!.Model.Name);
        Assert.Null(bundles.LoadHeld("Shield", GameBundles.HeldUse.Body));
        // Skinned armour: attach_skin, on the body; no offsets, no equip offset.
        var chest = bundles.LoadHeld("Chest", GameBundles.HeldUse.Body)!;
        Assert.True(chest.Skin);
        Assert.Single(chest.Model.Parts);
        Assert.Empty(chest.Offsets);
        Assert.Null(chest.EquipOffset);
        Assert.Null(bundles.LoadHeld("Chest", GameBundles.HeldUse.Stand));
        Assert.Null(bundles.LoadHeld("Stone", GameBundles.HeldUse.Stand));
        Assert.Null(bundles.LoadHeld("Nothing", GameBundles.HeldUse.Stand));
    }

    [Fact]
    public void AStandsPointsAreWhereItHangsWhatItHolds()
    {
        var bundles = Game();
        var wall = bundles.LoadStandPoints("itemstand")!;
        Assert.Equal(2, wall.Orientation);
        Assert.Null(wall.Slots);
        // The attach point under its parent, relative to the root (the root's own position left out).
        Assert.Equal([0f, 1f, 0.147f], [wall.Attach![3], wall.Attach[7], wall.Attach[11]]);
        Assert.Same(wall, bundles.LoadStandPoints("itemstand"));
        var armour = bundles.LoadStandPoints("ArmorStand")!;
        Assert.Null(armour.Attach);
        Assert.Equal([0, 6, 4], armour.Slots);
        Assert.Equal(["body", "helmet", "leftHand"], armour.Joints.Keys.Order());
        // The hand's place down the chain: 0.1 + 0.5 * 100 * 0.01, and its scale 0.5 * 100 * 0.01.
        float[] hand = armour.Joints["leftHand"];
        Assert.Equal(0.6f, hand[7], 4);
        Assert.Equal(0.5f, hand[0], 4);
        // The body: the renderer's parent (Visual), with its scale.
        float[] body = armour.Joints["body"];
        Assert.Equal((0.5f, 0.1f), (body[0], body[7]));
        Assert.Null(bundles.LoadStandPoints("Stone"));
        Assert.Null(bundles.LoadStandPoints("Nothing"));
    }
}
