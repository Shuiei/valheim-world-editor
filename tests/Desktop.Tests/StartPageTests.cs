using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.StartPageRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page: the bridge address last connected to, a world folder typed with ~, the home page's Homestead line,
/// and the saved-server tile on it. Saved servers and settings go to the tests' own files; it runs alone.
/// </summary>
[Collection("DataDir")]
public class StartPageTests
{
    [AvaloniaFact]
    public void TheBridgeAddressLastConnectedToIsFilledIn()
    {
        using var r = new Run();
        Assert.Equal("127.0.0.1:5182", r.P.LiveUrl.Text);
        Assert.Equal("http://10.0.0.5:15182", new StartPage(new AppSettings { LiveUrl = "http://10.0.0.5:15182" }).LiveUrl.Text);
    }

    [AvaloniaFact]
    public void AFolderTypedWithAHomeTildeIsUnderstood()
    {
        using var r = new Run();
        r.P.OpenFolder("\"~/no-world-for-vwe-tests-here\"");
        Assert.NotEqual("", r.P.PathError.Text);
        Assert.Empty(r.Opened);
    }

    [AvaloniaFact]
    public void HomesteadInstalledIsSaidWithWhereItIs()
    {
        using var r = new Run();
        r.P.ShowHomestead(new Homestead.Status(true, "1.4.0", ["Steam", "Steam", "r2modman"], "/somewhere"));
        Assert.Equal("Homestead 1.4.0 is installed (Steam, r2modman): the editor's blueprints show in its hammer tab, to build in game.", r.P.HomesteadText.Text);
    }
}
