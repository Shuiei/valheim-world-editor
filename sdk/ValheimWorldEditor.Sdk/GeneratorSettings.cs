namespace ValheimWorldEditor.Sdk;

/// <summary>What a world generator keeps in a world file it made (<see cref="IWorldGenerator.ReadHeader"/>).</summary>
public sealed class GeneratorSettings
{
    /// <summary>Settings as the generator read them.</summary>
    /// <param name="description">How the player is told them ("builtin.amur v2"): two settings told alike lay out the same worlds.</param>
    /// <param name="value">The settings, in the generator's own form (the editor hands it back).</param>
    public GeneratorSettings(string description, object value)
    {
        Description = description;
        Value = value;
    }

    /// <summary>How the player is told the settings; two settings told alike lay out the same worlds.</summary>
    public string Description { get; }

    /// <summary>The settings, in the generator's own form.</summary>
    public object Value { get; }
}
