using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Workshop: a blank, flat meadow plot that is no world, to build a building on (Place and Select,
/// as in a world) and keep it as a Homestead blueprint (BlueprintsPanel). Blueprints open in it to be
/// changed. Its middle at ground level is the blueprint's anchor, as Homestead measures it.
/// </summary>
public static partial class Workshop
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
}
