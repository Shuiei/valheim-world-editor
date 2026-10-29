using TerrainEditor.App;
using TerrainEditor.Save;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>Writes a blueprint for PlanBuild (or as .vbuild) into the export folder (objects only).</summary>
    /// <param name="id">The blueprint's id ("hs:" and its file name, or its store id).</param>
    /// <param name="format">The file extension and format: "blueprint" or "vbuild".</param>
    /// <returns>The file written, null when not.</returns>
    public string? Export(string id, string format)
    {
        if (Load(id, out Said? problem) is not var (clip, name, _, _))
        {
            Message?.Invoke(problem ?? Said.Of("blueprints.msg.exportUnreadable"));
            return null;
        }
        string text = BlueprintFormats.Write(clip, format, name, n => Terrain.PieceCatalog.Get(StableHash.Of(n))?.Category ?? 0);
        string dir = Path.Combine(Store.Directory, "export");
        string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(Homestead.FileName(name)) + "." + format);
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing a blueprint in another mod's format");
            Message?.Invoke(Said.Of("blueprints.msg.writeFailed", path, ex.Message));
            return null;
        }
        Message?.Invoke(Said.Of("blueprints.msg.written", path)
            + (format == "blueprint" ? Said.Of("blueprints.msg.forPlanBuild") : Said.Empty)
            + Said.Of("blueprints.msg.objectsOnly"));
        return path;
    }
}
