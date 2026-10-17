using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>The first five names, separated by commas, with "…" after them when there are more.</summary>
    /// <param name="names">The names (kinds of objects: game data, the same in every language).</param>
    private static string FirstFew(List<string> names) => string.Join(", ", names.Take(5)) + (names.Count > 5 ? "…" : "");

    /// <summary>The "#Tags:" line of a blueprint file (null when it has none).</summary>
    private static string? TagsOf(string text) => text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))?[6..];

    /// <summary>A row's meta line: its parts that are there, separated by " · ".</summary>
    /// <param name="parts">The parts, in order (null: left out).</param>
    private static string Meta(params string?[] parts) => string.Join(" · ", parts.Where(p => p != null));

    /// <summary>The label of the .vbuild buttons: a file format's name, the same in every language.</summary>
    private const string VbuildLabel = ".vbuild";

    /// <summary>What an import says: what was kept, what was left out, and the reminder while Homestead is not installed.</summary>
    /// <param name="name">The name it was saved under.</param>
    /// <param name="count">The objects kept.</param>
    /// <param name="terrain">Whether the file had terrain, which is not kept.</param>
    /// <param name="unknown">The kinds this world cannot make (left out).</param>
    /// <param name="skipped">The lines that could not be read.</param>
    internal Said ImportedSaid(string name, int count, bool terrain, List<string> unknown, int skipped)
    {
        return Said.Of("blueprints.msg.imported", name, count)
            + (terrain ? Said.Of("blueprints.msg.terrainNotKept") : Said.Empty)
            + (unknown.Count > 0 ? Said.Of("blueprints.msg.unknownImported", unknown.Count, FirstFew(unknown)) : Said.Empty)
            + (skipped > 0 ? Said.Of("blueprints.msg.skippedLines", skipped) : Said.Empty)
            + Reminder;
    }
}
