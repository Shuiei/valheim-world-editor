// The prefab roots of a bundle's build pieces, found once per bundle (see GameCatalogues).

using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// Found once for the readers that need them (pieces, piece-support, piece-place, piece-cost).
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bundle, List<(Bundle.Inner, long, AssetTypeValueField, AssetTypeValueField)>> PieceRootsOf = [];

    /// <summary>
    /// Finds a bundle's piece roots: see <c>PieceRoots</c>.
    /// </summary>
    private static List<(Bundle.Inner, long, AssetTypeValueField, AssetTypeValueField)> FindPieceRoots(Bundle b, Game game)
    {
        var res = new List<(Bundle.Inner, long, AssetTypeValueField, AssetTypeValueField)>();
        var scripts = new HashSet<long>();
        var owners = new List<(Bundle.Inner, long)>();
        foreach (var (f, info) in b.Objects)
        {
            if (info.TypeId == MonoBehaviourType && game.ScriptClass(b, f, info) == "Piece" && Layout.First(f, info) is (0, var go))
            {
                scripts.Add(info.PathId);
                owners.Add((f, go));
            }
        }
        if (owners.Count == 0)
        {
            return res;
        }
        var roots = Layout.Roots(owners);
        Layout.Check(b, owners[0].Item2, roots);
        foreach (var (f, info) in b.Objects)
        {
            if (info.TypeId != GameObjectType || !roots.Contains(info.PathId) || b.Field(f, info.PathId) is not { } go)
            {
                continue;
            }
            var piece = b.ComponentIds(go).Where(c => scripts.Contains(c.Pid)).Select(c => b.Field(c.Pid)).FirstOrDefault(p => p != null && IsPiece(p));
            if (piece != null)
            {
                res.Add((f, info.PathId, go, piece));
            }
        }
        return res;
    }
}
