
namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// The things of these ids that can be moved or changed (indices into the scene's things, each once), and why the
    /// others cannot: not an object of the area, or a runestone (the game builds it from its location).
    /// </summary>
    internal static (List<int> Ok, List<string> Problems) Usable(WorldScene scene, IEnumerable<int> ids)
    {
        var ok = new List<int>();
        var problems = new List<string>();
        foreach (int id in ids.Distinct())
        {
            if (id < 0 || id >= scene.Things.Count || scene.Things[id].Gone)
            {
                problems.Add($"{id}: not an object of the area");
            }
            else if (scene.Things[id].Runestone)
            {
                problems.Add($"{id}: a runestone, which the game builds from its location: it can only be taken away");
            }
            else
            {
                ok.Add(id);
            }
        }
        return (ok, problems);
    }

    /// <summary>
    /// The answer when nothing could be done: how it begins, then why for each one given, or that none was given (an
    /// empty list).
    /// </summary>
    /// <param name="nothing">How the answer begins ("Nothing moved").</param>
    /// <param name="problems">Why each one given could not be done.</param>
    internal static string NothingDone(string nothing, IReadOnlyCollection<string> problems) =>
        $"{nothing}: " + (problems.Count > 0 ? string.Join("; ", problems) : "none was given.");

    /// <summary>
    /// The scene whose things the ids last given were indices into, and its read count then (list_objects,
    /// get_selection, place_pieces, each change's new ids...). Null until ids were given.
    /// </summary>
    private (WorldScene Scene, int Reads)? _ids;

    /// <summary>
    /// Notes that the ids given now are indices into this scene's things as they were at that read count, taken before
    /// the ids were worked out: a save that reads the world again meanwhile (on another thread) leaves them refused.
    /// </summary>
    /// <param name="scene">The scene the ids are of.</param>
    /// <param name="reads">Its read count before the ids were worked out.</param>
    private void GaveIds(WorldScene scene, int reads) => _ids = (scene, reads);

    /// <summary>
    /// Why the ids given before cannot be used on this scene: they were given for another area (opened since, or this
    /// one opened again), or for this one before the world was read again (after a save), which lists its things anew;
    /// whatever objects have those indices now would be hit. Null when they fit (or none was given yet: each is checked
    /// on its own).
    /// </summary>
    private string? StaleIds(WorldScene scene) => _ids is { } ids && (ids.Scene != scene || ids.Reads != scene.Reads)
        ? $"The ids are from {(ids.Scene != scene ? "another area" : "before the world was read again")}: list_objects (or get_selection) again for this one."
        : null;

    /// <summary>
    /// Why the object with that id cannot be read or changed on this scene: the ids given before no longer fit it
    /// (<see cref="StaleIds"/>), or there is no such object (out of range, or taken away); null when it is one.
    /// </summary>
    internal string? ObjectProblem(WorldScene scene, int id) =>
        StaleIds(scene) ?? (id < 0 || id >= scene.Things.Count || scene.Things[id].Gone ? "No object with that id in the area." : null);

    /// <summary>
    /// The start of a change to objects by their ids: why none may be made now (no area open, live with Auto on, or the
    /// ids given before no longer fit the area), or null when it may go on.
    /// </summary>
    private string? EditStart() => CannotEdit() ?? StaleIds(_w.Session!.Scene);
}
