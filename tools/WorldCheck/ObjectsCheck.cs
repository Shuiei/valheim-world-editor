using TerrainEditor.Save;

/// <summary>The objects check: the most common kinds of saved objects, or the objects of one prefab byte by byte.</summary>
internal static class ObjectsCheck
{
    /// <summary>Prints the kinds of objects, or with a prefab name or hash, each of its objects with its saved bytes; returns the exit code.</summary>
    /// <param name="args">objects, the world folder, then an optional prefab name or hash.</param>
    /// <param name="world">The world whose objects are listed.</param>
    internal static int Run(string[] args, WorldSave world)
    {
        if (args.Length < 3)
        {
            foreach (var kind in world.Objects.GroupBy(o => o.Prefab).OrderByDescending(g => g.Count()).Take(30))
            {
                Console.WriteLine($"  prefab {kind.Key}: {kind.Count():N0} objects");
            }

            return 0;
        }
        int prefab = StableHash.OfName(args[2]);
        foreach (var (id, _, position, rotation, scale) in world.Objects.Where(o => o.Prefab == prefab))
        {
            byte[] bytes = world.ObjectBytes(id);
            Console.WriteLine($"  object {id} at ({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00}), {bytes.Length} B: {Convert.ToHexString(bytes)}");
        }
        return 0;
    }
}
