using System.Numerics;
using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop;

public sealed partial class ModelStore
{
    /// <summary>
    /// Files in the exporter's layout (see the top).
    /// </summary>
    private sealed partial class Files : ISource
    {
        /// <summary>
        /// The folder the files are in.
        /// </summary>
        private readonly string _root;
        /// <summary>
        /// Object model names by prefab hash, from objects.json.
        /// </summary>
        private readonly Dictionary<int, string> _names = [];
        /// <summary>
        /// meshinfo.json (each mesh's vertex count and submesh index counts) and materials.json.
        /// </summary>
        private readonly JsonObject _meshInfo, _materials;

        /// <summary>
        /// Reads the index files; meshes, pieces and textures are read when asked.
        /// </summary>
        public Files(string root)
        {
            _root = root;
            _meshInfo = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "meshinfo.json")))!.AsObject();
            _materials = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "materials.json")))!.AsObject();
            foreach (var (k, v) in JsonNode.Parse(File.ReadAllText(Path.Combine(root, "objects.json")))!.AsObject())
            {
                if (int.TryParse(k, out int hash) && v?.GetValue<string>() is string n)
                {
                    _names[hash] = n;
                }
            }
        }

        /// <inheritdoc/>
        public string? ObjectName(int prefab) => _names.TryGetValue(prefab, out string? n) ? n : null;

        /// <inheritdoc/>
        public Model? Model(string name)
        {
            string file = Path.Combine(_root, "pieces", name + ".json");
            if (!File.Exists(file))
            {
                return null;
            }
            var doc = JsonNode.Parse(File.ReadAllText(file))!;
            var parts = new List<Part>();
            foreach (var p in doc["parts"]!.AsArray())
            {
                float[] m = [.. p!["m"]!.AsArray().Select(v => v!.GetValue<float>())];
                // three.js keeps matrices column by column; read in order, they are System.Numerics' rows
                // (row vectors), the same transform.
                var mat = new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
                parts.Add(new Part(p["mesh"]!.GetValue<string>(), p["sub"]!.GetValue<int>(), p["mat"]!.GetValue<string>(), mat));
            }
            float[] rs = doc["rootScale"]?.AsArray().Select(v => v!.GetValue<float>()).ToArray() ?? UnitScale;
            return new Model(name, parts, new Vector3(rs[0], rs[1], rs[2]));
        }

        /// <inheritdoc/>
        public MeshData? Mesh(string id)
        {
            if (_meshInfo[id] is not JsonObject info)
            {
                return null;
            }
            int v = info["v"]!.GetValue<int>();
            int[] subs = [.. info["sub"]!.AsArray().Select(c => c!.GetValue<int>())];
            byte[] bytes = File.ReadAllBytes(Path.Combine(_root, "meshes", id + ".bin"));
            float[] verts = new float[v * 8];
            Buffer.BlockCopy(bytes, 0, verts, 0, v * 32);
            int off = v * 32;
            uint[][] indices = new uint[subs.Length][];
            for (int s = 0; s < subs.Length; s++)
            {
                indices[s] = new uint[subs[s]];
                Buffer.BlockCopy(bytes, off, indices[s], 0, subs[s] * 4);
                off += subs[s] * 4;
            }
            return new MeshData(verts, indices);
        }
    }
}
