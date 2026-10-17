using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.HistoryFileSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Apply live writes the history in the background: a write later is taken when asked, before a read or a removal, with the objects as they were.
/// </summary>
[Collection("World files")]
public class HistoryFileWriteLaterTests
{
    // Apply live writes the history in the background (it can come at every edit): what is written is taken when it
    // is asked for, a read waits for it, the last write asked for is the one kept, and a removal is not undone by a
    // write still queued.
    [AvaloniaFact]
    public void AWriteLaterIsTakenWhenAskedAndComesBeforeAReadOrARemoval()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            var owner = scene.Owner!;
            s.Shape(32, 32, Formula.Compile("3", []), 4, 0, "Raise");
            HistoryFile.WriteLater(owner, s.Export());
            s.Shape(40, 40, Formula.Compile("-3", []), 4, 0, "Lower");
            string[] Labels() => [.. HistoryFile.Read(owner)!.Kept.Undo.Select(c => c.Label.Split(' ')[0])];
            Assert.Equal(["Raise"], Labels());
            HistoryFile.WriteLater(owner, s.Export());
            Assert.Equal(["Raise", "Lower"], Labels());
            string path = HistoryFile.PathOf(HistoryFile.KeyOf(owner));
            HistoryFile.WriteLater(owner, s.Export());
            HistoryFile.Delete(owner);
            Assert.False(File.Exists(path));
            HistoryFile.Flush();
            Assert.False(File.Exists(path));
            owner.Dispose();
        }
        finally
        {
            Done(dir);
        }
    }

    // Apply live's write finds the objects and reads their data in the background, from the world and the edits as
    // they were when it was asked for: what changes meanwhile (the objects added dropped, as a reload does; another
    // builder chosen) is not in it, and the file is the one Write makes of the same state.
    [AvaloniaFact]
    public void AWriteLaterWritesTheObjectsAsTheyWereWhenAsked()
    {
        string dir = Fixtures.CopyFixture();
        long builder = WorldSave.Builder;
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            var owner = scene.Owner!;
            s.Delete([s.Scene.Things.FindIndex(t => !t.Piece)]);
            s.Commit("Place", null, [],
            [
                (new Editing.NewObject(0, StableHash.Of("Rock_4"), new Vector3(5, H(s, 37, 37), 5), Vector3.Zero, 0f), false),
                (new Editing.NewObject(0, StableHash.Of("woodwall"), new Vector3(8, H(s, 40, 40), 8), Vector3.Zero, 0f), true),
            ]);
            var kept = s.Export();
            string path = HistoryFile.PathOf(HistoryFile.KeyOf(owner));
            HistoryFile.Write(owner, kept);
            var (written, objects) = Contents(path);
            Assert.Equal(3, objects);
            // The background write cannot look the objects up before the world's lock is let go.
            lock (owner.World.Sync)
            {
                HistoryFile.WriteLater(owner, kept);
                owner.Edits.ResetFrom(owner.World);
                WorldSave.Builder = builder + 1;
            }
            HistoryFile.Flush();
            Assert.Equal(written, Contents(path).Raw);
            owner.Dispose();
        }
        finally
        {
            WorldSave.Builder = builder;
            Done(dir);
        }
    }
}
