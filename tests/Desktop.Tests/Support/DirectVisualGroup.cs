using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>DirectEditorProcess</c> between the tests of the "Visual direct" collection.
/// </summary>
[CollectionDefinition("Visual direct")]
public sealed class DirectVisualGroup : ICollectionFixture<DirectEditorProcess>
{
}
