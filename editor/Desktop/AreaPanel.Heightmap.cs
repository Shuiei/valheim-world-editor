using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    // ---- Heightmaps: the ground as a 16-bit picture (with its lowest and highest heights), and a picture back
    // into the ground; both the selection's box, or the whole area without a selection.
    /// <summary>Where Export writes (null: the heightmaps folder in the app's data folder). Tests use their own.</summary>
    internal string? HeightmapFolder { get; set; }

    /// <summary>
    /// Writes the ground as a 16-bit PNG, with its lowest and highest heights in it, named after the world and its
    /// zones: the ground of the selection's own box, the one an import fits a picture to (and named after that box
    /// too), or of the whole area without a selection.
    /// </summary>
    /// <param name="folder">Where to write it; null: <see cref="HeightmapFolder"/>.</param>
    /// <returns>The message for the status bar, or null when no world is open.</returns>
    internal Said? ExportHeightmap(string? folder = null)
    {
        if (_view.Scene is not { World: { } world } s)
        {
            return null;
        }
        int x1 = s.X0 + s.ZonesX - 1, z1 = s.Z0 + s.ZonesZ - 1;
        var box = Area.GridBox(s.W, s.H);
        var (bx0, bz0, bx1, bz1) = box ?? (0, 0, s.W - 1, s.H - 1);
        int w = bx1 - bx0 + 1, h = bz1 - bz0 + 1;
        float[] heights = s.Heights;
        if (box != null)
        {
            heights = new float[w * h];
            for (int row = 0; row < h; row++)
            {
                Array.Copy(s.Heights, (bz0 + row) * s.W + bx0, heights, row * w, w);
            }
        }
        // Where the box is in the area, for a selection: two selections of one area do not write over each other.
        string part = box == null ? "" : $"_at_{bx0}_{bz0}_{w}x{h}";
        string named = $"{world.Name} zones {s.X0},{s.Z0} to {x1},{z1}" + (box == null ? "" : $", {w} x {h} m from {bx0},{bz0}");
        byte[] png = Heightmaps.Encode(w, h, heights, named, out float min, out float max);
        string dir = folder ?? HeightmapFolder ?? Path.Combine(AppSettings.DataDir, "heightmaps");
        string safe = string.Concat(world.Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        string path = Path.Combine(dir, $"{safe}_{s.X0}_{s.Z0}_{x1}_{z1}{part}.png");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, png);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing the heightmap picture");
            return Said.Of("area.msg.heightmapFailed", path, ex.Message);
        }
        return box == null ? Said.Of("area.msg.heightmapWritten", path, w, h, min, max) : Said.Of("area.msg.heightmapWrittenSelection", path, w, h, min, max);
    }

    /// <summary>
    /// Puts the picture read into the ground, from Lowest (black) to Highest (white), faded by the soft edge.
    /// Locked points are skipped; points the game's ±8 m keeps from their height are counted in the message.
    /// </summary>
    internal void PutPicture()
    {
        if (_picture is not { } p || _session() is not { } session)
        {
            return;
        }
        if (p.Ground != session.Ground)
        {
            // Fitted to another area's ground: its points would land anywhere in this one.
            _picture = null;
            _pictureBox.IsVisible = false;
            Say(Said.Of("area.msg.pictureOtherArea"));
            return;
        }
        float lo = Value(LowestBox), hi = Value(HighestBox);
        int clamped = 0;
        var touched = session.EditGround(Lang.T("area.undo.heightmap"), g =>
        {
            var (list, c) = PutHeights(g, p.Values, p.X0, p.Z0, p.W, p.H, p.Cells, lo, hi);
            clamped = c;
            return (list, (0, 0, g.W - 1, g.H - 1));
        });
        _picture = null;
        _pictureBox.IsVisible = false;
        Say(clamped > 0 ? Said.Of("area.msg.importClamped", clamped) : Said.Of("area.msg.imported", touched.Count));
    }

    /// <summary>
    /// The selection changed: a picture read was fitted to the old one, so it is dropped, unless the outline is still
    /// the same (a polygon closed with Enter, a box redrawn where it is).
    /// </summary>
    private void DropPicture()
    {
        if (_picture is not { } p)
        {
            return;
        }
        var now = Area.Polygon();
        if (p.Fit == null ? now == null : now != null && p.Fit.SequenceEqual(now))
        {
            return;
        }
        _picture = null;
        _pictureBox.IsVisible = false;
        Say(Said.Of("area.msg.pictureDropped"));
    }
}
