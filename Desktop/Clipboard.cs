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
public sealed class CopyData
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
    /// How much of the ground at each point of a w × h grid (world x0, z0 at its first point) goes with the building
    /// pieces among the things: 1 under a piece and Margin around it, fading to 0 over Blend. Pieces hidden far below
    /// the ground (PasteTool.HiddenDepth) take none.
    /// </summary>
    private static float[] Footprint(IEnumerable<WorldScene.Thing> things, int w, int h, float x0, float z0, Func<float, float, float> groundAt)
    {
        float[] foot = new float[w * h];
        foreach (var t in things)
        {
            if (Extent(t.Prefab, t.Position, t.Rotation, t.Scale) is not var (ax, az, bx, bz, bottom)
                || bottom < groundAt(t.Position.X, t.Position.Z) - PasteTool.HiddenDepth)
            {
                continue;
            }
            (ax, az, bx, bz) = (ax - Margin - x0, az - Margin - z0, bx + Margin - x0, bz + Margin - z0);
            for (int gz = Math.Max(0, (int)MathF.Floor(az - Blend)); gz <= Math.Min(h - 1, (int)MathF.Ceiling(bz + Blend)); gz++)
            {
                for (int gx = Math.Max(0, (int)MathF.Floor(ax - Blend)); gx <= Math.Min(w - 1, (int)MathF.Ceiling(bx + Blend)); gx++)
                {
                    float dx = MathF.Max(0, MathF.Max(ax - gx, gx - bx)), dz = MathF.Max(0, MathF.Max(az - gz, gz - bz));
                    float k = 1 - MathF.Sqrt(dx * dx + dz * dz) / Blend;
                    if (k > foot[gz * w + gx])
                    {
                        foot[gz * w + gx] = k;
                    }
                }
            }
        }
        return foot;
    }

    /// <summary>
    /// The shown objects inside an Area selection, with the ground their buildings stand on (Footprint); null without
    /// a selection.
    /// </summary>
    /// <param name="area">The selection.</param>
    /// <param name="g">The ground.</param>
    /// <param name="s">The scene the objects are in.</param>
    /// <param name="objects">The objects to copy, as indices into the scene's things (kinds the world cannot make are left out).</param>
    /// <param name="nameOf">A prefab hash's name, null when unknown.</param>
    public static CopyData? FromArea(AreaTool area, Ground g, WorldScene s, IReadOnlyList<int> objects, Func<int, string?> nameOf)
    {
        if (area.WeightsIn(g.W, g.H) is not { Cells.Count: > 0 } a)
        {
            return null;
        }
        // A linked group's parts go with it (ObjectLinks: a ship); it takes no ground (it does not stand on it).
        var (chosen, parts, _, _) = s.Session?.WithLinked(objects) ?? ([.. objects], [], 0, LinkRule.DefaultName);
        objects = chosen;
        float reference = a.Cells.Average(c => g.HeightOf(c.G));
        int w = a.X1 - a.X0 + 1, h = a.Z1 - a.Z0 + 1;
        float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
        float[] foot = Footprint(objects.Where(i => !parts.Contains(i)).Select(i => s.Things[i]), w, h, ox + a.X0, oz + a.Z0,
            (x, z) => g.HeightOf(Math.Clamp((int)MathF.Round(z - oz), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(x - ox), 0, g.W - 1)));
        float[] rel = [.. Enumerable.Repeat(float.NaN, w * h)];
        float[] wt = new float[w * h];
        float[] pnt = [.. Enumerable.Repeat(-1f, w * h * 4)];
        foreach (var (p, k) in a.Cells)
        {
            int gx = p % g.W, gz = p / g.W, i = (gz - a.Z0) * w + gx - a.X0;
            if (foot[i] <= 0)
            {
                continue;
            }
            rel[i] = g.HeightOf(p) - reference;
            wt[i] = k * foot[i];
            if (g.PMod[p] != 0)
            {
                Array.Copy(g.Paint, p * 4, pnt, i * 4, 4);
            }
        }
        float cx = (a.X0 + a.X1) / 2f, cz = (a.Z0 + a.Z1) / 2f;
        var taken = objects.Where(i => s.World == null || s.World.CanCreate(s.Things[i].Prefab)).ToList();
        var objs = taken.Select(i => s.Things[i]).Select(t => new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(),
            t.Position.X - ox - cx, t.Position.Z - oz - cz, t.Position.Y - reference, t.Rotation, t.Scale, SourceOf(s, t), Data: DataOf(s, t))).ToList();
        return new CopyData { W = w, H = h, Rel = rel, Wt = wt, Pnt = pnt, Objects = objs, Poly = [.. a.Poly.Select(p => p - new Vector2(cx, cz))], Taken = taken };
    }

    /// <summary>
    /// The Select tool's copy: trees and the like each keep their height above the ground where they land; building
    /// pieces keep the building's shape instead, with the ground they stand on (Footprint, without paint). Null when
    /// none can be copied.
    /// </summary>
    /// <param name="s">The scene the objects are in.</param>
    /// <param name="things">The objects, as indices into the scene's things.</param>
    /// <param name="nameOf">A prefab hash's name, null when unknown.</param>
    /// <param name="groundAt">The ground's height at a world point (x, z).</param>
    public static CopyData? FromThings(WorldScene s, IReadOnlyList<int> things, Func<int, string?> nameOf, Func<float, float, float> groundAt)
    {
        // A linked group's parts go with it (ObjectLinks: a ship), as one block; it takes no ground (it does not stand on it).
        var (chosen, parts, _, _) = s.Session?.WithLinked(things) ?? ([.. things], [], 0, LinkRule.DefaultName);
        var shown = chosen.Where(i => !s.Things[i].Gone && (s.World == null || s.World.CanCreate(s.Things[i].Prefab))).ToList();
        var list = shown.Select(i => s.Things[i]).ToList();
        if (list.Count == 0)
        {
            return null;
        }
        var grounded = shown.Where(i => !parts.Contains(i)).Select(i => s.Things[i]).ToList();
        // The ground goes on whole metres: the grid around the pieces, its middle on a metre or half (none without pieces).
        var ext = grounded.Select(t => Extent(t.Prefab, t.Position, t.Rotation, t.Scale)).OfType<(float X0, float Z0, float X1, float Z1, float Bottom)>().ToList();
        float reach = Margin + Blend;
        int bx0 = ext.Count == 0 ? 0 : (int)MathF.Floor(ext.Min(e => e.X0) - reach), bx1 = ext.Count == 0 ? 0 : (int)MathF.Ceiling(ext.Max(e => e.X1) + reach);
        int bz0 = ext.Count == 0 ? 0 : (int)MathF.Floor(ext.Min(e => e.Z0) - reach), bz1 = ext.Count == 0 ? 0 : (int)MathF.Ceiling(ext.Max(e => e.Z1) + reach);
        float cx = ext.Count == 0 ? list.Average(t => t.Position.X) : (bx0 + bx1) / 2f, cz = ext.Count == 0 ? list.Average(t => t.Position.Z) : (bz0 + bz1) / 2f;
        float x0 = list.Min(t => t.Position.X) - cx - 1, x1 = list.Max(t => t.Position.X) - cx + 1, z0 = list.Min(t => t.Position.Z) - cz - 1, z1 = list.Max(t => t.Position.Z) - cz + 1;
        if (ext.Count > 0)
        {
            (x0, x1, z0, z1) = (MathF.Min(x0, bx0 - cx), MathF.Max(x1, bx1 - cx), MathF.Min(z0, bz0 - cz), MathF.Max(z1, bz1 - cz));
        }
        // Trees, rocks and the like keep their height above the ground where each lands; building pieces
        // keep the building's shape: their height from the ground at the selection's middle (pieces
        // following the ground one by one would come apart on a slope).
        float mid = groundAt(cx, cz);
        var objs = shown.Select(i =>
        {
            var t = s.Things[i];
            bool piece = t.Piece || PieceCatalog.Get(t.Prefab) != null || parts.Contains(i);
            return new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(), t.Position.X - cx, t.Position.Z - cz,
                t.Position.Y - (piece ? mid : groundAt(t.Position.X, t.Position.Z)), t.Rotation, t.Scale, SourceOf(s, t), Follow: !piece, Data: DataOf(s, t));
        }).ToList();
        int w = bx1 - bx0 + 1, h = bz1 - bz0 + 1;
        float[] foot = ext.Count == 0 ? [0f] : Footprint(grounded, w, h, bx0, bz0, groundAt);
        return new CopyData
        {
            W = w,
            H = h,
            Rel = [.. foot.Select((k, i) => k > 0 ? groundAt(bx0 + i % w, bz0 + i / w) - mid : float.NaN)],
            Wt = foot,
            Pnt = [.. Enumerable.Repeat(-1f, w * h * 4)],
            Objects = objs,
            Poly = [new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)],
            Taken = shown,
        };
    }

    /// <summary>
    /// The saved object a thing was copied from: its own id, or for one added in this session the id it was itself
    /// copied from (null: none).
    /// </summary>
    private static int? SourceOf(WorldScene s, WorldScene.Thing t) => t.Id >= 0 ? t.Id : s.Session?.Edits.FindAdded(t.Id)?.SourceId;

    /// <summary>
    /// Everything a thing holds, builder included, for its copies (null when it cannot be read): taken when copying,
    /// so a copy stays whole after the world is saved (the ids change) and in another world.
    /// </summary>
    private static List<ObjectField>? DataOf(WorldScene s, WorldScene.Thing t) =>
        s.World != null && s.Session is { } e && ObjectData.Bytes(s.World, e.Edits, t.Id) is { } bytes ? ObjectData.Fields(bytes, builder: true) : null;
}

/// <summary>
/// Pasting a clip (the Paste tool): where it goes, turned (degrees, counter-clockwise seen from above)
/// and mirrored, with a height offset and repeats (like WorldEdit's //stack).
/// </summary>
public sealed class PasteTool
{
    /// <summary>What is pasted, null before the first copy.</summary>
    public CopyData? Clip { get; set; }
    /// <summary>The turn, in degrees, 0 to 360 (counter-clockwise seen from above).</summary>
    public float Turn { get; set; }
    /// <summary>Mirror east to west (before turning).</summary>
    public bool Mirror { get; set; }
    /// <summary>Paste the ground (shape and paint).</summary>
    public bool Ground { get; set; } = true;
    /// <summary>Paste the objects.</summary>
    public bool Objects { get; set; } = true;
    /// <summary>
    /// Clear the site: where the pasted building stands, the ground in its way is dug down to its lowest
    /// piece (a metre around it too), and the trees, rocks and the like there go (Site, for the caller).
    /// </summary>
    public bool ClearSite { get; set; } = true;
    /// <summary>The grid points the last Apply cleared (its buildings' outlines and the metre around them).</summary>
    public HashSet<(int X, int Z)> Site { get; } = [];
    /// <summary>The height added to the paste, in metres.</summary>
    public float Offset { get; set; }
    /// <summary>How many copies (see <see cref="Count"/>).</summary>
    public int Copies { get; set; } = 1;
    /// <summary>Which way the copies repeat: east along the copy's width, north along its depth, or stacked up.</summary>
    public enum Along
    {
        /// <summary>East, along the copy's width.</summary>
        Width,
        /// <summary>North, along the copy's depth.</summary>
        Depth,
        /// <summary>Stacked up.</summary>
        Up,
    }
    /// <summary>Which way the copies repeat (turned and mirrored with the paste).</summary>
    public Along Direction { get; set; } = Along.Width;
    /// <summary>The gap between copies, in metres.</summary>
    public float Gap { get; set; }
    /// <summary>Where the mouse is (grid point), for the preview.</summary>
    public Vector2? At { get; set; }
    /// <summary>A setting or the clip changed.</summary>
    public event Action? Changed;
    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>Turns the paste by some degrees (kept 0 to 360).</summary>
    public void TurnBy(float degrees)
    {
        Turn = ((Turn + degrees) % 360 + 360) % 360;
        Notify();
    }

    /// <summary>Source offset (from the clip's middle) → offset where it lands: mirrored, then turned.</summary>
    public Vector2 Xf(Vector2 d)
    {
        if (Mirror)
        {
            d.X = -d.X;
        }
        float t = Turn * MathF.PI / 180, c = MathF.Cos(t), s = MathF.Sin(t);
        return new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
    }

    /// <summary>Offset where it lands → source offset: the inverse of <see cref="Xf"/>.</summary>
    public Vector2 Inv(Vector2 d)
    {
        float t = -Turn * MathF.PI / 180, c = MathF.Cos(t), s = MathF.Sin(t);
        d = new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        if (Mirror)
        {
            d.X = -d.X;
        }
        return d;
    }

    /// <summary>The step between two copies: the size of the copy plus the gap along the chosen direction.</summary>
    private (Vector2 D, float Up) Step()
    {
        var c = Clip!;
        if (Direction == Along.Up)
        {
            float y0 = c.Objects.Count > 0 ? c.Objects.Min(o => o.Dy) : 0, y1 = c.Objects.Count > 0 ? c.Objects.Max(o => o.Dy) : 0;
            return (Vector2.Zero, (c.Objects.Count > 0 ? MathF.Max(1, y1 - y0 + 1) : 2) + Gap);
        }
        float x0 = c.Poly.Min(p => p.X), x1 = c.Poly.Max(p => p.X), z0 = c.Poly.Min(p => p.Y), z1 = c.Poly.Max(p => p.Y);
        return Direction == Along.Width ? (new Vector2(x1 - x0 + Gap, 0), 0) : (new Vector2(0, z1 - z0 + Gap), 0);
    }

    /// <summary>
    /// Copy k's anchor (grid point) and the height added to it. The anchor is moved (half a metre at most) so that
    /// the copy's ground points land on the ground's: the ground goes in a whole metre at a time, and the objects
    /// must stand on it as they stood on the original.
    /// </summary>
    public (Vector2 At, float Up) CopyAt(Vector2 at, int k)
    {
        var (d, up) = Step();
        var c = Clip!;
        var corner = Xf(new Vector2(-(c.W - 1) / 2f, -(c.H - 1) / 2f));
        var ca = at + Xf(d * k) + corner;
        return (new Vector2(MathF.Round(ca.X), MathF.Round(ca.Y)) - corner, up * k);
    }

    /// <summary>The number of copies, 1 to 50.</summary>
    public int Count => Math.Clamp(Copies, 1, 50);

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
        float baseH = heightAt(at) + Offset;
        for (int k = 0; k < Count; k++)
        {
            var (ca, up) = CopyAt(at, k);
            float anchor = (Direction == Along.Up ? baseH : heightAt(ca) + Offset) + up;
            foreach (var o in c.Objects)
            {
                var p = ca + Xf(new Vector2(o.Dx, o.Dz));
                objs.Add(new Vector3(p.X, o.Follow ? heightAt(p) + o.Dy + up : anchor + o.Dy, p.Y));
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
        float baseH = heightAt(at) + Offset;
        for (int k = 0; k < Count; k++)
        {
            var (ca, up) = CopyAt(at, k);
            float anchor = (Direction == Along.Up ? baseH : heightAt(ca) + Offset) + up;
            foreach (var o in c.Objects)
            {
                var p = ca + Xf(new Vector2(o.Dx, o.Dz));
                float y = o.Follow ? heightAt(p) + o.Dy + up : anchor + o.Dy;
                var r = o.Rotation;
                list.Add((o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), new Vector3(Mirror ? -r.X : r.X, (Mirror ? -r.Y : r.Y) - Turn, Mirror ? -r.Z : r.Z), o.Scale));
            }
        }
        return list;
    }

    /// <summary>
    /// Whether a paste at grid point at, every copy of it, lies inside a ground of w × h points (its edge
    /// points excepted): past the open area nothing could shape the ground or clear the site under it.
    /// </summary>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="w">The ground's points across.</param>
    /// <param name="h">The ground's points down.</param>
    public bool Fits(Vector2 at, int w, int h)
    {
        if (Clip is not { } c)
        {
            return false;
        }
        for (int k = 0; k < Count; k++)
        {
            var (ca, _) = CopyAt(at, k);
            foreach (var p in c.Poly.Concat(c.Objects.Select(o => new Vector2(o.Dx, o.Dz))).Select(d => ca + Xf(d)))
            {
                if (p.X < 1 || p.Y < 1 || p.X > w - 2 || p.Y > h - 2)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Pieces this far (metres) below the ground they land on are out of sight on purpose (mods keep crafting
    /// stations thousands of metres down): Clear the site does not dig down to them.
    /// </summary>
    public const float HiddenDepth = 100f;

    /// <summary>
    /// Pastes at a grid point: the ground (shape and paint, with the copy's soft edge, past the game's
    /// ±8 m so the building stands on the ground it was built on) and the objects, as new objects copied
    /// from the originals with all they held. Returns the ground points changed and their rectangle,
    /// and the objects to add (Piece: a player-built piece).
    /// </summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    /// <param name="linkIds">
    /// The linked groups' ids in use (ObjectLinks): each pasted group gets ids of its own (see ObjectLinks.Remap); null: none
    /// known.
    /// </param>
    public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, List<(NewObject, bool)> Add) Apply(Ground g, Vector2 at, Func<int, float>? mask = null, ISet<int>? linkIds = null)
    {
        var c = Clip!;
        var touched = new List<int>();
        var add = new List<(NewObject, bool)>();
        // The points given the copy's ground in full: it is the ground the building stood on, nothing to dig there.
        var shaped = new HashSet<int>();
        Site.Clear();
        int half = (int)MathF.Ceiling(MathF.Sqrt(c.W * c.W + c.H * c.H) / 2) + 1;
        int bx0 = g.W, bx1 = 0, bz0 = g.H, bz1 = 0;
        float baseH = g.HeightOf(Index(g, at)) + Offset;
        float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
        bool up = Direction == Along.Up;
        bool limit = g.NoLimit;
        g.NoLimit = true;
        // Where each copy's objects start in the list.
        var copyStarts = new List<int>();
        for (int k = 0; k < Count; k++)
        {
            copyStarts.Add(add.Count);
            var (ca, lift) = CopyAt(at, k);
            float anchor = (up ? baseH : g.HeightOf(Index(g, ca)) + Offset) + lift;
            int x0 = Math.Max(1, (int)MathF.Floor(ca.X - half)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(ca.X + half));
            int z0 = Math.Max(1, (int)MathF.Floor(ca.Y - half)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(ca.Y + half));
            // Stacked upwards, only the first copy shapes the ground.
            if (Ground && !(up && k > 0))
            {
                (bx0, bx1, bz0, bz1) = (Math.Min(bx0, x0), Math.Max(bx1, x1), Math.Min(bz0, z0), Math.Max(bz1, z1));
                for (int gz = z0; gz <= z1; gz++)
                {
                    for (int gx = x0; gx <= x1; gx++)
                    {
                        var src = Inv(new Vector2(gx - ca.X, gz - ca.Y));
                        int ix = (int)MathF.Round(src.X + (c.W - 1) / 2f), iz = (int)MathF.Round(src.Y + (c.H - 1) / 2f);
                        if (ix < 0 || iz < 0 || ix >= c.W || iz >= c.H)
                        {
                            continue;
                        }
                        int i = iz * c.W + ix;
                        if (float.IsNaN(c.Rel[i]) || g.Locked(gx, gz))
                        {
                            continue;
                        }
                        int p = gz * g.W + gx;
                        float w = c.Wt[i] * (mask?.Invoke(p) ?? 1);
                        if (w <= 0)
                        {
                            continue;
                        }
                        float h = g.HeightOf(p);
                        g.SetHeight(p, h + (anchor + c.Rel[i] - h) * w);
                        if (w >= 0.999f)
                        {
                            shaped.Add(p);
                        }
                        if (c.Pnt[i * 4] >= 0)
                        {
                            if (g.PMod[p] == 0)
                            {
                                g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
                                g.Paint[p * 4 + 3] = 1;
                                g.PMod[p] = 1;
                            }
                            for (int ch = 0; ch < 4; ch++)
                            {
                                g.Paint[p * 4 + ch] += (c.Pnt[i * 4 + ch] - g.Paint[p * 4 + ch]) * w;
                            }
                        }
                        touched.Add(p);
                    }
                }
            }
            if (Objects)
            {
                foreach (var o in c.Objects)
                {
                    var p = ca + Xf(new Vector2(o.Dx, o.Dz));
                    // Objects copied with the Select tool keep their height above the ground where they land.
                    float y = o.Follow ? g.HeightOf(Index(g, p)) + o.Dy + lift : anchor + o.Dy;
                    var r = o.Rotation;
                    var rot = new Vector3(Mirror ? -r.X : r.X, (Mirror ? -r.Y : r.Y) - Turn, Mirror ? -r.Z : r.Z);
                    add.Add((new NewObject(0, o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), rot, o.Scale, o.SourceId, true, Data: o.Data), PieceCatalog.Get(o.Prefab)?.Tool != null));
                }
            }
        }
        g.NoLimit = limit;
        // Each copy's groups made its own first: a part pasted without its root is an ordinary piece, dug for below.
        var made = OwnGroups(add, copyStarts, linkIds ?? new HashSet<int>());
        if (ClearSite && Objects)
        {
            var (dug, rect) = Clear(g, made, shaped, mask);
            touched.AddRange(dug);
            if (dug.Count > 0)
            {
                (bx0, bx1, bz0, bz1) = (Math.Min(bx0, rect.X0), Math.Max(bx1, rect.X1), Math.Min(bz0, rect.Z0), Math.Max(bz1, rect.Z1));
            }
        }
        return (touched, (Math.Min(bx0, bx1) - 1, Math.Min(bz0, bz1) - 1, bx1 + 1, bz1 + 1), made);
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

    /// <summary>
    /// Each grid point under (or within a metre of) a pasted buildable piece: the ground there dug down
    /// to the lowest such piece's bottom (its colliders, as the game has them), never raised; past the
    /// game's ±8 m if need be (a building dug into a mountain). Not where the copy's own ground went in
    /// full (shaped): the building stood on it, its pieces set into it as they were. Pieces hidden far
    /// below (HiddenDepth) are left out.
    /// </summary>
    private (List<int> Dug, (int X0, int Z0, int X1, int Z1) Rect) Clear(Ground g, List<(NewObject O, bool Piece)> placed, HashSet<int> shaped, Func<int, float>? mask)
    {
        float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
        var to = new Dictionary<(int, int), float>();
        // A linked group (its parts, and what they hang on) does not stand on the ground: none is dug under it.
        var links = placed.Select(p => ObjectLinks.With(null, p.O.Data)).ToList();
        var held = links.Where(l => l is { Holder: not 0 }).Select(l => l!.Holder).ToHashSet();
        for (int k = 0; k < placed.Count; k++)
        {
            var o = placed[k].O;
            if (links[k] is { } l && (l.Holder != 0 || held.Contains(l.Id)))
            {
                continue;
            }
            if (CopyData.Extent(o.Prefab, o.Position, o.Rotation, o.Scale) is not var (ax, az, bx, bz, bottom)
                || bottom < g.HeightOf(Index(g, new Vector2(o.Position.X - ox, o.Position.Z - oz))) - HiddenDepth)
            {
                continue;
            }
            int x0 = (int)MathF.Floor(ax - ox) - 1, x1 = (int)MathF.Ceiling(bx - ox) + 1;
            int z0 = (int)MathF.Floor(az - oz) - 1, z1 = (int)MathF.Ceiling(bz - oz) + 1;
            for (int gz = Math.Max(1, z0); gz <= Math.Min(g.H - 2, z1); gz++)
            {
                for (int gx = Math.Max(1, x0); gx <= Math.Min(g.W - 2, x1); gx++)
                {
                    to[(gx, gz)] = to.TryGetValue((gx, gz), out float was) ? MathF.Min(was, bottom) : bottom;
                }
            }
        }
        var dug = new List<int>();
        int rx0 = g.W, rx1 = 0, rz0 = g.H, rz1 = 0;
        bool limit = g.NoLimit;
        g.NoLimit = true;
        foreach (var ((gx, gz), y) in to)
        {
            Site.Add((gx, gz));
            int p = gz * g.W + gx;
            if (g.Locked(gx, gz) || shaped.Contains(p) || (mask?.Invoke(p) ?? 1) <= 0 || g.HeightOf(p) <= y)
            {
                continue;
            }
            g.SetHeight(p, y);
            dug.Add(p);
            (rx0, rx1, rz0, rz1) = (Math.Min(rx0, gx), Math.Max(rx1, gx), Math.Min(rz0, gz), Math.Max(rz1, gz));
        }
        g.NoLimit = limit;
        return (dug, (rx0, rz0, rx1, rz1));
    }

    /// <summary>The ground point nearest a grid position, clamped to the ground.</summary>
    private static int Index(Ground g, Vector2 p) => Math.Clamp((int)MathF.Round(p.Y), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(p.X), 0, g.W - 1);
}
