using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Presses buttons in tests the way a click does.
/// </summary>
internal static class SharedButtons
{
    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    internal static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
