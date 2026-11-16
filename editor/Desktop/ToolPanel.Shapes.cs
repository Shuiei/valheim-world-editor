using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>A new pattern of natural bumps (the web editor's New pattern): another seed for the noise.</summary>
    /// <param name="seed">The noise's seed; null for a random one.</param>
    internal void NewPattern(int? seed = null)
    {
        Brush.NewPattern(seed);
        Message?.Invoke(Said.Of("brush.msg.newPattern"));
    }

    /// <summary>Fills the Shape list: the built-in shapes, then every stamp.</summary>
    private void FillShapes() => ShapeBox.ItemsSource = BuiltInShapes().Concat(StampList.Select(s => s.Item)).ToList();

    /// <summary>The Shape list's choice into the brush (a stamp's weights and name too).</summary>
    private void ShapeChosen()
    {
        int i = Math.Max(0, ShapeBox.SelectedIndex);
        if (i >= 4 && i - 4 < StampList.Count)
        {
            Brush.Shape = BrushShape.Stamp;
            Brush.StampData = StampList[i - 4].Data;
            Brush.StampLabel = StampList[i - 4].Shown;
        }
        else
        {
            Brush.Shape = (BrushShape)Math.Min(i, 3);
        }
        SyncStamp();
    }

    /// <summary>
    /// Shows the rows that fit the shape. A stamp: no falloff (the picture has its own), and it turns. (The whole
    /// picture in one click is the Landform tool's Picture kind.)
    /// </summary>
    private void SyncStamp()
    {
        bool stamp = Brush.Shape == BrushShape.Stamp;
        _turnRow.IsVisible = Brush.Shape is BrushShape.Square or BrushShape.Stamp;
        _falloffRow.IsVisible = !stamp;
        int i = ShapeBox.SelectedIndex - 4;
        ForgetStampButton.IsVisible = i >= 0 && i < StampList.Count && StampList[i].Loaded;
    }

    /// <summary>A picture loaded as a stamp, kept for next time: a brush shape, and one of the Landform tool's pictures.</summary>
    /// <param name="label">Its name in the Shape list.</param>
    /// <param name="data">Its weights (see Stamps.FromPicture).</param>
    /// <param name="choose">Whether it becomes the brush's Shape (not when the Landform tool loaded it).</param>
    /// <returns>The stamp added.</returns>
    public Stamps.Stamp AddStamp(string label, float[] data, bool choose = true)
    {
        var st = new Stamps.Stamp($"pic:{DateTime.Now.Ticks}", label, data, Loaded: true);
        string? shape = ShapeBox.SelectedItem as string;
        StampList.Add(st);
        Stamps.SaveKept(StampList.Where(s => s.Loaded));
        FillShapes();
        ShapeBox.SelectedIndex = choose ? 4 + StampList.Count - 1 : Math.Max(0, ShapeBox.Items.IndexOf(shape));
        StampsChanged?.Invoke();
        Message?.Invoke(Said.Of("brush.msg.stampLoaded", label));
        return st;
    }

    /// <summary>
    /// Forgets a loaded picture (built-in stamps stay): gone from the Shape list (the brush goes back to a circle
    /// when it was its shape) and from the Landform tool's pictures.
    /// </summary>
    /// <param name="stamp">The stamp to forget.</param>
    /// <returns>False when it is a built-in one, or not in the list: nothing forgotten.</returns>
    public bool ForgetStamp(Stamps.Stamp stamp)
    {
        int i = StampList.IndexOf(stamp);
        if (i < 0 || !stamp.Loaded)
        {
            return false;
        }
        int chosen = ShapeBox.SelectedIndex - 4;
        string? shape = ShapeBox.SelectedItem as string;
        StampList.RemoveAt(i);
        Stamps.SaveKept(StampList.Where(s => s.Loaded));
        FillShapes();
        ShapeBox.SelectedIndex = chosen == i ? 0 : Math.Max(0, ShapeBox.Items.IndexOf(shape));
        StampsChanged?.Invoke();
        return true;
    }
}
