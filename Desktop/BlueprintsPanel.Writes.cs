using System.Text.Json.Nodes;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>Writes a Homestead blueprint and its picture (named after the blueprint, replacing one of that name).</summary>
    /// <returns>The path of the file written.</returns>
    private string WriteHomestead(JsonObject clip, Homestead.Details details, string? world)
    {
        Directory.CreateDirectory(Status.Folder);
        string path = Path.Combine(Status.Folder, Homestead.FileName(details.Name));
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, Homestead.Write(clip, details.Name, "Valheim World Editor", world, DateTime.Now, details.Description, details.Tags));
        File.Move(tmp, path, overwrite: true);
        File.WriteAllBytes(Path.ChangeExtension(path, ".png"), BuildingPicture.Draw(BuildingPicture.FromClip(clip), Models()));
        return path;
    }

    /// <summary>
    /// A building written as a Homestead blueprint with these details, without asking (replacing one of that name),
    /// with its picture; Message tells what was done.
    /// </summary>
    /// <returns>Null when saved, else why not, in English (Claude's save_blueprint answers with it).</returns>
    internal string? SaveBuildingAs(JsonObject clip, Homestead.Details details)
    {
        Status = FindHomestead();
        try
        {
            WriteHomestead(clip, details, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "saving the Workshop's building as a blueprint");
            var problem = Said.Of("blueprints.msg.saveFailed", ex.Message);
            Message?.Invoke(problem);
            return problem.English;
        }
        int count = (clip["objects"] as JsonArray)?.Count ?? 0;
        Message?.Invoke(Said.Of("blueprints.msg.savedBuilding", details.Name, count) + Reminder);
        Refresh();
        return null;
    }

    /// <summary>A blueprint file written or removed; when the folder refuses (read-only, locked), says so (false).</summary>
    private bool TryWrite(Action write)
    {
        try
        {
            write();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing into the blueprints folder");
            Message?.Invoke(Said.Of("blueprints.msg.folderFailed", ex.Message));
            return false;
        }
    }

    /// <summary>Whether a Homestead blueprint of that name is already there.</summary>
    private bool HomesteadExists(string name) => File.Exists(Path.Combine(Status.Folder, Homestead.FileName(name)));

    /// <summary>Whether a copy holds ground (heights other than the "none" marker, -32768).</summary>
    internal static bool HasGround(JsonObject clip) => clip["rel"] is JsonArray rel && rel.Any(v => v != null && (int)v != -32768);

    /// <summary>The sentence saying a copy's ground is not kept, when it has some (empty otherwise).</summary>
    /// <param name="clip">The copy.</param>
    internal static Said GroundNotKept(JsonObject clip) => HasGround(clip) ? Said.Of("blueprints.msg.groundNotKept") : Said.Empty;

    /// <summary>The sentence ending a message while Homestead is not installed, empty when it is.</summary>
    private Said Reminder => Status.Installed ? Said.Empty : Said.Of("blueprints.msg.notInstalledYet");
}
