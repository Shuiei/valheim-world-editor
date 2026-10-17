using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The generator the replay compares against: built for the seed name it is given, with that seed.
/// </summary>
public class DumpVerifierInitTests
{
    [Fact]
    public void TheGeneratorHasTheSeedOfTheName()
    {
        WorldGenerator gen = DumpVerifier.Init("5DCcdIcuYJ");

        Assert.Equal(StableHash.Of("5DCcdIcuYJ"), gen.GetSeed());
    }
}
