using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Which kinds of object the Select and View tools pick (the Select panel's Pick, as Unity's pick toggles): what is
// left out is still drawn, but neither picked nor in the way of what is behind it.
public sealed partial class GlView
{
    /// <summary>What the Select and View tools pick.</summary>
    internal enum PickKinds
    {
        /// <summary>Every shown object.</summary>
        Everything,
        /// <summary>The pieces players built (ObjectKind.Buildings).</summary>
        Built,
        /// <summary>Trees, rocks, ore, bushes, pickables and remains.</summary>
        Nature,
    }

    /// <summary>The kinds that are nature (<see cref="PickKinds.Nature"/>).</summary>
    private static readonly HashSet<ObjectKind> NatureKinds =
        [ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Ore, ObjectKind.Bushes, ObjectKind.Pickables, ObjectKind.Remains];

    /// <summary>What the Select and View tools pick now.</summary>
    internal PickKinds Picks { get; set; }

    /// <summary>
    /// Whether the pick filter lets an object of this kind be picked: always outside the Select and View tools (the
    /// Place tool's "on top", the eyedropper).
    /// </summary>
    /// <param name="kind">The object's kind.</param>
    private bool PickFilterLets(ObjectKind kind) => !(SelectMode || Mode == ToolMode.View) || Picks switch
    {
        PickKinds.Built => kind == ObjectKind.Buildings,
        PickKinds.Nature => NatureKinds.Contains(kind),
        _ => true,
    };
}
