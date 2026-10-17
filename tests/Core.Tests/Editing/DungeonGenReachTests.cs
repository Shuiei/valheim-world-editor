using Xunit;
using static WorldEditor.Tests.DungeonGenSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) over the samples: every room can be reached, and the boss's key before the boss's gate.
/// </summary>
public class DungeonGenReachTests
{
    [Theory]
    [MemberData(nameof(DungeonGenTests.Samples), MemberType = typeof(DungeonGenTests))]
    public void EveryRoomCanBeReachedAndTheKeyBeforeTheBoss(string style, string biome, int size, int levels, int seed)
    {
        var r = Make(style, biome, size, levels, seed);
        var map = r.Map!;
        int entrance = map.ToList().FindIndex(m => m.Name == "Entrance hall");
        Assert.True(entrance >= 0);
        HashSet<int> Reach(bool throughKeyGate)
        {
            var seen = new HashSet<int> { entrance };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                foreach (var d in r.Doors!.Where(d => d.A == at || d.B == at))
                {
                    if (d.Kind == "Key" && !throughKeyGate)
                    {
                        continue;
                    }
                    int other = d.A == at ? d.B : d.A;
                    if (seen.Add(other))
                    {
                        queue.Enqueue(other);
                    }
                }
            }
            return seen;
        }
        Assert.Equal(map.Count, Reach(true).Count);
        int key = map.ToList().FindIndex(m => m.Key);
        if (key >= 0)
        {
            Assert.Contains(key, Reach(false));
            Assert.DoesNotContain(map.ToList().FindIndex(m => m.Name == "Arena"), Reach(false));
        }
    }
}
