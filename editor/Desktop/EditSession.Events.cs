using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Persistent events: starting and stopping them as a step's entries say, and the entries a removal puts back.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>Asks for the events' starts and stops, or drops them, as one side of a step's entries says.</summary>
    /// <param name="events">The step's entries.</param>
    /// <param name="after">The side to set: after the step (do, redo) or before it (undo).</param>
    private void SetEvents(IReadOnlyCollection<(EventEdit Edit, bool Before, bool After)> events, bool after)
    {
        foreach (var (e, b, a) in events)
        {
            Edits.SetEvent(e, after ? a : b);
        }
    }

    /// <summary>
    /// The event entries a removal puts back: those whose start or stop is still as the step left it.
    /// </summary>
    /// <param name="c">The change taken out.</param>
    internal (EventEdit Edit, bool Before, bool After)[] EventsToRevert(Change c)
    {
        var asked = Edits.Events.ToHashSet();
        return [.. c.Events.Where(e => e.Before != e.After && asked.Contains(e.Edit) == e.After).Select(e => (e.Edit, e.After, e.Before))];
    }
}
