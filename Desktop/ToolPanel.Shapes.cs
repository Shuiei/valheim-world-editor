using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>A new pattern of natural bumps (the web editor's New pattern): another seed for the noise.</summary>
    /// <param name="seed">The noise's seed; null for a random one.</param>
    internal void NewPattern(int? seed = null)
    {
        Brush.Noise = new Noise(seed ?? Random.Shared.Next());
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
    /// Shows the rows that fit the shape. A stamp: no falloff (the picture has its own), it turns, and Raise and
    /// Lower can stamp it once.
    /// </summary>
    private void SyncStamp()
    {
        bool stamp = Brush.Shape == BrushShape.Stamp;
        _turnRow.IsVisible = Brush.Shape is BrushShape.Square or BrushShape.Stamp;
        _falloffRow.IsVisible = !stamp;
        int i = ShapeBox.SelectedIndex - 4;
        ForgetStampButton.IsVisible = i >= 0 && i < StampList.Count && StampList[i].Loaded;
        _stampOnceRows.IsVisible = stamp && Tool is BrushTool.Raise or BrushTool.Lower;
        _stampHeightRow.IsVisible = Brush.StampOnce;
    }

    /// <summary>A picture loaded as a stamp: chosen as the Shape, and kept for next time.</summary>
    /// <param name="label">Its name in the Shape list.</param>
    /// <param name="data">Its weights (see Stamps.FromPicture).</param>
    public void AddStamp(string label, float[] data)
    {
        var st = new Stamps.Stamp($"pic:{DateTime.Now.Ticks}", label, data, Loaded: true);
        StampList.Add(st);
        Stamps.SaveKept(StampList.Where(s => s.Loaded));
        FillShapes();
        ShapeBox.SelectedIndex = 4 + StampList.Count - 1;
        Message?.Invoke(Said.Of("brush.msg.stampLoaded", label));
    }
}
