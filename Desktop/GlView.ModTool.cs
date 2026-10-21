using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimWorldEditor.Sdk;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace TerrainEditor.Desktop;

// A mod's own tool in the 3D view (ToolMode.Mod): the pointer given to it, and its preview drawn over the area.
public sealed partial class GlView
{
    /// <summary>The mod's tool in use (with <see cref="ToolMode.Mod"/>), and what it asks the editor through.</summary>
    internal (ModExtensions.Tool Tool, IToolContext Context)? ModTool { get; set; }

    /// <summary>The left button held down on the mod's tool.</summary>
    private bool _modDown;

    /// <summary>The keys held at the last move.</summary>
    private KeyModifiers _modKeys;

    /// <summary>The mod's tool's preview: triangles in the view's frame by colour, swapped whole.</summary>
    private volatile (Vector4 Color, float[] Triangles)[] _modPreview = [];

    /// <summary>The preview's vertex array and buffer.</summary>
    private uint _modPreviewVao, _modPreviewVbo;

    /// <summary>The left button pressed on a mod's tool: given to it, and the camera's drag left alone.</summary>
    private bool PressMod(PointerPressedEventArgs e, Point at)
    {
        if (Mode != ToolMode.Mod || ModTool == null)
        {
            return false;
        }
        _dragFrom = null;
        _modDown = true;
        PointMod(PointerAction.Press, at, e.KeyModifiers);
        Wake();
        return true;
    }

    /// <summary>The left button let go on a mod's tool.</summary>
    private void UpMod(Point at, KeyModifiers mods)
    {
        if (_modDown)
        {
            _modDown = false;
            PointMod(PointerAction.Release, at, mods);
        }
    }

    /// <summary>
    /// Gives the mod's tool the pointer: its ray in the world's frame, and what it meets first (an object, a mod's
    /// layer's mesh, the ground) with the point and the surface's normal there. A release with no ray (the pointer off
    /// the view) still reaches it, pointing at nothing, so each press it is given ends.
    /// </summary>
    private void PointMod(PointerAction action, Point at, KeyModifiers mods)
    {
        if (ModTool is not var (tool, ctx))
        {
            return;
        }
        if (ViewRay(at, _surfaceSize) is not var (s, o, dv))
        {
            if (action == PointerAction.Release)
            {
                tool.Pointed(ctx, new PointerInput(action, Keys(mods), Vector3.Zero, Vector3.Zero));
            }
            return;
        }
        var d = Vector3.Normalize(dv);
        var input = new PointerInput(action, Keys(mods), WorldOf(s, o), new Vector3(d.X, d.Y, -d.Z));
        Aim(input, s, o, d, at);
        tool.Pointed(ctx, input);
    }

    /// <summary>What the ray meets first: an object, a layer's mesh or the ground (or nothing).</summary>
    private void Aim(PointerInput input, WorldScene s, Vector3 o, Vector3 d, Point at)
    {
        float? ground = Picking.HitGround(s, o, d);
        var face = SceneLayers.Last(s).Picks.HitFace(o, d, ground ?? 4000);
        if (ObjectAt(at, _surfaceSize) is int i && i < s.Things.Count)
        {
            var t = s.Things[i];
            input.Target = PointerTarget.Thing;
            input.Point = t.Position;
            input.Normal = Vector3.UnitY;
            input.Thing = s.ObjectBytes(t) is { } bytes
                ? new ObjectView(PrefabCatalog.NameOf(t.Prefab) ?? "", t.Position, t.Rotation, ZdoData.Parse(bytes), t.Scale == 0 ? 1 : t.Scale) { Id = t.Id }
                : null;
            return;
        }
        if (ground is not float g)
        {
            return;
        }
        var point = WorldOf(s, o + d * g);
        input.Point = point;
        if (face is var (ft, fn) && ft <= g + 1e-3f)
        {
            input.Target = PointerTarget.Scene;
            input.Normal = new Vector3(fn.X, fn.Y, -fn.Z);
            return;
        }
        input.Target = PointerTarget.Ground;
        var area = new SceneLayers.AreaView(s);
        const float Half = 0.5f;
        input.Normal = Vector3.Normalize(new Vector3(
            area.GroundHeight(point.X - Half, point.Z) - area.GroundHeight(point.X + Half, point.Z),
            2 * Half,
            area.GroundHeight(point.X, point.Z - Half) - area.GroundHeight(point.X, point.Z + Half)));
    }

    /// <summary>A view point in the world's frame.</summary>
    private static Vector3 WorldOf(WorldScene s, Vector3 v) => new(v.X + s.Cx, v.Y, -v.Z + s.Cz);

    /// <summary>Avalonia's modifier keys as the SDK's.</summary>
    internal static ValheimWorldEditor.Sdk.KeyModifiers Keys(KeyModifiers m) =>
        (m.HasFlag(KeyModifiers.Shift) ? ValheimWorldEditor.Sdk.KeyModifiers.Shift : 0)
        | (m.HasFlag(KeyModifiers.Control) || m.HasFlag(KeyModifiers.Meta) ? ValheimWorldEditor.Sdk.KeyModifiers.Ctrl : 0)
        | (m.HasFlag(KeyModifiers.Alt) ? ValheimWorldEditor.Sdk.KeyModifiers.Alt : 0);

    /// <summary>
    /// What a mod's tool shows over the area: its shapes' triangles in the view's frame (their own frame taken as the
    /// world's), drawn see-through over everything until it says otherwise; none clears it.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="shapes">The shapes.</param>
    internal void SetModPreview(WorldScene s, IReadOnlyList<Shape> shapes)
    {
        var parts = new List<(Vector4, float[])>();
        foreach (var shape in shapes.Where(ValidPreview))
        {
            float[] tris = new float[shape.Triangles.Length * 3];
            for (int k = 0; k < shape.Triangles.Length; k++)
            {
                var v = SceneLayers.ToView(s, shape.Vertices[shape.Triangles[k]]);
                (tris[k * 3], tris[k * 3 + 1], tris[k * 3 + 2]) = (v.X, v.Y, v.Z);
            }
            parts.Add((shape.Color, tris));
        }
        _modPreview = [.. parts];
        Wake();
    }

    /// <summary>Whether a preview shape can be drawn: whole triangles of corners it has, finite corners.</summary>
    private static bool ValidPreview(Shape s) =>
        s?.Vertices != null && s.Triangles != null && s.Triangles.Length % 3 == 0 && s.Triangles.All(i => i >= 0 && i < s.Vertices.Length)
        && s.Vertices.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z));

    /// <summary>The mod's tool's preview, while it is in use.</summary>
    private void DrawModPreview(Matrix4x4 vp)
    {
        if (Mode != ToolMode.Mod)
        {
            return;
        }
        foreach (var (color, tris) in _modPreview)
        {
            DrawLines(ref _modPreviewVao, ref _modPreviewVbo, tris, vp, color, PrimitiveType.Triangles, blend: true);
        }
    }
}
