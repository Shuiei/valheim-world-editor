using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool panel's rows for the settings it shares with the brushes and the Area tool: the bumps of Naturalize
/// and of the Natural look, and Erode's rest angle, each shown with the action that uses it.
/// </summary>
public sealed partial class PathPanel
{
    /// <summary>Naturalize and Natural look: another random pattern for the bumps (the brush's New pattern).</summary>
    internal Button NewPatternButton { get; }

    /// <summary>Erode: the rest angle, in degrees (the brush's, as the Erode brush and the Area tool show it).</summary>
    internal Slider RestSlider { get; }

    /// <summary>Erode: the rest angle's row.</summary>
    internal Control RestRow { get; }

    /// <summary>The bump rows (the brush's Bumps, Bump size and New pattern), for Naturalize and the Natural look.</summary>
    internal Control NaturalRows { get; }

    /// <summary>What the action does, for the actions the brushes and the Area tool have too (empty for the others).</summary>
    private readonly TextBlock _actionHelp = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>A line for the window's message bar, in both languages.</summary>
    public event Action<Said>? Message;

    /// <summary>What an action does, in a line; null for the actions whose rows say it.</summary>
    /// <param name="act">The action.</param>
    private static string? HelpOf(PathTool.Action act) => act switch
    {
        PathTool.Action.Smooth => Lang.T("path.hint.smooth"),
        PathTool.Action.Naturalize => Lang.T("path.hint.natural"),
        PathTool.Action.Erode => Lang.T("path.hint.erode"),
        PathTool.Action.Restore => Lang.T("path.hint.restore"),
        _ => null,
    };

    /// <summary>Shows the current action's help line, or hides it when the action has none.</summary>
    private void ShowActionHelp()
    {
        string? help = HelpOf(_path.Act);
        _actionHelp.Text = help ?? "";
        _actionHelp.IsVisible = help != null;
    }

    /// <summary>Whether the action uses the brush's bumps whatever the Natural look says (Naturalize).</summary>
    /// <param name="act">The action.</param>
    private static bool UsesBumps(PathTool.Action act) => act == PathTool.Action.Naturalize;
}
