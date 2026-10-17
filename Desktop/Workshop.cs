using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Workshop: a blank, flat meadow plot that is no world, to build a building on (Place and Select,
/// as in a world) and keep it as a Homestead blueprint (BlueprintsPanel). Blueprints open in it to be
/// changed. Its middle at ground level is the blueprint's anchor, as Homestead measures it.
/// </summary>
public static class Workshop
{
    /// <summary>The plot's ground height in metres, everywhere.</summary>
    public const float Ground = 34f;
    /// <summary>The plot's size in zones (64 m each) across, centred on the world's middle.</summary>
    public const int Zones = 3;

    /// <summary>The plot, with nothing on it, and its edit session.</summary>
    /// <param name="title">The scene's name, shown as the title.</param>
    public static WorldScene Create(string title)
    {
        int w = Zones * 64 + 1;
        var scene = new WorldScene
        {
            World = new WorldSave { Directory = "", SaveNumber = 0 },
            Name = title,
            X0 = -(Zones / 2),
            Z0 = -(Zones / 2),
            ZonesX = Zones,
            ZonesZ = Zones,
            W = w,
            H = w,
            Heights = [.. Enumerable.Repeat(Ground, w * w)],
            // Meadows everywhere (biome 1: the terrain shader's plain grass).
            Biomes = [.. Enumerable.Repeat((int)ValheimGen.Heightmap.Biome.Meadows, w * w)],
            Things = [],
            BiomeColor = new byte[w * w * 4],
            Mask = new byte[w * w * 4],
            OceanDepth = new float[w * w],
            Limit = new float[w * w],
            Cx = 0,
            Cz = 0,
            LoadInfo = Lang.T("workshop.loadInfo"),
        };
        var ground = new Ground(w, w, scene.X0, scene.Z0, Zones);
        Array.Fill(ground.Base, Ground);
        scene.Session = new EditSession(scene, ground, new EditStore(scene.World));
        return scene;
    }

    /// <summary>The middle of the plot at ground level: the blueprint's anchor.</summary>
    public static Vector3 Anchor(WorldScene s) => new(s.Cx, Ground, s.Cz);

    /// <summary>
    /// A blueprint file's pieces onto the plot (one undo step), its anchor at the plot's middle; the plot takes the
    /// blueprint's ruin flag. Returns what <c>Add</c> does.
    /// </summary>
    public static (int Placed, List<string> Unknown, float Lift, Dictionary<(int, int), float> Terrain) Open(EditSession s, string path, string? text = null) => Add(s, path, text, null);

    /// <summary>
    /// A blueprint file's pieces added to what is on the plot (one undo step), its anchor at world point
    /// at (x, z; on the ground there), or the plot's middle, its lowest point on the ground. Homestead's are
    /// measured from their anchor; others (PlanBuild, .vbuild) from a corner, so they are centred.
    /// </summary>
    /// <param name="s">The Workshop's edit session.</param>
    /// <param name="path">The blueprint file (its name picks the format).</param>
    /// <param name="text">The file's text when already read; null reads <paramref name="path"/>.</param>
    /// <param name="at">Where its anchor goes (world x, z); null for the plot's middle, which also takes the
    /// blueprint's ruin flag.</param>
    /// <param name="turn">Degrees it is turned round its anchor, as yaw turns (90: north becomes east).</param>
    /// <param name="ground">The height its lowest point goes to; null: the plot's ground.</param>
    /// <param name="label">The history's name for the step; null: "Opened" or "Added" and the blueprint's name.</param>
    /// <returns>
    /// How many were put, the kinds the editor cannot make, how far it was lifted (saving it back
    /// takes that off: Homestead's anchor keeps its height), and the ground it stood on (GroundAt), lowest height
    /// per whole metre (x, z).
    /// </returns>
    public static (int Placed, List<string> Unknown, float Lift, Dictionary<(int, int), float> Terrain) Add(EditSession s, string path, string? text, Vector2? at,
        float turn = 0, float? ground = null, string? label = null) => Put(s, Plan(s, path, text, at, turn, ground), label);

    /// <summary>
    /// A blueprint worked out for <c>Add</c> but not put down yet: the objects to add, where they go, and what
    /// <c>Add</c> reports. Nothing of the session is touched until <c>Put</c>, so what would be added can be
    /// checked first (Claude's add_blueprint refuses pieces outside the area) without a step in the history.
    /// </summary>
    /// <param name="Name">The blueprint's name.</param>
    /// <param name="AtMiddle">Its anchor at the plot's middle (<c>Add</c>'s at was null): the plot takes its ruin flag.</param>
    /// <param name="Ruin">The blueprint's ruin flag.</param>
    /// <param name="Adds">The objects to add, in order, with whether each is a player-built piece.</param>
    /// <param name="Unknown">The kinds the editor cannot make, each once, sorted.</param>
    /// <param name="Lift">How far it was lifted.</param>
    /// <param name="Terrain">The ground it stood on, lowest height per whole metre (x, z).</param>
    public sealed record Planned(string Name, bool AtMiddle, bool Ruin, List<(NewObject Object, bool Piece)> Adds, List<string> Unknown, float Lift,
        Dictionary<(int, int), float> Terrain);

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

    /// <summary>A terrain contact's point from its first three fields; false when one is not a number.</summary>
    internal static bool TryPoint(string[] f, out Vector3 p)
    {
        var n = System.Globalization.NumberStyles.Float;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        if (float.TryParse(f[0], n, c, out float x) && float.TryParse(f[1], n, c, out float y) && float.TryParse(f[2], n, c, out float z))
        {
            p = new Vector3(x, y, z);
            return true;
        }
        p = default;
        return false;
    }

    /// <summary>
    /// Puts a planned blueprint on the plot as one undo step (the plot takes its ruin flag when its anchor is at the
    /// middle), and returns what <c>Add</c> does.
    /// </summary>
    /// <param name="s">The edit session <c>Plan</c> was given.</param>
    /// <param name="plan">What to put down.</param>
    /// <param name="label">The history's name for the step; null: "Opened" or "Added" and the blueprint's name.</param>
    public static (int Placed, List<string> Unknown, float Lift, Dictionary<(int, int), float> Terrain) Put(EditSession s, Planned plan, string? label = null)
    {
        if (plan.AtMiddle)
        {
            s.Scene.Ruin = plan.Ruin;
        }
        s.Commit(label ?? (plan.AtMiddle ? Lang.T("workshop.undo.opened", plan.Name) : Lang.T("workshop.undo.added", plan.Name)), null, [], plan.Adds);
        return (plan.Adds.Count, plan.Unknown, plan.Lift, plan.Terrain);
    }

    /// <summary>Pieces players build: the hammer's, the cultivator's, the serving tray's (not the hoe's rocks).</summary>
    public static bool Buildable(string prefab) => PieceCatalog.Get(StableHash.Of(prefab))?.IsBuildPiece == true;

    /// <summary>
    /// The ground the support check counts at a world point: the plot's, or (higher) the ground a
    /// blueprint stood on in game at that metre (Add's terrain).
    /// </summary>
    public static float GroundAt(IReadOnlyDictionary<(int, int), float> terrain, float x, float z) =>
        terrain.TryGetValue(((int)MathF.Floor(x), (int)MathF.Floor(z)), out float y) ? MathF.Max(Ground, y) : Ground;

    /// <summary>
    /// What is on the plot as a copy (the clipboard format, for BlueprintsPanel): every object standing
    /// (the plot starts bare: all on it was put there), measured from the anchor, with what it holds (a
    /// chest's contents, a sign's text, a creature's stars...).
    /// </summary>
    /// <param name="s">The Workshop scene.</param>
    /// <param name="name">The copy's name.</param>
    /// <param name="lift">How far the blueprint opened was lifted (its anchor that much below the ground).</param>
    public static JsonObject Building(WorldScene s, string name, float lift = 0)
    {
        var anchor = Anchor(s) + new Vector3(0, lift, 0);
        var objects = new JsonArray();
        lock (s.Things)
        {
            foreach (var t in s.Things.Where(t => !t.Gone && PrefabCatalog.Details(t.Prefab) != null))
            {
                var o = new JsonObject
                {
                    ["name"] = PrefabCatalog.NameOf(t.Prefab),
                    ["dx"] = R(t.Position.X - anchor.X),
                    ["dz"] = R(t.Position.Z - anchor.Z),
                    ["dy"] = R(t.Position.Y - anchor.Y),
                    ["rx"] = R(t.Rotation.X),
                    ["ry"] = R(t.Rotation.Y),
                    ["rz"] = R(t.Rotation.Z),
                    ["scale"] = R(t.Scale),
                    ["sourceId"] = null,
                };
                var data = DataOf(s, t);
                if (data.Count > 0)
                {
                    o["data"] = BlueprintFormats.DataJson(data);
                }
                objects.Add(o);
            }
        }
        float half = objects.Count == 0 ? 1 : objects.OfType<JsonObject>().Max(o => MathF.Max(MathF.Abs((float)o["dx"]!), MathF.Abs((float)o["dz"]!))) + 2;
        return new JsonObject
        {
            ["w"] = 1,
            ["h"] = 1,
            ["rel"] = new JsonArray(-32768),
            ["wt"] = new JsonArray(0),
            ["pnt"] = new JsonArray(-255, -255, -255, -255),
            ["objects"] = objects,
            ["poly"] = new JsonArray(P(-half, -half), P(half, -half), P(half, half), P(-half, half)),
            ["name"] = name,
        };
        static JsonObject P(float x, float z) => new() { ["gx"] = x, ["gz"] = z };
    }

    /// <summary>
    /// What an object of the plot holds: every value of its data but its builder and scale (a blueprint
    /// has its own), and no builder at all on a ruin's: the plot opened from one, or a building piece
    /// without a builder (added from one: the others got the chosen builder when they were put).
    /// </summary>
    private static List<ObjectField> DataOf(WorldScene s, WorldScene.Thing t)
    {
        var list = new List<ObjectField>();
        bool ruin = s.Ruin;
        if (s.Session is { } session && ObjectData.Bytes(s.World, session.Edits, t.Id) is { } bytes)
        {
            ruin |= WorldSave.Builder != 0 && PieceCatalog.Get(t.Prefab)?.Tool != null && !ZdoData.Parse(bytes).LongList.Any(f => f.Key == ObjectField.CreatorKey);
            list.AddRange(ObjectData.Fields(bytes, builder: false));
        }
        if (ruin)
        {
            list.Add(ObjectField.NoBuilder);
        }
        return list;
    }

    /// <summary>How many of each kind of building piece stand on the plot (prefab name → count).</summary>
    public static Dictionary<string, int> Kinds(WorldScene s)
    {
        lock (s.Things)
        {
            return s.Things.Where(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null).GroupBy(t => PrefabCatalog.NameOf(t.Prefab) ?? "?").ToDictionary(g => g.Key, g => g.Count());
        }
    }

    /// <summary>How many building pieces stand on the plot.</summary>
    public static int Pieces(WorldScene s)
    {
        lock (s.Things)
        {
            return s.Things.Count(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null);
        }
    }

    /// <summary>Rounds to the millimetre (or thousandth) for the copy's numbers.</summary>
    private static float R(float v) => MathF.Round(v * 1000f) / 1000f;
}
