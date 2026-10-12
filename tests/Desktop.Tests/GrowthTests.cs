using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Growing the game's vegetation: refused without the world's generator or over too many zones, then on a copy of the
/// test world not where the same kind stands close by or a building is near, and never twice in one place.
/// </summary>
[Collection("World files")]
public class GrowthTests
{
    /// <summary>A prefab hash's display name.</summary>
    private static string? NameOf(int prefab) => Terrain.PrefabCatalog.DisplayName(prefab);

    [Fact]
    public async Task WithoutTheGeneratorItSaysSo()
    {
        var s = TestScenes.Flat(1);
        string? said = null;
        var spots = await Growth.Spots(s, -30, -30, 30, 30, _ => true, NameOf, m => said = m);
        Assert.Null(spots);
        Assert.Equal("Regrow needs the world's generator (not available here).", said);
    }

    [Fact]
    public async Task ItGrowsWhereNothingStandsAndNeverTwice()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            var said = new List<string>();
            // Too many zones for the limit.
            Assert.Null(await Growth.Spots(s, -32, -32, 100, 100, _ => true, NameOf, said.Add, maxZones: 1));
            Assert.StartsWith("Choose a smaller area: regrow works on up to 1 zones (64 x 64 m)", said[^1]);
            // The trees and rocks taken away first: the game grows them back.
            s.Delete([.. Enumerable.Range(0, scene.Things.Count).Where(i => !scene.Things[i].Piece && !scene.Things[i].Gone)]);
            var first = await Growth.Spots(s, -30, -30, 30, 30, _ => true, NameOf, said.Add);
            Assert.NotNull(first);
            Assert.NotEmpty(first);
            Assert.Equal("Working out what the game grows here…", said[^1]);
            // The same kind 2 m from one spot, a building 2 m from another (far from the first).
            var a = first[0];
            var b = first.First(o => MathF.Abs(o.X - a.X) + MathF.Abs(o.Z - a.Z) > 12);
            lock (scene.Things)
            {
                scene.Things.Add(new WorldScene.Thing(int.MaxValue - 1, StableHash.Of(a.Name), new Vector3(a.X + 2, a.Y, a.Z), Vector3.Zero, 1, false));
                scene.Things.Add(new WorldScene.Thing(int.MaxValue - 2, StableHash.Of("wood_wall_half"), new Vector3(b.X + 2, b.Y, b.Z), Vector3.Zero, 0, true));
            }
            var second = await Growth.Spots(s, -30, -30, 30, 30, _ => true, NameOf, said.Add);
            Assert.NotNull(second);
            Assert.DoesNotContain(a, second);
            Assert.DoesNotContain(b, second);
            Assert.NotEmpty(second);
            // Grown: nothing is left to grow there.
            Growth.Commit(s, "Area: regrew", second);
            Assert.Equal(second.Count, s.Pending.Added);
            Assert.Empty((await Growth.Spots(s, -30, -30, 30, 30, _ => true, NameOf, said.Add))!);
            // Nothing kept: none.
            Assert.Empty((await Growth.Spots(s, -30, -30, 30, 30, _ => false, NameOf, said.Add))!);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
