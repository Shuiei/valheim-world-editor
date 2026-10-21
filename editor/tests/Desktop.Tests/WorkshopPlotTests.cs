using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's plot: a blueprint opened onto the blank plot and saved back the same (measured from the anchor), and an
/// imported building standing on its lowest buildable piece with the terrain under its posts.
/// </summary>
public class WorkshopPlotTests
{
    private static readonly string[] Expected1 = ["no_such_piece_xyz"];
    [Fact]
    public void ABlueprintOpensOnThePlotAndComesBackTheSame()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-ws-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var scene = Workshop.Create("Workshop");
            var s = scene.Session!;
            var (placed, unknown, lift, _, _) = Workshop.Open(s, BlueprintFiles.Hut(dir));
            Assert.Equal(3, placed);
            // Its lowest point (the floor's collider, a little below its origin) on the ground.
            Assert.Equal(-Hammer.Bottom("wood_floor", Quaternion.Identity), lift, 3);
            Assert.Equal(Expected1, unknown);
            Assert.True(s.CanUndo);
            Assert.Equal(3, Workshop.Pieces(scene));
            Assert.Contains(scene.Things, t => !t.Gone && Vector3.Distance(t.Position, Workshop.Anchor(scene) + new Vector3(1, 1 + lift, 0)) < 1e-3f);
            // Saved back: as it was, from Homestead's anchor.
            var clip = Workshop.Building(scene, "Hut", lift);
            string text = Homestead.Write(clip, "Hut", "test", null, DateTime.Now);
            var back = BlueprintFormats.Parse("Hut.blueprint", text);
            Assert.Equal(3, back.Pieces.Count);
            Assert.Contains(back.Pieces, p => p.Name == "woodwall" && Vector3.Distance(p.Position, new Vector3(1, 1, 0)) < 1e-3f && MathF.Abs(p.Euler.Y - 90) < 0.5f);
            // Other files are centred, their lowest point on the ground (a wall reaches 1 m below its middle).
            string other = Path.Combine(dir, "other.blueprint");
            File.WriteAllText(other, "#Name:Other\n#Pieces\nwoodwall;Misc;10;5;10;0;0;0;1;\"\";1;1;1\nwoodwall;Misc;14;7;10;0;0;0;1;\"\";1;1;1\n");
            var plot = Workshop.Create("Workshop");
            Workshop.Open(plot.Session!, other);
            var walls = plot.Things.Where(t => !t.Gone).Select(t => t.Position - Workshop.Anchor(plot)).OrderBy(p => p.X).ToList();
            Assert.Equal(new Vector3(-2, 1, 0), walls[0]);
            Assert.Equal(new Vector3(2, 3, 0), walls[1]);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // An imported building: its lowest buildable piece on the ground (a rock a builder left lower does
    // not count), and the ground it stood on in game assumed under each of its lowest pieces (the
    // terrain reached each post): a wall standing higher on its own spot is on the ground, one on top
    // of another is not.
    [Fact]
    public void ImportsStandOnTheirLowestBuildablePieceAndTheTerrainUnderTheirPosts()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-ws-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, "posts.blueprint");
            File.WriteAllText(f, "#Name:Posts\n#Pieces\n"
                + "Placeable_Stone;Misc;3;-4;0;0;0;0;1;\"\";1;1;1\n"
                + "woodwall;Misc;0;1;0;0;0;0;1;\"\";1;1;1\n"
                + "woodwall;Misc;0;3;0;0;0;0;1;\"\";1;1;1\n"
                + "woodwall;Misc;6;3;0;0;0;0;1;\"\";1;1;1\n");
            var scene = Workshop.Create("W");
            var (_, _, lift, terrain, _) = Workshop.Open(scene.Session!, f);
            // The lowest wall's bottom (0) on the ground; the rock (lower) left where it was in the building.
            Assert.Equal(0, lift, 3);
            var walls = scene.Things.Where(t => !t.Gone && PrefabCatalog.NameOf(t.Prefab) == "woodwall").OrderBy(t => t.Position.X).ThenBy(t => t.Position.Y).ToList();
            Assert.Equal(Workshop.Ground + 1, walls[0].Position.Y, 3);
            Assert.True(Workshop.Buildable("woodwall"));
            Assert.False(Workshop.Buildable("Placeable_Stone"));
            var pieces = walls.Select(t => new Stability.Piece("woodwall", t.Position, Quaternion.Identity)).ToList();
            var r = Stability.Solve(pieces, (x, z) => Workshop.GroundAt(terrain, x, z));
            // The low wall and the high wall standing alone: on the ground; the one on top: not.
            Assert.Equal(100, r.Support[0], 1);
            Assert.Equal(100, r.Support[2], 1);
            Assert.True(r.Support[1] < 100);
            Assert.Empty(r.Falls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
