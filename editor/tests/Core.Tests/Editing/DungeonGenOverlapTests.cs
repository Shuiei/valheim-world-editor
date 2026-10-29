using Xunit;
using static WorldEditor.Tests.DungeonGenSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) over the samples: rooms do not overlap, and doorways join their rooms, not walls.
/// </summary>
public class DungeonGenOverlapTests
{
    [Theory]
    [MemberData(nameof(DungeonGenTests.Samples), MemberType = typeof(DungeonGenTests))]
    public void RoomsDoNotOverlapAndDoorwaysJoinTheirRooms(string style, string biome, int size, int levels, int seed)
    {
        var r = Make(style, biome, size, levels, seed);
        var map = r.Map!;
        for (int i = 0; i < map.Count; i++)
        {
            for (int j = i + 1; j < map.Count; j++)
            {
                var (a, b) = (map[i], map[j]);
                bool overlap = a.Level == b.Level && a.Floor.Any(p => b.Floor.Any(q => p.X0 < q.X1 - 0.01f && q.X0 < p.X1 - 0.01f && p.Z0 < q.Z1 - 0.01f && q.Z0 < p.Z1 - 0.01f));
                Assert.False(overlap && !(a.Corridor && b.Corridor), $"{a.Name} and {b.Name} overlap");
            }
        }
        foreach (var d in r.Doors!.Where(d => d.Kind != "Stairs"))
        {
            // On the edge of both rooms' boxes.
            foreach (var room in new[] { map[d.A], map[d.B] })
            {
                // On a side of one of its parts (a shaped room's doorway may straddle two parts along one wall).
                bool onEdge = room.Floor.Any(p => (MathF.Abs(d.X - p.X0) < 0.01f || MathF.Abs(d.X - p.X1) < 0.01f) && d.Z >= p.Z0 - 0.01f && d.Z <= p.Z1 + 0.01f
                    || (MathF.Abs(d.Z - p.Z0) < 0.01f || MathF.Abs(d.Z - p.Z1) < 0.01f) && d.X >= p.X0 - 0.01f && d.X <= p.X1 + 0.01f);
                Assert.True(onEdge, $"a {d.Kind} doorway at {d.X}, {d.Z} is not on the edge of {room.Name}");
            }
        }
    }
}
