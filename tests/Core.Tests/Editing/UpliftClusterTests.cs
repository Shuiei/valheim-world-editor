using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How the lifted zones of an uplift are grouped (Uplift.Clusters) and which zones each group works on (Uplift.WorkingZones):
/// lifts far apart are apart, a chain of near lifts is one group worked only near its lifts.
/// </summary>
[Collection("World files")]
public class UpliftClusterTests
{
    [Fact]
    public void LiftsFarApartAreGroupedApart()
    {
        // The working block was the box round every lifted zone: lifts at two ends of the world made one grid of
        // billions of points. Zones whose working zones (a zone round each) overlap or touch go together, the others apart.
        var groups = Uplift.Clusters([(0, 0), (1, 0), (100, 100), (4, 0), (8, 0), (1, 0)]);
        Assert.Equal(3, groups.Count);
        Assert.Equal([(0, 0), (1, 0), (4, 0)], groups[0]);
        Assert.Equal([(100, 100)], groups[1]);
        Assert.Equal([(8, 0)], groups[2]);
        // A chain of near ones is one group.
        Assert.Single(Uplift.Clusters([(0, 0), (3, 3), (6, 6), (9, 9)]));
        // A zone far from every other is a group of its own, even inside the box round another group.
        var ring = Uplift.Clusters([(0, 0), (3, 0), (6, 0), (9, 0), (12, 0), (12, 3), (12, 6), (12, 9), (12, 12), (0, 12)]);
        Assert.Equal(2, ring.Count);
        Assert.Equal([(0, 12)], ring[1]);
        // Whatever the zones, no two groups' working zones overlap or touch: 60 zones scattered by fixed steps over
        // 30 × 31, which make groups of one to twenty.
        var many = Uplift.Clusters([.. Enumerable.Range(0, 60).Select(i => (i * 37 % 30 - 15, (i * 53 + 29) % 31 - 15))]);
        Assert.True(many.Count > 1);
        for (int i = 0; i < many.Count; i++)
        {
            for (int j = i + 1; j < many.Count; j++)
            {
                foreach (var (a, b) in many[i].SelectMany(a => many[j].Select(b => (a, b))))
                {
                    Assert.True(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z)) > 3, $"zones {a} and {b} meet");
                }
            }
        }
    }

    [Fact]
    public void ASparseChainOfLiftsIsWorkedOnlyNearItsLifts()
    {
        // A chain of lifts each three zones from the next is one group, but its working zones are only those round
        // each lift (a zone each way), not the box round the chain (300 zones a side).
        var chain = Enumerable.Range(0, 100).Select(i => (X: 3 * i, Z: 3 * i)).ToList();
        Assert.Single(Uplift.Clusters(chain));
        var zones = Uplift.WorkingZones(chain);
        Assert.Equal(100 * 9, zones.Count);
        Assert.All(zones, z => Assert.Contains(chain, c => Math.Abs(c.X - z.X) <= 1 && Math.Abs(c.Z - z.Z) <= 1));
        // Lifts side by side fill their box: the zones worked on are the box and a zone round it, as before.
        var square = Uplift.WorkingZones([(0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (2, 0)]);
        Assert.Equal([.. from z in Enumerable.Range(-1, 4) from x in Enumerable.Range(-1, 5) select (x, z)], square);
    }
}
