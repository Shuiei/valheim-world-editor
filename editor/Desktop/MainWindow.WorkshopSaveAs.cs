using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Save blueprint without asking (Claude's save_blueprint): the plot's building under these details. Refused when
    /// there is no building piece, when pieces would fall in game (unless evenIfFalling), or when another blueprint of
    /// that name exists (unless overwrite; the one the building was opened from or last saved as may always be saved
    /// over).
    /// </summary>
    /// <returns>Null when saved, else why not.</returns>
    internal string? SaveWorkshopAs(Homestead.Details details, bool overwrite, bool evenIfFalling)
    {
        if (!InWorkshop || Session is not { } s)
        {
            return "Blueprints are saved from the Workshop: open_workshop first.";
        }
        View.SelectTool.Commit();
        if (Workshop.Pieces(s.Scene) == 0)
        {
            return "There is no building piece on the plot to save.";
        }
        // Worked out even when the Support check is off: its switch only shows the colours.
        if (SolveSupport(s).Result is { Breaking: > 0 } r && !evenIfFalling)
        {
            return $"{r.Breaking} piece(s) would fall in game (support_check lists them): fix them, or save anyway with evenIfFalling.";
        }
        // The library's folder as it is now (Homestead may have been installed or moved since).
        Blueprints.Refresh();
        string path = Path.Combine(Blueprints.Status.Folder, Homestead.FileName(details.Name));
        if (File.Exists(path) && !overwrite && !string.Equals(path, _workshopFile, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return $"There is a blueprint called {details.Name} already: ask the user before replacing it (overwrite), or choose another name.";
        }
        var clip = Workshop.Building(s.Scene, details.Name, _workshopLift);
        if (Blueprints.SaveBuildingAs(clip, details) is string problem)
        {
            return problem;
        }
        WorkshopSavedAs(s, details);
        return null;
    }
}
