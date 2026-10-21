using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>Objects of a paste (PasteTool.Objects.cs): added where each copy has them, and their linked groups made their own.</summary>
public sealed partial class PasteTool
{
    /// <summary>
    /// Adds the objects of one copy as new objects (with the ground's origin), each placed where the copy has it: those
    /// copied with the Select tool keep their height above the ground where they land.
    /// </summary>
    /// <param name="g">The ground the objects are placed on.</param>
    /// <param name="c">The copy.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="lift">The copy's lift (see CopyAt).</param>
    /// <param name="anchor">The height the copy's anchored objects stand at.</param>
    /// <param name="add">The objects to add, with whether each is a player-built piece; added to.</param>
    internal void AddObjects(Ground g, CopyData c, Vector2 ca, float lift, float anchor, List<(NewObject, bool)> add)
    {
        float ox = g.OriginX, oz = g.OriginZ;
        foreach (var o in c.Objects)
        {
            var p = ca + Xf(new Vector2(o.Dx, o.Dz));
            // Objects copied with the Select tool keep their height above the ground where they land.
            float y = o.Follow ? g.HeightOf(Index(g, p)) + o.Dy + lift : anchor + o.Dy;
            add.Add((new NewObject(0, o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), Rotated(o.Rotation), o.Scale, o.SourceId, true, Data: o.Data), PieceCatalog.Get(o.Prefab)?.Tool != null));
        }
    }

    /// <summary>
    /// Each copy's linked groups made its own (new ids, parts stored as their mod saves them; see ObjectLinks.Remap):
    /// two groups under one id would share their parts in the game.
    /// </summary>
    /// <param name="add">The objects of every copy.</param>
    /// <param name="copyStarts">Where each copy's objects start in the list.</param>
    /// <param name="taken">The PersistentIDs in use; the new ones are added.</param>
    private List<(NewObject, bool)> OwnGroups(List<(NewObject O, bool Piece)> add, List<int> copyStarts, ISet<int> taken)
    {
        var made = new List<(NewObject, bool)>();
        for (int k = 0; k < copyStarts.Count; k++)
        {
            int end = k + 1 < copyStarts.Count ? copyStarts[k + 1] : add.Count;
            var copy = add.GetRange(copyStarts[k], end - copyStarts[k]);
            var own = ObjectLinks.Remap([.. copy.Select(c => c.O)], taken, Mirror);
            for (int j = 0; j < copy.Count; j++)
            {
                made.Add((own[j], copy[j].Piece));
            }
        }
        return made;
    }
}
