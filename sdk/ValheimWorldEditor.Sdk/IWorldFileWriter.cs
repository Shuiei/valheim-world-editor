namespace ValheimWorldEditor.Sdk;

/// <summary>A world file as a generator writes its header (the game's ZPackage forms, as <see cref="IWorldFileReader"/> reads them).</summary>
public interface IWorldFileWriter
{
    /// <summary>A 32-bit integer.</summary>
    void WriteInt(int value);

    /// <summary>A string.</summary>
    void WriteString(string value);

    /// <summary>A byte (a count below 128 is written so too).</summary>
    void WriteByte(byte value);

    /// <summary>A 64-bit integer.</summary>
    void WriteLong(long value);

    /// <summary>A 32-bit float.</summary>
    void WriteSingle(float value);

    /// <summary>A bool (one byte).</summary>
    void WriteBool(bool value);
}
