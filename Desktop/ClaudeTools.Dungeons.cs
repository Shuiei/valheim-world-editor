using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// A Dungeon tool of its own on the open area's dungeon of that id (the view's tool is left as the user has it),
    /// telling its messages to said; or why there is none.
    /// </summary>
    internal (DungeonTool? Tool, string? Problem) DungeonOf(int id, Action<Said>? said = null, int seed = 1)
    {
        if (_w.Session?.Scene is not { } scene || _w.InWorkshop)
        {
            return (null, "Dungeons are edited in a world's area: open_area where one is (list_dungeons).");
        }
        var tool = new DungeonTool { Scene = scene, Random = new Random(seed).NextSingle };
        var d = tool.All.FirstOrDefault(x => x.Index == id);
        if (d == null)
        {
            return (null, $"No dungeon with id {id} in the area (list_dungeons).");
        }
        if (d.Problem != null)
        {
            return (null, d.Problem.English);
        }
        tool.Open(d);
        if (said != null)
        {
            tool.Message += said;
        }
        return (tool, null);
    }

    /// <summary>
    /// The start of a change to a dungeon: why none may be made now, else the dungeon's tool (see <see cref="DungeonOf"/>),
    /// with the messages its change says collected for the answer.
    /// </summary>
    /// <param name="id">The dungeon's id (list_dungeons).</param>
    /// <param name="seed">Any number: other contents and door rolls.</param>
    /// <returns>The tool (null when none), the messages it said, and why there is no tool or no edit.</returns>
    private (DungeonTool? Tool, Said Said, string? Problem) DungeonEdit(int id, int seed = 1)
    {
        if (CannotEdit() is string why)
        {
            return (null, Said.Empty, why);
        }
        var said = Said.Empty;
        var (tool, problem) = DungeonOf(id, m => said = m, seed);
        return (tool, said, problem);
    }

    /// <summary>The session's last step, to tell afterwards whether a tool made one (it may also make none and say why).</summary>
    private EditSession.Change? LastStep() => _w.Session?.UndoList is { Count: > 0 } steps ? steps[^1] : null;

    /// <summary>Whether a step was made since <see cref="LastStep"/> gave this one.</summary>
    private bool Stepped(EditSession.Change? before) => !ReferenceEquals(LastStep(), before);

    /// <summary>The answer when a dungeon tool made no step: what the tool said, else that nothing changed.</summary>
    private static string Unchanged(Said said) => said.English.Length > 0 ? said.English : "Nothing changed.";

    /// <summary>
    /// The answer after a dungeon change: what the tool said (the status bar shows it in the language in use, Claude is
    /// answered in English), the dungeon's new id and its room count.
    /// </summary>
    private string AfterDungeon(DungeonTool tool, Said said)
    {
        _w.AfterClaudeEdit(said);
        return tool.Dungeon is { } d ? $"{said.English} The dungeon's id is now {d.Index} ({d.Rooms.Count} rooms). Now pending: {Pending()}" : said.English;
    }
}
