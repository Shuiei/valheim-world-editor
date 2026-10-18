using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// Distances, a seed's stats as text, and biome names, as the page shows them.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// A distance for people, to the nearest 100 m: metres under 1 km ("&lt; 100 m" under 50 m), else km to one decimal;
    /// "none" for infinity.
    /// </summary>
    internal static string Km(float m)
    {
        if (float.IsPositiveInfinity(m))
        {
            return Lang.T("newWorld.none");
        }
        // Rounded to 100 m first, so 950 m and more reads 1.0 km (not "1000 m"), and under 50 m "< 100 m" (not "000 m").
        float r = MathF.Round(m / 100, MidpointRounding.AwayFromZero) * 100;
        return r < 100 ? Lang.T("newWorld.km.under100") : r < 1000 ? Lang.T("newWorld.km.metres", r) : Lang.T("newWorld.km.km", r / 1000);
    }

    /// <summary>
    /// A seed's stats as text, for a search result's tooltip.
    /// </summary>
    internal static string Describe(SeedPreview.Stats s)
    {
        var lines = new List<string>
        {
            Lang.T("newWorld.statsLand", s.Land * 100, s.StartContinent * 100, s.MainContinent * 100, s.LandNearStart * 100),
            Lang.T("newWorld.statsBiomes", string.Join(", ", SeedPreview.Biomes.Where(b => s.Shares[b] > 0.005f).Select(b => Lang.T("newWorld.biomeShare", Name(b), s.Shares[b] * 100)))),
            Lang.T("newWorld.statsNearest", string.Join(", ", SeedPreview.Biomes.Skip(1).Where(b => b is not (Heightmap.Biome.AshLands or Heightmap.Biome.DeepNorth))
                .Select(b => s.SameLand[b] || float.IsPositiveInfinity(s.Distance[b]) ? Lang.T("newWorld.nameDistance", Name(b), Km(s.Distance[b])) : Lang.T("newWorld.bySea", Name(b), Km(s.Distance[b]))))),
            s.TempleStart
                ? Lang.T("newWorld.statsTemple", s.Start.X, s.Start.Z)
                : Lang.T("newWorld.statsStartNear", s.Start.X, s.Start.Z),
        };
        if (s.Nearest is { } n)
        {
            string Of(string name) => Km(n.TryGetValue(name, out float d) ? d : float.PositiveInfinity);
            lines.Add(Lang.T("newWorld.statsTraders", Of("Haldor"), Of("The Bog Witch"), Of("Hildir")));
            lines.Add(Lang.T("newWorld.statsBosses", string.Join(", ", SeedPreview.Landmarks.Where(l => l.Kind == "boss").Select(l => Lang.T("newWorld.nameDistance", LandmarkName(l.Name), Of(l.Name))))));
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// A biome's name as the game shows it, in the language in use (ZoneFilter.BiomeName's catalogue text).
    /// </summary>
    /// <param name="b">The biome.</param>
    internal static string Name(Heightmap.Biome b)
    {
        return ZoneFilter.BiomeName(b).Shown;
    }
}
