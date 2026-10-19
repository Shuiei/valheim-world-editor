using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The items the area's stands hold, as they are now (edits included), for the 3D view to draw: each item stand's
/// item at its attach point and each armour stand's items at their joints, as the game shows them. Read again after
/// each edit (the stand's data holds what it has), like dungeon rooms.
/// </summary>
public static partial class StandItems
{
    /// <summary>An item drawn on a stand.</summary>
    /// <param name="Model">The held model's key (StandLook.KeyOf), the kind it is drawn as.</param>
    /// <param name="Position">Where the stand is, in world metres.</param>
    /// <param name="Rotation">The stand's turn, Euler degrees.</param>
    /// <param name="Scale">The stand's own size (0: the prefab's).</param>
    /// <param name="Pre">Where the item goes on the stand, in the view's space (applied before the stand's placement).</param>
    /// <param name="Kind">The stand's kind (the item is shown with it).</param>
    public readonly record struct Held(int Model, Vector3 Position, Vector3 Rotation, float Scale, Matrix4x4 Pre, ObjectKind Kind);

    /// <summary>The held items of each scene, read again after each change (see SceneReads).</summary>
    private static readonly SceneReads<IReadOnlyList<Held>> Reads = new(Read, [], "reading what the stands hold (what was shown is kept)");

    /// <summary>The scene's held items as they are now, read when the cache is out of date (on the caller's thread).</summary>
    public static IReadOnlyList<Held> Of(WorldScene s) => Reads.Of(s);

    /// <summary>
    /// For drawing (each frame): what was read last, without waiting; after an edit the stands are read again on a
    /// worker, and ready is called when they are (or the read failed: logged).
    /// </summary>
    public static IReadOnlyList<Held> Shown(WorldScene s, Action ready) => Reads.Shown(s, ready);

    /// <summary>The scene's held items read anew from the game's bundles (none without the game).</summary>
    private static List<Held> Read(WorldScene s) =>
        GameLook.Bundles is { } game ? Find(s, game.LoadStandPoints, game.LoadHeld) : [];
}
