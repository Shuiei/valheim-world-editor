namespace WorldEditor.Tests.Unity;

/// <summary>
/// One GameObject of a made-up prefab and its Transform: its components and children are added as the prefab is built;
/// the GameObject and Transform themselves are written last (their path ids are kept from the start, so components and
/// children can point at them).
/// </summary>
public sealed partial class PrefabNode
{
    /// <summary>
    /// The bundle written to.
    /// </summary>
    private readonly TestBundle _b;
    /// <summary>
    /// The parent's Transform (0: a prefab's root).
    /// </summary>
    private readonly long _father;
    /// <summary>
    /// The components after the Transform, in order.
    /// </summary>
    private readonly List<long> _components = [];
    /// <summary>
    /// The children's Transforms, in order.
    /// </summary>
    private readonly List<long> _children = [];

    /// <summary>
    /// A node of this name under the Transform <paramref name="father"/> (0: none).
    /// </summary>
    internal PrefabNode(TestBundle b, string name, long father)
    {
        _b = b;
        _father = father;
        Name = name;
        Active = true;
        Rotation = (0, 0, 0, 1);
        Scale = (1, 1, 1);
        Go = b.Reserve();
        Tr = b.Reserve();
    }

    /// <summary>
    /// The GameObject's path id.
    /// </summary>
    public long Go { get; }

    /// <summary>
    /// The Transform's path id.
    /// </summary>
    public long Tr { get; }

    /// <summary>
    /// The GameObject's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Its layer.
    /// </summary>
    public uint Layer { get; set; }

    /// <summary>
    /// Its tag (20000: the game's "snappoint").
    /// </summary>
    public int Tag { get; set; }

    /// <summary>
    /// Whether it is active.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Its local position.
    /// </summary>
    public (double X, double Y, double Z) Position { get; set; }

    /// <summary>
    /// Its local rotation (x, y, z, w).
    /// </summary>
    public (double X, double Y, double Z, double W) Rotation { get; set; }

    /// <summary>
    /// Its local scale.
    /// </summary>
    public (double X, double Y, double Z) Scale { get; set; }

    /// <summary>
    /// Adds a script's MonoBehaviour (see TestBundle.Mono).
    /// </summary>
    public long Mono(string className, Fields? values = null, bool enabled = true, UnityField[]? fields = null)
    {
        long pid = _b.Mono(Go, className, values, enabled, fields: fields);
        _components.Add(pid);
        return pid;
    }

    /// <summary>
    /// Adds a component of a Unity class with these values (its m_GameObject set here).
    /// </summary>
    public long Component(int classId, UnityField tree, Fields? values = null)
    {
        var v = values ?? [];
        v["m_GameObject"] = Fields.Ref(Go);
        long pid = _b.Assets.Add(classId, tree, v);
        _components.Add(pid);
        return pid;
    }

    /// <summary>
    /// A component listed on the GameObject that is not in the file (a missing reference).
    /// </summary>
    public void Missing(long pathId) => _components.Add(pathId);

    /// <summary>
    /// Adds a box collider.
    /// </summary>
    public long Box((double X, double Y, double Z) center, (double X, double Y, double Z) size, bool enabled = true, bool trigger = false) =>
        Component(UnityClass.BoxCollider, UnityTypes.BoxCollider, new Fields { ["m_Enabled"] = enabled, ["m_IsTrigger"] = trigger, ["m_Center"] = Fields.Vector3(center.X, center.Y, center.Z), ["m_Size"] = Fields.Vector3(size.X, size.Y, size.Z) });

    /// <summary>
    /// Adds a rigidbody (kinematic, as a door's leaf is).
    /// </summary>
    public long Rigidbody() =>
        Component(UnityClass.Rigidbody, UnityTypes.Rigidbody, new Fields { ["m_Mass"] = 1.0, ["m_IsKinematic"] = true });

    /// <summary>
    /// Adds a sphere collider.
    /// </summary>
    public long Sphere((double X, double Y, double Z) center, double radius) =>
        Component(UnityClass.SphereCollider, UnityTypes.SphereCollider, new Fields { ["m_Enabled"] = true, ["m_Center"] = Fields.Vector3(center.X, center.Y, center.Z), ["m_Radius"] = radius });

    /// <summary>
    /// Adds a capsule collider along an axis (0 x, 1 y, 2 z).
    /// </summary>
    public long Capsule((double X, double Y, double Z) center, double radius, double height, int direction) =>
        Component(UnityClass.CapsuleCollider, UnityTypes.CapsuleCollider, new Fields { ["m_Enabled"] = true, ["m_Center"] = Fields.Vector3(center.X, center.Y, center.Z), ["m_Radius"] = radius, ["m_Height"] = height, ["m_Direction"] = direction });

    /// <summary>
    /// Adds a mesh collider on a mesh of the same file.
    /// </summary>
    public long MeshCollider(long mesh, bool convex = false) =>
        Component(UnityClass.MeshCollider, UnityTypes.MeshCollider, new Fields { ["m_Enabled"] = true, ["m_Convex"] = convex, ["m_Mesh"] = Fields.Ref(mesh) });

    /// <summary>
    /// Adds a mesh filter and a renderer drawing <paramref name="mesh"/> with these materials (references); returns
    /// the renderer.
    /// </summary>
    public long Renderer(Fields mesh, IEnumerable<Fields> materials, bool enabled = true)
    {
        Component(UnityClass.MeshFilter, UnityTypes.MeshFilter, new Fields { ["m_Mesh"] = mesh });
        return Component(UnityClass.MeshRenderer, UnityTypes.MeshRenderer, new Fields { ["m_Enabled"] = enabled, ["m_Materials"] = materials.ToList() });
    }
}
