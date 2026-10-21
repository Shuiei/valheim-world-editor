using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    // Copy and paste: the window copies, pastes and keeps blueprints.
    /// <summary>Asks the window to copy the selection.</summary>
    internal Button CopyButton { get; } = new() { Content = Lang.T("area.label.copy"), FontSize = 12 };

    /// <summary>Asks the window to cut: copy, then remove the originals.</summary>
    internal Button CutButton { get; } = new() { Content = Lang.T("area.label.cut"), FontSize = 12 };

    /// <summary>Asks the window to paste.</summary>
    internal Button PasteButton { get; } = new() { Content = Lang.T("area.label.paste"), FontSize = 12 };

    /// <summary>Asks the window to save the selection as a blueprint.</summary>
    internal Button SaveBlueprintButton { get; } = new() { Content = Lang.T("area.label.saveBlueprint"), FontSize = 12 };

    /// <summary>Asks the window to open the blueprint library.</summary>
    internal Button LibraryButton { get; } = new() { Content = Lang.T("area.label.library"), FontSize = 12 };

    /// <summary>What copying takes.</summary>
    internal TextBlock ClipInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>The Copy, Cut and paste buttons, handled by the window.</summary>
    public event Action? CopyAsked, CutAsked, PasteAsked, SaveBlueprintAsked, LibraryAsked;
}
