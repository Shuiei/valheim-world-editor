using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// A blueprint added to the building on the plot (at a world point x, z; null: the middle), turned by degrees,
    /// as one step named label (null: "Added" and its name). Only in the Workshop.
    /// </summary>
    /// <param name="path">The blueprint file (its name picks the format).</param>
    /// <param name="at">Where its anchor goes (world x, z); null for the plot's middle.</param>
    /// <param name="turn">Degrees it is turned round its anchor.</param>
    /// <param name="label">The history's name for the step; null: "Added" and its name.</param>
    /// <param name="text">The blueprint's text when it is not a file of the Library; null reads <paramref name="path"/>.</param>
    internal void AddToWorkshop(string path, Vector2? at, float turn = 0, string? label = null, string? text = null)
    {
        if (!InWorkshop || Session is not { } s)
        {
            return;
        }
        try
        {
            var (placed, unknown, _, terrain) = Workshop.Add(s, path, text, at, turn, label: label);
            foreach (var (cell, y) in terrain)
            {
                _workshopTerrain[cell] = _workshopTerrain.TryGetValue(cell, out float was) ? MathF.Min(was, y) : y;
            }
            RefreshSupport();
            string name = (text == null ? Homestead.Read(path) : Homestead.Read(path, text.Split('\n')))?.Name ?? Path.GetFileNameWithoutExtension(path);
            Msg(Said.Of("workshop.msg.added", name, placed) + UnknownLeftOut(unknown) + Said.Of("workshop.msg.undoHint"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "adding a blueprint to the Workshop's plot");
            Msg("workshop.msg.addFailed", ex.Message);
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// The Workshop, empty or with a blueprint's pieces on it. Leaves the open world first (asking when something is
    /// not saved), and a building not saved as a blueprint (asking too).
    /// </summary>
    /// <param name="path">The blueprint to open, or null for a blank plot.</param>
    internal async Task OpenWorkshop(string? path)
    {
        // A second open while one is under way (a double click while the game's files are read) would ask and show
        // the plot twice.
        if (_openingWorkshop)
        {
            Msg("workshop.msg.alreadyOpening");
            return;
        }
        _openingWorkshop = true;
        try
        {
            await OpenWorkshopOnce(path);
        }
        finally
        {
            _openingWorkshop = false;
        }
    }

    /// <summary>
    /// True while <see cref="OpenWorkshop"/> runs.
    /// </summary>
    private bool _openingWorkshop;
}
