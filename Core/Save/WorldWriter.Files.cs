using TerrainEditor.App;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>
    /// Writes a chunk file anew: its objects copied byte for byte, less the removed ones, with patched TCData where
    /// given, then the additions.
    /// </summary>
    /// <param name="patches">New TCData by object start offset, or null.</param>
    /// <param name="additions">Objects to append, or null.</param>
    /// <param name="removals">Start offsets of objects to leave out, or null.</param>
    /// <param name="sourcePath">The chunk file as it is.</param>
    /// <param name="targetPath">The new chunk file; must not exist yet.</param>
    /// <param name="file">The chunk file as read.</param>
    /// <returns>How many objects the new file holds.</returns>
    private static int WriteChunk(string sourcePath, string targetPath, ChunkFile file, Dictionary<long, (ZdoLocation Loc, byte[] Data)>? patches, List<byte[]>? additions, HashSet<long>? removals)
    {
        byte[] source = File.ReadAllBytes(sourcePath);
        int count = file.Count + (additions?.Count ?? 0) - file.Objects.Count(o => removals?.Contains(o.Start) == true);
        using (FileStream fs = new(targetPath, FileMode.CreateNew, FileAccess.Write))
        using (BinaryWriter w = new(fs))
        {
            w.Write((short)file.WorldVersion);
            w.Write(count);
            foreach (var (start, end) in file.Objects)
            {
                if (removals != null && removals.Contains(start))
                {
                    continue;
                }
                if (patches != null && patches.TryGetValue(start, out var patch))
                {
                    var (loc, data) = patch;
                    w.Write(source, (int)start, (int)(loc.DataStart - start));
                    w.Write(data.Length);
                    w.Write(data);
                    w.Write(source, (int)loc.DataEnd, (int)(end - loc.DataEnd));
                }
                else
                {
                    w.Write(source, (int)start, (int)(end - start));
                }
            }
            foreach (byte[] obj in additions ?? [])
            {
                w.Write(obj);
            }
            w.Flush();
            fs.Flush(flushToDisk: true);
        }
        return count;
    }

    /// <summary>
    /// A .chunks index (version, total object count, chunk count, then per chunk its id, size, file version and object
    /// count) in the given save format, flushed to disk before returning: the save's and a new world's.
    /// </summary>
    internal static void WriteIndex(string path, int format, IReadOnlyList<(ushort Chunk, byte Size, uint Version, int Count)> chunks)
    {
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        using BinaryWriter w = new(fs);
        w.Write((short)format);
        w.Write(chunks.Sum(c => c.Count));
        w.Write(chunks.Count);
        foreach (var (chunk, size, version, count) in chunks)
        {
            w.Write(chunk);
            w.Write(size);
            w.Write(version);
            w.Write(count);
        }
        w.Flush();
        fs.Flush(flushToDisk: true);
    }

    /// <summary>Writes a file and flushes it to disk before returning.</summary>
    internal static void WriteAllBytesDurable(string path, byte[] bytes)
    {
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        fs.Write(bytes);
        fs.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Copies a file (over one there) and flushes the copy to disk before returning: the commit marker written after
    /// it must not be on disk before the files it vouches for.
    /// </summary>
    private static void CopyDurable(string source, string target)
    {
        using FileStream from = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream to = new(target, FileMode.Create, FileAccess.Write);
        from.CopyTo(to);
        to.Flush(flushToDisk: true);
    }

    /// <summary>The file extensions of one save number's _main files.</summary>
    private static readonly string[] MainExtensions = [".ok", ".chunks", ".db2", ".fwl2"];

    /// <summary>Deletes the files, ignoring any that cannot be deleted.</summary>
    private static void RemoveAll(IEnumerable<string> files)
    {
        foreach (string f in files)
        {
            try
            {
                File.Delete(f);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.CaughtBrief(ex, "removing a file of a save that was not made (left behind)");
            }
        }
    }
}
