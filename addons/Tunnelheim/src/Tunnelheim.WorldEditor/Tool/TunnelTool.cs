using System.Globalization;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// The Tunnel tool, on the editor's rail while Tunnelheim is installed: the mod's own actions (round and square pickaxe
/// digs, Add Earth, Smooth Surface, Level Wall, Level Ceiling) where the ground or the rock is clicked, and the editor's
/// shapes made of the same edits (a bore from one click to the next, a room, a shaft, stairs). Each click is one step of
/// the history, saved into the mod's records as the game saves its own edits (<see cref="TunnelEdits"/>).
/// </summary>
/// <param name="host">The editor, for the game's hoe strokes.</param>
/// <param name="log">Where what cannot be read is told.</param>
public sealed class TunnelTool(IEditorHost host, Action<string> log) : IEditorTool
{
    /// <summary>What a click does, in the action list's order.</summary>
    private enum Action
    {
        RoundDig,
        SquareDig,
        AddEarth,
        Smooth,
        Wall,
        Ceiling,
        Bore,
        Room,
        Shaft,
        Stairs,
    }

    private static readonly string[] Labels =
    [
        "Dig (round)", "Dig (square)", "Add Earth", "Smooth Surface", "Level Wall", "Level Ceiling", "Bore", "Room", "Shaft", "Stairs",
    ];

    private static readonly string[] Hints =
    [
        "Click rock or ground: the pickaxe's round dig, along the surface there.",
        "Click rock or ground: the pickaxe's square dig, on a grid of its size.",
        "Click rock: a lump of rock a metre round (not above the building height).",
        "Click rock: eases it towards the surface's own plane.",
        "Click rock: eases it towards an upright wall facing you.",
        "Click rock: eases it towards a flat ceiling.",
        "Click where the bore starts, then where it ends (Esc forgets the start).",
        "Click the middle of the room's floor, in the rock (a tunnel's floor, a slope): it goes up from there.",
        "Click the middle of the shaft's mouth.",
        "Click the top of the stairs: they go down the way you look.",
    ];

    /// <summary>Where a bore starts, once its first click is in.</summary>
    private Vector3? _boreStart;

    /// <summary>
    /// The area's rock as last built (at the last click, or for the preview since): what the preview reads, so that it
    /// shows the edit a click makes without building the rock at every move. Null once it may have changed.
    /// </summary>
    private RockGrid? _rock;

    /// <inheritdoc/>
    public ToolInfo Info { get; } = new("tunnel", "Tunnel", "Dig, fill and shape rock as Tunnelheim does: pick an action, then click the ground or the rock.")
    {
        Icon = "M3 20V11a9 9 0 0 1 18 0v9h-5v-8a4 4 0 0 0-8 0v8z",
    };

    /// <inheritdoc/>
    public IReadOnlyList<ToolControl> Controls { get; } =
    [
        new ChoiceControl("action", "Action", Labels),
        new TextControl("hint", Hints[0]),
        new SliderControl("size", "Size", 0.5f, 4f, 1.5f) { Step = 0.25f, Unit = "m", Help = "The pickaxe's dig size (1 antler, 1.75 black metal, 2.5 the tunneller), the smoothing's reach, a bore's radius, half a shaft's width." },
        new SliderControl("width", "Room width", 2f, 32f, 6f) { Step = 2f, Unit = "m" },
        new SliderControl("depth", "Room depth", 2f, 32f, 6f) { Step = 2f, Unit = "m" },
        new SliderControl("height", "Room height", 2f, 16f, 4f) { Step = 2f, Unit = "m" },
        new SliderControl("length", "Shaft depth", 2f, 64f, 10f) { Step = 1f, Unit = "m" },
        new SliderControl("steps", "Steps", 1f, 32f, 4f) { Step = 1f },
    ];

    /// <inheritdoc/>
    public void OnChosen(IToolContext ctx)
    {
        _rock = null;
        Hint(ctx);
    }

    /// <inheritdoc/>
    public void OnLeft(IToolContext ctx)
    {
        _boreStart = null;
        _rock = null;
        ctx.Preview([]);
    }

    /// <inheritdoc/>
    public void OnPointer(IToolContext ctx, PointerInput input)
    {
        if (input.Target is not (PointerTarget.Ground or PointerTarget.Scene))
        {
            if (input.Action == PointerAction.Press)
            {
                ctx.Status("Click the ground or the rock: not an object, nor the sky.");
            }
            ctx.Preview([]);
            return;
        }
        if (input.Action == PointerAction.Press)
        {
            Act(ctx, input);
        }
        ctx.Preview(Preview(ctx, input));
    }

    /// <inheritdoc/>
    public bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
    {
        if (key == "Escape" && _boreStart != null)
        {
            _boreStart = null;
            ctx.Status("The bore's start is forgotten.");
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public void OnControl(IToolContext ctx, string id)
    {
        if (id == "action")
        {
            _boreStart = null;
            Hint(ctx);
        }
    }

    /// <summary>The area's rock, built now (a click's edits are played on a fresh build).</summary>
    private RockGrid Rock(IAreaView area) => TunnelArea.Build(area, host, log);

    private static Action Chosen(IToolContext ctx) => (Action)Math.Max(0, Math.Min(Labels.Length - 1, ctx.Choice("action")));

    private static void Hint(IToolContext ctx) => ctx.SetText("hint", Hints[(int)Chosen(ctx)]);

    /// <summary>A click: the chosen action's edits played on the area's rock and saved into its records, as one step.</summary>
    private void Act(IToolContext ctx, PointerInput input)
    {
        var action = Chosen(ctx);
        if (action == Action.Bore && _boreStart == null)
        {
            _boreStart = input.Point;
            ctx.Status("The bore starts here: click where it ends.");
            return;
        }
        var area = ctx.Area;
        var grid = Rock(area);
        var ops = Ops(ctx, action, grid, input);
        if (action == Action.AddEarth && !DigOps.MayFill(grid, ops[0]))
        {
            ctx.Status(string.Format(CultureInfo.InvariantCulture, "Too high for Add Earth: the mod adds rock up to {0:0.#} m above the ground.", grid.MaxBuildHeight));
            return;
        }
        _boreStart = null;
        // Played on the rock now: built again for the next look.
        _rock = null;
        var result = TunnelEdits.Make(area, grid, ops, log);
        if (result.Edit == null)
        {
            ctx.Status("Nothing changes there.");
            return;
        }
        ctx.Commit(Labels[(int)action], result.Edit);
        ctx.Status(string.Format(CultureInfo.InvariantCulture, "{0}: {1} edit(s), kept by {2} column record(s).", Labels[(int)action], ops.Count, result.Columns));
    }

    /// <summary>The edits of an action at the pointer.</summary>
    private List<DigOp> Ops(IToolContext ctx, Action action, RockGrid grid, PointerInput input)
    {
        float size = ctx.Number("size");
        var p = input.Point;
        return action switch
        {
            Action.RoundDig => [DigOps.RoundDig(grid, p, input.Direction, size)],
            Action.SquareDig => [DigOps.SquareDig(p, input.Direction, size)],
            Action.AddEarth => [DigOps.AddEarth(p, grid.SurfaceNormal(p, input.Normal))],
            Action.Smooth => [DigOps.Plane(p, grid.SurfaceNormal(p, input.Normal), size)],
            Action.Wall => [DigOps.Plane(p, DigOps.WallNormal(input.Direction, input.Normal), size)],
            Action.Ceiling => [DigOps.Plane(p, DigOps.CeilingNormal, size)],
            Action.Bore => DigOps.Bore(_boreStart!.Value, p, size),
            Action.Room => DigOps.Room(p, ctx.Number("width"), ctx.Number("depth"), ctx.Number("height")),
            Action.Shaft => DigOps.Shaft(p, size, ctx.Number("length")),
            _ => [DigOps.Stairs(p, input.Direction, (int)ctx.Number("steps"))],
        };
    }

    /// <summary>Add Earth's lump where a click would put it, on the rock's own normal; none where it is too high to go.</summary>
    private List<Shape> AddEarthPreview(IToolContext ctx, PointerInput input)
    {
        var rock = _rock ??= Rock(ctx.Area);
        var fill = DigOps.AddEarth(input.Point, rock.SurfaceNormal(input.Point, input.Normal));
        return DigOps.MayFill(rock, fill) ? [PreviewShapes.Ball(fill.Position, fill.Value, adds: true)] : [];
    }

    /// <summary>The box a run of box edits takes out, as the preview of what they dig.</summary>
    private static Shape Covering(IReadOnlyList<DigOp> ops)
    {
        var (min, max) = DigOps.Extent(ops);
        return PreviewShapes.Box((min + max) / 2, (max - min) / 2);
    }

    /// <summary>Where the chosen action would go: a ball for the round edits, a box for the square ones.</summary>
    private List<Shape> Preview(IToolContext ctx, PointerInput input)
    {
        float size = ctx.Number("size");
        var p = input.Point;
        return Chosen(ctx) switch
        {
            Action.SquareDig => [PreviewShapes.Box(DigOps.SquareDig(p, input.Direction, size).Position, new Vector3(size))],
            Action.AddEarth => AddEarthPreview(ctx, input),
            Action.Bore when _boreStart is { } start => [PreviewShapes.Ball(start, size), PreviewShapes.Tube(start, p, size), PreviewShapes.Ball(p, size)],
            Action.Room => [Covering(DigOps.Room(p, ctx.Number("width"), ctx.Number("depth"), ctx.Number("height")))],
            Action.Shaft => [Covering(DigOps.Shaft(p, size, ctx.Number("length")))],
            Action.Stairs => [PreviewShapes.Ball(p, 1f)],
            _ => [PreviewShapes.Ball(p, size)],
        };
    }
}
