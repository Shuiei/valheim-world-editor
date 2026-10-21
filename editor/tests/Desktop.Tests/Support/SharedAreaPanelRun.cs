using System.Numerics;
using Avalonia.Controls;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A main window on its own copy of the test world, opened in the Area tool with a zone selected unless told not
/// to; deleted when disposed. The Area panel tests share it.
/// </summary>
internal sealed class SharedAreaPanelRun : IDisposable
{
    public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
    public string Dir { get; } = Fixtures.CopyFixture();
    public EditSession S { get; }
    public AreaPanel P => W.AreaPanel;
    public string Said => W.MessageText.Text ?? "";

    public SharedAreaPanelRun(bool select = true, int size = 1)
    {
        var scene = WorldScene.Load(Dir, 0, 0, size);
        W.Show();
        W.View.Show(scene, null);
        W.Edit(scene.Session!);
        S = scene.Session!;
        W.Tools.ChooseMode(ToolMode.Area);
        if (select)
        {
            Select(2, 2, 62, 62);
        }
    }

    /// <summary>
    /// Selects the box between two grid points, as a drag does: the points replaced, then one change notice.
    /// </summary>
    public void Select(float x0, float z0, float x1, float z1)
    {
        var points = W.View.Area.Points;
        lock (points)
        {
            points.Clear();
            points.Add(new Vector2(x0, z0));
            points.Add(new Vector2(x1, z1));
        }
        W.View.Area.Notify();
    }

    /// <summary>The entries an items control is showing, as text.</summary>
    public static List<string> Entries(ItemsControl box) => [.. ((IEnumerable<object>)box.ItemsSource!).Cast<string>()];

    /// <summary>Unticks every kind of object, so the object actions find none.</summary>
    public void ChooseNoKinds()
    {
        foreach (var b in P.KindButtons.Values)
        {
            b.IsChecked = false;
        }
    }

    /// <summary>Replace chosen with neither of its lists chosen, and applied.</summary>
    public async Task ReplaceWithNothingChosen()
    {
        P.Choose(AreaPanel.Act.Replace);
        P.Refresh();
        P.FromBox.SelectedIndex = -1;
        P.ToBox.SelectedIndex = -1;
        await P.Apply();
    }

    /// <summary>The heightmap exported (Export clicked) into a "maps" folder next to the world; its file.</summary>
    public string ExportHeightmap()
    {
        string folder = Path.Combine(Path.GetDirectoryName(Dir)!, "maps");
        P.HeightmapFolder = folder;
        SharedButtons.Click(P.ExportButton);
        return Assert.Single(Directory.GetFiles(folder, "*.png"));
    }

    /// <summary>A file named like a picture that is not one, next to <paramref name="png"/>, picked for import.</summary>
    public void PickABadPicture(string png)
    {
        string bad = Path.Combine(Path.GetDirectoryName(png)!, "bad.png");
        File.WriteAllText(bad, "not a picture");
        P.PickPicture = () => Task.FromResult<string?>(bad);
    }

    /// <summary>
    /// The ground height at grid point (x, z).
    /// </summary>
    public float H(int x, int z) => S.Scene.Heights[z * S.Scene.W + x];

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
}
