using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>
    /// Reads a history file and fits it to the world: objects found again by kind and place (to the centimetre),
    /// the others kept as gone objects an undo or redo brings back, and the points on one grid holding them all.
    /// </summary>
    /// <returns>Null when the file is for another world or layout version, or holds no steps.</returns>
    private static Restored? ReadFile(WorldSession w, string path)
    {
        using var file = File.OpenRead(path);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var i = new BinaryReader(gz, Encoding.UTF8);
        if (i.ReadInt32() != Magic || i.ReadInt32() != Version || i.ReadString() != KeyOf(w))
        {
            return null;
        }
        bool hasStamp = i.ReadBoolean();
        string stamp = i.ReadString();
        var savedAt = new DateTime(i.ReadInt64(), DateTimeKind.Utc);
        bool changed = !hasStamp || stamp != Stamp(w);
        // The world's objects by kind and place (to the centimetre: a save keeps them exactly).
        var world = new ObjectPlaces();
        foreach (var (id, prefab, p, _, _) in w.World.Objects)
        {
            world.Add(id, prefab, p);
        }
        foreach (var (id, prefab, p, _) in w.World.Pieces)
        {
            world.Add(id, prefab, p);
        }
        int count = ReadCount(i);
        var ids = new List<int>(Capacity(count));
        var gone = new List<NewObject>();
        for (int n = 0; n < count; n++)
        {
            int prefab = i.ReadInt32();
            i.ReadBoolean();
            var position = ReadVector(i);
            var rotation = ReadVector(i);
            float scale = i.ReadSingle();
            int length = i.ReadInt32();
            byte[]? bytes = length >= 0 ? i.ReadExactBytes(length) : null;
            if (world.Take(prefab, position) is int found)
            {
                ids.Add(found);
            }
            else
            {
                // Not in the world now (deleted, or added and undone): an undo or redo brings it back.
                var o = new NewObject(w.NextId(), prefab, position, rotation, scale, Fresh: bytes == null, Raw: bytes);
                ids.Add(o.Id);
                gone.Add(o);
            }
        }
        int undoCount = ReadCount(i), redoCount = ReadCount(i);
        var (changes, points, thingIds) = ReadChanges(i, undoCount, redoCount, ids);
        if (changes.Count == 0)
        {
            return null;
        }
        var all = points.SelectMany(p => p).ToList();
        int ox = all.Count > 0 ? all.Min(p => p.X) : 0, oz = all.Count > 0 ? all.Min(p => p.Z) : 0;
        int width = all.Count > 0 ? all.Max(p => p.X) - ox + 1 : 1;
        var fitted = new List<EditSession.Change>();
        var keptIds = new Dictionary<EditSession.Change, int[]>();
        for (int n = 0; n < changes.Count; n++)
        {
            var (c, revertOf) = changes[n];
            var f = c with
            {
                Points = [.. points[n].Select(p => (p.Z - oz) * width + (p.X - ox))],
                RevertOf = revertOf >= 0 && revertOf < n ? fitted[revertOf] : null,
            };
            fitted.Add(f);
            keptIds[f] = thingIds[n];
        }
        w.Edits.KeepGone(gone);
        var kept = new EditSession.Kept([.. fitted.Take(undoCount)], [.. fitted.Skip(undoCount)], ox, oz, width, 0, 0, keptIds);
        return new Restored(kept, changes.Count, changed, savedAt.ToLocalTime());
    }
}
