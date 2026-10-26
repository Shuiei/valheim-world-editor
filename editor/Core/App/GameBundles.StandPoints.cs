using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Loading where a stand hangs what it holds: an item stand's attach point, an armour stand's slots and the joints
/// its equipment goes to.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>The joints of an armour stand's equipment (VisEquipment's fields), by the names <see cref="StandPoints.Joints"/> uses.</summary>
    private static readonly (string Name, string Field)[] JointFields =
    [
        ("helmet", "m_helmet"), ("leftHand", "m_leftHand"), ("rightHand", "m_rightHand"), ("backShield", "m_backShield"), ("backMelee", "m_backMelee"),
        ("backTwohandedMelee", "m_backTwohandedMelee"), ("backBow", "m_backBow"), ("backTool", "m_backTool"), ("backAtgeir", "m_backAtgeir"),
    ];

    /// <summary>
    /// Where a stand hangs what it holds, relative to its root (the root's own transform left out, as in its model).
    /// </summary>
    /// <param name="Attach">An item stand's attach point (ItemStand.m_attachOther) as a matrix; null for an armour stand.</param>
    /// <param name="Orientation">An item stand's orientation (ItemStand.m_orientationType: 1 vertical, 2 horizontal).</param>
    /// <param name="Slots">An armour stand's slots, each the equipment slot it fills (VisSlot: 0 left hand, 1 right hand, 2
    /// left back, 3 right back, 4 chest, 5 legs, 6 helmet, 7 shoulder, 8 utility); null for an item stand.</param>
    /// <param name="Joints">An armour stand's joints as matrices: "helmet", "leftHand", "rightHand", "backShield", "backMelee",
    /// "backTwohandedMelee", "backBow", "backTool", "backAtgeir" (those the stand has), and "body" (where skinned armour
    /// goes: the body renderer's parent).</param>
    public sealed record StandPoints(float[]? Attach, int Orientation, IReadOnlyList<int>? Slots, IReadOnlyDictionary<string, float[]> Joints);

    /// <summary>The stand points read so far, by prefab; null for prefabs that are no stand.</summary>
    private readonly Dictionary<string, StandPoints?> _standPoints = [];

    /// <summary>
    /// Where a stand prefab hangs what it holds (cached per name); null when the game has no such prefab or it is
    /// neither an item stand nor an armour stand.
    /// </summary>
    public StandPoints? LoadStandPoints(string name)
    {
        lock (_sync)
        {
            if (_standPoints.TryGetValue(name, out var known))
            {
                return known;
            }
            try
            {
                return _standPoints[name] = EnsureIndex().TryGetValue(name, out var root) ? ReadStandPoints(root) : null;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>Reads a stand's points from its root's ItemStand or ArmorStand script (see <see cref="LoadStandPoints"/>).</summary>
    private StandPoints? ReadStandPoints(Where root)
    {
        if (Read(root.Bundle, root.PathId) is not { } go)
        {
            return null;
        }
        var cs = Components(root.Bundle, go).ToList();
        long rootTr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).Pid;
        foreach (var (t, _, f) in cs)
        {
            if (t != (int)AssetClassID.MonoBehaviour)
            {
                continue;
            }
            if (Has(f, "m_attachOther") && Has(f, "m_supportedTypes"))
            {
                return new StandPoints(ToRoot(root.Bundle, Resolve(root.Bundle, f["m_attachOther"]), rootTr), Has(f, "m_orientationType") ? f["m_orientationType"].AsInt : 1, null, new Dictionary<string, float[]>());
            }
            if (Has(f, "m_slots") && Has(f, "m_poseCount"))
            {
                var slots = f["m_slots.Array"].Children.Select(s => s["m_slot"].AsInt).ToList();
                return new StandPoints(null, 0, slots, Joints(root.Bundle, Resolve(root.Bundle, f["m_visEquipment"]), rootTr));
            }
        }
        return null;
    }

    /// <summary>
    /// An armour stand's joints from its VisEquipment script: those of <see cref="JointFields"/> it has, and "body".
    /// </summary>
    private Dictionary<string, float[]> Joints(string bundle, Where? visEquipment, long rootTr)
    {
        var joints = new Dictionary<string, float[]>();
        if (visEquipment is not { } at || Read(at.Bundle, at.PathId) is not { } vis)
        {
            return joints;
        }
        foreach (var (name, field) in JointFields)
        {
            if (Has(vis, field) && ToRoot(bundle, Resolve(at.Bundle, vis[field]), rootTr) is { } m)
            {
                joints[name] = m;
            }
        }
        if (Has(vis, "m_bodyModel") && BodyJoint(bundle, Resolve(at.Bundle, vis["m_bodyModel"]), rootTr) is { } body)
        {
            joints["body"] = body;
        }
        return joints;
    }

    /// <summary>
    /// Where skinned armour goes: under the parent of the body's skinned renderer (VisEquipment.m_bodyModel), relative
    /// to the root; null when the renderer or its parent cannot be found.
    /// </summary>
    private float[]? BodyJoint(string bundle, Where? renderer, long rootTr)
    {
        if (renderer == null || Read(renderer.Bundle, renderer.PathId) is not { } r || Read(renderer.Bundle, r["m_GameObject.m_PathID"].AsLong) is not { } go)
        {
            return null;
        }
        var tr = Components(renderer.Bundle, go).FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F;
        return tr == null ? null : ToRoot(bundle, new Where(renderer.Bundle, tr["m_Father.m_PathID"].AsLong), rootTr);
    }

    /// <summary>
    /// A Transform's matrix relative to the prefab's root: its local transform and its parents' up to the root (the
    /// root's own left out). Null when there is no such transform or it is not under the root.
    /// </summary>
    /// <param name="bundle">The prefab's bundle.</param>
    /// <param name="at">The transform.</param>
    /// <param name="rootTr">The root's Transform.</param>
    private float[]? ToRoot(string bundle, Where? at, long rootTr)
    {
        if (at == null)
        {
            return null;
        }
        float[] m = Matrix.Identity();
        long pid = at.PathId;
        for (int depth = 0; pid != 0 && pid != rootTr; depth++)
        {
            if (depth > 256 || Read(bundle, pid) is not { } tr)
            {
                return null;
            }
            m = Matrix.Mul(Matrix.Trs(tr["m_LocalPosition"], tr["m_LocalRotation"], tr["m_LocalScale"]), m);
            pid = tr["m_Father.m_PathID"].AsLong;
        }
        return pid == rootTr ? m : null;
    }
}
