using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Shares one <c>TinyBudgetEditorProcess</c> between the tests of the "Visual budget" collection.
/// </summary>
[CollectionDefinition("Visual budget", DisableParallelization = true)]
public sealed class VisualBudgetDefinition : ICollectionFixture<TinyBudgetEditorProcess>
{
}
