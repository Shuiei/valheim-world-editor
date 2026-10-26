namespace ValheimWorldEditor.Sdk;

/// <summary>A world file as a generator reads its header (the game's ZPackage forms).</summary>
public interface IWorldFileReader
{
    /// <summary>A 32-bit integer.</summary>
    int ReadInt();

    /// <summary>A string (its length in the 7-bit form, then UTF-8).</summary>
    string ReadString();

    /// <summary>A byte.</summary>
    byte ReadByte();

    /// <summary>A count in the game's compact form (ZPackage.ReadNumItems).</summary>
    int ReadCount();

    /// <summary>A 64-bit integer.</summary>
    long ReadLong();

    /// <summary>A 32-bit float.</summary>
    float ReadSingle();

    /// <summary>A bool (one byte).</summary>
    bool ReadBool();
}
