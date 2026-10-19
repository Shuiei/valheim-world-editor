using System.Globalization;

namespace ValheimGen;

// Biome names to biomes and back, as Expand World Data reads and writes them.
public sealed partial class BiomeTable
{
    /// <summary>A biome by its name (case does not matter); false for a name the table has no biome of.</summary>
    /// <param name="name">The name.</param>
    /// <param name="biome">The biome.</param>
    public bool TryGet(string name, out Heightmap.Biome biome)
    {
        if (_byName.TryGetValue(name, out var b))
        {
            biome = b.Value;
            return true;
        }
        biome = Heightmap.Biome.None;
        return false;
    }

    /// <summary>
    /// Biomes from a list of names as the mod reads one (DataManager.ToBiomes): a comma-separated list of names (case does
    /// not matter) or numbers (added as they are), "None" for none, an empty text for every biome of the table. A name
    /// the table has no biome of is passed over and told to <paramref name="unknown"/>.
    /// </summary>
    /// <param name="names">The list.</param>
    /// <param name="unknown">Told each name that is no biome and no number; null to pass over them silently.</param>
    public Heightmap.Biome Parse(string names, Action<string>? unknown = null)
    {
        if (string.Equals(names, "None", StringComparison.OrdinalIgnoreCase))
        {
            return Heightmap.Biome.None;
        }
        if (names == "")
        {
            return All;
        }
        Heightmap.Biome result = Heightmap.Biome.None;
        foreach (string name in names.Split(',').Select(s => s.Trim()).Where(s => s != ""))
        {
            if (TryGet(name, out var biome))
            {
                result |= biome;
            }
            else if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                result += number;
            }
            else
            {
                unknown?.Invoke(name);
            }
        }
        return result;
    }

    /// <summary>
    /// A biome's name: the table's for one of its biomes, "None" for none, the names of each flag set for a combination
    /// (a flag the table has no biome of as its number), joined with ", ".
    /// </summary>
    /// <param name="biome">The biome, or biomes.</param>
    public string NameOf(Heightmap.Biome biome)
    {
        if (biome == Heightmap.Biome.None)
        {
            return "None";
        }
        if (_byValue.TryGetValue(biome, out var one))
        {
            return one.Id;
        }
        List<string> names = [];
        for (uint bit = 1; bit != 0; bit <<= 1)
        {
            var flag = (Heightmap.Biome)bit;
            if ((biome & flag) != 0)
            {
                names.Add(_byValue.TryGetValue(flag, out var b) ? b.Id : bit.ToString(CultureInfo.InvariantCulture));
            }
        }
        return string.Join(", ", names);
    }
}
