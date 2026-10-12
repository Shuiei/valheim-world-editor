namespace WorldEditor.Tests.Unity;

/// <summary>
/// Unity's vertex formats a test mesh's channels can be written in (VertexFormat, the numbers Unity keeps).
/// </summary>
public enum VertexFormat
{
    /// <summary>32-bit floats (Unity's Float).</summary>
    Ieee32 = 0,
    /// <summary>16-bit floats (Unity's Float16).</summary>
    Ieee16 = 1,
    /// <summary>Bytes for 0..1.</summary>
    UNorm8 = 2,
    /// <summary>Signed bytes for -1..1.</summary>
    SNorm8 = 3,
    /// <summary>16-bit numbers for 0..1.</summary>
    UNorm16 = 4,
    /// <summary>Signed 16-bit numbers for -1..1.</summary>
    SNorm16 = 5,
}
