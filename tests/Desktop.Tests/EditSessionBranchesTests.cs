using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session's remaining branches: the history's one-line description, which objects count as natural, a
/// stroke past the game's limit, a masked mountain, moving copies and a step that changes nothing.
/// </summary>
public class EditSessionBranchesTests
{
    [Fact]
    public void AChangeSaysHowManyZonesAndObjects()
    {
        var one = new EditSession.Change("a", [], new Ground.State([], [], [], [], [], []), new Ground.State([], [], [], [], [], []), [(0, 0)]);
        Assert.Equal("1 zone", one.Describe());
        var two = one with { Zones = [(0, 0), (1, 0)], Things = [(4, true, false)] };
        Assert.Equal("2 zones · 1 new", two.Describe());
        Assert.Equal("", (one with { Zones = [] }).Describe());
    }

    [Fact]
    public void OnlyWildObjectsAreNatural()
    {
        var tree = new WorldScene.Thing(1, StableHash.Of("Beech1"), Vector3.Zero, Vector3.Zero, 1, false);
        Assert.True(EditSession.Natural(tree));
        Assert.False(EditSession.Natural(tree with { Piece = true }));
        Assert.False(EditSession.Natural(tree with { Tamed = true }));
        Assert.False(EditSession.Natural(new WorldScene.Thing(2, StableHash.Of("stone_wall_4x2"), Vector3.Zero, Vector3.Zero, 1, false)));
    }

    [Fact]
    public void AStrokePastTheLimitSaysSo()
    {
        var s = TestScenes.Flat(2);
        s.Brush.Radius = 6;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Raise, 64, 64);
        for (int i = 0; i < 40; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        Assert.StartsWith("Reached the game limit", s.EndStroke());
        Assert.Equal("Raise", s.UndoLabel);
    }

    [Fact]
    public void AMaskedMountainClearsOnlyTheWildThingsItBuries()
    {
        float o = -32;
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(o + 160, 30, o + 160), Vector3.Zero, 1, false);
        var goneTree = new WorldScene.Thing(6, StableHash.Of("Beech1"), new Vector3(o + 161, 30, o + 160), Vector3.Zero, 1, false) { Gone = true };
        var farTree = new WorldScene.Thing(7, StableHash.Of("Beech1"), new Vector3(o + 20, 30, o + 20), Vector3.Zero, 1, false);
        var s = TestScenes.Flat(5, tree, goneTree, farTree);
        // On but letting everything through: the mask is asked at every point.
        s.Mask.On = true;
        s.Mask.HeightMax = 1000;
        var done = s.Mountain(160, 160, new MountainSpec(MountainKind.Peak, 80, 60, 0.6f, 0, 42), clear: true, "Mountain: masked");
        Assert.NotNull(done);
        Assert.True(done.Value.Touched > 0);
        Assert.Equal(1, done.Value.Cleared);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.False(s.Scene.Things[2].Gone);
        Assert.True(s.Ground.HeightOf(160 * s.Ground.W + 160) > 80);
    }

    [Fact]
    public void AMaskedShapeAsksTheMaskAtEveryPoint()
    {
        var s = TestScenes.Flat(1);
        s.Mask.On = true;
        s.Mask.HeightMax = 1000;
        var (touched, clamped, bad) = s.Shape(32, 32, _ => 1, 4, 0, "Shape");
        Assert.True(touched > 0);
        Assert.False(clamped);
        Assert.Equal(0, bad);
    }

    [Fact]
    public void MovingACopyKeepsWhatItWasCopiedFrom()
    {
        var rock = new WorldScene.Thing(3, StableHash.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
        var s = TestScenes.Flat(1, rock);
        // A copy made in this session (a negative id the edit store knows).
        int copy = s.Commit("Paste", null, [], [(new NewObject(0, rock.Prefab, new Vector3(5, 30, 5), Vector3.Zero, 1, 3, false), false)])[0];
        int moved = s.Move([(copy, new Vector3(8, 30, 8), Vector3.Zero)])[0];
        var n = s.Edits.FindAdded(s.Scene.Things[moved].Id);
        Assert.NotNull(n);
        Assert.Equal(3, n.SourceId);
        Assert.False(n.Fresh);
        // An object with a negative id the edit store does not know: a fresh one with no source.
        s.Scene.Things.Add(new WorldScene.Thing(-999, rock.Prefab, new Vector3(2, 30, 2), Vector3.Zero, 1, false));
        int other = s.Move([(s.Scene.Things.Count - 1, new Vector3(3, 30, 3), Vector3.Zero)], "Moved stray")[0];
        var m = s.Edits.FindAdded(s.Scene.Things[other].Id);
        Assert.NotNull(m);
        Assert.Null(m.SourceId);
        Assert.True(m.Fresh);
        Assert.Equal("Moved stray", s.UndoLabel);
    }

    [Fact]
    public void AStepThatChangesNothingIsNotKept()
    {
        var s = TestScenes.Flat(1);
        Assert.Empty(s.Commit("Nothing", gr => ([], (0, 0, 0, 0)), [], [], []));
        Assert.Empty(s.UndoList);
    }
}
