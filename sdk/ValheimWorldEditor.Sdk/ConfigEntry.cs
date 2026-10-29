using System;

namespace ValheimWorldEditor.Sdk;

/// <summary>A file or folder in the connected game's BepInEx/config (<see cref="ILiveGame.ConfigFolder"/>).</summary>
public sealed class ConfigEntry
{
    /// <summary>An entry as the game's bridge lists it.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="folder">Whether it is a folder.</param>
    /// <param name="size">Its size, bytes (0 for a folder).</param>
    /// <param name="modified">When it was last changed (UTC).</param>
    public ConfigEntry(string name, bool folder, long size, DateTime modified)
    {
        Name = name;
        Folder = folder;
        Size = size;
        Modified = modified;
    }

    /// <summary>Its name.</summary>
    public string Name { get; }

    /// <summary>Whether it is a folder.</summary>
    public bool Folder { get; }

    /// <summary>Its size, bytes (0 for a folder).</summary>
    public long Size { get; }

    /// <summary>When it was last changed (UTC).</summary>
    public DateTime Modified { get; }
}
