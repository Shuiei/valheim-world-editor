namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A cluster of things that belong together, anchored at (u, v) and facing yaw (into the room): a camp (bedroll,
    /// fire, firewood), a meal (table, seats, food), a scribe's desk (table, chair, candles, chest), stores (crates
    /// stacked, barrels), an armoury (weapon and armour stands), an altar (statue, candles, offerings), bones
    /// (a stack, skulls, a long bone), a workshop (bench and tools). Whatever has no room is left out.
    /// </summary>
    private void Cluster(Frame f, string kind, float u, float v, float yaw)
    {
        switch (kind)
        {
            case "camp":
                ClusterCamp(f, u, v, yaw);
                break;
            case "meal":
                ClusterMeal(f, u, v, yaw);
                break;
            case "scribe":
                ClusterScribe(f, u, v, yaw);
                break;
            case "stores":
                ClusterStores(f, u, v, yaw);
                break;
            case "armoury":
                ClusterArmoury(f, u, v, yaw);
                break;
            case "altar":
                ClusterAltar(f, u, v, yaw);
                break;
            case "bones":
                ClusterBones(f, u, v, yaw);
                break;
            case "workshop":
                ClusterWorkshop(f, u, v, yaw);
                break;
            default:
                break;
        }
    }

    /// <summary>The spots either side of a cluster's anchor along the wall: the left (-1.1 m) and the right (+1.1 m).</summary>
    private static (float LU, float LV, float RU, float RV) ClusterSides(float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        return (lu, lv, ru, rv);
    }

    /// <summary>
    /// Up to n clusters in the room's corners (the convex ones, away from doorways), each a kind from the list in
    /// turn: the secondary groups that make a room read as lived in, beside its one main feature.
    /// </summary>
    private void Clusters(Frame f, int n, params string[] kinds)
    {
        int k = 0;
        foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => _rnd.Next()))
        {
            if (k >= n)
            {
                break;
            }
            var (cu, cv) = In((u, v, yaw), 1.3f);
            if (f.NearDoor(cu, cv, 2.4f) || !f.Free(cu, cv, 0.9f))
            {
                continue;
            }
            Cluster(f, kinds[k % kinds.Length], cu, cv, yaw);
            k++;
        }
    }
}
