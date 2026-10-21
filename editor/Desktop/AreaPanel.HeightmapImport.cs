using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Reads a picture to import, fitted to the selection's box or else the whole area (as far as the Mask lets it),
    /// and fills Lowest and Highest with the heights it was exported with, else the range of the ground it covers.
    /// </summary>
    internal void LoadPicture(string path)
    {
        if (_session() is not { } session)
        {
            return;
        }
        var g = session.Ground;
        // Where it goes: the selection's box, or the whole area (as far as the Mask lets it).
        List<(int G, float W)> cells;
        int x0, z0, w, h;
        var mask = session.MaskNow();
        if (Area.WeightsIn(g.W, g.H, mask) is { } a && Area.GridBox(g.W, g.H) is { } box)
        {
            // Fitted to the selection's own box, not the weights' (one point wider all round).
            (cells, x0, z0, w, h) = (a.Cells, box.X0, box.Z0, box.X1 - box.X0 + 1, box.Z1 - box.Z0 + 1);
        }
        else
        {
            cells = [.. Enumerable.Range(0, g.W * g.H).Select(p => (p, mask?.Invoke(p) ?? 1f)).Where(c => c.Item2 > 0)];
            (x0, z0, w, h) = (0, 0, g.W, g.H);
        }
        Heightmaps.Picture pic;
        try
        {
            pic = Heightmaps.Read(File.ReadAllBytes(path), w, h);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Log.CaughtBrief(ex, "reading the heightmap picture to import");
            Say(Said.Of("area.msg.pictureUnusable", ex.Message));
            return;
        }
        _picture = (pic.Values, g, x0, z0, w, h, cells, Area.Polygon());
        // The heights written in the picture by the editor, else the range of the ground it covers now.
        float lo = cells.Count > 0 ? cells.Min(c => g.HeightOf(c.G)) : 0, hi = cells.Count > 0 ? cells.Max(c => g.HeightOf(c.G)) : 0;
        LowestBox.Value = (decimal)(pic.Min is float mn ? MathF.Round(mn, 3) : MathF.Round(lo, 1));
        HighestBox.Value = (decimal)(pic.Max is float mx ? MathF.Round(mx, 3) : MathF.Round(hi, 1));
        bool selection = Area.Polygon() != null, heights = pic.Min != null;
        PictureInfo.Text = (selection, heights) switch
        {
            (true, true) => Lang.T("area.picture.selectionHeights", pic.Width, pic.Height, w, h),
            (true, false) => Lang.T("area.picture.selection", pic.Width, pic.Height, w, h),
            (false, true) => Lang.T("area.picture.wholeHeights", pic.Width, pic.Height, w, h),
            _ => Lang.T("area.picture.whole", pic.Width, pic.Height, w, h),
        };
        _pictureBox.IsVisible = true;
    }

    /// <summary>
    /// A heightmap's values (0 black to 1 white, w × h from grid point x0, z0) into the ground from lo to hi, each
    /// point faded by its weight; locked points and those outside the picture are left alone.
    /// </summary>
    /// <returns>The points changed, and how many fully weighted ones the game's ±8 m kept from their height.</returns>
    internal static (List<int> Touched, int Clamped) PutHeights(Ground g, float[] values, int x0, int z0, int w, int h, List<(int G, float W)> cells, float lo, float hi)
    {
        var list = new List<int>();
        int clamped = 0;
        foreach (var (pt, wt) in cells)
        {
            int gx = pt % g.W, gz = pt / g.W, ix = gx - x0, iz = gz - z0;
            if (g.Locked(gx, gz) || ix < 0 || iz < 0 || ix >= w || iz >= h)
            {
                continue;
            }
            float want = lo + values[iz * w + ix] * (hi - lo), now = g.HeightOf(pt);
            g.SetHeight(pt, now + (want - now) * wt);
            if (wt > 0.999f && MathF.Abs(g.HeightOf(pt) - want) > 0.05f)
            {
                clamped++;
            }
            list.Add(pt);
        }
        return (list, clamped);
    }
}
