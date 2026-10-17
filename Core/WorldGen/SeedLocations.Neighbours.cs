namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// Instances by a name (prefab or group), for "is one within this distance" (3D, as the game measures).
    /// </summary>
    internal sealed class Neighbours
    {
        /// <summary>
        /// The instances under each name.
        /// </summary>
        private readonly Dictionary<string, List<Placed>> _byName = [];

        /// <summary>
        /// Adds an instance under a name.
        /// </summary>
        public void Add(string name, Placed p)
        {
            if (!_byName.TryGetValue(name, out var list))
            {
                _byName[name] = list = [];
            }
            list.Add(p);
        }

        /// <summary>
        /// Whether an instance under the name is closer than distance (metres, 3D) to at.
        /// </summary>
        public bool Within(string name, Vector3 at, float distance)
        {
            if (!_byName.TryGetValue(name, out var list))
            {
                return false;
            }
            float limit = distance * distance;
            foreach (var p in list)
            {
                float dx = p.X - at.X, dy = p.Y - at.Y, dz = p.Z - at.Z;
                if (dx * dx + dy * dy + dz * dz < limit)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
