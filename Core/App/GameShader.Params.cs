namespace TerrainEditor.App;

// Reading the names of a parameter blob: the uniform buffers' members and the textures (see GameShader).
public static partial class GameShader
{
    /// <summary>
    /// The names of the constant buffers' members and of the textures. Records are a length-prefixed name
    /// (padded to 4 bytes) followed by numbers: a constant buffer (size, member count), a member (type,
    /// rows, columns, is matrix, array size, offset), a texture (0, binding with flags in the top byte,
    /// sampler, dimension). Only buffers whose name contains "Globals" are kept.
    /// </summary>
    internal static Params Parameters(byte[] entry)
    {
        var recs = ParameterRecords(entry);
        var buffers = new Dictionary<string, Dictionary<uint, string>>();
        var textures = new Dictionary<uint, string>();
        int i = 0;
        while (i < recs.Count)
        {
            var (name, nums) = recs[i];
            bool globals = name.Contains("Globals", StringComparison.Ordinal);
            if (globals && nums.Count >= 2 && nums[1] > 0 && nums[1] < recs.Count - i && recs.Skip(i + 1).Take((int)nums[1]).All(r => r.Nums.Count >= 6))
            {
                var members = new Dictionary<uint, string>();
                foreach (var (mname, m) in recs.Skip(i + 1).Take((int)nums[1]))
                {
                    members[m[5]] = mname;
                }
                buffers[name] = members;
                i += 1 + (int)nums[1];
                continue;
            }
            if (!globals && nums.Count >= 4 && nums[0] == 0)
            {
                textures[nums[1] & 0xFFFF] = name;
            }
            i++;
        }
        return new Params(buffers, textures);
    }
}
