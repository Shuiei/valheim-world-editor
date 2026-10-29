using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// The contents search: the world's containers' items, or the texts of its objects (signs, portal tags, ward and
/// tombstone names), read object by object. Damaged objects are passed over and logged.
/// </summary>
internal static class WorldSearchContents
{
    /// <summary>Searches the objects that hold what the search looks at, adding the matches to the tally.</summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits (added and deleted objects).</param>
    /// <param name="tally">Where the hits go.</param>
    /// <param name="what">"items" for container contents; anything else for texts.</param>
    internal static void Run(WorldSave world, EditStore edits, WorldSearchTally tally, string what)
    {
        // Only objects with byte arrays (containers) or strings are read.
        ushort needed = what == "items" ? ZdoData.ByteArrays : ZdoData.Strings;
        Func<ObjectRef, byte[]> source = world.SourceReader();
        foreach (var (id, bytes) in Candidates(world, edits, needed, source))
        {
            if (ReadObject(bytes) is not { } z)
            {
                continue;
            }
            string name = PrefabCatalog.DisplayName(z.Prefab) ?? z.Prefab.ToString(CultureInfo.InvariantCulture);
            if (what == "items")
            {
                WorldSearchMatch.Containers(tally, id, name, z);
            }
            else
            {
                WorldSearchMatch.Texts(tally, id, name, z);
            }
        }
    }

    /// <summary>The objects to read: the world's not deleted, not vanished and not terrain, with the needed data; then the added ones.</summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits.</param>
    /// <param name="needed">The data flags an object must have.</param>
    /// <param name="source">Reads an object's saved bytes from the world's file.</param>
    private static IEnumerable<(int Id, Func<byte[]> Bytes)> Candidates(WorldSave world, EditStore edits, ushort needed, Func<ObjectRef, byte[]> source)
    {
        HashSet<int> deleted = edits.Deleted;
        for (int id = 0; id < world.ObjectRefs.Count; id++)
        {
            ObjectRef o = world.ObjectRefs[id];
            if (!deleted.Contains(id) && !world.Vanished.Contains(id) && !o.IsTerrain && (o.Flags & needed) != 0)
            {
                yield return (id, () => source(o)[(int)o.Start..(int)o.End]);
            }
        }
        foreach (NewObject n in edits.Added)
        {
            yield return (n.Id, () => world.NewObjectBytes(n, source) ?? []);
        }
    }

    /// <summary>Parses one object's bytes; a new object's bytes are built from its raw ones, which are parsed on the way.</summary>
    /// <param name="bytes">The object's bytes.</param>
    /// <returns>The object, or null when it has no bytes or is damaged (logged, and passed over).</returns>
    private static ZdoData? ReadObject(Func<byte[]> bytes)
    {
        try
        {
            // A new object's bytes are built from its raw ones, which are parsed on the way: damaged ones throw
            // here too, and that object alone is skipped.
            byte[] b = bytes();
            return b.Length == 0 ? null : ZdoData.Parse(b);
        }
        catch (Exception ex) when (CheckedReads.Damaged(ex))
        {
            Log.CaughtBrief(ex, "reading an object while searching the world (damaged: passed over)");
            return null;
        }
    }
}
