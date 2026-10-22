using Avalonia.Controls;
using Avalonia.Layout;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Controls laid out in a window, to check that what they show fits: a control's natural width (with no limit) against
/// the width it is given.
/// </summary>
internal static class LaidOut
{
    /// <summary>
    /// Shows the content in a window of that width, lays it out, runs the check and closes the window.
    /// </summary>
    /// <param name="content">What the window shows.</param>
    /// <param name="width">The window's width.</param>
    /// <param name="check">What is checked once it is laid out.</param>
    public static void In(Control content, double width, Action check)
    {
        var window = new Window { Width = width, Height = 900, Content = content };
        try
        {
            window.Show();
            window.UpdateLayout();
            check();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The width a control takes when nothing limits it: shown alone in a row as wide as it wants.
    /// </summary>
    /// <param name="fresh">A control not shown anywhere else.</param>
    public static double NaturalWidth(Control fresh)
    {
        double width = 0;
        In(new StackPanel { Orientation = Orientation.Horizontal, Children = { fresh } }, 4000, () => width = fresh.Bounds.Width);
        return width;
    }
}
