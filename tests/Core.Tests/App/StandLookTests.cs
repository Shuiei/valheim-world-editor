using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

using static TerrainEditor.App.GameBundles;

namespace WorldEditor.Tests;

/// <summary>
/// How the view names and keys the items stands hold (StandLook) and where it puts them: on an item stand as the
/// game places the attach child, with the item's offset and quality; in an armour stand's slot at the joint the slot
/// and the item's type give.
/// </summary>
public class StandLookTests
{
    private static readonly float[] Zero = [0, 0, 0], One = [1, 1, 1], NoTurn = [0, 0, 0, 1];

    /// <summary>A held model with no parts and these placing facts.</summary>
    private static HeldModel Held(float[] position, float[] scale, bool skin = false, float scaleByQuality = 0, StandOffset[]? offsets = null, (float[], float[])? equip = null) =>
        new(new Model("x", [], One), position, NoTurn, scale, skin, scaleByQuality, offsets ?? [], equip);

    [Fact]
    public void HeldModelsAreNamedByItemAndUseAndKeyedForTheView()
    {
        Assert.Equal("ShieldBronzeBuckler/stand", StandLook.ModelName("ShieldBronzeBuckler", HeldUse.Stand));
        Assert.Equal(("HelmetBronze", HeldUse.Hand), StandLook.Parse("HelmetBronze/hand"));
        Assert.Equal(("SwordBronze", HeldUse.Back), StandLook.Parse("SwordBronze/back"));
        Assert.Equal(("ArmorBronzeChest", HeldUse.Body), StandLook.Parse("ArmorBronzeChest/body"));
        Assert.Null(StandLook.Parse("ShieldBronzeBuckler"));
        Assert.Null(StandLook.Parse("ShieldBronzeBuckler/other"));
        Assert.Null(StandLook.Parse("/stand"));
        int key = StandLook.KeyOf("TrophyDeer/stand");
        Assert.Equal(StableHash.Of("TrophyDeer/stand"), key);
        Assert.Equal("TrophyDeer/stand", StandLook.NameOf(key));
        Assert.Null(StandLook.NameOf(StableHash.Of("TrophyBoar/stand")));
        Assert.Equal(HeldUse.Hand, StandLook.UseOfSlot(1));
        Assert.Equal(HeldUse.Hand, StandLook.UseOfSlot(6));
        Assert.Equal(HeldUse.Back, StandLook.UseOfSlot(3));
        Assert.Equal(HeldUse.Body, StandLook.UseOfSlot(7));
    }

    [Fact]
    public void AnItemStandPlacesTheAttachChildAtItsPointWithTheItemsOffsetAndQuality()
    {
        // A flat stand (horizontal) with its attach point 0.2 m up.
        var stand = new StandPoints(Matrix.Trs(0, 0.2f, 0, 0, 0, 0, 1, 1, 1, 1), 2, null, new Dictionary<string, float[]>());
        var offsets = new[] { new StandOffset(1, [0, 9, 0], Zero, One), new StandOffset(2, [0, 0.05f, 0], [0, 90, 0], [2, 2, 2]), new StandOffset(3, [0, 0.5f, 0], Zero, One) };
        var held = Held([0, -0.05f, 0.1f], One, scaleByQuality: 0.1f, offsets: offsets);
        // Orientation 0: the first offset matching the stand (the second: horizontal), turned 90° about y: x goes to -z.
        float[] m = StandLook.OnItemStand(stand, held, 1, 0)!;
        Assert.Equal((0f, 0.2f, 0.1f), (m[3], m[7], m[11]));
        Assert.Equal(-2f, m[8], 4);
        Assert.Equal(0f, m[0], 4);
        // Orientation 1: the next matching offset; past the last: the last. Quality 3 scales by 1.2.
        float[] second = StandLook.OnItemStand(stand, held, 3, 1)!;
        Assert.Equal((0.65f, 1.2f), (second[7], second[0]));
        Assert.Equal(second, StandLook.OnItemStand(stand, held, 3, 7));
        // No offset for the stand's orientation: the attach child as it is.
        var wall = stand with { Orientation = 1 };
        float[] plain = StandLook.OnItemStand(wall, Held([0, -0.05f, 0.1f], [2, 2, 2], offsets: [offsets[1]]), 1, 0)!;
        Assert.Equal((0f, 0.15f, 0.1f, 2f), (plain[3], plain[7], plain[11], plain[0]));
        Assert.Null(StandLook.OnItemStand(new StandPoints(null, 0, [0], new Dictionary<string, float[]>()), held, 1, 0));
    }

    [Fact]
    public void AnArmourStandPlacesItemsAtTheirSlotsJoints()
    {
        // A hand joint scaled down the chain (0.005), a helmet joint, a back joint and the body.
        var joints = new Dictionary<string, float[]>
        {
            ["leftHand"] = Matrix.Trs(0.3f, 1, 0, 0, 0, 0, 1, 0.005f, 0.005f, 0.005f),
            ["helmet"] = Matrix.Trs(0, 1.7f, 0, 0, 0, 0, 1, 1, 1, 1),
            ["backShield"] = Matrix.Trs(0, 1.2f, -0.2f, 0, 0, 0, 1, 1, 1, 1),
            ["body"] = Matrix.Trs(0, 0.1f, 0, 0, 0, 0, 1, 0.5f, 0.5f, 0.5f),
        };
        var stand = new StandPoints(null, 0, [0, 6, 2, 4], joints);
        var held = Held([0, 5, 0], [2, 2, 2], equip: ([0, 0.1f, 0], NoTurn));
        // In the hand: at the joint's place without its scale, the attach child's own scale and equip offset kept, its
        // position left out.
        float[] hand = StandLook.OnArmourStand(stand, held, 0, 5)!;
        Assert.Equal((0.3f, 1.1f, 0f, 2f), (hand[3], hand[7], hand[11], hand[0]));
        Assert.Equal(1.8f, StandLook.OnArmourStand(stand, held, 1, 6)![7], 4);
        // On the back: by the item's type (a shield), and nothing for a type with no back joint.
        Assert.Equal(-0.2f, StandLook.OnArmourStand(stand, held, 2, 5)![11]);
        Assert.Null(StandLook.OnArmourStand(stand, held, 2, 13));
        // Skinned armour: on the body, with its scale, whatever the slot.
        float[] body = StandLook.OnArmourStand(stand, Held(Zero, One, skin: true), 3, 7)!;
        Assert.Equal((0.5f, 0.1f), (body[0], body[7]));
        // No such slot, and a slot whose joint the stand lacks.
        Assert.Null(StandLook.OnArmourStand(stand, held, 4, 5));
        Assert.Null(StandLook.OnArmourStand(stand with { Joints = new Dictionary<string, float[]>() }, held, 0, 5));
        Assert.Null(StandLook.OnArmourStand(stand with { Slots = null }, held, 0, 5));
    }

    [Fact]
    public void EulerAnglesTurnAsUnityDoes()
    {
        // 90° about y takes x to -z; 90° about x takes y to z; 90° about z takes x to y. Applied z, then x, then y:
        // (0, 90, 90) takes y to -x (about z), then -x to z (about y).
        static float[] Turned(float[] q, float x, float y, float z)
        {
            float[] m = StandLook.Trs(Zero, q, One);
            return [m[0] * x + m[1] * y + m[2] * z, m[4] * x + m[5] * y + m[6] * z, m[8] * x + m[9] * y + m[10] * z];
        }
        AssertNear([0, 0, -1], Turned(StandLook.Euler([0, 90, 0]), 1, 0, 0));
        AssertNear([0, 0, 1], Turned(StandLook.Euler([90, 0, 0]), 0, 1, 0));
        AssertNear([0, 1, 0], Turned(StandLook.Euler([0, 0, 90]), 1, 0, 0));
        AssertNear([0, 0, 1], Turned(StandLook.Euler([0, 90, 90]), 0, 1, 0));
        // The scale taken out of a matrix leaves its turn and place.
        float[] unscaled = StandLook.Unscaled(Matrix.Trs(1, 2, 3, 0, 0.7071068f, 0, 0.7071068f, 3, 3, 3));
        AssertNear([0, 0, -1], [unscaled[0], unscaled[4], unscaled[8]]);
        Assert.Equal((1f, 2f, 3f), (unscaled[3], unscaled[7], unscaled[11]));
        // A zero axis stays as it is.
        Assert.Equal(0f, StandLook.Unscaled(Matrix.Trs(0, 0, 0, 0, 0, 0, 1, 0, 1, 1))[0]);
    }

    /// <summary>The vectors are the same within rounding.</summary>
    private static void AssertNear(float[] expected, float[] actual)
    {
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(expected[i], actual[i], 4);
        }
    }
}
