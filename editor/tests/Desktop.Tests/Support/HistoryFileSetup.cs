using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The history file tests' setup: a copy of the test world with its edits saved, and the history file's raw contents, zipped
/// and unzipped.
/// </summary>
internal static class HistoryFileSetup
{
    /// <summary>
    /// Deletes the copy of the test world (its temporary parent folder).
    /// </summary>
    internal static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    internal static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    /// <summary>
    /// Raises the ground, deletes a tree, places a rock; saves; the world is left (the editor closed).
    /// </summary>
    internal static (float Was, WorldScene.Thing Tree, Vector3 Rock) EditAndSave(string dir)
    {
        var scene = WorldScene.Load(dir, 0, 0, 1);
        var s = scene.Session!;
        float was = H(s, 32, 32);
        int tree = s.Scene.Things.FindIndex(t => !t.Piece);
        var treeThing = s.Scene.Things[tree];
        s.Shape(32, 32, Formula.Compile("3", []), 4, 0, "Raise");
        s.Delete([tree]);
        var rock = new Vector3(5, H(s, 37, 37), 5);
        s.Commit("Place", null, [], [(new Editing.NewObject(0, StableHash.Of("Rock_4"), rock, Vector3.Zero, 0f), false)]);
        Assert.True(s.Save().Saved);
        Assert.True(File.Exists(HistoryFile.PathOf(HistoryFile.KeyOf(scene.Owner!))));
        scene.Owner!.Dispose();
        return (was, treeThing, rock);
    }

    /// <summary>
    /// A history file's unzipped contents.
    /// </summary>
    internal static byte[] Unzip(string path)
    {
        using var gz = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Writes a history file's contents, zipped, in place of the file at <paramref name="path"/>.
    /// </summary>
    internal static void Zip(string path, byte[] raw)
    {
        using var gz = new System.IO.Compression.GZipStream(File.Create(path), System.IO.Compression.CompressionLevel.Fastest);
        gz.Write(raw);
    }

    /// <summary>
    /// The offset of the object count in unzipped history contents: after the header (magic, version, key, has stamp,
    /// stamp, saved at).
    /// </summary>
    internal static int CountOffset(byte[] raw)
    {
        using var r = new BinaryReader(new MemoryStream(raw), System.Text.Encoding.UTF8);
        r.ReadInt32();
        r.ReadInt32();
        r.ReadString();
        r.ReadBoolean();
        r.ReadString();
        r.ReadInt64();
        return (int)r.BaseStream.Position;
    }

    /// <summary>
    /// A history file's contents, unzipped, with the time it was written blanked (it differs from one write to the
    /// next), and how many objects it holds.
    /// </summary>
    internal static (byte[] Raw, int Objects) Contents(string path)
    {
        byte[] raw = Unzip(path);
        int at = CountOffset(raw) - sizeof(long);
        int objects = BitConverter.ToInt32(raw, at + sizeof(long));
        Array.Clear(raw, at, sizeof(long));
        return (raw, objects);
    }
}
