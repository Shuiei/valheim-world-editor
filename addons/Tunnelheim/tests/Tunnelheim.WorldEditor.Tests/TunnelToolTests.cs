using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The Tunnel tool through a stand-in for the editor: a click with an action commits one step of records, the card's
/// hint follows the action, a bore takes two clicks (Esc forgets the first), Add Earth too high is refused, clicks off
/// the ground do nothing, and the pointer previews where the edit goes.
/// </summary>
public class TunnelToolTests
{
    /// <summary>The editor as the tool sees it: flat ground at 40 m, the action and sliders given; what the tool did, kept.</summary>
    private sealed class Editor(int action, params IObjectView[] objects) : IToolContext
    {
        public List<(string Label, ToolEdit Edit)> Commits { get; } = [];

        public IReadOnlyList<Shape> Shown { get; private set; } = [];

        public string Said { get; private set; } = "";

        public Dictionary<string, string> Texts { get; } = [];

        public int Action { get; set; } = action;

        public IAreaView Area { get; } = new FakeArea(40, objects);

        public float Number(string id) => id switch
        {
            "size" => 1.5f,
            "steps" => 4,
            "length" => 10,
            "height" => 3,
            _ => 4,
        };

        public int Choice(string id) => Action;

        public bool IsOn(string id) => false;

        public void SetText(string id, string text) => Texts[id] = text;

        public void Preview(IReadOnlyList<Shape> shapes) => Shown = shapes;

        public void Status(string text) => Said = text;

        public void Commit(string label, ToolEdit edit) => Commits.Add((label, edit));
    }

    private static TunnelTool Tool() => new(new FakeHost(), _ => { });

    private static PointerInput Click(float x, float y, float z, PointerAction action = PointerAction.Press) =>
        new(action, KeyModifiers.None, new Vector3(x, y + 20, z - 20), Vector3.Normalize(new Vector3(0, -1, 1)))
        {
            Target = PointerTarget.Ground,
            Point = new Vector3(x, y, z),
            Normal = Vector3.UnitY,
        };

    [Theory]
    [InlineData(0, "Dig (round)")]
    [InlineData(1, "Dig (square)")]
    [InlineData(4, "Level Wall")]
    [InlineData(5, "Level Ceiling")]
    [InlineData(8, "Shaft")]
    [InlineData(9, "Stairs")]
    public void AClickCommitsOneStepOfRecords(int action, string label)
    {
        var tool = Tool();
        var ed = new Editor(action);
        tool.OnChosen(ed);
        Assert.False(string.IsNullOrEmpty(ed.Texts["hint"]));
        tool.OnPointer(ed, Click(8, 40, 8));
        var (committed, edit) = Assert.Single(ed.Commits);
        Assert.Equal(label, committed);
        Assert.NotEmpty(edit.Added);
        Assert.All(edit.Added, a => Assert.Equal(TunnelheimMod.RecordPrefabName, a.Prefab));
        Assert.StartsWith(label + ": ", ed.Said);
        Assert.NotEmpty(ed.Shown);
    }

    [Fact]
    public void ARoomIsDugUpFromItsFloorAndFlatGroundNeedsNoSmoothing()
    {
        var tool = Tool();
        var ed = new Editor(7);
        // On open ground the room would stand in the air: nothing to dig.
        tool.OnPointer(ed, Click(8, 40, 8));
        Assert.Empty(ed.Commits);
        // Its floor in the rock (a tunnel's floor), it goes up into it.
        tool.OnPointer(ed, Click(8, 34, 8));
        Assert.Equal("Room", Assert.Single(ed.Commits).Label);

        ed.Action = 3;
        tool.OnPointer(ed, Click(8, 40, 8));
        Assert.Single(ed.Commits);
        Assert.Equal("Nothing changes there.", ed.Said);
    }

    [Fact]
    public void AddEarthOnTheGroundAddsRockAndTooHighIsRefused()
    {
        var tool = Tool();
        var ed = new Editor(2);
        tool.OnPointer(ed, Click(8, 40, 8));
        Assert.Equal("Add Earth", Assert.Single(ed.Commits).Label);

        tool.OnPointer(ed, Click(8, 120, 8));
        Assert.Single(ed.Commits);
        Assert.StartsWith("Too high for Add Earth", ed.Said);
        // Its preview shows where a click puts the lump, and nothing where it cannot go.
        Assert.Empty(ed.Shown);
        tool.OnPointer(ed, Click(8, 40, 8, PointerAction.Move));
        Assert.Equal(new Vector3(8, 40.4f, 8), Assert.Single(ed.Shown).Vertices.Aggregate(Vector3.Zero, (a, v) => a + v) / ed.Shown[0].Vertices.Length, new Vector3Near());
    }

    [Fact]
    public void ABoreTakesTwoClicksAndEscForgetsTheFirst()
    {
        var tool = Tool();
        var ed = new Editor(6);
        tool.OnPointer(ed, Click(4, 40, 4));
        Assert.Empty(ed.Commits);
        Assert.Equal("The bore starts here: click where it ends.", ed.Said);
        Assert.True(tool.OnKey(ed, "Escape", KeyModifiers.None));
        Assert.False(tool.OnKey(ed, "Escape", KeyModifiers.None));

        tool.OnPointer(ed, Click(4, 40, 4));
        tool.OnPointer(ed, Click(4, 40, 4, PointerAction.Move));
        Assert.Equal(2, ed.Shown.Count);
        tool.OnPointer(ed, Click(28, 38, 4));
        var (label, edit) = Assert.Single(ed.Commits);
        Assert.Equal("Bore", label);
        // From column 0 to column 1: a record in each.
        Assert.Equal(2, edit.Added.Count);
    }

    [Fact]
    public void ChangingTheActionForgetsABoresStartAndChangesTheHint()
    {
        var tool = Tool();
        var ed = new Editor(6);
        tool.OnPointer(ed, Click(4, 40, 4));
        ed.Action = 0;
        tool.OnControl(ed, "action");
        Assert.StartsWith("Click rock or ground", ed.Texts["hint"]);
        ed.Action = 6;
        tool.OnPointer(ed, Click(28, 40, 4));
        Assert.Empty(ed.Commits);
    }

    [Fact]
    public void NothingHappensOffTheGroundOrWhereNothingChanges()
    {
        var tool = Tool();
        var ed = new Editor(0);
        tool.OnPointer(ed, new PointerInput(PointerAction.Press, KeyModifiers.None, Vector3.Zero, -Vector3.UnitY) { Target = PointerTarget.Thing });
        Assert.Empty(ed.Commits);
        Assert.Empty(ed.Shown);
        Assert.StartsWith("Click the ground or the rock", ed.Said);
        tool.OnPointer(ed, Click(8, 90, 8));
        Assert.Empty(ed.Commits);
        Assert.Equal("Nothing changes there.", ed.Said);
        tool.OnLeft(ed);
        Assert.Empty(ed.Shown);
        Assert.False(tool.OnKey(ed, "A", KeyModifiers.None));
    }

    [Fact]
    public void ADigNextToARecordGoesIntoIt()
    {
        var record = FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, []), 8, 8);
        var tool = Tool();
        var ed = new Editor(0, record);
        tool.OnPointer(ed, Click(8, 40, 8));
        var edit = Assert.Single(ed.Commits).Edit;
        Assert.Same(record, Assert.Single(edit.Changed).Object);
        Assert.Equal(DigKind.Chip, Assert.Single(RecordCopy.Of(edit.Changed[0].Change).Record.Ops).Kind);
    }

    /// <summary>Points equal to a millimetre.</summary>
    private sealed class Vector3Near : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 1e-3f;

        public int GetHashCode(Vector3 v) => 0;
    }
}
