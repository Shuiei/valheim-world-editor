using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>FolderEditorProcess</c> between the tests of the "Visual folder" collection.
/// </summary>
[CollectionDefinition("Visual folder", DisableParallelization = true)]
public sealed class FolderVisualGroup : ICollectionFixture<FolderEditorProcess>
{
}
