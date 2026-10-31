using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// A mod's pieces without a Piece: see <c>ReadLoose</c>.
    /// </summary>
    internal sealed partial class PieceScan
    {
        /// <summary>
        /// The game's scripts that make a root prefab something other than a build piece: an item, a creature, a
        /// projectile or an effect.
        /// </summary>
        private static readonly HashSet<string> NotPieces = new(StringComparer.Ordinal)
        {
            "ItemDrop", "Character", "Humanoid", "Player", "MonsterAI", "AnimalAI", "BaseAI", "Projectile", "Aoe", "TimedDestruction", "ZSFX",
        };

        /// <summary>
        /// A mod's root prefabs that are build pieces without a Piece (the mod adds it in code): those with snap points,
        /// none of the scripts of <see cref="NotPieces"/>, not a piece root already. Each is a piece of the Building tab
        /// ("cat" 2, "loose" 1) with its snap points and box, on the hammer.
        /// </summary>
        /// <param name="b">The bundle.</param>
        /// <param name="game">The bundles, for the scripts' classes.</param>
        /// <param name="taken">The piece roots found with a Piece.</param>
        private void ReadLoose(Bundle b, Game game, HashSet<long> taken)
        {
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId != TransformType || b.Field(f, info.PathId) is not { } tr || tr["m_Father.m_PathID"].AsLong != 0)
                {
                    continue;
                }
                long pid = tr["m_GameObject.m_PathID"].AsLong;
                if (taken.Contains(pid) || b.Field(f, pid) is not { } go
                    || b.ComponentIds(go).Any(c => c.Type == MonoBehaviourType && game.ScriptClass(b, c.Pid) is string s && NotPieces.Contains(s)))
                {
                    continue;
                }
                var pts = new List<V3>();
                var snaps = new List<object?>();
                Walk(b, pid, default, Quat.Root, (1, 1, 1), true, pts, snaps);
                string name = go["m_Name"].AsString;
                if (snaps.Count == 0 || _entries.ContainsKey(name))
                {
                    continue;
                }
                var entry = new Dict { ["cat"] = 2L, ["comfort"] = 0L, ["snap"] = snaps, ["loose"] = 1L };
                if (pts.Count > 0)
                {
                    entry["box"] = new List<object?> { R(Min(pts.Select(q => q.X)), 3), R(Max(pts.Select(q => q.X)), 3), R(Min(pts.Select(q => q.Z)), 3), R(Max(pts.Select(q => q.Z)), 3), R(Min(pts.Select(q => q.Y)), 3), R(Max(pts.Select(q => q.Y)), 3) };
                }
                _entries[name] = entry;
                _loose.Add(name);
            }
        }
    }
}
