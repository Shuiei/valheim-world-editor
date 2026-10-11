using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>LiveEditorProcess</c> between the tests of the "Visual live" collection.
/// </summary>
[CollectionDefinition("Visual live")]
public sealed class LiveVisualGroup : ICollectionFixture<LiveEditorProcess>
{
}
