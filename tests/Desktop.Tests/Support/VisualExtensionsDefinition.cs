using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>ExtensionEditorProcess</c> between the tests of the "Visual extensions" collection.
/// </summary>
[CollectionDefinition("Visual extensions", DisableParallelization = true)]
public sealed class VisualExtensionsDefinition : ICollectionFixture<ExtensionEditorProcess>
{
}
