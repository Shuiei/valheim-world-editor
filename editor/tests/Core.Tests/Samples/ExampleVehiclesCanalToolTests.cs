using System.Numerics;
using ExampleVehicles.WorldEditor;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SDK sample's Canals tool (sdk/samples/ExampleVehicles.WorldEditor/CanalTool.cs), through a stand-in for the editor:
/// clicks on the ground lay a path out (previewed), Dig adds a canal keeping it with the sliders' width and depth,
/// Shift + click fills the nearest canal in, Ctrl + click resizes it, Backspace and Esc take points back.
/// </summary>
public class ExampleVehiclesCanalToolTests
{
    /// <summary>The editor as the tool sees it: flat ground at 40 m holding the canals given; what the tool did, kept.</summary>
    private sealed class Editor(params IObjectView[] canals) : IToolContext
    {
        public List<(string Label, ToolEdit Edit)> Commits { get; } = [];

        public IReadOnlyList<Shape> Shown { get; private set; } = [];

        public string Said { get; private set; } = "";

        public Dictionary<string, string> Texts { get; } = [];

        public IAreaView Area { get; } = new PlainArea(-32, -32, 32, 32, 40) { Objects = new Dictionary<string, IReadOnlyList<IObjectView>> { [Keys.Canal] = canals } };

        public float Number(string id) => id == "width" ? 4 : 2;

        public int Choice(string id) => 0;

        public bool IsOn(string id) => false;

        public void SetText(string id, string text) => Texts[id] = text;

        public void Preview(IReadOnlyList<Shape> shapes) => Shown = shapes;

        public void Status(string text) => Said = text;

        public void Commit(string label, ToolEdit edit) => Commits.Add((label, edit));
    }

    private static PointerInput Click(float x, float z, KeyModifiers mods = KeyModifiers.None) =>
        new(PointerAction.Press, mods, new Vector3(x, 100, z), -Vector3.UnitY) { Target = PointerTarget.Ground, Point = new Vector3(x, 40, z), Normal = Vector3.UnitY };

    [Fact]
    public void ClicksLayAPathOutAndDigAddsACanal()
    {
        var tool = new CanalTool();
        var ed = new Editor();
        tool.OnChosen(ed);
        Assert.Equal("No points yet.", ed.Texts["points"]);
        tool.OnPointer(ed, Click(0, 0));
        tool.OnPointer(ed, Click(10, 0));
        tool.OnPointer(ed, new PointerInput(PointerAction.Move, KeyModifiers.None, default, default) { Target = PointerTarget.Ground, Point = new Vector3(10, 40, 5) });
        Assert.Equal("2 point(s) laid out.", ed.Texts["points"]);
        Assert.Equal(2, ed.Shown.Count);
        Assert.All(ed.Shown, s => Assert.True(s.WorldSpace && s.Vertices.All(v => v.Y == 40.2f)));
        Assert.True(tool.OnKey(ed, "Enter", KeyModifiers.None));
        var (label, edit) = Assert.Single(ed.Commits);
        Assert.Equal("Dig a canal", label);
        var (prefab, position, _, data) = Assert.Single(edit.Added);
        Assert.Equal((Keys.Canal, new Vector3(0, 40, 0)), (prefab, position));
        var copy = new DataCopy(Keys.Canal, default, default, new ZdoData());
        data!(copy);
        Assert.True(copy.TryGetBytes(Keys.Path, out byte[] path));
        Assert.Equal([new Vector2(0, 0), new Vector2(10, 0)], Canals.ReadPath(path)!);
        Assert.True(copy.TryGetFloat(Keys.Width, out float w));
        Assert.Equal(4f, w);
        Assert.True(copy.TryGetFloat(Keys.Depth, out float d));
        Assert.Equal(2f, d);
        Assert.Equal("No points yet.", ed.Texts["points"]);
    }

    [Fact]
    public void ACanalNeedsTwoPointsAndKeysTakePointsBack()
    {
        var tool = new CanalTool();
        var ed = new Editor();
        tool.OnPointer(ed, Click(0, 0));
        tool.OnControl(ed, "dig");
        Assert.Empty(ed.Commits);
        Assert.Equal("A canal needs two points at least.", ed.Said);
        Assert.True(tool.OnKey(ed, "Back", KeyModifiers.None));
        Assert.False(tool.OnKey(ed, "Back", KeyModifiers.None));
        tool.OnPointer(ed, Click(0, 0));
        Assert.True(tool.OnKey(ed, "Escape", KeyModifiers.None));
        Assert.False(tool.OnKey(ed, "Escape", KeyModifiers.None));
        Assert.False(tool.OnKey(ed, "A", KeyModifiers.None));
        tool.OnControl(ed, "clear");
        tool.OnLeft(ed);
        Assert.Empty(ed.Shown);
    }

    [Fact]
    public void ShiftFillsTheNearestCanalInAndCtrlResizesIt()
    {
        var z = new ZdoData { Prefab = StableHash.Of(Keys.Canal) };
        z.ByteList.Add((StableHash.Of(Keys.Path), Canals.WritePath([new(0, 0), new(10, 0)])));
        var canal = new ObjectView(Keys.Canal, Vector3.Zero, Vector3.Zero, z);
        var tool = new CanalTool();
        var ed = new Editor(canal);
        tool.OnPointer(ed, Click(5, 20, KeyModifiers.Shift));
        Assert.Equal("No canal there.", ed.Said);
        tool.OnPointer(ed, Click(5, 1, KeyModifiers.Shift));
        Assert.Same(canal, Assert.Single(Assert.Single(ed.Commits).Edit.Removed));
        tool.OnPointer(ed, Click(5, -1, KeyModifiers.Ctrl));
        var (obj, change) = Assert.Single(ed.Commits[1].Edit.Changed);
        Assert.Same(canal, obj);
        var copy = new DataCopy(Keys.Canal, default, default, new ZdoData());
        change(copy);
        Assert.True(copy.TryGetFloat(Keys.Width, out float w));
        Assert.Equal(4f, w);
    }
}
