using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// Creatures are never sunk into building pieces: one whose origin lies inside a piece's collider is lifted to stand on
/// the top of the highest such piece at its spot (Stability.Standing), wherever the editor positions objects (pieces put
/// down, moves, copies, pastes, scripts, blueprints opened or added). The ground is not a piece: a creature stands on
/// it as placed.
/// </summary>
internal static class CreatureStand
{
    /// <summary>
    /// Stands the creatures among the objects about to go into the area on the pieces under them, changing their
    /// heights in place. The pieces counted are those of the area as it will be: its own (the ones leaving aside), the
    /// moved ones where they go, and the new ones.
    /// </summary>
    /// <param name="scene">The open area.</param>
    /// <param name="adds">New objects, with whether each is a player-built piece; a creature's height may change.</param>
    /// <param name="moves">Objects moved; a creature's height may change.</param>
    /// <param name="leaving">Indices of the scene's things taken away in the same step, which hold nothing up.</param>
    /// <returns>How many creatures were lifted.</returns>
    internal static int Apply(WorldScene scene, List<(NewObject Object, bool Piece)>? adds, List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)>? moves = null, IReadOnlyCollection<int>? leaving = null)
    {
        var standing = new Stability.Standing(PiecesAround(scene, adds, moves, leaving));
        int stood = Lift(adds, standing, a => (a.Object.Prefab, a.Object.Position), (a, y) => (a.Object with { Position = a.Object.Position with { Y = y } }, a.Piece));
        return stood + Lift(moves, standing, m => (scene.Things[m.Index].Prefab, m.Position), (m, y) => m with { Position = m.Position with { Y = y } });
    }

    /// <summary>The creatures of the area whose origin lies inside a building piece (indices into the scene's things).</summary>
    /// <param name="scene">The open area.</param>
    internal static List<int> Sunk(WorldScene scene)
    {
        var standing = new Stability.Standing(PiecesAround(scene, null, null, null));
        var sunk = new List<int>();
        lock (scene.Things)
        {
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (!t.Gone && PrefabCatalog.IsCreature(t.Prefab) && standing.Top(t.Position) != null)
                {
                    sunk.Add(i);
                }
            }
        }
        return sunk;
    }

    /// <summary>The sentence a step's message ends with when creatures were lifted; empty when none were.</summary>
    /// <param name="stood">How many creatures were lifted.</param>
    internal static Said Note(int stood) => stood > 0 ? Said.Of("creatures.stood", stood) : Said.Empty;

    /// <summary>
    /// The building pieces (those with support data) of the area as it will be after a step: the scene's own that stay
    /// where they are, the moved ones where they go, and the new ones.
    /// </summary>
    private static List<Stability.Piece> PiecesAround(WorldScene scene, List<(NewObject Object, bool Piece)>? adds, List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)>? moves, IReadOnlyCollection<int>? leaving)
    {
        var pieces = new List<Stability.Piece>();
        var moving = (moves ?? []).Select(m => m.Index).Concat(leaving ?? []).ToHashSet();
        lock (scene.Things)
        {
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (!t.Gone && !moving.Contains(i))
                {
                    AddPiece(pieces, t.Prefab, t.Position, t.Rotation, t.Scale);
                }
            }
            foreach (var m in moves ?? [])
            {
                AddPiece(pieces, scene.Things[m.Index].Prefab, m.Position, m.Rotation, m.Scale);
            }
        }
        foreach (var (o, _) in adds ?? [])
        {
            AddPiece(pieces, o.Prefab, o.Position, o.Rotation, o.Scale);
        }
        return pieces;
    }

    /// <summary>Adds an object as a piece when the support data knows its prefab.</summary>
    private static void AddPiece(List<Stability.Piece> pieces, int prefab, Vector3 position, Vector3 rotation, float scale)
    {
        if (PrefabCatalog.NameOf(prefab) is string name && Stability.Known(name))
        {
            pieces.Add(new Stability.Piece(name, position, BlueprintFormats.FromEuler(rotation), scale > 0 ? scale : 1f));
        }
    }

    /// <summary>
    /// Lifts the creatures of a list onto the pieces under them, each replaced in the list at its new height.
    /// </summary>
    /// <param name="items">The objects; null for none.</param>
    /// <param name="standing">The pieces around.</param>
    /// <param name="of">An item's prefab and origin.</param>
    /// <param name="lifted">The item at a new height.</param>
    /// <returns>How many were lifted.</returns>
    private static int Lift<T>(List<T>? items, Stability.Standing standing, Func<T, (int Prefab, Vector3 Position)> of, Func<T, float, T> lifted)
    {
        int stood = 0;
        for (int i = 0; i < items?.Count; i++)
        {
            var (prefab, position) = of(items[i]);
            if (PrefabCatalog.IsCreature(prefab) && standing.Top(position) is float y)
            {
                items[i] = lifted(items[i], y);
                stood++;
            }
        }
        return stood;
    }
}
