using TerrainEditor.Save;
using ValheimGen;
using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

// The ground the game keeps clear round locations, and round Expand World Data's vegetation that asks for it.
public static partial class Regrow
{
    /// <summary>A clear area: no vegetation grows within its radius of its centre.</summary>
    /// <param name="X">The centre's x, metres.</param>
    /// <param name="Z">The centre's z, metres.</param>
    /// <param name="Radius">The radius, metres.</param>
    public readonly record struct ClearArea(float X, float Z, float Radius);

    /// <summary>
    /// The clear areas of a world's locations, by zone (ZoneSystem.PlaceLocations: the zone's own location when its
    /// kind clears its area, within its exterior radius). With Expand World Data installed, a location in a
    /// neighbouring zone that is wider than half a zone clears its area too (the mod's ClearAreasFromAdjacentZones).
    /// The zone's own location counts as when the zone was first made (the game skips it once placed).
    /// </summary>
    public sealed class ClearAreas
    {
        /// <summary>Each zone's location that clears its area.</summary>
        private readonly Dictionary<(int X, int Z), ClearArea> _byZone = [];

        /// <summary>Whether the neighbouring zones' wide locations count (Expand World Data installed).</summary>
        private readonly bool _neighbours;

        /// <summary>
        /// The clear areas of these location instances, by their kinds' rules (the world's own, else the game's).
        /// </summary>
        /// <param name="gen">The world's generator (its rules).</param>
        /// <param name="instances">The instances: the kind's prefab hash and the position.</param>
        public ClearAreas(WorldGenerator gen, IEnumerable<(int Hash, float X, float Z)> instances)
        {
            _neighbours = gen.Rules.DataInstalled;
            var kinds = new Dictionary<int, LocationRule>();
            foreach (var rule in SeedLocations.RulesFor(gen).Locations)
            {
                kinds.TryAdd(StableHash.Of(rule.Prefab), rule);
            }
            foreach (var (hash, x, z) in instances)
            {
                if (kinds.TryGetValue(hash, out var rule) && rule.ClearArea != 0)
                {
                    _byZone[ChunkMath.SectorOf(x, z)] = new ClearArea(x, z, rule.ExteriorRadius);
                }
            }
        }

        /// <summary>The zone a spot stands in.</summary>
        internal static (int X, int Z) ZoneOf(Spot s) => ChunkMath.SectorOf(s.X, s.Z);

        /// <summary>The clear areas of a world's own locations (its zone file's location instances); none without one.</summary>
        /// <param name="world">The world.</param>
        /// <param name="gen">Its generator.</param>
        public static ClearAreas Of(WorldSave world, WorldGenerator gen) =>
            new(gen, (world.Zones?.Locations ?? []).Select(l => (l.Hash, l.Position.X, l.Position.Z)));

        /// <summary>A zone's clear areas from locations: its own, then (with the mod) its neighbours' wide ones.</summary>
        public IEnumerable<ClearArea> For(int zx, int zz)
        {
            if (_byZone.TryGetValue((zx, zz), out var own))
            {
                yield return own;
            }
            if (!_neighbours)
            {
                yield break;
            }
            for (int i = zx - 1; i <= zx + 1; i++)
            {
                for (int j = zz - 1; j <= zz + 1; j++)
                {
                    if ((i != zx || j != zz) && _byZone.TryGetValue((i, j), out var area) && area.Radius >= 32f)
                    {
                        yield return area;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The game's last check of a spot, ZoneSystem.InsideClearArea: Expand World Data's own distance check (part of its
    /// replacement of it), then the clear areas of the zone.
    /// </summary>
    private static bool PassesClearAreas(Pass pass, Rule veg, float px, float py, float pz) =>
        PassesOwnDistance(veg, px, py, pz) && !InsideClearArea(pass, veg, px, pz);

    /// <summary>
    /// Whether a spot lies in a clear area of its zone (ZoneSystem.InsideClearArea): within the square of half-width
    /// the radius round the centre; with Expand World Data's vegetation file, within the circle of the radius plus the
    /// rule's own clear radius (the mod's InsideClearArea).
    /// </summary>
    private static bool InsideClearArea(Pass pass, Rule veg, float px, float pz)
    {
        foreach (var a in pass.Clear)
        {
            if (pass.Circles)
            {
                float dx = (float)((double)px - a.X), dz = (float)((double)pz - a.Z);
                if (MathF.Sqrt((float)((double)dx * dx + (double)dz * dz)) < (double)a.Radius + veg.clearRadius)
                {
                    return true;
                }
            }
            else if (px > (double)a.X - a.Radius && px < (double)a.X + a.Radius && pz > (double)a.Z - a.Radius && pz < (double)a.Z + a.Radius)
            {
                return true;
            }
        }
        return false;
    }
}
