using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The live follow tests' setup: a stand-in game opened live, the game's zone reads, one look at it, and the objects of zone 0, 0.
/// </summary>
internal static class LiveFollowSetup
{
    internal static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);
    internal static readonly int TCData = StableHash.Of("TCData");

    /// <summary>
    /// What the game has in a zone, as the world was read (its objects' ZDOIDs and bytes).
    /// </summary>
    internal static List<(long, uint, byte[])> AsRead(WorldSave w, (int X, int Z) zone) =>
        [.. Enumerable.Range(0, w.ObjectRefs.Count).Where(id => !w.Vanished.Contains(id) && w.ObjectRefs[id].Zone == zone).Select(id => (w.ObjectRefs[id].LiveId.User, w.ObjectRefs[id].LiveId.Id, w.ObjectBytes(id)))];

    /// <summary>
    /// A stand-in game, the world opened live from it, and zone 0, 0 as the area.
    /// </summary>
    internal static async Task<(FakeGame Game, WorldSession World, EditSession Area)> Open()
    {
        var game = new FakeGame();
        var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var area = WorldScene.Load(world, 0, 0, 1).Session!;
        return (game, world, area);
    }

    /// <summary>
    /// A stand-in game opened as <see cref="Open"/> does, with the first saved tree of zone 0, 0: its scene index and its object id.
    /// </summary>
    internal static async Task<(FakeGame Game, WorldSession World, EditSession Area, int Tree, int Id)> OpenWithTree()
    {
        var (game, world, s) = await Open();
        int tree = Tree(s);
        return (game, world, s, tree, s.Scene.Things[tree].Id);
    }

    /// <summary>
    /// One look at the game: read and merged (null: nothing changed).
    /// </summary>
    internal static async Task<WorldSession.Followed?> Follow(WorldSession w, params (int, int)[] zones) =>
        await w.FetchGame(zones) is { } f ? w.MergeGame(f) : null;

    /// <summary>
    /// The index in the scene of a saved, non-piece object of zone 0, 0 (after skipping this many).
    /// </summary>
    internal static int Tree(EditSession s, int skip = 0) =>
        s.Scene.Things.Select((t, i) => (t, i)).Where(x => !x.t.Piece && x.t.Id >= 0 && s.Scene.World.ObjectRefs[x.t.Id].Zone == (0, 0)).Skip(skip).First().i;

    /// <summary>
    /// An object's ZDOID in the game.
    /// </summary>
    internal static (long, uint) Zdo(WorldSave w, int id) => w.ObjectRefs[id].LiveId;

    /// <summary>
    /// The game's ground for zone 0, 0: its terrain object's bytes with this data.
    /// </summary>
    internal static List<(long, uint, byte[])> Dug(WorldSave w, ZoneEdit ground)
    {
        var src = w.TerrainZones.First(t => t.ZoneX == 0 && t.ZoneZ == 0).Source!;
        int compiler = Enumerable.Range(0, w.ObjectRefs.Count).First(id => w.ObjectRefs[id].IsTerrain && w.ObjectRefs[id].File == src.File && w.ObjectRefs[id].Start == src.Start);
        var z = ZdoData.Parse(w.ObjectBytes(compiler));
        z.Set("bytes", TCData, Convert.ToBase64String(WorldWriter.EncodeTerrain(ground)));
        var objects = AsRead(w, (0, 0));
        int at = objects.FindIndex(o => (o.Item1, o.Item2) == Zdo(w, compiler));
        objects[at] = (objects[at].Item1, objects[at].Item2, z.Serialize());
        return objects;
    }
}
