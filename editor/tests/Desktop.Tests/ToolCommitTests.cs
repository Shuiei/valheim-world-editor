using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A mod's tool's edit made a step of an area's history (ToolCommit): data holders added and changed as hidden objects,
/// other objects as things, a change a copy of the object with the tool's values (its source the old one), an object
/// the editor did not hand over left alone and said.
/// </summary>
public sealed class ToolCommitTests
{
    [Fact]
    public void AnEditBecomesOneStep()
    {
        var s = TestScenes.Flat(1, TestScenes.Beech(4, new Vector3(1, 30, 1)));
        var holder = new ZdoData { Prefab = StableHash.Of(ExampleMod.Fleet) };
        holder.IntList.Add((StableHash.Of("n"), 1));
        var fleet = new ObjectView(ExampleMod.Fleet, new Vector3(5, 30, 5), Vector3.Zero, holder) { Id = 12 };
        var beech = new ObjectView("Beech1", new Vector3(1, 30, 1), Vector3.Zero, new ZdoData { Prefab = StableHash.Of("Beech1") }) { Id = 4 };
        var stranger = new ObjectView("Beech1", default, default, new ZdoData());
        var edit = new ToolEdit()
            .Add(ExampleMod.Fleet, new Vector3(3, 30, 3), Vector3.Zero, c => c.SetInt("n", 5))
            .Add("Beech1", new Vector3(6, 30, 6), Vector3.Zero)
            .Change(fleet, c => c.SetInt("n", 2))
            .Remove(beech)
            .Remove(stranger);

        var plan = ToolCommit.PlanOf(s, edit);
        Assert.Equal([12], plan.RemoveHidden);
        Assert.Equal([0], plan.RemoveThings);
        Assert.Single(plan.AddThings);
        Assert.Equal(2, plan.AddHidden.Count);
        var changed = plan.AddHidden[1];
        Assert.Equal((12, false, new Vector3(5, 30, 5)), (changed.SourceId, changed.Fresh, changed.Position));
        Assert.Equal(2, ZdoData.Parse(changed.Raw!).GetInt(StableHash.Of("n")));
        Assert.Equal(5, ZdoData.Parse(plan.AddHidden[0].Raw!).GetInt(StableHash.Of("n")));
        Assert.Equal(["Beech1"], plan.Skipped);

        ToolCommit.Apply(s, "Tool", edit);
        var step = Assert.Single(s.UndoList);
        Assert.Equal(("Tool", 2, 3), (step.Label, step.Things.Length, step.Hidden.Length));
    }
}
