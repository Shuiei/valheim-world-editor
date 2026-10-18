using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>BrokenLookEditorProcess</c> between the tests of the "Visual broken look" collection.
/// </summary>
[CollectionDefinition("Visual broken look", DisableParallelization = true)]
public sealed class VisualBrokenLookDefinition : ICollectionFixture<BrokenLookEditorProcess>
{
}
