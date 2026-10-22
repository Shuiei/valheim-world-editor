using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page's controls: a world folder too long for its box keeps its end in sight.
/// </summary>
public sealed class NewWorldPageControlsTests
{
    [AvaloniaFact]
    public void ALongFolderKeepsItsEndInSight()
    {
        using var page = new NewWorldPage([], previewSize: 16);
        var shown = Assert.IsType<TextBlock>(page.Where.ItemTemplate!.Build("~/a/long/path/IronGate/Valheim/worlds_local"));
        Assert.Equal("~/a/long/path/IronGate/Valheim/worlds_local", shown.Text);
        Assert.Equal(TextTrimming.PrefixCharacterEllipsis, shown.TextTrimming);
    }
}
