using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;

namespace WorldEditor.Tests;

/// <summary>
/// The world the map data tests read: the world with its terrain service (a 64 px overview) and its edit store, and the
/// height of a layer at a column and row.
/// </summary>
public static class MapDataTestWorld
{
    /// <summary>
    /// The world, its terrain service (a 64 px overview) and its edit store.
    /// </summary>
    public static (WorldSave World, TerrainService Terrain, EditStore Edits) Open(TempWorld w)
    {
        var world = w.Load();
        return (world, new TerrainService(world, null, overviewSize: 64), new EditStore(world));
    }

    /// <summary>
    /// The map data of the world, read with its terrain service and edit store.
    /// </summary>
    public static MapData Map(TempWorld w)
    {
        var (_, terrain, edits) = Open(w);
        return new MapData(terrain, edits);
    }

    /// <summary>
    /// The height in the layers at this column and row.
    /// </summary>
    public static float H(MapData.Layers l, int col, int row) => (float)l.Heights[row * l.Width + col];
}
