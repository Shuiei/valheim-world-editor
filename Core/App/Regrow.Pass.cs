using ValheimGen;
using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

// What one Regrow call shares between its zones, rules and groups.
public static partial class Regrow
{
    /// <summary>
    /// One Regrow call: the world's terrain and ground, its rules, the zones read so far (groups reach into
    /// neighbours), the alt biomes' say, and the clear areas of the zone being placed (those of its locations, then
    /// those its own objects add).
    /// </summary>
    /// <param name="terrain">The world's terrain.</param>
    /// <param name="ground">The ground as it is now, one zone around the zones placed.</param>
    /// <param name="rules">The vegetation rules.</param>
    /// <param name="areas">The clear areas of the world's locations; null for none.</param>
    private sealed class Pass(TerrainService terrain, Ground ground, IReadOnlyList<Rule> rules, ClearAreas? areas)
    {
        /// <summary>The world's terrain.</summary>
        public TerrainService Terrain => terrain;

        /// <summary>The ground as it is now.</summary>
        public Ground Ground => ground;

        /// <summary>The vegetation rules, in the game's order.</summary>
        public IReadOnlyList<Rule> Rules => rules;

        /// <summary>The alt biomes' say over each rule.</summary>
        public AltCheck Alts { get; } = new(terrain);

        /// <summary>The zones read so far.</summary>
        public Dictionary<(int, int), Zone> Zones { get; } = [];

        /// <summary>
        /// Whether a clear area is a circle widened by the rule's own clear radius (Expand World Data's check, with its
        /// vegetation file in use) rather than the game's square.
        /// </summary>
        public bool Circles { get; } = terrain.Generator.Rules.DataInstalled && terrain.Generator.Rules.Vegetation != null;

        /// <summary>The clear areas of the zone being placed.</summary>
        public List<ClearArea> Clear { get; } = [];

        /// <summary>The zone holding a world point.</summary>
        public Zone ZoneAt(float x, float z) => Regrow.ZoneAt(terrain, ground, Zones, x, z);

        /// <summary>
        /// Keeps the ground round an object clear of the vegetation placed after it in its zone, when its rule asks for
        /// it (Expand World Data's, as the object is made).
        /// </summary>
        public void KeepClear(Rule veg, float x, float z)
        {
            if (veg.clearArea != 0)
            {
                Clear.Add(new ClearArea(x, z, veg.clearRadius));
            }
        }

        /// <summary>Starts a zone: its locations' clear areas.</summary>
        public void StartZone(int zx, int zz)
        {
            Clear.Clear();
            if (areas != null)
            {
                Clear.AddRange(areas.For(zx, zz));
            }
        }
    }
}
