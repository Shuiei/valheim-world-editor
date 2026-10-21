using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The Canals tool, the mod's own on the editor's rail (the tool hooks): click the ground along a canal's way, then
/// Dig (Enter) adds a canal object keeping that path (Canals draws it); Shift + click a canal fills it in (removes it);
/// Ctrl + click a canal gives it the sliders' width and depth (changes it). Backspace takes the last point back, Esc
/// clears them.
/// </summary>
public sealed class CanalTool : IEditorTool
{
    /// <summary>How near a click must be to a canal's path to pick it, in metres.</summary>
    private const float PickReach = 3f;

    /// <summary>The path laid out so far.</summary>
    private readonly List<Vector2> _points = [];

    /// <summary>Where the pointer last met the ground, for the preview's last stretch.</summary>
    private Vector3? _cursor;

    /// <inheritdoc/>
    public ToolInfo Info { get; } = new("canals", "Canals", "Click along the ground to lay out a canal, then Dig (Enter). Shift + click a canal to fill it in; Ctrl + click one to give it these width and depth.")
    {
        Icon = "M3 17c3-2 6-2 9 0s6 2 9 0M3 12c3-2 6-2 9 0s6 2 9 0M5 7h14",
    };

    /// <inheritdoc/>
    public IReadOnlyList<ToolControl> Controls { get; } =
    [
        new SliderControl("width", "Width", 1, 8, 3) { Step = 0.5f, Unit = "m" },
        new SliderControl("depth", "Depth", 0.5f, 4, 1.5f) { Step = 0.5f, Unit = "m" },
        new TextControl("points", "No points yet."),
        new ButtonControl("dig", "Dig (Enter)"),
        new ButtonControl("clear", "Clear (Esc)"),
    ];

    /// <inheritdoc/>
    public void OnChosen(IToolContext ctx) => Show(ctx);

    /// <inheritdoc/>
    public void OnLeft(IToolContext ctx)
    {
        _cursor = null;
        ctx.Preview([]);
    }

    /// <inheritdoc/>
    public void OnPointer(IToolContext ctx, PointerInput input)
    {
        bool onGround = input.Target is PointerTarget.Ground or PointerTarget.Scene;
        _cursor = onGround ? input.Point : null;
        if (input.Action == PointerAction.Press && onGround)
        {
            if ((input.Modifiers & (KeyModifiers.Shift | KeyModifiers.Ctrl)) != 0)
            {
                Pick(ctx, input);
                return;
            }
            _points.Add(new Vector2(input.Point.X, input.Point.Z));
        }
        Show(ctx);
    }

    /// <inheritdoc/>
    public bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
    {
        switch (key)
        {
            case "Enter":
                Dig(ctx);
                return true;
            case "Escape" when _points.Count > 0:
                _points.Clear();
                Show(ctx);
                return true;
            case "Back" when _points.Count > 0:
                _points.RemoveAt(_points.Count - 1);
                Show(ctx);
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public void OnControl(IToolContext ctx, string id)
    {
        if (id == "dig")
        {
            Dig(ctx);
        }
        else if (id == "clear")
        {
            _points.Clear();
        }
        Show(ctx);
    }

    /// <summary>Adds a canal along the points laid out (two at least), as wide and deep as the sliders say.</summary>
    private void Dig(IToolContext ctx)
    {
        if (_points.Count < 2)
        {
            ctx.Status("A canal needs two points at least.");
            return;
        }
        byte[] path = Canals.WritePath(_points);
        float width = ctx.Number("width"), depth = ctx.Number("depth");
        var first = _points[0];
        var at = new Vector3(first.X, ctx.Area.GroundHeight(first.X, first.Y), first.Y);
        ctx.Commit("Dig a canal", new ToolEdit().Add(Keys.Canal, at, Vector3.Zero, c =>
        {
            c.SetBytes(Keys.Path, path);
            c.SetFloat(Keys.Width, width);
            c.SetFloat(Keys.Depth, depth);
        }));
        _points.Clear();
        Show(ctx);
    }

    /// <summary>The canal nearest a click, within reach: Shift fills it in, Ctrl gives it the sliders' width and depth.</summary>
    private static void Pick(IToolContext ctx, PointerInput input)
    {
        var at = new Vector2(input.Point.X, input.Point.Z);
        IObjectView? nearest = null;
        float best = PickReach;
        foreach (var canal in ctx.Area.ObjectsOf(Keys.Canal))
        {
            if (canal.TryGetBytes(Keys.Path, out byte[] bytes) && Canals.ReadPath(bytes) is { } path && Canals.Distance(path, at) is var d && d < best)
            {
                (nearest, best) = (canal, d);
            }
        }
        if (nearest == null)
        {
            ctx.Status("No canal there.");
            return;
        }
        if ((input.Modifiers & KeyModifiers.Shift) != 0)
        {
            ctx.Commit("Fill a canal in", new ToolEdit().Remove(nearest));
            return;
        }
        float width = ctx.Number("width"), depth = ctx.Number("depth");
        ctx.Commit("Resize a canal", new ToolEdit().Change(nearest, c =>
        {
            c.SetFloat(Keys.Width, width);
            c.SetFloat(Keys.Depth, depth);
        }));
    }

    /// <summary>The card's line and the preview: the path so far, and the stretch to the pointer.</summary>
    private void Show(IToolContext ctx)
    {
        ctx.SetText("points", _points.Count == 0 ? "No points yet." : string.Format(CultureInfo.InvariantCulture, "{0} point(s) laid out.", _points.Count));
        var line = new List<Vector2>(_points);
        if (_cursor is Vector3 c && line.Count > 0)
        {
            line.Add(new Vector2(c.X, c.Z));
        }
        var shapes = new List<Shape>();
        for (int i = 0; i + 1 < line.Count; i++)
        {
            shapes.Add(Band(ctx.Area, line[i], line[i + 1], ctx.Number("width") / 2));
        }
        ctx.Preview(shapes);
    }

    /// <summary>A flat band half a width either side of a stretch, a little over the ground.</summary>
    private static Shape Band(IAreaView area, Vector2 a, Vector2 b, float half)
    {
        var along = b - a;
        var side = along.LengthSquared() > 0 ? Vector2.Normalize(new Vector2(-along.Y, along.X)) * half : new Vector2(half, 0);
        Vector3 Up(Vector2 p) => new(p.X, area.GroundHeight(p.X, p.Y) + 0.2f, p.Y);
        Vector3[] corners = [Up(a - side), Up(a + side), Up(b + side), Up(b - side)];
        return new Shape(corners, [0, 1, 2, 0, 2, 3], new Vector4(0.2f, 0.5f, 1f, 0.5f)) { BothSides = true, WorldSpace = true };
    }
}
