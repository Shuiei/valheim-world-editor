using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>Tunnelheim's data objects as the add-on knows them: told apart, by the game's hashes of their names.</summary>
public class TunnelheimModTests
{
    [Fact]
    public void ItsDataObjectsAreToldApart()
    {
        Assert.True(TunnelheimMod.IsData(TunnelheimMod.RecordPrefab));
        Assert.True(TunnelheimMod.IsData(TunnelheimMod.CheckpointPrefab));
        Assert.Equal(1906006406, TunnelheimMod.RecordPrefab);
        Assert.Equal(-1717259926, TunnelheimMod.CheckpointPrefab);
        Assert.False(TunnelheimMod.IsData(0));
    }
}
