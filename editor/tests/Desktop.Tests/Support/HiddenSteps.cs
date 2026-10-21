using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>Checks of the steps that add mods' data holders (hidden objects).</summary>
internal static class HiddenSteps
{
    /// <summary>
    /// The session's history holds one step, adding one hidden object, which is there until the step is undone.
    /// </summary>
    /// <returns>The step's name.</returns>
    internal static string OneAddedThenUndone(EditSession s)
    {
        var step = Assert.Single(s.UndoList);
        int id = Assert.Single(step.Hidden).Id;
        Assert.False(s.Edits.IsGone(id));
        Assert.True(s.Undo());
        Assert.True(s.Edits.IsGone(id));
        return step.Label;
    }
}
