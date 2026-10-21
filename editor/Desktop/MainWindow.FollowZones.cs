namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The zones an area covers, by their zone coordinates, row by row from its lowest Z.
    /// </summary>
    /// <param name="scene">The area.</param>
    /// <returns>Each zone's X and Z, in the order the rows are read.</returns>
    internal static List<(int, int)> ZonesOf(WorldScene scene)
    {
        var zones = new List<(int, int)>();
        for (int z = 0; z < scene.ZonesZ; z++)
        {
            for (int x = 0; x < scene.ZonesX; x++)
            {
                zones.Add((scene.X0 + x, scene.Z0 + z));
            }
        }
        return zones;
    }
}
