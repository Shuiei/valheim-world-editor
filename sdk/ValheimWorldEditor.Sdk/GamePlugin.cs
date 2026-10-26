namespace ValheimWorldEditor.Sdk;

/// <summary>A BepInEx plugin the connected game loaded (<see cref="ILiveGame.Plugins"/>).</summary>
public sealed class GamePlugin
{
    /// <summary>A plugin as the game's bridge lists it.</summary>
    /// <param name="id">Its id (BepInEx GUID).</param>
    /// <param name="name">Its name.</param>
    /// <param name="version">Its version.</param>
    public GamePlugin(string id, string name, string version)
    {
        Id = id;
        Name = name;
        Version = version;
    }

    /// <summary>Its id (BepInEx GUID).</summary>
    public string Id { get; }

    /// <summary>Its name.</summary>
    public string Name { get; }

    /// <summary>Its version.</summary>
    public string Version { get; }
}
