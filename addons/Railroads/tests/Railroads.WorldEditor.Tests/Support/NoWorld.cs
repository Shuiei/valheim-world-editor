using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor.Tests;

/// <summary>The world around an object with nothing else in it: the add-on's shapes reach no other object.</summary>
internal sealed class NoWorld : IWorldView
{
    public bool TryFind(string idKey, int id, out IObjectView obj)
    {
        obj = null!;
        return false;
    }
}
