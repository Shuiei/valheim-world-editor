using System.Text.Json.Nodes;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>
    /// Saves the clipboard as a Homestead blueprint (asks for the name; one with that name is replaced
    /// after asking). Homestead blueprints hold pieces only: copied ground is not kept.
    /// </summary>
    /// <returns>The name it was saved under, null when not saved.</returns>
    public async Task<string?> Save()
    {
        var clip = _paste.Clip;
        if (clip == null)
        {
            Message?.Invoke(Said.Of("blueprints.msg.copyFirst"));
            return null;
        }
        Status = FindHomestead();
        var json = CopyFormat.ToJson(clip.WithoutBuilders());
        if (await Ask(Named(clip.Name ?? Lang.T("blueprints.defaultName", DateTime.Now)), json) is not { } details)
        {
            return null;
        }
        string name = details.Name;
        clip.Name = name;
        json = CopyFormat.ToJson(clip.WithoutBuilders());
        try
        {
            WriteHomestead(json, details, _scene()?.World?.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "saving the copy as a blueprint");
            Message?.Invoke(Said.Of("blueprints.msg.saveFailed", ex.Message));
            return null;
        }
        Message?.Invoke(Said.Of("blueprints.msg.saved", name, clip.Objects.Count) + GroundNotKept(json) + Reminder);
        Refresh();
        return name;
    }

    /// <summary>
    /// The Workshop's building as a Homestead blueprint (asks for the details, suggested; one with that
    /// name is replaced after asking, except the file it was opened from). What was saved, or null.
    /// </summary>
    public async Task<Homestead.Details?> SaveBuilding(JsonObject clip, Homestead.Details suggested, string? openedFrom = null)
    {
        Status = FindHomestead();
        if (await Ask(suggested, clip, openedFrom != null ? Path.GetFileName(openedFrom) : null) is not { } details)
        {
            return null;
        }
        return SaveBuildingAs(clip, details) == null ? details : null;
    }
}
