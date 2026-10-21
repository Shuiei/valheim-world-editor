using System.Text.Json.Nodes;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>Asks for the details (none: null), and before replacing a blueprint of that name.</summary>
    /// <param name="start">The details to start from.</param>
    /// <param name="clip">The copy, for what it costs.</param>
    /// <param name="keep">The file name being saved over on purpose (the one the building was opened from): no question for it.</param>
    private async Task<Homestead.Details?> Ask(Homestead.Details start, JsonObject clip, string? keep = null)
    {
        var cost = Homestead.Cost(BuildingPicture.FromClip(clip).Select(p => p.Name));
        var d = await AskDetails(start, cost.Describe(8));
        if (d == null || string.IsNullOrWhiteSpace(d.Name))
        {
            return null;
        }
        d = d with { Name = d.Name.Trim(), Description = d.Description.Trim() };
        if (!string.Equals(Homestead.FileName(d.Name), keep, StringComparison.OrdinalIgnoreCase) && HomesteadExists(d.Name)
            && !await Confirm(Lang.T("blueprints.ask.replace.text", d.Name)))
        {
            return null;
        }
        return d;
    }

    /// <summary>
    /// Details…: a blueprint's name, description and tags, changed in its file (renamed with its
    /// picture when the name changes and the new file name is free).
    /// </summary>
    /// <returns>Whether the details were saved.</returns>
    public async Task<bool> EditDetails(Homestead.Entry e)
    {
        var d = await AskDetails(e.Details, Homestead.Cost(e.Kinds).Describe(8));
        if (d == null || string.IsNullOrWhiteSpace(d.Name))
        {
            return false;
        }
        d = d with { Name = d.Name.Trim(), Description = d.Description.Trim() };
        try
        {
            string path = e.Path, target = Path.Combine(Path.GetDirectoryName(path)!, Homestead.FileName(d.Name));
            File.WriteAllText(path, Homestead.WithDetails(File.ReadAllText(path), d));
            if (!string.Equals(target, path, StringComparison.Ordinal) && !File.Exists(target))
            {
                File.Move(path, target);
                if (File.Exists(Path.ChangeExtension(path, ".png")))
                {
                    File.Move(Path.ChangeExtension(path, ".png"), Path.ChangeExtension(target, ".png"));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "saving a blueprint's details");
            Message?.Invoke(Said.Of("blueprints.msg.changeFailed", ex.Message));
            return false;
        }
        Message?.Invoke(Said.Of("blueprints.msg.detailsSaved", d.Name));
        Refresh();
        return true;
    }

    /// <summary>Details with only a name.</summary>
    private static Homestead.Details Named(string name) => new(name, "", []);
}
