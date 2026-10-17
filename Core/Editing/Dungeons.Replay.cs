using System.Numerics;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>
    /// What the game makes with a room or a location it spawns: its networked objects, those its random parts keep,
    /// rolled the game's way (Random.InitState with the seed, then each RandomSpawn's chance, then each RandomObject's
    /// pick, in order), moved and turned with it. The shared Unity random state is put back after.
    /// </summary>
    /// <param name="contents">Its networked objects, in its frame.</param>
    /// <param name="spawns">Its RandomSpawn parts, in the order the game rolls them.</param>
    /// <param name="picks">Its RandomObject parts, rolled after the spawns.</param>
    /// <param name="nodes">Each random part's parent node (-1: the prefab itself).</param>
    /// <param name="themes">The dungeon's themes a part may require (every bit set: none is checked, as for a location).</param>
    /// <param name="y">The elevation the parts' limits are checked against.</param>
    /// <param name="seed">What the rolls start from.</param>
    /// <param name="position">Where it stands, world metres.</param>
    /// <param name="rotation">Its turn.</param>
    internal static List<Made> Replay(Content[] contents, Spawn[] spawns, Pick[] picks, int[] nodes, int themes, float y, int seed, Vector3 position, Quaternion rotation)
    {
        var made = new List<Made>();
        if (contents.Length == 0)
        {
            return made;
        }
        bool[] off = new bool[nodes.Length];
        var was = Rnd.state;
        try
        {
            Rnd.InitState(seed);
            foreach (var s in spawns)
            {
                bool spawned = Rnd.Range(0f, 100f) <= s.Chance;
                if (s.Theme != 0 && (themes & s.Theme) == 0 || y < s.MinY || y > s.MaxY)
                {
                    spawned = false;
                }
                Set(s.Node, !spawned);
                if (s.Off >= 0)
                {
                    Set(s.Off, spawned);
                }
            }
            foreach (var pick in picks)
            {
                float total = pick.Choices.Sum(c => c.Weight), roll = Rnd.Range(0f, total), sum = 0;
                int? chosen = null;
                foreach (var (node, weight) in pick.Choices)
                {
                    sum += weight;
                    if (roll <= sum)
                    {
                        chosen = node;
                        break;
                    }
                }
                if (pick.Theme != 0 && (themes & pick.Theme) == 0 || y < pick.MinY || y > pick.MaxY)
                {
                    chosen = null;
                }
                foreach (var (node, _) in pick.Choices)
                {
                    Set(node, node != chosen);
                }
                if (chosen == null)
                {
                    Set(pick.Node, true);
                }
            }
        }
        finally
        {
            Rnd.state = was;
        }
        foreach (var c in contents)
        {
            // Up its parents to the prefab: a node past the list (damaged scan data) ends the walk, and so does a walk
            // longer than the list (a loop).
            bool shown = true;
            for (int n = c.Node, steps = 0; n >= 0 && n < nodes.Length && shown && steps <= nodes.Length; n = nodes[n], steps++)
            {
                shown = !off[n];
            }
            if (shown)
            {
                made.Add(new Made(c.Prefab, position + Vector3.Transform(c.Position, rotation), Quaternion.Normalize(rotation * c.Rotation)));
            }
        }
        return made;

        void Set(int node, bool hidden)
        {
            if (node >= 0 && node < off.Length)
            {
                off[node] = hidden;
            }
        }
    }
}
