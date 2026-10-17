using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The setup the edit replay tests share: the game's saves, the editor's moves and raises, the ids objects come back
/// with, and the save the editor made.
/// </summary>
public static class EditReplayHelpers
{
    /// <summary>A source of ids for objects that come back, as the session gives them: -100, -101, and so on.</summary>
    public static Func<int> IdSource()
    {
        int next = -100;
        return () => next--;
    }

    /// <summary>Objects of the save near the world's middle (the test world's far ones are outside the world).</summary>
    public static List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> Near(WorldSave world) =>
        [.. world.Objects.Where(o => MathF.Abs(o.Position.X) < 200 && MathF.Abs(o.Position.Z) < 200)];

    /// <summary>The game saving the world: the next save, with these objects removed and these added (the base stays).</summary>
    public static void GameSaves(string dir, IReadOnlyList<ZoneEdit> ground, int[] removed, NewObject[]? added = null)
    {
        var r = WorldWriter.Save(WorldSave.Load(dir), ground, removed, added, options: new WorldWriter.Options(KeepBase: true));
        Assert.True(r.Saved, r.Message);
    }

    /// <summary>Whether a save has an object of this kind at this place.</summary>
    public static bool Has(WorldSave world, int prefab, Vector3 at) => world.ObjectRefs.Any(o => o.Prefab == prefab && Vector3.Distance(o.Position, at) < 0.01f);

    /// <summary>An object moved: deleted where it was, a copy keeping its data where it goes.</summary>
    public static void Move(EditStore edits, (int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale) o, int id, Vector3 to)
    {
        edits.SetDeleted([o.Id], true);
        edits.AddObjects([new NewObject(id, o.Prefab, to, o.Rotation, 0f, o.Id, Fresh: false)]);
    }

    /// <summary>One point of zone 0, 0 raised by the editor.</summary>
    public static void Raise(EditStore edits, int point, float by)
    {
        var zone = edits.Get(0, 0)!;
        zone.Modified[point] = true;
        zone.Level[point] = by;
        edits.Put(zone);
    }

    /// <summary>The zone 0, 0 as the game has it after it changed point 300 (to -1.5).</summary>
    public static ZoneEdit GameGround(TempWorld w)
    {
        var game = new EditStore(w.Load()).Get(0, 0)!;
        game.Modified[300] = true;
        game.Level[300] = -1.5f;
        return game;
    }

    /// <summary>The editor's save as the replay reads it: the zones, deleted objects and added objects it wrote.</summary>
    public static EditReplay.Saved SavedBy(EditStore edits, params (int X, int Z)[] zones) =>
        new(zones.ToHashSet(), edits.Deleted, edits.Added.ToHashSet());

    /// <summary>Copies the save files of one number to the next number (the game saving from the editor's save).</summary>
    public static void CopySave(string dir, int from, int to)
    {
        foreach (string ext in new[] { ".chunks", ".db2", ".fwl2", ".ok" })
        {
            File.Copy(Path.Combine(dir, $"_main.{from}{ext}"), Path.Combine(dir, $"_main.{to}{ext}"));
        }
    }
}
