using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Loading what a stand draws of an item it holds: the item prefab's "attach" child (or the one an armour stand's
/// slot takes), as the game instantiates it under the stand's attach point.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Which of an item's children a stand draws (the game's ItemStand.GetAttachPrefab and VisEquipment.AttachItem /
    /// AttachArmor): the child's name decides, and the way it is placed with it.
    /// </summary>
    public enum HeldUse
    {
        /// <summary>On an item stand: "attach" (its "attachobj" child when it has one), placed with its own local transform.</summary>
        Stand,
        /// <summary>In an armour stand's hand or on its head: "attach", else "attach_skin"; at the joint.</summary>
        Hand,
        /// <summary>On an armour stand's back: "attach_back", else "attach"; at the joint.</summary>
        Back,
        /// <summary>On an armour stand's body (chest, legs, cape, belt): "attach_skin" only; on the body.</summary>
        Body,
    }

    /// <summary>
    /// The way an item sits on an item stand (ItemStand.OrientationSettings): an offset the stand adds to the attach
    /// child's transform, for the stands of these orientations.
    /// </summary>
    /// <param name="Orientations">The stand orientations it applies to (1 vertical, 2 horizontal; a mask).</param>
    /// <param name="Position">The offset added to the position.</param>
    /// <param name="Rotation">The turn added, Euler degrees.</param>
    /// <param name="Scale">What the scale is multiplied by.</param>
    public sealed record StandOffset(int Orientations, float[] Position, float[] Rotation, float[] Scale);

    /// <summary>
    /// What a stand draws of an item: the attach child's parts relative to that child, and how the game places it.
    /// </summary>
    /// <param name="Model">The attach child's parts, relative to it (its own transform left out; root scale 1).</param>
    /// <param name="Position">The attach child's local position in the item prefab.</param>
    /// <param name="Rotation">Its local rotation (x, y, z, w).</param>
    /// <param name="Scale">Its local scale.</param>
    /// <param name="Skin">The child is "attach_skin": an armour stand draws it on the body, not at a joint.</param>
    /// <param name="ScaleByQuality">What each quality level above 1 adds to the item's scale.</param>
    /// <param name="Offsets">The item's offsets on item stands, by stand orientation.</param>
    /// <param name="EquipOffset">The item's "equipoffset" child's position and rotation, added at a joint; null without.</param>
    public sealed record HeldModel(Model Model, float[] Position, float[] Rotation, float[] Scale, bool Skin, float ScaleByQuality,
        IReadOnlyList<StandOffset> Offsets, (float[] Position, float[] Rotation)? EquipOffset);

    /// <summary>The held models read so far, by item and use; null for items that have none.</summary>
    private readonly Dictionary<(string, HeldUse), HeldModel?> _held = [];

    /// <summary>
    /// What a stand draws of an item (cached per item and use); null when the game has no such prefab or the item has
    /// no child the use takes.
    /// </summary>
    /// <param name="item">The item's prefab name.</param>
    /// <param name="use">Where it goes, which picks the child.</param>
    public HeldModel? LoadHeld(string item, HeldUse use)
    {
        lock (_sync)
        {
            if (_held.TryGetValue((item, use), out var known))
            {
                return known;
            }
            try
            {
                return _held[(item, use)] = EnsureIndex().TryGetValue(item, out var root) ? ReadHeld(item, root, use) : null;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>The names of the item's children each use takes, in the order the game looks for them.</summary>
    private static string[] ChildrenFor(HeldUse use) => use switch
    {
        HeldUse.Stand => ["attach"],
        HeldUse.Hand => ["attach", "attach_skin"],
        HeldUse.Back => ["attach_back", "attach"],
        _ => ["attach_skin"],
    };

    /// <summary>Reads the held model of an item prefab's root (see <see cref="LoadHeld"/>).</summary>
    private HeldModel? ReadHeld(string item, Where root, HeldUse use)
    {
        if (Read(root.Bundle, root.PathId) is not { } rootGo)
        {
            return null;
        }
        var children = ChildrenOf(root.Bundle, rootGo).ToList();
        var (attachName, attachGo, tr) = ChildrenFor(use).Select(n => children.FirstOrDefault(c => c.Name == n)).FirstOrDefault(c => c.Name != null);
        if (attachName == null)
        {
            return null;
        }
        // An item stand draws the "attachobj" child of the attach child when there is one, placed as the attach child.
        long drawn = attachGo;
        if (use == HeldUse.Stand && Read(root.Bundle, attachGo) is { } attachFields && ChildrenOf(root.Bundle, attachFields).FirstOrDefault(c => c.Name == "attachobj") is { Name: not null } obj)
        {
            drawn = obj.Go;
        }
        var walk = new ModelWalk(this, root.Bundle);
        walk.Scan(root.PathId);
        walk.Walk(drawn, Matrix.Identity(), true);
        var (scaleByQuality, offsets) = ItemFacts(root.Bundle, rootGo);
        var (equipName, _, equipTr) = children.FirstOrDefault(c => c.Name == "equipoffset");
        return new HeldModel(new Model(StandLook.ModelName(item, use), walk.Parts, [1, 1, 1]), V3(tr["m_LocalPosition"]), Q4(tr["m_LocalRotation"]), V3(tr["m_LocalScale"]),
            attachName == "attach_skin", scaleByQuality, offsets, equipName == null ? null : (V3(equipTr["m_LocalPosition"]), Q4(equipTr["m_LocalRotation"])));
    }

    /// <summary>
    /// What the item's ItemDrop says of its placing: the scale each quality level adds, and its offsets on item stands.
    /// </summary>
    private (float ScaleByQuality, List<StandOffset> Offsets) ItemFacts(string bundle, AssetTypeValueField rootGo)
    {
        var offsets = new List<StandOffset>();
        float scaleByQuality = 0;
        foreach (var (t, _, f) in Components(bundle, rootGo))
        {
            if (t != (int)AssetClassID.MonoBehaviour || !Has(f, "m_itemData"))
            {
                continue;
            }
            var shared = f["m_itemData"]["m_shared"];
            scaleByQuality = Has(shared, "m_scaleByQuality") ? shared["m_scaleByQuality"].AsFloat : 0;
            if (Has(shared, "m_itemStandOffsets"))
            {
                offsets.AddRange(shared["m_itemStandOffsets.Array"].Children.Select(o => new StandOffset(o["m_orientations"].AsInt, V3(o["m_position"]), V3(o["m_rotation"]), V3(o["m_scale"]))));
            }
            break;
        }
        return (scaleByQuality, offsets);
    }

    /// <summary>A GameObject's children: each one's name, GameObject and Transform fields (those that can be read).</summary>
    private IEnumerable<(string Name, long Go, AssetTypeValueField Transform)> ChildrenOf(string bundle, AssetTypeValueField go)
    {
        if (Components(bundle, go).FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F is not { } tr)
        {
            yield break;
        }
        foreach (var ch in tr["m_Children.Array"].Children)
        {
            if (Read(bundle, ch["m_PathID"].AsLong) is { } ct && Read(bundle, ct["m_GameObject.m_PathID"].AsLong) is { } cg)
            {
                yield return (cg["m_Name"].AsString, ct["m_GameObject.m_PathID"].AsLong, ct);
            }
        }
    }

    /// <summary>A vector field's three floats.</summary>
    private static float[] V3(AssetTypeValueField v) => [v["x"].AsFloat, v["y"].AsFloat, v["z"].AsFloat];

    /// <summary>A quaternion field's four floats.</summary>
    private static float[] Q4(AssetTypeValueField v) => [v["x"].AsFloat, v["y"].AsFloat, v["z"].AsFloat, v["w"].AsFloat];
}
