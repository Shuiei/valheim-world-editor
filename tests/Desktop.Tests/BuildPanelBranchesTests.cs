using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using SkiaSharp;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Build panel's rarer ways: the Library with no search (a picture drawn once for a blueprint without one, kept for
/// the next listing; a good picture shown), a row pressed and barely moved (no drag), and the pieces listed with no
/// search or with several chosen, Build keeping a single piece already chosen.
/// </summary>
public sealed class BuildPanelBranchesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-build-branches-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>The Library row of a blueprint file.</summary>
    private static Border Row(BuildPanel p, string file) => p.LibraryList.Children.OfType<Border>().Single(b => (string?)b.Tag == file);

    /// <summary>A row's picture.</summary>
    private static Image Picture(Border row) => row.GetLogicalDescendants().OfType<Image>().First();

    /// <summary>A mouse with the left button down or not.</summary>
    private static PointerPointProperties Mouse(bool left) =>
        new(left ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None, left ? PointerUpdateKind.LeftButtonPressed : PointerUpdateKind.Other);

    /// <summary>A new mouse pointer.</summary>
    private static Pointer NewPointer() => new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

    /// <summary>The left button pressed on a row (on <paramref name="source"/>, the row or one of its buttons).</summary>
    private static void Press(Border row, object source) =>
        row.RaiseEvent(new PointerPressedEventArgs(source, NewPointer(), row, new Avalonia.Point(10, 10), 0, Mouse(true), KeyModifiers.None, 1));

    /// <summary>The mouse moved over a row, to a point of it.</summary>
    private static void Move(Border row, double to) =>
        row.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, row, NewPointer(), row, new Avalonia.Point(to, to), 0, Mouse(true), KeyModifiers.None));

    /// <summary>The left button released over a row.</summary>
    private static void Release(Border row) =>
        row.RaiseEvent(new PointerReleasedEventArgs(row, NewPointer(), row, new Avalonia.Point(10, 10), 0, Mouse(false), KeyModifiers.None, MouseButton.Left));

    [AvaloniaFact]
    public async Task TheLibraryDrawsMissingPicturesOnceAndKeepsThem()
    {
        Directory.CreateDirectory(_dir);
        var clip = CopyFormat.ToJson(BlueprintsPanelRun.Clip("Hut", "woodwall"));
        foreach (string name in new[] { "Hut", "Gate" })
        {
            File.WriteAllText(Path.Combine(_dir, Homestead.FileName(name)), Homestead.Write(clip, name, "test", null, DateTime.Now));
        }
        using (var bmp = new SKBitmap(4, 4))
        {
            bmp.Erase(SKColors.Red);
            using var f = File.Create(Path.Combine(_dir, "Gate.png"));
            bmp.Encode(f, SKEncodedImageFormat.Png, 100);
        }
        var p = new MainWindow(load: false).BuildPanel;
        p.Folder = () => _dir;
        p.LibrarySearch.Text = "x";
        p.LibrarySearch.Text = null;
        Assert.Equal(2, p.LibraryList.Children.Count);
        string hut = Path.Combine(_dir, Homestead.FileName("Hut"));
        Assert.NotNull(Picture(Row(p, Path.Combine(_dir, Homestead.FileName("Gate")))).Source);
        // Hut's picture is drawn from its pieces, away from the window's thread.
        for (int i = 0; i < 500 && Picture(Row(p, hut)).Source == null; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Assert.NotNull(Picture(Row(p, hut)).Source);
        // Listed again: the picture drawn is used at once.
        p.RenderLibrary();
        var row = Row(p, hut);
        Assert.NotNull(Picture(row).Source);
        // Pressed and moved a pixel or two, or pressed on a button: no drag.
        Move(row, 40);
        Press(row, row);
        Move(row, 12);
        Release(row);
        Press(row, row.GetLogicalDescendants().OfType<Button>().First());
        Move(row, 12);
        Assert.Same(row, Row(p, hut));
    }

    [AvaloniaFact]
    public void PiecesAreListedWithoutASearchOrASingleChoice()
    {
        var w = new MainWindow(load: false);
        var p = w.BuildPanel;
        var t = w.PlaceTool;
        t.Chosen.Clear();
        t.Chosen.AddRange(["woodwall", "wood_floor"]);
        // Several chosen: no tile is lit.
        p.Search.Text = "wall";
        Assert.NotEmpty(p.Tiles.Children.OfType<Button>());
        Assert.DoesNotContain(p.Tiles.Children.OfType<Button>(), b => b.Classes.Contains("on"));
        // No search at all: the tab's pieces.
        p.Search.Text = null;
        Assert.NotEmpty(p.Tiles.Children.OfType<Button>());
        // Build: the wall chosen when no single piece is; a single piece already chosen is kept.
        p.Start();
        Assert.Equal(["woodwall"], t.Chosen);
        t.Chosen[0] = "wood_floor";
        p.Start();
        Assert.Equal(["wood_floor"], t.Chosen);
    }
}
