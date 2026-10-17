// The SoftRef manifest beside the bundles folder, read for the prefabs' names and the bundles the main scene needs (see GameLocations).

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// The SoftRef manifest beside the bundles folder: each asset's file name by its ID, and each
    /// bundle's dependencies.
    /// </summary>
    private sealed class Manifest
    {
        /// <summary>
        /// Each asset's file name (without extension) by its asset ID.
        /// </summary>
        public Dictionary<string, string> Names { get; } = [];
        /// <summary>
        /// Each bundle's dependencies.
        /// </summary>
        private readonly Dictionary<string, List<string>> _needs = [];
        /// <summary>
        /// Each scene's bundle, by scene name.
        /// </summary>
        private readonly Dictionary<string, string> _sceneBundle = [];

        /// <summary>
        /// Reads the manifest beside a bundles folder (its parent's "manifest" file).
        /// </summary>
        public static Manifest Read(string bundlesDir)
        {
            var m = new Manifest();
            string? id = null, bundle = null, depsOf = null;
            foreach (string raw in File.ReadLines(Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(bundlesDir))!, "manifest")))
            {
                string line = raw.Trim();
                if (raw.StartsWith("- bundle: ", StringComparison.Ordinal))
                {
                    depsOf = line[10..];
                    m._needs[depsOf] = [];
                }
                else if (depsOf != null && raw.StartsWith("  - ", StringComparison.Ordinal))
                {
                    m._needs[depsOf].Add(line[2..]);
                }
                else if (line.StartsWith("- asset ID: ", StringComparison.Ordinal))
                {
                    depsOf = null;
                    id = line[12..];
                    bundle = null;
                }
                else if (line.StartsWith("bundle: ", StringComparison.Ordinal))
                {
                    bundle = line[8..];
                }
                else if (line.StartsWith("path in bundle: ", StringComparison.Ordinal) && id != null)
                {
                    string file = line[16..];
                    m.Names[id] = Path.GetFileNameWithoutExtension(file);
                    if (file.EndsWith(".unity", StringComparison.Ordinal) && bundle != null)
                    {
                        m._sceneBundle[Path.GetFileNameWithoutExtension(file)] = bundle;
                    }
                }
            }
            return m;
        }

        /// <summary>
        /// A scene's bundle, then the bundles it needs.
        /// </summary>
        public List<string> SceneBundles(string scene) => _sceneBundle.TryGetValue(scene, out string? b)
            ? [b, .. _needs.GetValueOrDefault(b) ?? []]
            : [];
    }
}
