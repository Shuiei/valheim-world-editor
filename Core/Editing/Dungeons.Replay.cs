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
    internal static List<Made> Replay(Content[] contents, Spawn[] spawns, Pick[] picks, int[] nodes, int themes, float y, int seed, Vector3 position, Quaternion rotation) =>
        Replay(contents, spawns, picks, nodes, themes, y, seed, position, rotation, out _);

    /// <summary>
    /// <see cref="Replay(Content[], Spawn[], Pick[], int[], int, float, int, Vector3, Quaternion)"/>, with the random
    /// state the rolls leave (what the game draws next from, as it spawns the objects).
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
    /// <param name="rolled">The random state after the rolls.</param>
    internal static List<Made> Replay(Content[] contents, Spawn[] spawns, Pick[] picks, int[] nodes, int themes, float y, int seed, Vector3 position, Quaternion rotation, out Rnd.RandomState rolled)
    {
        var made = new List<Made>();
        bool[] off = new bool[nodes.Length];
        var was = Rnd.State;
        try
        {
            Rnd.InitState(seed);
            RollSpawns(spawns, themes, y, off);
            RollPicks(picks, themes, y, off);
            rolled = Rnd.State;
        }
        finally
        {
            Rnd.State = was;
        }
        foreach (var c in contents)
        {
            if (IsShown(c.Node, nodes, off))
            {
                made.Add(new Made(c.Prefab, position + Vector3.Transform(c.Position, rotation), Quaternion.Normalize(rotation * c.Rotation), c.Snap));
            }
        }
        return made;
    }

    /// <summary>
    /// Whether a part is shown: no node up its parents to the prefab is hidden. A node past the list (damaged scan
    /// data) ends the walk, and so does a walk longer than the list (a loop).
    /// </summary>
    internal static bool IsShown(int node, int[] nodes, bool[] off)
    {
        bool shown = true;
        for (int n = node, steps = 0; n >= 0 && n < nodes.Length && shown && steps <= nodes.Length; n = nodes[n], steps++)
        {
            shown = !off[n];
        }
        return shown;
    }

    /// <summary>Sets whether a node is hidden; a node outside the list is ignored.</summary>
    internal static void SetHidden(bool[] off, int node, bool hidden)
    {
        if (node >= 0 && node < off.Length)
        {
            off[node] = hidden;
        }
    }
}
