
namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    public sealed partial record Outcome
    {
        /// <summary>
        /// Whether the game has all a step did: nothing of it is in the parts that did not go in (a step's reset of a
        /// zone not waiting any more, unmarked since, is not). With the objects not sent after No limit ground became
        /// ground discs, no step has all it did: the discs are not in the game, and no step says which made them.
        /// </summary>
        public bool Sent(EditSession.Change c) =>
            !(Lifted && Unsent.HasFlag(LiveParts.Objects))
            && (!Unsent.HasFlag(LiveParts.Objects) || c.Things.Length == 0)
            && !c.Resets.Any(r => UnsentResets.Contains((r.Reset.X, r.Reset.Z)));
    }
}
