using System.IO.Compression;
using System.Text;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>The id of an object of the file left out: it was in a zone reset since (see <see cref="Fitting.Reset"/>).</summary>
    internal const int Dropped = int.MinValue;

    /// <summary>
    /// Reads a history file and fits it to the world: objects found again by kind and place (to the centimetre),
    /// the others kept as gone objects an undo or redo brings back, the ground values onto the generated ground as it
    /// is now, and the points on one grid holding them all.
    /// </summary>
    /// <param name="w">The world.</param>
    /// <param name="from">The file's contents; left open.</param>
    /// <param name="fit">How the steps are fitted to the world as it is now.</param>
    /// <returns>Null when the file is for another world or layout version, or holds no steps.</returns>
    private static Restored? ReadFrom(WorldSession w, Stream from, Fitting fit)
    {
        using var gz = new GZipStream(from, CompressionMode.Decompress, leaveOpen: true);
        using var i = new BinaryReader(gz, Encoding.UTF8);
        int magic = i.ReadInt32(), version = i.ReadInt32();
        // Formats 1 (before mods' hidden objects) and 2 (before the generated ground) are read too.
        if (magic != Magic || version is not (1 or 2 or Version) || i.ReadString() != KeyOf(w))
        {
            return null;
        }
        bool hasStamp = i.ReadBoolean();
        string stamp = i.ReadString();
        var savedAt = new DateTime(i.ReadInt64(), DateTimeKind.Utc);
        // Carried across a re-read, the steps match the world: the session itself changed it.
        bool changed = !fit.Carried && (!hasStamp || stamp != Stamp(w));
        // The world's objects by kind and place (to the centimetre: a save keeps them exactly), and those added and
        // not saved yet (a history carried across an Apply live).
        var world = new ObjectPlaces();
        foreach (var (id, prefab, p, _, _) in w.World.Objects)
        {
            world.Add(id, prefab, p);
        }
        foreach (var (id, prefab, p, _) in w.World.Pieces)
        {
            world.Add(id, prefab, p);
        }
        foreach (var o in w.Edits.Added)
        {
            world.Add(o.Id, o.Prefab, o.Position);
        }
        int count = ReadCount(i);
        var ids = new List<int>(Capacity(count));
        var gone = new List<NewObject>();
        for (int n = 0; n < count; n++)
        {
            int prefab = i.ReadInt32();
            bool piece = i.ReadBoolean();
            var position = ReadVector(i);
            var rotation = ReadVector(i);
            float scale = i.ReadSingle();
            int length = i.ReadInt32();
            byte[]? bytes = length >= 0 ? i.ReadExactBytes(length) : null;
            if (fit.Reset.TryGetValue(ChunkMath.SectorOf(position.X, position.Z), out var reset)
                && !(reset.KeepBuildings && (piece || TerrainEditor.App.ModManifests.IsData(prefab))))
            {
                // Its zone was made again (the game may have put the same kind back at the same place): what the steps
                // did to it is left out. Buildings the reset kept are still theirs.
                ids.Add(Dropped);
            }
            else if (world.Take(prefab, position) is int found)
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
        var (read, readPoints, readIds) = ReadChanges(i, undoCount, redoCount, ids, version, fit);
        var lifted = version >= 3 ? ReadLifted(i) : [];
        // A step left with nothing to do (all of it in zones reset since) is left out.
        var keep = Enumerable.Range(0, read.Count)
            .Where(n => readPoints[n].Length > 0 || read[n].C.Things.Length > 0 || read[n].C.Hidden.Length > 0 || read[n].C.Resets.Length > 0).ToList();
        if (keep.Count == 0)
        {
            return null;
        }
        var all = keep.SelectMany(n => readPoints[n]).ToList();
        int ox = all.Count > 0 ? all.Min(p => p.X) : 0, oz = all.Count > 0 ? all.Min(p => p.Z) : 0;
        int width = all.Count > 0 ? all.Max(p => p.X) - ox + 1 : 1;
        var fitted = new List<EditSession.Change>();
        var at = new Dictionary<int, EditSession.Change>();
        var keptIds = new Dictionary<EditSession.Change, int[]>();
        foreach (int n in keep)
        {
            var (c, revertOf) = read[n];
            var f = c with
            {
                Points = [.. readPoints[n].Select(p => (p.Z - oz) * width + (p.X - ox))],
                RevertOf = revertOf >= 0 && revertOf < n && at.TryGetValue(revertOf, out var reverted) ? reverted : null,
            };
            fitted.Add(f);
            at[n] = f;
            keptIds[f] = readIds[n];
        }
        int undoKept = keep.Count(n => n < undoCount);
        w.Edits.KeepGone(gone);
        TakeLiftedBack(w, lifted);
        var kept = new EditSession.Kept([.. fitted.Take(undoKept)], [.. fitted.Skip(undoKept)], ox, oz, width, 0, 0, keptIds);
        return new Restored(kept, fitted.Count, changed, savedAt.ToLocalTime());
    }
}
