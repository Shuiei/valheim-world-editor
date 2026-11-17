namespace ExpandWorld.WorldEditor;

// Expand World Data's persistent events (expand_persistent_events.yaml), as the Persistent event tool places them.
public static partial class ExpandWorldMod
{
    /// <summary>The data file's kind, and its switch in Expand World Data's settings.</summary>
    internal const string EventsKind = "persistent_events", EventsSwitch = "Persistent event data";

    /// <summary>Where an event's objects may stand, as the file's "water" field says.</summary>
    public enum EventWater
    {
        /// <summary>On land and in water (CanSpawnInWater, the default).</summary>
        Anywhere,
        /// <summary>On land only (CanNotSpawnInWater).</summary>
        LandOnly,
        /// <summary>In water only (OnlySpawnInWater).</summary>
        WaterOnly,
    }

    /// <summary>A group of objects an event makes when it starts (an entry of its "objects").</summary>
    /// <param name="Prefabs">The prefabs, one picked for each object.</param>
    /// <param name="Important">Whether more places are tried for each object.</param>
    /// <param name="RandomRotation">Whether each object is turned at random.</param>
    /// <param name="MinCount">The fewest objects.</param>
    /// <param name="MaxCount">The most objects.</param>
    /// <param name="Spacing">The least distance between the event's objects, in metres.</param>
    /// <param name="MaxDistance">The most distance from the event's middle, in metres (0: the event's radius).</param>
    /// <param name="MaxSlope">The steepest ground, in degrees.</param>
    /// <param name="Water">Where they may stand.</param>
    /// <param name="HasData">Whether the group names a data entry (which the tool does not apply).</param>
    public sealed record EventObjects(IReadOnlyList<string> Prefabs, bool Important, bool RandomRotation, int MinCount, int MaxCount, float Spacing, float MaxDistance, float MaxSlope,
        EventWater Water, bool HasData);

    /// <summary>A persistent event of the file.</summary>
    /// <param name="Index">Its place in the file: what the world's save names it by.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Enabled">Whether the game may start it.</param>
    /// <param name="MinRadius">Its smallest radius, in metres.</param>
    /// <param name="MaxRadius">Its largest radius, in metres.</param>
    /// <param name="MinDuration">Its shortest duration, in seconds.</param>
    /// <param name="MaxDuration">Its longest duration, in seconds (both 0: it does not end).</param>
    /// <param name="Location">The location the game makes at its middle ("": none).</param>
    /// <param name="Objects">The objects it makes when it starts.</param>
    public sealed record PersistentEvent(int Index, string Name, bool Enabled, float MinRadius, float MaxRadius, float MinDuration, float MaxDuration, string Location,
        IReadOnlyList<EventObjects> Objects);

    /// <summary>What was found of the persistent events.</summary>
    /// <param name="Events">The file's events, in its order; empty when they could not be read.</param>
    /// <param name="Problem">Why there are none, for the player ("": they were read).</param>
    public sealed record EventFile(IReadOnlyList<PersistentEvent> Events, string Problem);

    /// <summary>
    /// The persistent events of the first BepInEx folder with an Expand World mod: Expand World Data installed, its
    /// "Persistent event data" switch on, and its file read. Lines about the file's fields go to the log.
    /// </summary>
    /// <param name="found">The Expand World mods found.</param>
    public static EventFile ReadEvents(Found found)
    {
        if (!found.Data)
        {
            return new EventFile([], "Expand World Data is not installed in the BepInEx folders the editor knows.");
        }
        if (!Bool(CfgOf(found.DataCfg).GetValueOrDefault(EventsSwitch), false))
        {
            return new EventFile([], $"Turn on \"{EventsSwitch}\" in Expand World Data's settings: the game then reads the events file.");
        }
        List<string> notes = [];
        var yaml = YamlReader.Find(found.Plugins);
        var entries = ReadKind(found.DataFiles, EventsKind, yaml, notes);
        foreach (string note in notes)
        {
            Note($"Expand World: {note}");
        }
        if (entries == null)
        {
            return new EventFile([], $"No {YamlReader.DllName} in the plugins folder: the events file cannot be read.");
        }
        if (entries.Count == 0)
        {
            return new EventFile([], $"No expand_{EventsKind}.yaml yet: start the game once with the switch on, it writes the file.");
        }
        List<string> fieldNotes = [];
        var events = Events(entries, fieldNotes);
        foreach (string note in fieldNotes)
        {
            Note($"Expand World: {note}");
        }
        return new EventFile(events, "");
    }

    /// <summary>The events of the file's entries, each with its place among them.</summary>
    /// <param name="entries">The file's entries, in its order.</param>
    /// <param name="notes">Lines about fields that could not be read, added to.</param>
    internal static List<PersistentEvent> Events(IReadOnlyList<Entry> entries, List<string> notes) =>
        [.. entries.Select((e, i) => Event(new Fields(e, EventsKind, notes), i))];

    private static PersistentEvent Event(Fields f, int index)
    {
        List<EventObjects> objects = [];
        if (f.Raw("objects") is List<object?> list)
        {
            objects.AddRange(list.OfType<Dictionary<string, object?>>().Select(map => Objects(f.Of(map))));
        }
        float minRadius = f.F("minRadius", 100f);
        return new PersistentEvent(index, f.Str("name", $"event {index}"), f.B("enabled", true), minRadius, Math.Max(minRadius, f.F("maxRadius", 300f)),
            f.F("minDuration", 0f), f.F("maxDuration", 0f), f.Str("location"), objects);
    }

    private static EventObjects Objects(Fields f)
    {
        int min = Math.Max(0, f.I("minCount", 0));
        return new EventObjects([.. f.Rows("prefabs").SelectMany(SplitList)], f.B("important", false), f.B("randomRotation", true), min, Math.Max(min, f.I("maxCount", 10)),
            f.F("minDistanceFromOtherObjects", 10f), f.F("maxDistance", 100f), f.F("maxSlope", 90f), WaterOf(f.Str("water")), f.Has("data"));
    }

    private static EventWater WaterOf(string text) => text.Trim().ToLowerInvariant() switch
    {
        "cannotspawninwater" => EventWater.LandOnly,
        "onlyspawninwater" => EventWater.WaterOnly,
        _ => EventWater.Anywhere,
    };
}
