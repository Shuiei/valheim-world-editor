using System.Numerics;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// One call to the game's objects: what it sends, and what the game has once it accepts it (recorded only then:
    /// a refused call is sent again next time).
    /// </summary>
    private sealed class Call
    {
        /// <summary>The call's id, for a plugin that does a call once (<see cref="LiveBridge.Abilities.CanCallOnce"/>); null: none.</summary>
        public string? Id { get; set; }

        /// <summary>The plugin's run the call went to (<see cref="LiveBridge.Abilities.Run"/>), with an id.</summary>
        public string Run { get; set; } = "";

        /// <summary>The game objects to destroy.</summary>
        public List<LiveTarget> Destroy { get; } = [];

        /// <summary>The objects to make, in the save format.</summary>
        public List<byte[]> Create { get; } = [];

        /// <summary>The id each creation is for: a new object, or a saved one brought back.</summary>
        public List<int> CreateFor { get; } = [];

        /// <summary>What each creation is and where (for its LiveTarget once the game gives its ZDOID).</summary>
        public List<(int Prefab, Vector3 Position)> CreateWhat { get; } = [];

        /// <summary>New objects of kinds the editor cannot write, left out.</summary>
        public List<string> Skipped { get; } = [];

        /// <summary>Saved objects the call destroys.</summary>
        public List<int> DestroyIds { get; } = [];

        /// <summary>Saved objects the call makes again (deletions undone).</summary>
        public List<int> RestoreIds { get; } = [];

        /// <summary>New objects the call destroys again.</summary>
        public List<int> DropNew { get; } = [];

        /// <summary>The objects to move.</summary>
        public List<LiveMove> Moves { get; } = [];

        /// <summary>
        /// Each move's snapshot object, the id whose game object moves (that object, or a new object it was moved to)
        /// and the id it stands for once moved.
        /// </summary>
        public List<(int Source, int From, int To)> MoveFor { get; } = [];

        /// <summary>Whether there is nothing to send.</summary>
        public bool Empty => Destroy.Count + Create.Count + Moves.Count == 0;
    }

    /// <summary>
    /// The last call left without an answer (the connection dropped or timed out): the game may have done it. Sent
    /// again first, under its id; null when every call was answered.
    /// </summary>
    private Call? _unanswered;
}
