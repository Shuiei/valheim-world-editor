using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>
    /// Everything players build: the hammer's pieces, the cultivator's, the serving tray's and the mods' (prefab,
    /// English name, tab). The hoe's are ground edits, not pieces. Ordered by station, then by name. Worked out again
    /// when the game's data changes (mods' pieces learned from a live game) or the mods' manifests do (pieces not
    /// offered, groups).
    /// </summary>
    internal static List<(string Prefab, string Name, int Category)> Pieces => CurrentPieces();

    /// <summary>The pieces as worked out now: <see cref="ListPieces"/> again when the game's data or the mods' manifests changed since.</summary>
    /// <returns>The pieces, in <see cref="Pieces"/>' order.</returns>
    private static List<(string Prefab, string Name, int Category)> CurrentPieces()
    {
        var made = _pieceList;
        var now = (GameData.Generation, ModManifests.Generation);
        if (made.Generation != now)
        {
            made = (now, ListPieces());
            _pieceList = made;
        }
        return made.List;
    }

    /// <summary>Works out <see cref="Pieces"/>.</summary>
    private static List<(string Prefab, string Name, int Category)> ListPieces()
    {
        return [.. PieceCatalog.Names.Select(n => (n, PieceCatalog.Get(StableHash.Of(n))))
            .Where(p => p.Item2 is { IsBuildPiece: true } && !ModManifests.IsHidden(p.n))
            .Select(p => (p.n, PieceCost.PieceName(p.n), TabOf(p.n, p.Item2!)))
            .OrderBy(p => ModGroup(p.n), StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => PieceGroup(p.n) ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => StationRank(PieceCost.Get(p.n)?.Station))
            .ThenBy(p => PieceCost.Get(p.n)?.Station ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Item2, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The tab a piece is listed under: the mods' for a mod's piece, else the cultivator's, the serving tray's or its hammer category.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="piece">The piece's catalogue entry.</param>
    internal static int TabOf(string prefab, PieceCatalog.Info piece) => ModCatalogue.IsModded(prefab) ? Mods : piece.Tool switch { "cultivator" => Plants, "feaster" => Feasts, _ => piece.Category };
}
