#nullable disable

namespace ValheimGen;

/// <summary>
/// Valheim's Utils: the one helper the generator calls.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Where v lies from l (0) to h (1), held to 0..1.
    /// </summary>
    public static float LerpStep(float l, float h, float v) => Mathf.Clamp((v - l) / (h - l), 0f, 1f);
}
