using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Searching the whole world (like Amulet's find, or WorldEdit's //count): objects of a kind, containers
/// holding an item, or objects whose texts match (signs, portal tags, ward and tombstone names).
/// </summary>
public static class WorldSearch
{
    /// <summary>One object found.</summary>
    /// <param name="Id">The object's id (negative for one added in this session).</param>
    /// <param name="Name">Its display name, else its prefab hash.</param>
    /// <param name="X">World x (metres).</param>
    /// <param name="Y">Height (metres).</param>
    /// <param name="Z">World z (metres).</param>
    /// <param name="Match">What matched ("Item ×stack", "key: text"); null for a kind search.</param>
    public sealed record Hit(int Id, string Name, float X, float Y, float Z, string? Match);

    /// <summary>A search's results.</summary>
    /// <param name="Hits">The hits, at most the limit.</param>
    /// <param name="Counts">
    /// Per kind name, item name (stack sizes added up) or, for texts, object name: how many were found.
    /// </param>
    /// <param name="Total">How many hits there were in all.</param>
    /// <param name="Truncated">Whether more hits were found than <paramref name="Hits"/> holds.</param>
    public sealed record Result(List<Hit> Hits, Dictionary<string, int> Counts, int Total, bool Truncated);

    /// <summary>
    /// Searches the world's objects and those added in this session, deleted ones left out; matching ignores
    /// case. (Live: under the world's lock, so what the game changes comes in before or after, not during.)
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits (added and deleted objects).</param>
    /// <param name="query">The text looked for; empty finds nothing. Names starting with _ only when it does too.</param>
    /// <param name="what">"kinds" (object names), "items" (container contents), anything else: texts.</param>
    /// <param name="limit">How many hits to keep.</param>
    public static Result Search(WorldSave world, EditStore edits, string query, string what, int limit = 2000)
    {
        lock (world.Sync)
        {
            return SearchIn(world, edits, query, what, limit);
        }
    }

    /// <summary><see cref="Search"/>'s work, with the world's lock held.</summary>
    private static Result SearchIn(WorldSave world, EditStore edits, string query, string what, int limit)
    {
        query = query.Trim();
        var tally = new WorldSearchTally(query, limit);
        if (query.Length == 0)
        {
            return tally.Finish();
        }
        if (what == "kinds")
        {
            WorldSearchKinds.Run(world, edits, tally);
        }
        else
        {
            // Items and texts: only objects with byte arrays (containers) or strings are read.
            WorldSearchContents.Run(world, edits, tally, what);
        }
        return tally.Finish();
    }
}
