using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place panel's kind chooser as the tests use it: its tick boxes, and choosing kinds the way ticking them does.
/// </summary>
internal static class SharedKindChooser
{
    /// <summary>
    /// The kind chooser's tick boxes.
    /// </summary>
    internal static List<CheckBox> Ticks(SharedPlaceWindow.PanelRun r) => [.. r.P.List.GetLogicalDescendants().OfType<CheckBox>()];

    /// <summary>
    /// Chooses these kinds, as ticking them in the panel does.
    /// </summary>
    internal static void Choose(SharedPlaceWindow.PanelRun r, params string[] names)
    {
        r.T.Chosen.Clear();
        r.T.Chosen.AddRange(names);
        typeof(PlacePanel).GetMethod("Chosen", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(r.P, [true]);
        Dispatcher.UIThread.RunJobs();
    }
}
