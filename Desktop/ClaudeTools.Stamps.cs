using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The stamps offered: the built-in ones, then those the user loaded (by name or label).</summary>
    internal static List<Stamps.Stamp> StampsOffered() => [.. Stamps.BuiltIn, .. Stamps.LoadKept()];

    /// <summary>The picture of a stamp Claude named: a stamp's name or label, or the path of a PNG picture; or why there is none.</summary>
    /// <param name="stamp">The stamp's name, label or picture path.</param>
    /// <returns>The stamp's heights, or null with why.</returns>
    private static (float[]? Data, string? Problem) StampDataOf(string stamp)
    {
        float[]? data = StampsOffered().FirstOrDefault(st => st.Label.StartsWith(stamp, StringComparison.OrdinalIgnoreCase) || st.Name.Equals(stamp, StringComparison.OrdinalIgnoreCase))?.Data;
        if (data == null && File.Exists(stamp))
        {
            try
            {
                data = Stamps.FromPicture(File.ReadAllBytes(stamp));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.CaughtBrief(ex, "reading the stamp picture Claude named");
                return (null, $"That picture cannot be read: {ex.Message}");
            }
        }
        if (data == null)
        {
            return (null, $"No stamp \"{stamp}\": {string.Join(", ", StampsOffered().Select(st => st.Label))}, or a PNG picture's path.");
        }
        return (data, null);
    }

    /// <summary>stamp: a stamp's picture pressed into the ground at a point, raised or lowered, as one pending step.</summary>
    [McpServerTool(Name = "stamp", Title = "Press a stamp into the ground")]
    [Description("Presses a stamp into the ground of the open area as the Raise or Lower brush does with a stamp shape and Stamp once: the stamp's picture (white high, black untouched) radius metres round x, z, height metres up (negative: down, a hollow), turned by turn degrees. Stamps: Mountain, Mesa (flat top), Crater rim, Dunes, Rocky ground, and those the user loaded (a picture's name, or a PNG file's path to use any greyscale picture). A mask limits it to some ground. One pending step; the game's ±8 m limit holds.")]
    public Task<string> Stamp(
        [Description("The stamp's name (Mountain, Mesa, Crater rim, Dunes, Rocky ground, a loaded one), or the path of a PNG picture.")] string stamp,
        [Description("World x of its middle.")] float x,
        [Description("World z of its middle.")] float z,
        [Description("Radius, metres (half its side).")] float radius = 20,
        [Description("Metres up at its whitest (negative: down).")] float height = 4,
        [Description("Degrees to turn it (default 0).")] float turn = 0,
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var (data, badStamp) = StampDataOf(stamp);
        if (data == null)
        {
            return badStamp!;
        }
        var s = _w.Session!;
        var (minX, minZ, maxX, maxZ) = Bounds(s.Scene);
        if (x < minX || x > maxX || z < minZ || z > maxZ)
        {
            return $"({x}, {z}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
        }
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        var brush = new Brush { Shape = BrushShape.Stamp, StampData = data, Radius = Math.Clamp(radius, 2, 500), Turn = turn };
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ;
        bool clamped = false;
        var touched = s.EditGround(ClaudesStep(Lang.T("claude.undo.stamp", stamp)), g =>
        {
            var (t, rect, c) = Sculpt.StampOnce(g, brush, x - ox, z - oz, height, only);
            clamped = c;
            return (t, rect);
        });
        _w.AfterClaudeEdit(Said.Of("claude.msg.stamp", touched.Count));
        return touched.Count == 0 ? "Nothing changed." : $"Stamped {touched.Count} ground point(s){(clamped ? "; part of it reached the game's ±8 m limit" : "")}. Now pending: {Pending()}";
    });
}
