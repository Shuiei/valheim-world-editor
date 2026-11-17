namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>Persistent events to start and to stop, by id and kind.</summary>
    private readonly Dictionary<(int Id, bool Stop), EventEdit> _events = [];

    /// <summary>The next id for an event started here (negative, counted down).</summary>
    private int _nextEventId = -1;

    /// <summary>A copy of the persistent events to start and to stop.</summary>
    public List<EventEdit> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events.Values];
            }
        }
    }

    /// <summary>How many persistent events are to be started or stopped.</summary>
    public int EventCount
    {
        get
        {
            lock (_lock)
            {
                return _events.Count;
            }
        }
    }

    /// <summary>An id for an event started here: negative, never given twice.</summary>
    public int NewEventId()
    {
        lock (_lock)
        {
            return _nextEventId--;
        }
    }

    /// <summary>Asks for an event to be started or stopped, or drops that.</summary>
    /// <param name="edit">The start or the stop.</param>
    /// <param name="on">Whether it is asked for.</param>
    public void SetEvent(EventEdit edit, bool on)
    {
        lock (_lock)
        {
            if (on)
            {
                _events[(edit.Id, edit.Stop)] = edit;
                _nextEventId = Math.Min(_nextEventId, edit.Id - 1);
            }
            else
            {
                _events.Remove((edit.Id, edit.Stop));
            }
            Version++;
        }
    }
}
