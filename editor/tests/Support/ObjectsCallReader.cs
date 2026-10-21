namespace WorldEditor.Tests;

/// <summary>
/// Reads the parts of an objects call that the stand-in plugin and the stand-in game both answer from.
/// </summary>
public static class ObjectsCallReader
{
    /// <summary>
    /// Reads the objects an objects call creates: each one's bytes are skipped, and a new id is made for it by
    /// <paramref name="newId"/>. Returns the ids in the order the objects came.
    /// </summary>
    /// <param name="r">The call's request, positioned at the number of objects created.</param>
    /// <param name="newId">Makes the id of one created object (its text as the answer carries it).</param>
    public static List<string> ReadCreated(BinaryReader r, Func<string> newId)
    {
        int create = r.ReadInt32();
        var ids = new List<string>();
        for (int i = 0; i < create; i++)
        {
            r.ReadBytes(r.ReadInt32());
            ids.Add(newId());
        }
        return ids;
    }
}
