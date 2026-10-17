using System.Reflection;

namespace TerrainEditor.Desktop;

// Binding the script's API names to a run's callbacks.
public static partial class ScriptHost
{
    /// <summary>The callbacks of one run: the script's host class is given them by name.</summary>
    private sealed partial class Callbacks
    {
        /// <summary>Binds the API's names in the script's host class to this run's callbacks and the area's values.</summary>
        /// <param name="host">The script's <c>__Host</c> class.</param>
        public void Bind(Type host)
        {
            void Fill(string name, object? value) => host.GetField(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
            Fill("Height", new Func<float, float, float>((x, z) => Sample(_heights, x, z)));
            Fill("Original", new Func<float, float, float>((x, z) => Sample(_snap.Original, x, z)));
            Fill("Set", new Action<float, float, float>(SetHeight));
            Fill("Biome", new Func<float, float, string>((x, z) => Point(x, z) is int p and >= 0 ? ((ValheimGen.Heightmap.Biome)_snap.Biomes[p]).ToString() : "None"));
            Fill("Paint", new Action<float, float, string, float>(PaintAt));
            Fill("Mountain", new Action<float, float, string, float, float, float, float, int>(MountainAt));
            Fill("Objects", new Func<(int, string, float, float, float, string, bool, float, float, float, float)[]>(Objects));
            Fill("Remove", new Action<int>(RemoveThing));
            Fill("CanPlace", new Func<string, bool>(CanPlace));
            Fill("Place", new Action<string, float, float, float, float, float, float, float, bool>(PlaceObject));
            Fill("Move", new Action<int, float, float, float, float, float, float, float>(MoveObject));
            Fill("Noise", new Func<float, float, int, float>(NoiseAt));
            Fill("Print", new Action<string>(PrintLine));
            Fill("Check", new Action(_cancel.ThrowIfCancellationRequested));
            Fill("Offsets", new Func<float, float, float, float, (int, int)>(Offsets));
            Fill("MinX", _snap.Ox);
            Fill("MinZ", _snap.Oz);
            Fill("MaxX", _snap.Ox + _snap.W - 1);
            Fill("MaxZ", _snap.Oz + _snap.H - 1);
            Fill("Water", _snap.Water);
            Fill("Seed", _snap.Seed);
            Fill("World", _snap.World);
        }
    }
}
