namespace WorldEditor.Tests.Unity;

/// <summary>
/// The bundle as bytes and as a file: its serialized file, its resource file and its table of contents.
/// </summary>
public sealed partial class TestBundle
{
    /// <summary>
    /// The bundle's bytes: its serialized file (with the table of contents) and its resource file when it has one.
    /// </summary>
    public byte[] ToBytes(bool lz4 = true, int blockSize = 1 << 17)
    {
        if (WithContents)
        {
            Assets.Add(UnityClass.AssetBundle, UnityTypes.AssetBundle, new Fields
            {
                ["m_Name"] = Name,
                ["m_Container"] = _contents.Select(c => new Fields { ["first"] = c.Path, ["second"] = new Fields { ["asset"] = Fields.Ref(c.PathId) } }).ToList(),
            });
            WithContents = false;
        }
        var fs = new UnityFsWriter { Lz4 = lz4, BlockSize = blockSize }.File(Cab, Assets.ToBytes());
        if (_resource.Count > 0)
        {
            fs.Resource(ResourceName, [.. _resource]);
        }
        return fs.ToBytes();
    }

    /// <summary>
    /// Writes the bundle into a folder, under its name.
    /// </summary>
    public string Save(string dir, bool lz4 = true, int blockSize = 1 << 17)
    {
        string path = Path.Combine(dir, Name);
        File.WriteAllBytes(path, ToBytes(lz4, blockSize));
        return path;
    }
}
