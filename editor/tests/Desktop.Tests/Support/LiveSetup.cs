using Avalonia.Controls;
using Avalonia.LogicalTree;
using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The live tests' setup: the running world opened live from the stand-in game, a raise, a button press and the text shown.
/// </summary>
internal static class LiveSetup
{
    internal static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    /// <summary>
    /// Opens the running world of the stand-in game, live.
    /// </summary>
    internal static Task<WorldSession> OpenOn(FakeGame game) => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());

    /// <summary>
    /// Raises the ground at the middle of the area, as one step labelled raise.
    /// </summary>
    internal static void Raise(EditSession s) => s.Shape(32, 32, Two, 3, 0, "raise");

    /// <summary>
    /// Presses the button, as a click does.
    /// </summary>
    internal static void Press(Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Every text shown in a control and its children, joined with " | ".
    /// </summary>
    internal static string Texts(Control? c) => c == null ? "" : string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Prepend((c as TextBlock)?.Text).Where(t => !string.IsNullOrEmpty(t)));
}
