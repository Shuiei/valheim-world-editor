using System.Numerics;
using ValheimWorldEditor.Sdk;
using static ExpandWorld.WorldEditor.ExpandWorldMod;

namespace ExpandWorld.WorldEditor;

/// <summary>
/// The Persistent event tool: starts one of Expand World Data's persistent events (expand_persistent_events.yaml)
/// where the player clicks, as active in the world's save, with the objects the event makes when it starts placed
/// round it (the game makes them only for an event it starts itself), or stops the active event clicked in. The
/// active events are ringed while the tool is chosen.
/// </summary>
/// <param name="host">The editor (its BepInEx folders).</param>
internal sealed class PersistentEventTool(IEditorHost host) : IEditorTool
{
    private const string EventId = "event", ActionId = "action", SizeId = "size", ObjectsId = "objects", InfoId = "info";

    /// <summary>The water's height the game's placement checks use, whatever Expand World Rivers sets.</summary>
    internal const float Water = 30f;

    private static readonly Vector4 NewRing = new(1f, 0.75f, 0.2f, 0.9f), ActiveRing = new(0.35f, 0.75f, 1f, 0.8f), PendingRing = new(0.4f, 1f, 0.5f, 0.8f);

    /// <summary>The events the card's list was made from (read once: the card's list cannot change).</summary>
    private EventFile? _listed;

    /// <summary>The events as last read, when the tool was chosen.</summary>
    private EventFile _file = new([], "");

    private Vector3? _pointer;

    /// <inheritdoc/>
    public ToolInfo Info { get; } = new("persistent-event", "Persistent event",
        "Starts one of Expand World Data's persistent events where you click, with its objects, or stops the one you click in.")
    {
        Icon = "M8 1a7 7 0 1 0 0 14A7 7 0 0 0 8 1zm0 1.5a5.5 5.5 0 1 1 0 11a5.5 5.5 0 0 1 0-11zM8 5a3 3 0 1 0 0 6a3 3 0 0 0 0-6z",
    };

    /// <inheritdoc/>
    public IReadOnlyList<ToolControl> Controls
    {
        get
        {
            _listed ??= _file = ReadEvents(Find(host.BepInExFolders));
            return
            [
                new ChoiceControl(EventId, "Event", _listed.Events.Count > 0 ? [.. _listed.Events.Select(e => e.Name)] : ["(none)"]),
                new ChoiceControl(ActionId, "A click", ["starts the event", "stops the event there"]),
                new SliderControl(SizeId, "Size", 0, 100, 50) { Step = 1, Unit = "%", Help = "Between the event's smallest and largest radius." },
                new ToggleControl(ObjectsId, "Place its objects", true) { Help = "The objects the event makes when it starts, placed round its middle inside the open area." },
                new TextControl(InfoId, ""),
            ];
        }
    }

    /// <inheritdoc/>
    public void OnChosen(IToolContext ctx)
    {
        _file = ReadEvents(Find(host.BepInExFolders));
        Describe(ctx);
        Draw(ctx);
    }

    /// <inheritdoc/>
    public void OnLeft(IToolContext ctx)
    {
        _pointer = null;
        ctx.Preview([]);
    }

    /// <inheritdoc/>
    public bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => false;

    /// <inheritdoc/>
    public void OnControl(IToolContext ctx, string id)
    {
        Describe(ctx);
        Draw(ctx);
    }

    /// <inheritdoc/>
    public void OnPointer(IToolContext ctx, PointerInput input)
    {
        _pointer = input.Target is PointerTarget.None ? null : input.Point;
        if (input.Action == PointerAction.Press && _pointer is { } at)
        {
            if (ctx.Choice(ActionId) == 1)
            {
                Stop(ctx, at);
            }
            else
            {
                Start(ctx, at);
            }
        }
        Draw(ctx);
    }

    /// <summary>
    /// The event chosen; null when the file has none, or no longer lists the events the card does (the file changed
    /// since the editor started: the card's list cannot follow).
    /// </summary>
    private PersistentEvent? Chosen(IToolContext ctx)
    {
        bool same = _listed != null && _listed.Events.Select(e => e.Name).SequenceEqual(_file.Events.Select(e => e.Name));
        int i = ctx.Choice(EventId);
        return same && i >= 0 && i < _file.Events.Count ? _file.Events[i] : null;
    }

    private static float RadiusOf(PersistentEvent e, IToolContext ctx) => e.MinRadius + ((e.MaxRadius - e.MinRadius) * Math.Clamp(ctx.Number(SizeId), 0f, 100f) / 100f);

    private void Describe(IToolContext ctx)
    {
        if (_file.Problem != "")
        {
            ctx.SetText(InfoId, _file.Problem);
        }
        else if (Chosen(ctx) is not { } e)
        {
            ctx.SetText(InfoId, "The events file changed: restart the editor to list its events.");
        }
        else if (ctx.Area is not IWorldEvents { CanEdit: true })
        {
            ctx.SetText(InfoId, "Events are started and stopped in a saved world: not in live mode, nor in the Workshop.");
        }
        else
        {
            string lasts = e.MinDuration <= 0 && e.MaxDuration <= 0 ? "until stopped" : $"{e.MinDuration:0} to {Math.Max(e.MinDuration, e.MaxDuration):0} s";
            string location = e.Location != "" ? $" Its location ({e.Location}) is not placed." : "";
            ctx.SetText(InfoId, $"{RadiusOf(e, ctx):0} m radius, lasts {lasts}, {e.Objects.Count} group(s) of objects.{(e.Enabled ? "" : " Disabled in the file.")}{location}"
                + " Start messages and commands do not run.");
        }
    }

    private void Start(IToolContext ctx, Vector3 at)
    {
        if (Chosen(ctx) is not { } e || ctx.Area is not IWorldEvents { CanEdit: true })
        {
            Describe(ctx);
            ctx.Status("Persistent event: nothing started (see the tool's card).");
            return;
        }
        var area = ctx.Area;
        float radius = RadiusOf(e, ctx);
        var middle = new Vector3(at.X, area.GroundHeight(at.X, at.Z), at.Z);
        var random = new Random();
        double longest = Math.Max(e.MinDuration, e.MaxDuration);
        double duration = longest <= 0 ? 0 : e.MinDuration + (random.NextDouble() * (longest - e.MinDuration));
        var edit = new ToolEdit().StartEvent(e.Index, middle, radius, duration);
        var placed = ctx.IsOn(ObjectsId) ? Scatter(e, middle, radius, area, Game.HasPrefab, random) : new Scattered([], [], 0, false);
        foreach (var (prefab, position, yaw) in placed.Objects)
        {
            edit.Add(prefab, position, new Vector3(0, yaw, 0));
        }
        ctx.Commit($"Start {e.Name}", edit);
        List<string> said = [$"{e.Name} started ({radius:0} m): {placed.Objects.Count} object(s) placed"];
        if (placed.Unknown.Count > 0)
        {
            said.Add($"not placed (not a game object): {string.Join(", ", placed.Unknown.Take(4))}{(placed.Unknown.Count > 4 ? "…" : "")}");
        }
        if (placed.NoRoom > 0)
        {
            said.Add($"{placed.NoRoom} found no place");
        }
        if (placed.Outside)
        {
            said.Add("the event reaches past the open area, where none is placed");
        }
        ctx.Status(string.Join("; ", said) + ". Save the world to write it.");
    }

    private static void Stop(IToolContext ctx, Vector3 at)
    {
        if (ctx.Area is not IWorldEvents { CanEdit: true } events)
        {
            ctx.Status("Persistent event: events are stopped in a saved world.");
            return;
        }
        var hit = events.Active.Where(e => Flat(e.Position, at) <= e.Radius).OrderBy(e => Flat(e.Position, at)).FirstOrDefault();
        if (hit == null)
        {
            ctx.Status("Persistent event: no active event here.");
            return;
        }
        ctx.Commit("Stop event", new ToolEdit().StopEvent(hit.Id));
        ctx.Status("Persistent event stopped (its objects stay). Save the world to write it.");
    }

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    /// <summary>Rings the active events, and the event about to be started under the pointer.</summary>
    private void Draw(IToolContext ctx)
    {
        var area = ctx.Area;
        List<Shape> shapes = [];
        if (area is IWorldEvents events)
        {
            shapes.AddRange(events.Active.Select(e => Ring(area, e.Position, e.Radius, e.Pending ? PendingRing : ActiveRing)).OfType<Shape>());
        }
        if (_pointer is { } at && ctx.Choice(ActionId) == 0 && Chosen(ctx) is { } chosen && Ring(area, at, RadiusOf(chosen, ctx), NewRing) is { } ring)
        {
            shapes.Add(ring);
        }
        ctx.Preview(shapes);
    }

    /// <summary>A band on the ground (on the water, over the sea) along a circle, where it is inside the area; null when none of it is.</summary>
    internal static Shape? Ring(IAreaView area, Vector3 middle, float radius, Vector4 color)
    {
        const int Steps = 128;
        float half = Math.Clamp(radius / 100f, 0.5f, 2f);
        List<Vector3> corners = [];
        List<int> triangles = [];
        for (int i = 0; i < Steps; i++)
        {
            double a0 = i * Math.Tau / Steps, a1 = (i + 1) * Math.Tau / Steps;
            Vector3[] quad = [At(a0, radius - half), At(a0, radius + half), At(a1, radius + half), At(a1, radius - half)];
            if (!quad.All(p => Inside(area, p.X, p.Z)))
            {
                continue;
            }
            int n = corners.Count;
            // On the ground, or on the water over it.
            corners.AddRange(quad.Select(p => p with { Y = Math.Max(area.GroundHeight(p.X, p.Z), Water) + 0.4f }));
            triangles.AddRange([n, n + 1, n + 2, n, n + 2, n + 3]);
        }
        return corners.Count == 0 ? null : new Shape([.. corners], [.. triangles], color) { WorldSpace = true, BothSides = true };

        Vector3 At(double angle, float r) => new(middle.X + (float)(Math.Sin(angle) * r), 0, middle.Z + (float)(Math.Cos(angle) * r));
    }

    private static bool Inside(IAreaView area, float x, float z) => x >= area.MinX && x < area.MaxX && z >= area.MinZ && z < area.MaxZ;

    /// <summary>What an event's objects come to round a middle.</summary>
    /// <param name="Objects">The objects placed: prefab, place, turn round the vertical in degrees.</param>
    /// <param name="Unknown">The prefabs named that are no object of the game (a blueprint, a location): not placed.</param>
    /// <param name="NoRoom">How many objects found no place.</param>
    /// <param name="Outside">Whether places tried were outside the area.</param>
    internal sealed record Scattered(List<(string Prefab, Vector3 Position, float Yaw)> Objects, List<string> Unknown, int NoRoom, bool Outside);

    /// <summary>
    /// The objects an event makes when it starts, placed as its file says: for each group a number of objects between
    /// its counts, each of a prefab picked among the group's, at a place within the group's distance of the middle
    /// (the event's radius when 0) that is inside the area, no steeper than the group allows, in or out of the water as
    /// it asks, and no nearer the event's other objects than its spacing; a few places are tried for each (more for an
    /// important group).
    /// </summary>
    /// <param name="e">The event.</param>
    /// <param name="middle">Its middle.</param>
    /// <param name="radius">Its radius.</param>
    /// <param name="area">The area open.</param>
    /// <param name="known">Whether a name is an object of the game.</param>
    /// <param name="random">Where the numbers come from.</param>
    internal static Scattered Scatter(PersistentEvent e, Vector3 middle, float radius, IAreaView area, Func<string, bool> known, Random random)
    {
        var result = new Scattered([], [], 0, false);
        int noRoom = 0;
        bool outside = false;
        foreach (var group in e.Objects)
        {
            List<string> prefabs = [.. group.Prefabs.Where(known)];
            result.Unknown.AddRange(group.Prefabs.Where(p => !known(p) && !result.Unknown.Contains(p)));
            if (prefabs.Count == 0)
            {
                continue;
            }
            int count = random.Next(group.MinCount, group.MaxCount + 1);
            float reach = group.MaxDistance > 0 ? group.MaxDistance : radius;
            for (int n = 0; n < count; n++)
            {
                Vector3? place = null;
                for (int attempt = 0; attempt < (group.Important ? 100 : 10) && place == null; attempt++)
                {
                    double angle = random.NextDouble() * Math.Tau, distance = Math.Sqrt(random.NextDouble()) * reach;
                    float x = middle.X + (float)(Math.Sin(angle) * distance), z = middle.Z + (float)(Math.Cos(angle) * distance);
                    if (!Inside(area, x - 1, z - 1) || !Inside(area, x + 1, z + 1))
                    {
                        outside = true;
                        continue;
                    }
                    float ground = area.GroundHeight(x, z);
                    bool wet = ground < Water;
                    if (wet && group.Water == EventWater.LandOnly || !wet && group.Water == EventWater.WaterOnly || Slope(area, x, z) > group.MaxSlope)
                    {
                        continue;
                    }
                    var spot = new Vector3(x, wet && group.Water == EventWater.WaterOnly ? Water : ground, z);
                    if (result.Objects.All(o => Flat(o.Position, spot) >= group.Spacing))
                    {
                        place = spot;
                    }
                }
                if (place is { } found)
                {
                    result.Objects.Add((prefabs[random.Next(prefabs.Count)], found, group.RandomRotation ? (float)(random.NextDouble() * 360) : 0f));
                }
                else
                {
                    noRoom++;
                }
            }
        }
        return result with { NoRoom = noRoom, Outside = outside };
    }

    /// <summary>The ground's slope at a point, in degrees, from its heights a metre apart.</summary>
    private static float Slope(IAreaView area, float x, float z)
    {
        float dx = (area.GroundHeight(x + 0.5f, z) - area.GroundHeight(x - 0.5f, z)), dz = (area.GroundHeight(x, z + 0.5f) - area.GroundHeight(x, z - 0.5f));
        return (float)(Math.Atan(Math.Sqrt((dx * dx) + (dz * dz))) * 180 / Math.PI);
    }
}
