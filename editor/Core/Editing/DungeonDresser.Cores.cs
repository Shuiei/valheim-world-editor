
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The biome's cores on stands (or hung on a wall), the prize of its own dungeons; null where it has none.</summary>
    private (string Prefab, bool OnWall)? BiomeCore => _b.Name switch
    {
        "Meadows" or "Black Forest" or "Swamp" => ("Pickable_SurtlingCoreStand", false),
        "Mountain" => ("Pickable_FrostCoreHanger", true),
        "Mistlands" => ("Pickable_BlackCoreStand", false),
        "Ashlands" => ("Pickable_MoltenCoreStand", false),
        "Deep North" => ("Pickable_FrostCoreHanger", true),
        _ => null,
    };

    /// <summary>n of the biome's cores: on stands in the corners, or hung on the walls at head height.</summary>
    private void Cores(Frame f, int n)
    {
        if (BiomeCore is not { } core)
        {
            return;
        }
        if (core.OnWall)
        {
            foreach (var sp in f.WallSpots(2.6f, 2.8f).OrderBy(_ => _rnd.Next()).Take(n))
            {
                var (u, v) = In(sp, 0.3f);
                _o.Add(core.Prefab, f.W(u, v, f.Top + 1.5f), f.Yaw(sp.Yaw));
            }
            return;
        }
        foreach (var (cu, cv, yaw, _) in f.Corners().Where(c => c.Convex).OrderBy(_ => _rnd.Next()).Take(n))
        {
            var (u, v) = In((cu, cv, yaw), 0.7f);
            Put(f, core.Prefab, u, v, R(0, 360), 0.3f);
        }
    }

    /// <summary>The valuables set into Mörkhalla's walls: a gemstone in its socket at head height on a wall spot, in the north and the Ashlands.</summary>
    private void WallGems(Frame f, int n)
    {
        if (_b.Name is not ("Ashlands" or "Deep North"))
        {
            return;
        }
        foreach (var sp in f.WallSpots(2.6f, 2.8f).OrderBy(_ => _rnd.Next()).Take(n))
        {
            var (u, v) = In(sp, 0.3f);
            _o.Add(One("Morkhalla_Eye5_gemstone", "Morkhalla_Eye6_gemstone", "Morkhalla_Eye7_gemstone"), f.W(u, v, f.Top + 1.9f), f.Yaw(sp.Yaw));
        }
    }
}
