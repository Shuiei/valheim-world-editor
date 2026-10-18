using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>The preview of a paste (PasteTool.Preview.cs): the objects and outlines of every copy, and the ghosts drawn.</summary>
public sealed partial class PasteTool
{
    /// <summary>Where the objects of every copy would land (grid x, height, grid z), and each copy's outline.</summary>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    public (List<Vector3> Objects, List<List<Vector2>> Outlines) Preview(Vector2 at, Func<Vector2, float> heightAt)
    {
        var objs = new List<Vector3>();
        var outlines = new List<List<Vector2>>();
        if (Clip is not { } c)
        {
            return (objs, outlines);
        }
        foreach (var (ca, spots) in CopySpots(c, at, heightAt))
        {
            foreach (var (_, p, y) in spots)
            {
                objs.Add(new Vector3(p.X, y, p.Y));
            }
            outlines.Add([.. c.Poly.Select(p => ca + Xf(p))]);
        }
        return (objs, outlines);
    }

    /// <summary>
    /// The objects a paste at grid point at would place (as Apply places them, the ground as it is now):
    /// prefab, world position, rotation, scale; for drawing them as ghosts.
    /// </summary>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    /// <param name="ox">World x of grid x 0.</param>
    /// <param name="oz">World z of grid z 0.</param>
    public List<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> Ghosts(Vector2 at, Func<Vector2, float> heightAt, float ox, float oz)
    {
        var list = new List<(int, Vector3, Vector3, float)>();
        if (Clip is not { } c || !Objects)
        {
            return list;
        }
        foreach (var (_, spots) in CopySpots(c, at, heightAt))
        {
            foreach (var (o, p, y) in spots)
            {
                list.Add((o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), Rotated(o.Rotation), o.Scale));
            }
        }
        return list;
    }

    /// <summary>
    /// Each copy's anchor point on the grid, and where each of its objects stands (see <see cref="ObjectSpot"/>), in
    /// the order the copies and their objects are placed.
    /// </summary>
    /// <param name="c">The clip.</param>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    internal IEnumerable<(Vector2 Ca, IEnumerable<(CopyData.Obj O, Vector2 P, float Y)> Spots)> CopySpots(CopyData c, Vector2 at, Func<Vector2, float> heightAt) =>
        CopyAnchors(at, heightAt).Select(a => (a.Ca, Spots: c.Objects.Select(o =>
        {
            var (p, y) = ObjectSpot(o, a.Ca, a.Up, a.Anchor, heightAt);
            return (O: o, P: p, Y: y);
        })));
}
