
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Corridor kinds.
    /// <summary>
    /// What makes this corridor its own: a gate midway (a wall across with a door of the style's), a collapse (rubble
    /// and broken wall narrowing the way, a fallen pillar), bones along both walls (catacombs), a runner down the
    /// middle (a keep, a temple), vines on the walls (a crypt, ruins), or nothing but the torches. A long straight
    /// corridor only, never the bent ones. Then the biome's own: a muddy scrap pile to dig through in the swamp, a stake
    /// wall barricade or seeker creep in the mines, icicles and ice in the cold, spikes in the Ashlands, roots in the
    /// woods, a trap in a keep or a warren.
    /// </summary>
    private void CorridorKind(Frame f)
    {
        if (f.S.Shape != DungeonPlan.Shape.Rect || f.HalfD < 3)
        {
            return;
        }
        var kinds = new List<(string Kind, float Weight)> { ("plain", 2) };
        if (f.HalfD >= 4 && _mat.Name is not "Goblin")
        {
            kinds.Add(("gate", 1.2f));
        }
        if (_mat.Name is "Stone" or "Grausten" or "Marble")
        {
            // Chosen whatever the decay (so that the setting moves nothing else); a kept corridor then stays plain.
            kinds.Add(("collapse", 1.5f));
        }
        if (_style.Name is "Crypt" or "Catacombs" or "Ruins")
        {
            kinds.Add(("bones", _style.Name == "Catacombs" ? 2 : 1));
            kinds.Add(("vines", 0.8f));
        }
        if (_style.Name is "Fortress" or "Temple" or "Dvergr hold")
        {
            kinds.Add(("runner", 1.2f));
        }
        if (_style.Name is "Goblin warren")
        {
            kinds.Add(("stakes", 1));
        }
        // The biome's own: mud in the swamp's crypts, a barricade or creep in the mines, icicles in the cold,
        // spikes in the Ashlands, roots in the woods, a trap in a keep or a warren.
        switch (_b.Name)
        {
            case "Swamp":
                kinds.Add(("mud", 1.5f));
                kinds.Add(("roots", 1));
                break;
            case "Mistlands":
                kinds.Add(("barricade", 1.2f));
                kinds.Add(("creep", 1.2f));
                break;
            case "Mountain" or "Deep North":
                kinds.Add(("icicles", 1.5f));
                break;
            case "Ashlands":
                kinds.Add(("spikes", 1));
                break;
            case "Meadows" or "Black Forest":
                kinds.Add(("roots", 1));
                break;
            default:
                break;
        }
        if (_style.Name is "Fortress" or "Prison" or "Goblin warren" && f.HalfD >= 4)
        {
            kinds.Add(("trap", 0.8f));
        }
        CorridorAs(f, Weighted.Pick(kinds, k => k.Weight, _rnd.NextDouble()).Kind);
    }
}
