using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public static partial class Workshop
{
    /// <summary>
    /// Works out what <c>Add</c> would put down, from the same arguments, without changing the session or the scene:
    /// <c>Put</c> puts it. Reads the file when <paramref name="text"/> is null.
    /// </summary>
    public static Planned Plan(EditSession s, string path, string? text, Vector2? at, float turn = 0, float? ground = null)
    {
        text ??= File.ReadAllText(path);
        var parsed = BlueprintFormats.Parse(Path.GetFileName(path), text);
        bool homestead = text.Contains("#HomesteadVersion:", StringComparison.OrdinalIgnoreCase);
        var known = parsed.Pieces.Where(p => s.Scene.World.CanCreate(StableHash.Of(p.Name))).ToList();
        var unknown = parsed.Pieces.Select(p => p.Name).Where(n => !s.Scene.World.CanCreate(StableHash.Of(n))).Distinct().OrderBy(n => n).ToList();
        // The building as it is, moved as one: its lowest buildable piece (the colliders, as the game has
        // them) on the ground; across, others than Homestead's (measured from a corner) centred. Rocks
        // and the like (the hoe's) do not count: they are not the building.
        var built = known.Where(p => Buildable(p.Name)).ToList();
        Vector3 shift = Vector3.Zero;
        if (known.Count > 0)
        {
            float low = (built.Count > 0 ? built : known).Min(p => p.Position.Y + Hammer.Bottom(p.Name, BlueprintFormats.FromEuler(p.Euler), p.Scale > 0 ? p.Scale : 1));
            shift = homestead ? new Vector3(0, -low, 0)
                : new Vector3(-(known.Min(p => p.Position.X) + known.Max(p => p.Position.X)) / 2, -low, -(known.Min(p => p.Position.Z) + known.Max(p => p.Position.Z)) / 2);
        }
        var anchor = at is { } w ? new Vector3(w.X, ground ?? Ground, w.Y) : Anchor(s.Scene) with { Y = ground ?? Ground };
        // Turned round the anchor: the pieces' places from it, and their own turns.
        var spin = Matrix4x4.CreateRotationY(turn * MathF.PI / 180f);
        Vector3 At(Vector3 fromAnchor) => anchor + Vector3.Transform(fromAnchor, spin);
        Vector3 Euler(Vector3 e) => e with { Y = e.Y + turn };
        var adds = known.Select(p => (new NewObject(0, StableHash.Of(p.Name), At(p.Position + shift), Euler(p.Euler), MathF.Abs(p.Scale - 1) < 1e-3f ? 0 : p.Scale, Data: p.Data),
            PieceCatalog.Get(StableHash.Of(p.Name))?.Tool != null)).ToList();
        // The ground it stood on in game, for the support check: Homestead's terrain contacts when the file
        // has them, else the bottom of its lowest piece at each spot (the terrain reached each post).
        var contacts = text.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("#HomesteadTerrainContact:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l[25..].Split(';')).Where(f => f.Length >= 3)
            .Select(f => (Ok: TryPoint(f, out Vector3 p), P: p))
            .Where(c => c.Ok).Select(c => At(c.P + shift)).ToList();
        var terrain = new Dictionary<(int, int), float>();
        void Lowest(int cx, int cz, float y) => terrain[(cx, cz)] = terrain.TryGetValue((cx, cz), out float was) ? MathF.Min(was, y) : y;
        if (contacts.Count > 0)
        {
            foreach (var c in contacts)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Lowest((int)MathF.Floor(c.X) + dx, (int)MathF.Floor(c.Z) + dz, c.Y);
                    }
                }
            }
        }
        else
        {
            foreach (var p in built)
            {
                var rot = BlueprintFormats.FromEuler(Euler(p.Euler));
                var pts = Hammer.Outline(p.Name, rot, p.Scale > 0 ? p.Scale : 1).ToList();
                if (pts.Count == 0)
                {
                    continue;
                }
                var o = At(p.Position + shift);
                float bottom = o.Y + pts.Min(v => v.Y);
                int x0 = (int)MathF.Floor(o.X + pts.Min(v => v.X)), x1 = (int)MathF.Floor(o.X + pts.Max(v => v.X));
                int z0 = (int)MathF.Floor(o.Z + pts.Min(v => v.Z)), z1 = (int)MathF.Floor(o.Z + pts.Max(v => v.Z));
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        Lowest(cx, cz, bottom);
                    }
                }
            }
        }
        return new Planned(parsed.Name, at == null, parsed.Ruin, adds, unknown, shift.Y, terrain);
    }
}
