using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>GameLookEditorProcess</c> between the tests of the "Visual game look" collection.
/// </summary>
[CollectionDefinition("Visual game look", DisableParallelization = true)]
public sealed class VisualGameLookDefinition : ICollectionFixture<GameLookEditorProcess>
{
}
