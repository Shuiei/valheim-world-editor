using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

// What the editor reads of the build pieces (every prefab whose root has a Piece component) at its
// start (GameData): the pieces, their support, their placement and their costs.
public static partial class GameCatalogues
{
    /// <summary>
    /// Unity's class id of a BoxCollider.
    /// </summary>
    private const int BoxColliderType = (int)AssetClassID.BoxCollider;
    /// <summary>
    /// Unity's class id of a MeshCollider.
    /// </summary>
    private const int MeshColliderType = (int)AssetClassID.MeshCollider;
    /// <summary>
    /// Unity's class id of a SphereCollider.
    /// </summary>
    private const int SphereColliderType = (int)AssetClassID.SphereCollider;
    /// <summary>
    /// Unity's class id of a CapsuleCollider.
    /// </summary>
    private const int CapsuleColliderType = (int)AssetClassID.CapsuleCollider;
    /// <summary>
    /// Unity's class id of a Mesh.
    /// </summary>
    private const int MeshType = (int)AssetClassID.Mesh;
    /// <summary>
    /// Unity's class id of a Rigidbody.
    /// </summary>
    private const int RigidbodyType = (int)AssetClassID.Rigidbody;

    /// <summary>
    /// Whether a Unity class id is one of the colliders read here (box, mesh, sphere, capsule).
    /// </summary>
    private static bool IsCollider(int type) => type is BoxColliderType or MeshColliderType or SphereColliderType or CapsuleColliderType;

    /// <summary>
    /// A collider that counts: enabled, not a trigger.
    /// </summary>
    private static bool Solid(AssetTypeValueField c) => Num(Get(c, "m_Enabled", 1L)) == 1 && !Truthy(Get(c, "m_IsTrigger", 0L));

    /// <summary>
    /// A game object's Transform (null when it has none) and its solid colliders with their class ids, from its
    /// components. The renderers, scripts and the rest are not read.
    /// </summary>
    /// <param name="b">The bundle the object is in.</param>
    /// <param name="g">The game object.</param>
    private static (AssetTypeValueField? Transform, List<(int Type, AssetTypeValueField F)> Colliders) TransformAndColliders(Bundle b, AssetTypeValueField g)
    {
        AssetTypeValueField? tr = null;
        var colliders = new List<(int Type, AssetTypeValueField F)>();
        foreach (var (t, pid) in b.ComponentIds(g))
        {
            if (t == TransformType && b.Field(pid) is { } f)
            {
                tr = f;
            }
            else if (IsCollider(t) && b.Field(pid) is { } c && c.Children.Count > 0 && Solid(c))
            {
                colliders.Add((t, c));
            }
        }
        return (tr, colliders);
    }

    /// <summary>
    /// A snap point: tagged "snappoint" (<c>SnapTag</c>), or with "snappoint" in its name.
    /// </summary>
    private static bool IsSnapPoint(AssetTypeValueField go) => Get(go, "m_Tag", null) is long tag && tag == SnapTag || go["m_Name"].AsString.ToLowerInvariant().Contains("snappoint", StringComparison.Ordinal);

    /// <summary>
    /// Whether a GameObject is active (m_IsActive, true when missing).
    /// </summary>
    private static bool Active(AssetTypeValueField go) => Num(Get(go, "m_IsActive", 1L)) == 1;

    /// <summary>
    /// Each prefab with a Piece on its root, in the bundle's order: its file, path id, fields and Piece,
    /// for the readers at the editor's start. Found from the Piece scripts (told by their class) up to
    /// their GameObjects, kept to the prefab roots (the locations have many pieces below theirs); only
    /// these are read whole.
    /// </summary>
    private static List<(Bundle.Inner File, long Pid, AssetTypeValueField Go, AssetTypeValueField Piece)> PieceRoots(Bundle b, Game game) =>
        PieceRootsOf.GetValue(b, _ => FindPieceRoots(b, game));
}
