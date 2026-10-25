using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.StrokeRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The history kept whatever the world goes through: a save that resets zones (the world read again, what the steps
/// did in those zones left out), and the world opened again after No limit ground was saved as ground discs (the No
/// limit ground back over them, so an undo puts back exactly the ground it changed). On copies of the test world.
/// </summary>
[Collection("World files")]
public class HistoryCarryTests
{
    [Fact]
    public void SavingAZoneResetKeepsTheStepsOutsideIt()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            // Zones 1 to 3: a bump in zone 1, 1 and one in zone 3, 3, which is reset with its ground.
            var scene = WorldScene.Load(dir, 2, 2, 3);
            var s = scene.Session!;
            int here = 32 * s.Ground.W + 32, there = 160 * s.Ground.W + 160;
            float was = s.Ground.HeightOf(here), wasThere = s.Ground.HeightOf(there);
            s.Shape(32, 32, Formula.Compile("3", []), 4, 0, "Here");
            s.Shape(160, 160, Formula.Compile("3", []), 4, 0, "There");
            s.MarkResets([(3, 3)], keepBuildings: true, ground: true, "Reset");
            var r = s.Save();
            Assert.True(r.Saved, r.Message);
            // Read again after the reset: the bump outside it is still a step, applied; the rest went with the reset.
            var step = Assert.Single(s.UndoList);
            Assert.Equal("Here", step.Label);
            Assert.True(step.Applied);
            Assert.Empty(s.RedoList);
            Assert.Equal(wasThere, s.Ground.HeightOf(there), 2);
            Assert.True(s.Undo());
            Assert.Equal(was, s.Ground.HeightOf(here), 2);
            scene.Owner!.Dispose();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void AnObjectOfAZoneResetIsLeftOutEvenWhereTheGameMadeTheSameAgain()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            // A tree deleted, then its zone reset: live, the game may grow the same tree at the same place at once.
            // Here it is still there, as such a tree would be: the step must not take it as its own.
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            int tree = s.Scene.Things.FindIndex(t => !t.Piece && t.Id >= 0);
            var (zx, zz) = ChunkMath.SectorOf(s.Scene.Things[tree].Position.X, s.Scene.Things[tree].Position.Z);
            s.Delete([tree]);
            var owner = scene.Owner!;
            byte[] carried = HistoryFile.Carry(owner, s.Export())!;
            Assert.Null(HistoryFile.TakeBack(owner, carried, [new Editing.ZoneReset(zx, zz, KeepBuildings: true, Ground: false)]));
            // Without the reset, the step comes back with the tree.
            var back = HistoryFile.TakeBack(owner, carried, [])!;
            Assert.Single(back.Kept.Undo);
            owner.Dispose();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void OpenedAgainNoLimitGroundComesBackOverItsDiscs()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 2, 2, 3);
            var s = scene.Session!;
            s.Brush.Radius = 20;
            s.Brush.Strength = 1;
            int g = 96 * scene.W + 96, near = 96 * scene.W + 120;
            float before = scene.Heights[g], beforeNear = scene.Heights[near];
            s.Ground.NoLimit = true;
            Stroke(s, BrushTool.Raise, 96, 96, 600);
            float after = scene.Heights[g];
            Assert.True(after > before + 30, $"{after - before:0.0} m");
            Assert.Contains("ground disc", s.Save().Message);
            scene.Owner!.Dispose();

            // Opened again: the world has the discs; the No limit ground comes back over them, nothing to save.
            var again = WorldScene.Load(dir, 2, 2, 3);
            var a = again.Session!;
            Assert.NotEmpty(again.World.Discs);
            Assert.Equal(after, again.Heights[g], 0.01f);
            Assert.True(a.Ground.Lift[g] != 0);
            Assert.Equal((0, 0, 0, 0), a.Pending);
            // Saving something else leaves the discs as they are: they are not worked out again.
            int discs = again.World.Discs.Count;
            // A rock in zone 3, 3, away from the stroke (world x and z 200: grid point 168 of the area from zone 1).
            a.Commit("Place", null, [], [(new Editing.NewObject(0, StableHash.Of("Rock_4"), new Vector3(200, again.Heights[168 * again.W + 168], 200), Vector3.Zero, 0f), false)]);
            var other = a.Save();
            Assert.True(other.Saved, other.Message);
            Assert.DoesNotContain("ground disc", other.Message);
            Assert.Equal(discs, WorldSave.Load(dir).Discs.Count);
            Assert.True(a.Undo());
            // The stroke undone puts back exactly the ground it changed, and saved, the discs go.
            Assert.True(a.Undo());
            Assert.Equal(before, again.Heights[g], 0.01f);
            Assert.Equal(beforeNear, again.Heights[near], 0.01f);
            var r = a.Save();
            Assert.True(r.Saved, r.Message);
            again.Owner!.Dispose();
            var third = WorldScene.Load(dir, 2, 2, 3);
            Assert.Empty(third.World.Discs);
            Assert.Equal(before, third.Heights[g], 0.1f);
            Assert.Equal(beforeNear, third.Heights[near], 0.1f);
            third.Owner!.Dispose();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
