using System.ComponentModel;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>Which ground a change may touch, as the Mask panel's settings; what is left out does not limit.</summary>
    public sealed class MaskSpec
    {
        /// <summary>Lowest ground height that passes, metres.</summary>
        [Description("Lowest ground height that changes, metres (optional).")]
        public float? HeightMin { get; set; }

        /// <summary>Highest ground height that passes, metres.</summary>
        [Description("Highest ground height that changes, metres (optional).")]
        public float? HeightMax { get; set; }

        /// <summary>Smallest slope that passes, degrees.</summary>
        [Description("Smallest slope that changes, degrees (optional; e.g. 30: only steep ground).")]
        public float? SlopeMin { get; set; }

        /// <summary>Largest slope that passes, degrees.</summary>
        [Description("Largest slope that changes, degrees (optional; e.g. 15: only gentle ground).")]
        public float? SlopeMax { get; set; }

        /// <summary>The biomes that pass; none: all.</summary>
        [Description("Only in these biomes (optional): Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North, Ocean.")]
        public string[]? Biomes { get; set; }

        /// <summary>The rule on painted ground.</summary>
        [Description("By paint (optional): any, unpainted, painted, dirt, cultivated or paved.")]
        public string? Paint { get; set; }
    }

    /// <summary>The Mask over the session's ground for a spec (null: no mask), or why the spec is wrong.</summary>
    internal static (Func<int, float>? Mask, string? Problem) MaskOf(MaskSpec? spec, EditSession s)
    {
        if (spec == null)
        {
            return (null, null);
        }
        var mask = new Mask { On = true, HeightMin = spec.HeightMin, HeightMax = spec.HeightMax, SlopeMin = spec.SlopeMin, SlopeMax = spec.SlopeMax };
        if (BiomesIn(mask, spec.Biomes) is string badBiome)
        {
            return (null, badBiome);
        }
        if (PaintOf(mask, spec.Paint) is string badPaint)
        {
            return (null, badPaint);
        }
        if (mask.Warning(english: true) is { Length: > 0 } warning)
        {
            return (null, "The mask lets nothing through: " + warning);
        }
        return (mask.For(s.Ground, s.Scene.Biomes), null);
    }

    /// <summary>Adds the biomes a spec names to the mask, by their names without spaces and case; or says which name is none.</summary>
    /// <param name="mask">The mask the biomes are added to.</param>
    /// <param name="names">The names given (null: none, so all biomes pass).</param>
    /// <returns>Why a name is none of the biomes, or null when all are.</returns>
    internal static string? BiomesIn(Mask mask, string[]? names)
    {
        foreach (string b in names ?? [])
        {
            var found = ZoneFilter.BiomeChoices(english: true).Where(n => string.Equals(n.Name.Replace(" ", "", StringComparison.Ordinal), b.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)).ToList();
            if (found.Count == 0)
            {
                return $"{b}: the biomes are {string.Join(", ", ZoneFilter.BiomeChoices(english: true).Select(n => n.Name))}.";
            }
            mask.Biomes.Add(found[0].Value);
        }
        return null;
    }

    /// <summary>Sets the mask's paint rule from its name (none given: any paint); or says which name is none.</summary>
    /// <param name="mask">The mask whose paint rule is set.</param>
    /// <param name="paint">The rule's name, or null.</param>
    /// <returns>Why the name is wrong, or null when it is not.</returns>
    internal static string? PaintOf(Mask mask, string? paint)
    {
        if (paint == null)
        {
            return null;
        }
        if (!Enum.TryParse<Mask.PaintRule>(paint, true, out var rule))
        {
            return "The mask's paint is any, unpainted, painted, dirt, cultivated or paved.";
        }
        mask.Paint = rule;
        return null;
    }
}
