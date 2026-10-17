using System.Numerics;
using System.Text;

namespace TerrainEditor.Desktop;

// What a script changed, as the run reports it back to the editor.
public static partial class ScriptHost
{
    /// <summary>
    /// What a script did: the ground's new heights (grid points changed), paint, objects taken away (indices into the
    /// scene's things) and new ones (NaN height: on the ground at the end).
    /// </summary>
    public sealed class Changes
    {
        /// <summary>New heights by grid point.</summary>
        public Dictionary<int, float> Heights { get; } = [];
        /// <summary>New paint by grid point: four weights from 0 to 1.</summary>
        public Dictionary<int, float[]> Paint { get; } = [];
        /// <summary>Things to take away, indices into the scene's things.</summary>
        public HashSet<int> Remove { get; } = [];
        /// <summary>
        /// New objects: prefab hash and name, world position (NaN height: on the ground at the end), rotation (Euler
        /// degrees), scale, and whether it snaps to the pieces there as the hammer does.
        /// </summary>
        public List<(int Prefab, string Name, Vector3 Position, Vector3 Rotation, float Scale, bool Snap)> Add { get; } = [];
        /// <summary>Things moved, turned or sized: index into the scene's things, its new place, rotation and scale.</summary>
        public Dictionary<int, (Vector3 Position, Vector3 Rotation, float Scale)> Moves { get; } = [];
        /// <summary>What the script printed (2000 lines at most).</summary>
        public StringBuilder Output { get; } = new();
        /// <summary>
        /// Whether the heights may go past the game's ±8 m (the script's Ground.NoLimit when it ended).
        /// </summary>
        public bool NoLimit { get; set; } = true;
        /// <summary>The script changed nothing.</summary>
        public bool Empty => Heights.Count == 0 && Paint.Count == 0 && Remove.Count == 0 && Add.Count == 0 && Moves.Count == 0;
    }
}
