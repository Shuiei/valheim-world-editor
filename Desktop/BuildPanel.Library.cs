using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>The Pieces, Library and generator views, one shown at a time.</summary>
    private readonly StackPanel _pieces, _library, _dungeon;

    // ---- The Library tab: Homestead's blueprints, opened on the plot, added to it, or dragged onto it.
    /// <summary>Shows the pieces.</summary>
    internal Button PiecesTab { get; } = new() { Content = Lang.T("build.label.pieces"), FontSize = 12, Padding = new Thickness(8, 2) };

    /// <summary>Shows the Library: the blueprints.</summary>
    internal Button LibraryTab { get; } = new() { Content = Lang.T("build.label.library"), FontSize = 12, Padding = new Thickness(8, 2) };

    /// <summary>Searches the blueprints by name, description and tags (every word must match).</summary>
    internal TextBox LibrarySearch { get; } = BlueprintsPanel.SearchBox();

    /// <summary>Imports another mod's blueprint file (the window handles it).</summary>
    internal Button ImportButton { get; } = BlueprintsPanel.ImportFileButton();

    /// <summary>The blueprint rows.</summary>
    internal StackPanel LibraryList { get; } = new() { Spacing = 4 };

    /// <summary>Generate dungeon: the Dungeon panel's generator, made of building pieces, onto the plot.</summary>
    internal Button GenerateButton { get; } = new() { Content = Lang.T("build.label.generate"), FontSize = 12 };

    /// <summary>From the generator back to the Library.</summary>
    internal Button BackToLibrary { get; } = new() { Content = Lang.T("build.label.backToLibrary"), FontSize = 12 };

    /// <summary>The dungeon generator, in its Workshop form.</summary>
    internal DungeonGenPanel Dungeon { get; } = new(inWorkshop: true);

    /// <summary>Shows the Library (true, listed again) or the pieces (false).</summary>
    public void ShowLibrary(bool on)
    {
        _library.IsVisible = on;
        _pieces.IsVisible = !on;
        _dungeon.IsVisible = false;
        PiecesTab.Classes.Set("on", !on);
        LibraryTab.Classes.Set("on", on);
        if (on)
        {
            RenderLibrary();
        }
    }

    /// <summary>The generator, in the Library's place (its tab stays lit).</summary>
    internal void ShowDungeon()
    {
        _library.IsVisible = _pieces.IsVisible = false;
        _dungeon.IsVisible = true;
        PiecesTab.Classes.Set("on", false);
        LibraryTab.Classes.Set("on", true);
    }
}
