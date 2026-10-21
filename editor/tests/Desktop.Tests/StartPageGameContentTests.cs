using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's card in My game for the states the check can find without a running game: installed (with or without the
/// folder it is in), plugin not started yet, no plugin, and no BepInEx.
/// </summary>
public class StartPageGameContentTests
{
    /// <summary>The text of a card's line.</summary>
    private static string LineOf(Control c) => ((TextBlock)c).Text ?? "";

    [AvaloniaFact]
    public void AnInstalledGameSaysToStartValheim()
    {
        var card = Assert.IsType<StackPanel>(new StartPage(new AppSettings()).GameContent(null, "installed", ""));
        Assert.Equal(Lang.T("start.game.startValheim"), LineOf(card.Children[0]));
        Assert.Equal(Lang.T("start.game.installed"), LineOf(card.Children[1]));
    }

    [AvaloniaFact]
    public void AnInstalledGameNamesTheFolderItIsIn()
    {
        var card = Assert.IsType<StackPanel>(new StartPage(new AppSettings()).GameContent(null, "installed", "/games/valheim"));
        Assert.Equal(Lang.T("start.game.installedAt", "/games/valheim"), LineOf(card.Children[1]));
    }

    [AvaloniaFact]
    public void APluginNotStartedYetSaysToStartTheGameOnce()
    {
        var card = Assert.IsType<StackPanel>(new StartPage(new AppSettings()).GameContent(null, "plugin-not-started", ""));
        Assert.Equal(Lang.T("start.game.startOnce"), LineOf(card.Children[0]));
        Assert.Equal(Lang.T("start.game.startOnceHint"), LineOf(card.Children[1]));
    }

    [AvaloniaFact]
    public void WithoutThePluginTheStepsAreShownWithTheBepInExNote()
    {
        var card = Assert.IsType<StackPanel>(new StartPage(new AppSettings()).GameContent(null, "no-plugin", ""));
        Assert.Equal(Lang.T("start.game.needs"), LineOf(card.Children[0]));
        Assert.Equal(Lang.T("start.game.bepInExThere"), LineOf(card.Children[1]));
        Assert.Equal(Lang.T("start.game.step1"), LineOf(card.Children[2]));
    }

    [AvaloniaFact]
    public void WithoutBepInExTheStepsAreShownWithoutTheNote()
    {
        var card = Assert.IsType<StackPanel>(new StartPage(new AppSettings()).GameContent(null, "no-bepinex", ""));
        Assert.Equal(Lang.T("start.game.needs"), LineOf(card.Children[0]));
        Assert.Equal("", LineOf(card.Children[1]));
    }
}
