namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Runs a history panel's Take back Claude's changes over a session, for the tests that check what the panel says.
/// </summary>
internal static class HistoryPanelRun
{
    /// <summary>
    /// Takes Claude's changes back from a new history panel on the session, allowing the removal, and returns the message the
    /// panel showed (null when it showed none).
    /// </summary>
    internal static async Task<string?> TakeBackClaudesSaid(EditSession s)
    {
        string? said = null;
        var p = new HistoryPanel(() => s) { Allow = _ => Task.FromResult(true) };
        p.Message += m => said = m.Shown;
        await p.TakeBackClaudes();
        return said;
    }
}
