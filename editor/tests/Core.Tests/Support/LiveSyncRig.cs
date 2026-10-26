using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// A world, its edits and a sync, against a bridge that moves objects unless told otherwise.
/// </summary>
public sealed class LiveSyncRig : IDisposable
{
    public FakeBridge Bridge { get; } = new();
    public WorldSave World { get; } = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
    public EditStore Edits { get; }
    public LiveSync Sync { get; } = new();
    public LiveBridge Game { get; }
    public (int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale) Tree { get; }

    public LiveSyncRig(bool canMove = true)
    {
        if (canMove)
        {
            Bridge.Features.Add("move");
        }
        Edits = new EditStore(World);
        Game = new LiveBridge($"http://127.0.0.1:{Bridge.Port}", Bridge.Token);
        Tree = World.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
    }

    /// <summary>The tree moved as the editor moves things: deleted, with a copy keeping its data at the new place.</summary>
    public NewObject MoveTree(int newId, Vector3 offset, int? from = null)
    {
        if (from is int old)
        {
            Edits.SetDeleted([old], true);
        }
        else
        {
            Edits.SetDeleted([Tree.Id], true);
        }
        var n = new NewObject(newId, Tree.Prefab, Tree.Position + offset, Tree.Rotation + new Vector3(0, 30, 0), 0f, Tree.Id, Fresh: false);
        Edits.AddObjects([n]);
        return n;
    }

    /// <summary>The edits of an undo: the moved copy deleted, the tree restored.</summary>
    public void Undo()
    {
        Edits.SetDeleted([-1], true);
        Edits.SetDeleted([Tree.Id], false);
    }

    /// <summary>Applies the edits to the game through the sync.</summary>
    public Task<Said> Apply() => Sync.Apply(World, Edits, Game);

    public (long, uint) TreeZdo => World.ObjectRefs[Tree.Id].LiveId;

    public void Dispose()
    {
        Game.Dispose();
        Bridge.Dispose();
    }
}
