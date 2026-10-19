using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// A change that can be undone: the ground points it changed, its things and zones, and what it says of itself.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A change that can be undone: the ground points it changed, before and after, and the things it took away or
    /// brought.
    /// </summary>
    /// <param name="Label">The name shown in the history and on Undo.</param>
    /// <param name="Points">The ground grid points it changed (indices, sorted).</param>
    /// <param name="Before">Those points' values before it, in the same order.</param>
    /// <param name="After">Those points' values after it.</param>
    /// <param name="Zones">The zones of the area it touched (sent to the edit store on undo and redo).</param>
    public sealed record Change(string Label, int[] Points, Ground.State Before, Ground.State After, List<(int X, int Z)> Zones)
    {
        /// <summary>
        /// The things it took away or brought: index into the scene's things, gone before and after.
        /// </summary>
        public (int Index, bool Before, bool After)[] Things { get; init; } = [];
        /// <summary>
        /// Zones marked for reset (on) or not.
        /// </summary>
        public (ZoneReset Reset, bool Before, bool After)[] Resets { get; init; } = [];
        /// <summary>
        /// When it was made (local time).
        /// </summary>
        public DateTime Time { get; init; } = DateTime.Now;
        /// <summary>
        /// Taken out (History panel: Remove).
        /// </summary>
        public bool Removed { get; set; }
        /// <summary>
        /// For a removal, the change it takes out; null otherwise.
        /// </summary>
        public Change? RevertOf { get; init; }
        /// <summary>
        /// Live: already sent to the running game (Apply live); still undoable, which makes a new change.
        /// </summary>
        public bool Applied { get; set; }
        /// <summary>
        /// From an earlier session of the editor (HistoryFile): the world may have changed since.
        /// </summary>
        public bool Earlier { get; init; }

        /// <summary>
        /// About how much memory it holds: 64 bytes a ground point (its index, and six values before and after), a
        /// few for each thing, zone and reset (<see cref="KeepBytes"/>).
        /// </summary>
        public long Bytes => 64L * Points.Length + 8L * Things.Length + 8L * Zones.Count + 16L * Resets.Length;

        /// <summary>
        /// Whether it is this very change: a change is itself, not its values, since Removed and Applied are set after
        /// it is made while it keys the kept history's ids and frames (HistoryFile, Kept).
        /// </summary>
        public bool Equals(Change? other) => ReferenceEquals(this, other);

        /// <summary>The change's own hash, which setting Removed or Applied does not move.</summary>
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        /// <summary>
        /// A short line of what it did, like the web editor's history: "3 zones · 2 new · 1 removed · 1 zone reset".
        /// </summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (Zones.Count > 0)
            {
                parts.Add(Zones.Count > 1 ? Lang.T("save.zones.many", Zones.Count) : Lang.T("save.zones.one", Zones.Count));
            }

            int added = Things.Count(t => t.Before && !t.After), removed = Things.Count(t => !t.Before && t.After);
            if (added > 0)
            {
                parts.Add(Lang.T("history.change.new", added));
            }

            if (removed > 0)
            {
                parts.Add(Lang.T("history.change.removed", removed));
            }

            int resets = Resets.Select(r => (r.Reset.X, r.Reset.Z)).Distinct().Count();
            if (resets > 0)
            {
                parts.Add(Lang.T("save.count.resets", resets));
            }

            return string.Join(" · ", parts);
        }
    }
}
