using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A window on a copy of the test world with a one-room Frost Cave committed at At and the Dungeon tool on; Said keeps
/// the tool's last message. The Dungeon tool tests share it.
/// </summary>
internal sealed class SharedDungeonToolRun : IDisposable
{
    /// <summary>The kind of the cave the tests build.</summary>
    public static readonly int Cave = StableHash.Of("DG_Cave");

    /// <summary>Where the cave is made, in the world.</summary>
    public static readonly Vector3 At = new(10, 5100, 10);

    public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
    public string Dir { get; } = Fixtures.CopyFixture();
    public WorldScene Scene { get; }
    public DungeonTool T => W.View.Dungeon;
    public DungeonPanel P => W.DungeonPanel;
    public string? Said;

    public SharedDungeonToolRun()
    {
        Scene = WorldScene.Load(Dir, 0, 0, 1);
        W.Show();
        W.View.Show(Scene, null);
        W.Edit(Scene.Session!);
        // A Frost Cave of one room: its entrance, three open ends.
        var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), At, Quaternion.Identity);
        byte[] blank = ZdoBuilder.Blank(Cave, Terrain.PrefabCatalog.Get(Cave)!.Flags, At, Vector3.Zero, 0f);
        Scene.Session!.Commit("A dungeon", null, [], [(new NewObject(0, Cave, At, Vector3.Zero, 0, null, false, Dungeons.WithRooms(blank, [entrance])), false)]);
        T.Message += t => Said = t.Shown;
        W.Tools.ChooseMode(ToolMode.Dungeon);
    }

    public List<Dungeons.Placed> Rooms => T.Dungeon!.Rooms;

    // Points the tool at a free opening from straight above it.
    public void PointAt(Dungeons.OpenEnd end) => T.Hover(end.Position + new Vector3(0, 30, 0), -Vector3.UnitY);

    /// <summary>
    /// The room list the dungeon object holds now (what Save to world writes).
    /// </summary>
    public static List<Dungeons.Placed> Saved(SharedDungeonToolRun r)
    {
        var d = r.T.Dungeon!;
        byte[] bytes = ObjectData.Bytes(r.Scene.World, r.Scene.Session!.Edits, d.Thing.Id)!;
        return Dungeons.Read(ZdoData.Parse(bytes).GetBytes(Dungeons.RoomDataKey)!);
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
}
