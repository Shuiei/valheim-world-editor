using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Settings window's folder lists: one row per folder given, and the folders read back from the boxes as they are.
/// </summary>
public class SettingsDialogFormTests
{
    [AvaloniaFact]
    public void BuildFoldersShowsOneRowPerFolderAndReadsThemBack()
    {
        var form = new SettingsDialog.Form(new AppSettings());
        var panel = form.BuildFolders(["C:/a", "C:/b"], "Pick", out var read);
        var rows = Assert.IsType<StackPanel>(panel.Children[0]);
        Assert.Equal(2, rows.Children.Count);
        Assert.Equal(["C:/a", "C:/b"], read().ToList());
    }

    [AvaloniaFact]
    public void BuildFoldersWithNoFoldersStartsEmpty()
    {
        var form = new SettingsDialog.Form(new AppSettings());
        var panel = form.BuildFolders([], "Pick", out var read);
        Assert.Empty(Assert.IsType<StackPanel>(panel.Children[0]).Children);
        Assert.Empty(read());
    }
}
