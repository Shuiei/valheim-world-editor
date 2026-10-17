using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>Rolls each RandomSpawn in order: it spawns on its chance, and only where its theme and elevation allow.</summary>
    private static void RollSpawns(Spawn[] spawns, int themes, float y, bool[] off)
    {
        foreach (var s in spawns)
        {
            bool spawned = Rnd.Range(0f, 100f) <= s.Chance;
            if (!Applies(s.Theme, s.MinY, s.MaxY, themes, y))
            {
                spawned = false;
            }
            SetHidden(off, s.Node, !spawned);
            if (s.Off >= 0)
            {
                SetHidden(off, s.Off, spawned);
            }
        }
    }

    /// <summary>Rolls each RandomObject's pick: one of its choices, or none where its theme and elevation do not allow.</summary>
    private static void RollPicks(Pick[] picks, int themes, float y, bool[] off)
    {
        foreach (var pick in picks)
        {
            int? chosen = RollChoice(pick);
            if (!Applies(pick.Theme, pick.MinY, pick.MaxY, themes, y))
            {
                chosen = null;
            }
            foreach (var (node, _) in pick.Choices)
            {
                SetHidden(off, node, node != chosen);
            }
            if (chosen == null)
            {
                SetHidden(off, pick.Node, true);
            }
        }
    }

    /// <summary>Draws one choice of a pick, weighted; null when the weights reach none of them.</summary>
    private static int? RollChoice(Pick pick)
    {
        float total = pick.Choices.Sum(c => c.Weight), roll = Rnd.Range(0f, total), sum = 0;
        foreach (var (node, weight) in pick.Choices)
        {
            sum += weight;
            if (roll <= sum)
            {
                return node;
            }
        }
        return null;
    }

    /// <summary>Whether a part's theme (every bit of it set in the dungeon's themes, or none) and elevation limits allow it.</summary>
    private static bool Applies(int theme, int minY, int maxY, int themes, float y) =>
        !(theme != 0 && (themes & theme) == 0 || y < minY || y > maxY);
}
