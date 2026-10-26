using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>import_heightmap: a greyscale picture's heights into a rectangle of the ground, as one pending step.</summary>
    [McpServerTool(Name = "import_heightmap", Title = "Import a heightmap")]
    [Description("Puts a heightmap picture (a PNG file on this computer, greyscale: black low, white high) into the ground of a rectangle of the open area, as the Area tool's Heightmap import does: stretched over the rectangle, from lowest (black) to highest (white) metres, fading in over a soft edge going inwards. Pictures the editor exported carry their own lowest and highest; otherwise they default to the range of the ground there now. A mask limits it to some ground. One pending step; the game's ±8 m limit holds.")]
    public Task<string> ImportHeightmap(
        [Description("The PNG picture's path.")] string path,
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Height of black, metres (optional).")] float? lowest = null,
        [Description("Height of white, metres (optional).")] float? highest = null,
        [Description("The soft edge, metres inwards (default 3; 0: hard).")] float edge = 3,
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        if (BoxOver(s.Scene, x0, z0, x1, z1, edge).WeightsIn(s.Ground.W, s.Ground.H, only) is not { Cells.Count: > 0 } a)
        {
            return "The rectangle holds no ground of the open area.";
        }
        int w = a.X1 - a.X0 + 1, h = a.Z1 - a.Z0 + 1;
        var (pic, badPicture) = PictureOf(path, w, h);
        if (pic == null)
        {
            return badPicture!;
        }
        float lo = lowest ?? pic.Min ?? a.Cells.Min(c => s.Ground.HeightOf(c.G)), hi = highest ?? pic.Max ?? a.Cells.Max(c => s.Ground.HeightOf(c.G));
        int clamped = 0;
        var touched = s.EditGround(ClaudesStep(Lang.T("claude.undo.heightmap")), g =>
        {
            var (list, c) = AreaPanel.PutHeights(g, pic.Values, a.X0, a.Z0, w, h, a.Cells, lo, hi);
            clamped = c;
            return (list, (0, 0, g.W - 1, g.H - 1));
        });
        _w.AfterClaudeEdit(Said.Of("claude.msg.heightmap", touched.Count));
        return $"Imported {pic.Width} × {pic.Height} into {w} × {h} m, {lo:0.##} to {hi:0.##} m: {touched.Count} ground point(s){(clamped > 0 ? $", {clamped} kept from their height by the game's ±8 m" : "")}. Now pending: {Pending()}";
    });

    /// <summary>A heightmap picture read from a file and fitted to w by h points (import_heightmap), or why it cannot be read.</summary>
    /// <param name="path">The PNG picture's path.</param>
    /// <param name="w">The points across.</param>
    /// <param name="h">The points down.</param>
    internal static (Heightmaps.Picture? Picture, string? Problem) PictureOf(string path, int w, int h)
    {
        try
        {
            return (Heightmaps.Read(File.ReadAllBytes(path), w, h), null);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Log.CaughtBrief(ex, "reading the heightmap picture Claude named");
            return (null, $"That picture cannot be used: {ex.Message}");
        }
    }
}
