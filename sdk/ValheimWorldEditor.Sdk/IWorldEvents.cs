using System.Collections.Generic;
using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The world's persistent events (the game's long lasting events over a part of the world, as the Jotun invasion): the
/// area a tool or a scene layer is handed is one (<c>area is IWorldEvents events</c>). They are the world's, not the
/// area's: every active event is listed, wherever it is.
/// </summary>
public interface IWorldEvents
{
    /// <summary>
    /// Whether events can be started and stopped: the world is a saved one whose file holds them. False in live mode
    /// and in the Workshop (the list is then empty, and a tool's event edits are left out).
    /// </summary>
    bool CanEdit { get; }

    /// <summary>The active events, with the edits not saved yet: those started here, without those stopped here.</summary>
    IReadOnlyList<WorldEvent> Active { get; }
}

/// <summary>An active persistent event.</summary>
public sealed class WorldEvent
{
    /// <summary>An active event.</summary>
    /// <param name="id">Its id: the game's for a saved one, a negative one of the editor's for one not saved yet.</param>
    /// <param name="source">Which event it is: its place in the game's list of events (a mod's file's, when it sets them).</param>
    /// <param name="position">Its middle, in world metres.</param>
    /// <param name="radius">Its radius, in metres.</param>
    /// <param name="duration">How long it lasts, in seconds; 0 or less: until it is stopped.</param>
    public WorldEvent(int id, int source, Vector3 position, float radius, double duration)
    {
        Id = id;
        Source = source;
        Position = position;
        Radius = radius;
        Duration = duration;
    }

    /// <summary>Its id: the game's for a saved one, a negative one of the editor's for one not saved yet.</summary>
    public int Id { get; }

    /// <summary>Which event it is: its place in the game's list of events.</summary>
    public int Source { get; }

    /// <summary>Its middle, in world metres.</summary>
    public Vector3 Position { get; }

    /// <summary>Its radius, in metres.</summary>
    public float Radius { get; }

    /// <summary>How long it lasts, in seconds; 0 or less: until it is stopped.</summary>
    public double Duration { get; }

    /// <summary>Whether the editor started it and it is not saved yet.</summary>
    public bool Pending => Id < 0;
}
