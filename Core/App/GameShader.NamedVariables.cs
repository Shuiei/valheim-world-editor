namespace TerrainEditor.App;

// The names of a module's textures, inputs and outputs, put back from its bindings and locations (see GameShader).
public static partial class GameShader
{
    /// <summary>
    /// The names of a module's textures, inputs and outputs, from their bindings and locations.
    /// </summary>
    /// <param name="decl">The module's declarations.</param>
    /// <param name="prm">The names of the parameter blob.</param>
    /// <param name="add">The instructions to add to the module, collected here.</param>
    private static void AddVariableNames(SpirvDeclarations decl, Params prm, List<(uint Op, List<uint> Words)> add)
    {
        foreach (var (v, _, storage) in decl.Variables)
        {
            var d = decl.Decorations.GetValueOrDefault(v) ?? [];
            if (storage == 0 && d.TryGetValue(33, out uint[]? binding))
            {
                string name = prm.Textures.GetValueOrDefault(binding[0]) ?? throw new InvalidDataException($"The terrain shader has no texture for binding {binding[0]}.");
                add.Add((5, new List<uint> { v }.Concat(StringWords(name)).ToList()));
            }
            else if (storage == 1 && d.ContainsKey(30) && !decl.Names.ContainsKey(v))
            {
                add.Add((5, new List<uint> { v }.Concat(StringWords("vs_COLOR0")).ToList()));
            }
            else if (storage == 3 && d.TryGetValue(30, out uint[]? loc))
            {
                add.Add((5, new List<uint> { v }.Concat(StringWords($"SV_Target{loc[0]}")).ToList()));
            }
        }
    }
}
