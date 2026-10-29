using System.Numerics;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    // Heightmap: export the area, or import a picture into the selection (or the whole area).
    /// <summary>Writes the area's heightmap (see <see cref="ExportHeightmap"/>).</summary>
    internal Button ExportButton { get; } = new() { Content = Lang.T("area.label.export"), FontSize = 12 };

    /// <summary>Reads a picture to import (see <see cref="LoadPicture"/>).</summary>
    internal Button ImportButton { get; } = new() { Content = Lang.T("area.label.import"), FontSize = 12 };

    /// <summary>Import: the height black stands for, in metres.</summary>
    internal NumericUpDown LowestBox { get; } = new() { Increment = 0.5m, FormatString = "0.###", FontSize = 12 };

    /// <summary>Import: the height white stands for, in metres.</summary>
    internal NumericUpDown HighestBox { get; } = new() { Increment = 0.5m, FormatString = "0.###", FontSize = 12 };

    /// <summary>Import: puts the picture read into the ground (see <see cref="PutPicture"/>).</summary>
    internal Button PutButton { get; } = new Button { Content = Lang.T("area.label.put"), FontSize = 12 }.Classed("primary");

    /// <summary>Import: drops the picture read.</summary>
    internal Button CancelPictureButton { get; } = new() { Content = Lang.T("common.cancel"), FontSize = 12 };

    /// <summary>Import: the picture read and where it is fitted.</summary>
    internal TextBlock PictureInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>Asks for a picture to import (the window's file picker).</summary>
    internal Func<Task<string?>> PickPicture { get; set; } = () => Task.FromResult<string?>(null);

    /// <summary>
    /// The picture read: its values (0 to 1, row by row), the ground it is fitted to and the box of it (grid points),
    /// the ground points it goes into with their weights, and the selection it was fitted to (null: the whole
    /// area). Dropped when the selection becomes another, and refused for another ground: its points are that
    /// ground's.
    /// </summary>
    private (float[] Values, Ground Ground, int X0, int Z0, int W, int H, List<(int G, float W)> Cells, List<Vector2>? Fit)? _picture;

    /// <summary>The import rows, shown once a picture is read.</summary>
    private readonly Control _pictureBox = null!;
}
