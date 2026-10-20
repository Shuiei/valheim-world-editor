using System.Numerics;
using TerrainEditor.Editing;
using ValheimGen;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>
    /// The ground a location's objects with a SnapToGround are put on as the game spawns it (ZoneSystem.SpawnLocation
    /// ends with SnapToGround.SnappAll, a ray down onto the terrain, before Expand World Data changes the ground): each
    /// zone's ground as generated with the location's own terrain modifiers, between the terrain mesh's points; the
    /// zones worked out as they are asked for.
    /// </summary>
    /// <param name="terrain">The world's terrain.</param>
    /// <param name="location">The location's prefab hash (its terrain modifiers).</param>
    /// <param name="at">Where the location stands.</param>
    /// <param name="turn">Its turn.</param>
    internal sealed class SnapGround(TerrainService terrain, int location, Vector3 at, Quaternion turn)
    {
        /// <summary>The zones' heights worked out so far.</summary>
        private readonly Dictionary<(int X, int Z), float[]> _zones = [];

        /// <summary>The location's terrain modifiers, made at the first height asked for.</summary>
        private Terrain.TerrainModifiers? _modifiers;

        /// <summary>The ground's height under a point.</summary>
        /// <param name="point">The point, world metres.</param>
        public float HeightAt(Vector3 point)
        {
            var zone = ChunkMath.SectorOf(point.X, point.Z);
            if (!_zones.TryGetValue(zone, out float[]? heights))
            {
                _modifiers ??= new Terrain.TerrainModifiers([new PlacedObject(0, location, at, App.BlueprintFormats.ToEuler(turn), 0)]);
                heights = [.. terrain.BaseZone(zone.X, zone.Z)];
                _modifiers.Apply(zone.X, zone.Z, heights);
                _zones[zone] = heights;
            }
            return App.Regrow.GridHeight(heights, EditStore.Grid, zone.X * 64f - 32f, zone.Z * 64f - 32f, point.X, point.Z);
        }
    }
}
