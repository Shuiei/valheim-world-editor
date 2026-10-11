using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>EditorProcess</c> between the tests of the "Visual" collection.
/// </summary>
[CollectionDefinition("Visual")]
public sealed class VisualGroup : ICollectionFixture<EditorProcess>
{
}
