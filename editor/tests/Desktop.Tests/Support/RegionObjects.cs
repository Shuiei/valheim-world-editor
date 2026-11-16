namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Taking objects away with the Region tool, as a player does it: Select objects hands the objects of the chosen
/// kinds inside the selection to the Select tool, and Delete removes them there.
/// </summary>
internal static class RegionObjects
{
    /// <summary>
    /// Select objects applied, then the Select tool's Delete, then back to the Region tool (its selection kept).
    /// Nothing is deleted when no object of the chosen kinds is inside (the panel says so and stays).
    /// </summary>
    /// <param name="w">The window, in the Region tool with a selection.</param>
    public static async Task Remove(MainWindow w)
    {
        w.AreaPanel.Choose(AreaPanel.Act.Select);
        await w.AreaPanel.Apply();
        if (w.Tools.Mode == ToolMode.Select)
        {
            w.View.SelectTool.Delete();
            w.Tools.ChooseMode(ToolMode.Area);
        }
    }
}
