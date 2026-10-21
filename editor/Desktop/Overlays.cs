using System.Numerics;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the View panel's overlays draw, as line segments in view space (pairs of points), like the web editor
/// (editor.html borders and markers, editor/overlays.js rings): zone borders, location markers, the ground wards
/// protect, the range crafting stations let you build in, and the ground locations flatten. Built on the CPU from the
/// scene; GlView sends them to the graphics card.
/// </summary>
public static class Overlays
{
    /// <summary>The overlay layers, each drawn in its own colour and switched on or off on its own.</summary>
    public enum Layer
    {
        /// <summary>Zone borders, every 64 m.</summary>
        Borders,
        /// <summary>A 14 m post at each location in or near the area.</summary>
        Markers,
        /// <summary>The ground each ward protects.</summary>
        Wards,
        /// <summary>The range crafting stations let you build in.</summary>
        Stations,
        /// <summary>The flat part of the ground locations level.</summary>
        Flatten,
        /// <summary>Where the ground locations level blends back into the land (drawn fainter).</summary>
        FlattenEdge,
    }

    /// <summary>Every layer, in declaration order.</summary>
    public static readonly Layer[] All = Enum.GetValues<Layer>();

    /// <summary>Colour (sRGB) and opacity of each layer, and whether the ground and objects hide it.</summary>
    public static (Vector4 Color, bool Hidden) Style(Layer l) => l switch
    {
        Layer.Borders => (new Vector4(1, 1, 1, 0.25f), true),
        Layer.Markers => (new Vector4(1, 0.54f, 0.16f, 1), true),
        Layer.Wards => (new Vector4(1, 0.44f, 0.26f, 0.9f), false),
        Layer.Stations => (new Vector4(0.31f, 0.76f, 1, 0.9f), false),
        Layer.Flatten => (new Vector4(1, 0.84f, 0.31f, 0.95f), false),
        _ => (new Vector4(1, 0.84f, 0.31f, 0.35f), false),
    };

    /// <summary>The built overlay lines and the counts the View panel reports.</summary>
    /// <param name="Lines">Per layer, line segments as flat x, y, z floats in view space, two points per
    /// segment.</param>
    /// <param name="Wards">How many wards were ringed.</param>
    /// <param name="Stations">How many crafting stations were ringed.</param>
    /// <param name="Flattened">How many location ground modifiers (level or smooth) were drawn.</param>
    /// <param name="Locations">How many locations got a marker (in the area or within 40 m of it).</param>
    public sealed record Built(Dictionary<Layer, float[]> Lines, int Wards, int Stations, int Flattened, int Locations);

    /// <summary>
    /// The locations (villages, the trader, dungeon entrances...) in the area or within 40 m of it, as the Location
    /// markers show them.
    /// </summary>
    public static int LocationsNear(WorldScene s) => RealLocations(s).Count(l => Near(s, l.Position));

    /// <summary>
    /// Whether world point <paramref name="p"/> lies in the area or within 40 m of it (the area's last grid point is
    /// W - 1 m east of its first, and H - 1 m north).
    /// </summary>
    internal static bool Near(WorldScene s, Vector3 p)
    {
        float ox = s.OriginX, oz = s.OriginZ;
        return p.X >= ox - 40 && p.X <= ox + s.W - 1 + 40 && p.Z >= oz - 40 && p.Z <= oz + s.H - 1 + 40;
    }

    /// <summary>The world's locations without the invisible ground discs of No limit (not places to show).</summary>
    internal static IEnumerable<(Vector3 Position, int Location)> RealLocations(WorldScene s) =>
        (s.World?.Locations ?? []).Where(l => !Editing.Uplift.IsDisc(l.Location));

    /// <summary>Builds every layer's lines for the scene, following the ground height.</summary>
    /// <param name="s">The scene to draw over.</param>
    /// <param name="modifiers">The world's terrain modifiers, for the Flatten layers; null draws none.</param>
    /// <param name="names">Each thing's prefab name by index in the scene's things (null when unknown).</param>
    public static Built Build(WorldScene s, TerrainModifiers? modifiers, Func<int, string?> names)
    {
        var builder = new OverlayBuilder(s);
        builder.AddZoneBorders();
        int locations = builder.AddLocationMarkers();
        var (wards, stations) = builder.AddWardsAndStations(names);
        int flattened = builder.AddFlatten(modifiers);
        return new Built(builder.Finish(), wards, stations, flattened, locations);
    }
}
