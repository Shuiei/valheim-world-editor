using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor.Tests;

/// <summary>A pasted copy's new ids when no object of it holds an id the editor links: none.</summary>
internal sealed class NoIds : IIdMap
{
    public bool TryGetNew(int oldId, out int newId)
    {
        newId = 0;
        return false;
    }
}
