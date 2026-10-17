using System.Numerics;
using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop;

public sealed partial class ModelStore
{
    private sealed partial class Files
    {
        /// <inheritdoc/>
        public MaterialData Material(string id)
        {
            var m = _materials[id] as JsonObject;
            float[] c = m?["color"]?.AsArray().Select(x => x!.GetValue<float>()).ToArray() ?? [1f, 1f, 1f, 1f];
            float cutoff = m?["cutoff"]?.GetValue<float>() ?? 0f;
            float[] uv = m?["uv"]?.AsArray().Select(x => x!.GetValue<float>()).ToArray() ?? [1f, 1f, 0f, 0f];
            return new MaterialData(new Vector4(Linear(c[0]), Linear(c[1]), Linear(c[2]), c.Length > 3 ? c[3] : 1f), m?["map"]?.GetValue<string>(),
                cutoff, m?["doubleSided"]?.GetValue<bool>() == true, new Vector4(uv[0], uv[1], uv[2], uv[3]), m?["transparent"]?.GetValue<bool>() == true);
        }
    }
}
