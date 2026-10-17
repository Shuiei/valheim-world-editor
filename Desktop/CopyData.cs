using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// What Copy keeps, like the web editor's clipboard (editor/area.js): the ground's shape relative to its
/// average height (NaN: not copied), each point's weight (the soft edge), its paint (-1: not painted),
/// the objects relative to the middle, and the outline. W × H grid points around the middle. The ground
/// is only the buildings' (see Footprint): the rest of the land stays where it was copied from.
/// </summary>
public sealed partial class CopyData
{
    /// <summary>The copy's width in grid points (one per metre).</summary>
    public required int W { get; init; }
    /// <summary>The copy's depth in grid points.</summary>
    public required int H { get; init; }
    /// <summary>Each point's height relative to the reference height, row by row; NaN: not copied.</summary>
    public required float[] Rel { get; init; }
    /// <summary>Each point's weight, 0 to 1 (the soft edge).</summary>
    public required float[] Wt { get; init; }
    /// <summary>Each point's paint, four values a point; -1: not painted.</summary>
    public required float[] Pnt { get; init; }
    /// <summary>The objects copied.</summary>
    public required List<Obj> Objects { get; init; }
    /// <summary>The outline, relative to the middle (metres east, north).</summary>
    public required List<Vector2> Poly { get; init; }
    /// <summary>The blueprint's name, when it came from or went to one.</summary>
    public string? Name { get; set; }
    /// <summary>
    /// A dungeon blueprint (tagged so): pasted into a world, its pieces go 5000 m up behind an entrance on the ground
    /// where the click is, as a generated dungeon does (MainWindow.PasteDungeon), not onto the ground.
    /// </summary>
    public bool Dungeon { get; set; }
    /// <summary>
    /// The scene indices of the objects the copy was made from (the same order as <see cref="Objects"/>), for a cut
    /// to remove right after. Only meant in the scene it came from; not saved with the copy.
    /// </summary>
    public IReadOnlyList<int> Taken { get; init; } = [];

    /// <summary>An object of a copy.</summary>
    /// <param name="Prefab">Its prefab hash.</param>
    /// <param name="Name">Its prefab name.</param>
    /// <param name="Dx">Offset east from the middle, in metres.</param>
    /// <param name="Dz">Offset north from the middle, in metres.</param>
    /// <param name="Dy">Offset from the reference height (or, Follow, from the ground under it).</param>
    /// <param name="Rotation">Its turn, Unity Euler degrees.</param>
    /// <param name="Scale">Its scale.</param>
    /// <param name="SourceId">What it was copied from (its data goes with it), null when nothing.</param>
    /// <param name="Follow">Dy is from the ground under it: objects copied with the Select tool.</param>
    /// <param name="Data">
    /// What it holds, set on it as it is made (see ObjectField): all of the original's data when copied in a world (a
    /// chest's contents, a sign's text, its builder...), what a blueprint keeps.
    /// </param>
    public sealed record Obj(int Prefab, string Name, float Dx, float Dz, float Dy, Vector3 Rotation, float Scale, int? SourceId, bool Follow = false,
        IReadOnlyList<ObjectField>? Data = null);

    /// <summary>
    /// The copy without its builders, for a blueprint (as the Workshop's): pasted, its pieces are the chosen
    /// builder's. Pieces nobody built (a ruin's) stay so.
    /// </summary>
    public CopyData WithoutBuilders() => new()
    {
        W = W,
        H = H,
        Rel = Rel,
        Wt = Wt,
        Pnt = Pnt,
        Objects = [.. Objects.Select(o => o.Data == null ? o : o with { Data = [.. o.Data.Where(f => f.Key != ObjectField.CreatorKey || f == ObjectField.NoBuilder)] })],
        Poly = Poly,
        Name = Name,
        Dungeon = Dungeon,
    };

    /// <summary>
    /// The ground a building stands on is copied with it: under each piece and this far (metres) around it, as it is,
    /// so its foundations, cellar and floors stand on the ground they stood on...
    /// </summary>
    public const float Margin = 1f;

    /// <summary>...then blending into the ground where it is pasted over this many metres.</summary>
    public const float Blend = 3f;

    /// <summary>
    /// The place a building piece takes, from its colliders as the game has them: its rectangle in world x, z (turned
    /// and scaled) and its bottom; null for what is not a building piece or has no colliders.
    /// </summary>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="position">Where it is.</param>
    /// <param name="rotation">Its turn, Unity Euler degrees.</param>
    /// <param name="scale">Its scale (0: its own).</param>
    public static (float X0, float Z0, float X1, float Z1, float Bottom)? Extent(int prefab, Vector3 position, Vector3 rotation, float scale)
    {
        string? name = PrefabCatalog.NameOf(prefab);
        if (name == null || !Workshop.Buildable(name))
        {
            return null;
        }
        var pts = Hammer.Outline(name, BlueprintFormats.FromEuler(rotation), scale > 0 ? scale : 1).ToList();
        return pts.Count == 0 ? null
            : (position.X + pts.Min(v => v.X), position.Z + pts.Min(v => v.Z), position.X + pts.Max(v => v.X), position.Z + pts.Max(v => v.Z), position.Y + pts.Min(v => v.Y));
    }

    /// <summary>
    /// What a copy of a thing starts from (EditSession.CopySourceOf); with no session, its own id when saved and no data.
    /// </summary>
    private static (int? SourceId, IReadOnlyList<ObjectField>? Data) CopySourceOf(WorldScene s, WorldScene.Thing t) =>
        s.Session?.CopySourceOf(t) ?? (t.Id >= 0 ? t.Id : null, null);
}
